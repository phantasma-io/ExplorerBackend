using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Serilog;

namespace Backend.Price;

public class CoinGecko : Plugin, IDBAccessPlugin
{
    private bool _running = true;

    public override string Name => "Price.CoinGecko";


    public void Startup()
    {
        Log.Information("{Name} plugin: Startup ...", Name);

        if ( !Settings.Default.Enabled )
        {
            Log.Information("{Name} plugin is disabled, stopping", Name);
            return;
        }

        // Starting thread

        Thread mainThread = new(() =>
        {
            Thread.Sleep(Settings.Default.StartDelay * 1000);

            while ( _running )
                try
                {
                    // Coingecko ids can be found here:
                    // https://api.coingecko.com/api/v3/coins/list

                    // Get token symbols that are used in auctions <chainShortName, tokenSymbol>.
                    List<TokenMethods.Symbol> cryptoSymbols;
                    using ( MainDbContext databaseContext = new() )
                    {
                        cryptoSymbols = TokenMethods.GetSupportedTokens(databaseContext);
                    }

                    // Set tokens API IDs that are compatible with CoinGecko.
                    // CoinGecko's id for SOUL, for example, is "phantasma".
                    // Also we skip those symbols that are unavailable on CoinGecko.
                    foreach ( var t in cryptoSymbols )
                        switch ( t.NativeSymbol.ToUpper() )
                        {
                            case "SOUL":
                                // Using "PHANTASMA" id for "SOUL" tokens.
                                t.ApiSymbol = "phantasma"; // Case-sensitive!
                                break;
                            case "KCAL":
                                t.ApiSymbol = "phantasma-energy";
                                break;
                            case "ETH":
                                t.ApiSymbol = "ethereum";
                                break;
                            case "USDC":
                                t.ApiSymbol = "usd-coin";
                                break;
                            case "DAI":
                                t.ApiSymbol = "dai";
                                break;
                            case "USDT":
                                t.ApiSymbol = "tether";
                                break;
                            case "BNB":
                                t.ApiSymbol = "binancecoin";
                                break;
                            case "NEO":
                                t.ApiSymbol = "neo";
                                break;
                            case "GAS":
                                t.ApiSymbol = "gas";
                                break;
                            default:
                                //maybe add that info somehow to the database
                                // Just ignore, can't get this price from CoinGecko.
                                t.ApiSymbol = null;
                                break;
                        }

                    // Load prices for a given list of CoinGecko's ids.
                    LoadPrices(cryptoSymbols);

                    // Load daily token prices.
                    LoadPricesHistory(cryptoSymbols);

                    Thread.Sleep(( int ) Settings.Default.RunInterval *
                                 1000); // We repeat task every RunInterval seconds.
                }
                catch ( Exception e )
                {
                    LogEx.Exception($"{Name} plugin", e);

                    Thread.Sleep(( int ) Settings.Default.RunInterval * 1000);
                }
        });
        mainThread.Start();

        Log.Information("{Name} plugin: Startup finished", Name);
    }


    public void Shutdown()
    {
        Log.Information("{Name} plugin: Shutdown command received", Name);
        _running = false;
    }


    protected override void Configure()
    {
        Settings.Load(GetConfiguration());
    }


    // Fetching token prices from CoinGecko.
    public void Fetch()
    {
    }


    // Loads token prices from CoinGecko.
    // API documentation: https://www.coingecko.com/api/documentations/v3#/simple/get_simple_price
    private void LoadPrices(List<TokenMethods.Symbol> cryptoSymbols)
    {
        const string separator = "%2C";

        // Fiat currency symbols, supported by backend.
        var fiatSymbols = TokenMethods.GetSupportedFiatSymbols();

        var url = "https://api.coingecko.com/api/v3/simple/price?ids=" +
                  string.Join(separator,
                      cryptoSymbols.Where(x => !string.IsNullOrEmpty(x.ApiSymbol)).Select(x => x.ApiSymbol)
                          .Distinct()
                          .ToList()) + "&vs_currencies=" + string.Join(separator, fiatSymbols);

        var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse);
        if ( response == null ) return;

        if ( stringResponse.ToLowerInvariant().Contains("exceeded the rate limit") )
        {
            Log.Warning("[{Name}] Exceeded the rate limit, stopping for now", Name);
            return;
        }
        
        var pricesUpdated = 0;

        using ( MainDbContext databaseContext = new() )
        {
            foreach ( var cryptoSymbol in cryptoSymbols )
            {
                var chain = ChainMethods.Get(databaseContext, cryptoSymbol.ChainName);
                // TODO async
                var token = TokenMethods.GetAsync(databaseContext, chain, cryptoSymbol.NativeSymbol).Result;

                if ( string.IsNullOrEmpty(cryptoSymbol.ApiSymbol) ) continue;

                if ( response.RootElement.TryGetProperty(cryptoSymbol.ApiSymbol.ToLower(), out var priceNode) )
                    foreach ( var fiatSymbol in fiatSymbols )
                        if ( priceNode.TryGetProperty(fiatSymbol.ToLower(), out var priceElement) )
                        {
                            TokenMethods.SetPrice(databaseContext, token, fiatSymbol, priceElement.GetDecimal(), false);

                            pricesUpdated++;
                        }
                        else
                        {
                            Log.Warning(
                                "[{Name}] plugin: Can't find price for '{NativeSymbol}'/'{ApiSymbol}'/'{FiatSymbol}', url: {Url}",
                                Name, cryptoSymbol.NativeSymbol, cryptoSymbol.ApiSymbol, fiatSymbol, url);
                        }

                else
                {
                    Log.Warning("[{Name}] plugin: Price for '{NativeSymbol}' crypto is not available", Name,
                        cryptoSymbol.NativeSymbol);
                }
            }

            databaseContext.SaveChanges();
        }

        Log.Information("[{Name}] plugin: {PricesUpdated} prices updated", Name, pricesUpdated);
    }


    private void LoadPricesHistory(IReadOnlyCollection<TokenMethods.Symbol> cryptoSymbols)
    {
        var pricesUpdated = 0;

        // First pass. We get all tokens prices in USD.
        using ( MainDbContext databaseContext = new() )
        {
            var lastLoadedDate = databaseContext.TokenDailyPrices.OrderByDescending(x => x.DATE_UNIX_SECONDS)
                .Select(x => x.DATE_UNIX_SECONDS).FirstOrDefault();

            lastLoadedDate = lastLoadedDate == 0
                ? UnixSeconds.FromDateTime(Settings.Default.StartDate)
                : databaseContext.TokenDailyPrices.Count(x => x.DATE_UNIX_SECONDS == lastLoadedDate) < cryptoSymbols.Count ? lastLoadedDate : UnixSeconds.AddDays(lastLoadedDate, 1);

            var lastLoadedDateString = UnixSeconds.ToDateTime(lastLoadedDate).ToString("dd-MM-yyyy");

            while ( lastLoadedDate <= UnixSeconds.Now() )
            {
                foreach ( var cryptoSymbol in cryptoSymbols.Where(
                             x => !string.IsNullOrEmpty(x.ApiSymbol)) )
                {
                    Log.Verbose("[{Name}] Symbol {Symbol}, Date {Date}", Name, cryptoSymbol.ApiSymbol,
                        lastLoadedDateString);

                    var chain = ChainMethods.Get(databaseContext, cryptoSymbol.ChainName);
                    // TODO async
                    var token = TokenMethods.GetAsync(databaseContext, chain, cryptoSymbol.NativeSymbol).Result;

                    if ( token == null )
                    {
                        Log.Warning("[{Name}] Symbol {Symbol} could not be found", Name, cryptoSymbol.NativeSymbol);
                        continue;
                    }

                    var url = "https://api.coingecko.com/api/v3/coins/" + cryptoSymbol.ApiSymbol + "/history?date=" +
                              lastLoadedDateString;

                    var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse,
                        error =>
                        {
                            Log.Information(
                                "[{Name}] plugin: We reached request limit, saving changes and sleep for 5 seconds",
                                Name);
                        });

                    if ( response == null )
                    {
                        databaseContext.SaveChanges();
                        Log.Error(
                            "[{Name}] plugin: Stuck on '{ApiSymbol}' price update for '{LastLoadedDate}. Return Go on Later.'",
                            Name, cryptoSymbol.ApiSymbol, lastLoadedDateString);
                        return;
                    }

                    if ( stringResponse.ToLowerInvariant().Contains("exceeded the rate limit") )
                    {
                        databaseContext.SaveChanges();
                        Log.Warning("[{Name}] Exceeded the rate limit, stopping for now", Name);
                        return;
                    }

                    if ( response.RootElement.TryGetProperty("error", out var errorProperty) )
                    {
                        if ( errorProperty.TryGetProperty("status", out var statusProperty) )
                        {
                            if ( statusProperty.TryGetProperty("error_message", out var errorMessageProperty) )
                            {
                                var errorMessage = errorMessageProperty.GetString();
                                Log.Error("[{Name}] Cannot fetch data. Error: {Error}",
                                    Name, errorMessage);

                                if ( errorMessage is not null &&
                                     errorMessage.Contains("Could not find coin with the given id",
                                         StringComparison.CurrentCultureIgnoreCase) )
                                {
                                    Log.Warning(errorMessage);
                                }
                            }
                            else
                            {
                                Log.Error("[{Name}] Cannot parse status of error message, raw error: {Error}", Name, JsonObject.Create(errorProperty)?.ToJsonString());
                            }
                        }
                        else
                        {
                            Log.Error("[{Name}] Cannot parse error message, raw error: {Error}", Name, JsonObject.Create(errorProperty)?.ToJsonString());
                        }

                        continue;
                    }

                    var priceUsd = 0m;
                    if ( !response.RootElement.TryGetProperty("market_data", out var marketProperty) )
                    {
                        Log.Warning(
                            "[{Name}] plugin: market_data is unavailable for symbol {Symbol}, response: {Response}.'",
                            Name, cryptoSymbol.ApiSymbol, stringResponse);
                    }
                    else
                    {
                        if ( marketProperty.TryGetProperty("current_price", out var priceNode) )
                        {
                            //it is send in lowercase now
                            priceUsd = priceNode.GetProperty("usd").GetDecimal();
                        }
                    }

                    if ( cryptoSymbol.NativeSymbol.ToUpper() == "SOUL" )
                    {
                        // TODO async
                        token = TokenMethods.GetAsync(databaseContext, chain, "GOATI").Result;
                        // Setting GOATI pegged to 0.1 USD.
                        TokenDailyPricesMethods.Upsert(databaseContext, lastLoadedDate, token,
                            priceUsd,
                            false);
                    }

                    // TODO async
                    token = TokenMethods.GetAsync(databaseContext, chain, cryptoSymbol.NativeSymbol).Result;
                    TokenDailyPricesMethods.Upsert(databaseContext, lastLoadedDate, token,
                        priceUsd,
                        false);

                    pricesUpdated += 1;
                }

                lastLoadedDate = UnixSeconds.AddDays(lastLoadedDate, 1);
            }

            databaseContext.SaveChanges();
        }

        Log.Information("[{Name}] plugin: {PricesUpdated} daily prices updated", Name, pricesUpdated);
    }
}
