using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

        var pricesUpdated = 0;

        using ( MainDbContext databaseContext = new() )
        {
            foreach ( var cryptoSymbol in cryptoSymbols )
            {
                var chain = ChainMethods.Get(databaseContext, cryptoSymbol.ChainName);
                var token = TokenMethods.Get(databaseContext, chain, cryptoSymbol.NativeSymbol);

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
                : UnixSeconds.AddDays(lastLoadedDate, 1);

            var lastLoadedDateString = UnixSeconds.ToDateTime(lastLoadedDate).ToString("dd-MM-yyyy");

            while ( lastLoadedDate <= UnixSeconds.Now() )
            {
                foreach ( var cryptoSymbol in cryptoSymbols.Where(
                             x => !string.IsNullOrEmpty(x.ApiSymbol)) )
                {
                    Log.Verbose("[{Name}] Symbol {Symbol}, Date {Date}", Name, cryptoSymbol.ApiSymbol,
                        lastLoadedDateString);

                    var chain = ChainMethods.Get(databaseContext, cryptoSymbol.ChainName);
                    var token = TokenMethods.Get(databaseContext, chain, cryptoSymbol.NativeSymbol);

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

                    if ( response.RootElement.TryGetProperty("error", out var errorProperty) )
                    {
                        Log.Error("[{Name}] Cannot fetch Info. Error: {Error}",
                            Name, errorProperty.GetString());

                        if ( errorProperty.ToString().ToUpper()
                            .Contains("Could not find coin with the given id".ToUpper()) )
                            Log.Warning(errorProperty.ToString());
                        continue;
                    }


                    if ( !response.RootElement.TryGetProperty("market_data", out var marketProperty) ) continue;

                    if ( marketProperty.TryGetProperty("current_price", out var priceNode) )
                    {
                        //it is send in lowercase now
                        var priceAUD = priceNode.GetProperty("aud").GetDecimal();
                        var priceCAD = priceNode.GetProperty("cad").GetDecimal();
                        var priceCNY = priceNode.GetProperty("cny").GetDecimal();
                        var priceEUR = priceNode.GetProperty("eur").GetDecimal();
                        var priceGBP = priceNode.GetProperty("gbp").GetDecimal();
                        var priceJPY = priceNode.GetProperty("jpy").GetDecimal();
                        var priceRUB = priceNode.GetProperty("rub").GetDecimal();
                        var priceUSD = priceNode.GetProperty("usd").GetDecimal();

                        if ( cryptoSymbol.NativeSymbol.ToUpper() == "SOUL" )
                        {
                            // Calculating KCAL price as 1/5 of SOUL price for dates before 02.10.2020 (1601596800).
                            if ( lastLoadedDate < 1601596800 )
                            {
                                token = TokenMethods.Get(databaseContext, chain, "KCAL");
                                TokenDailyPricesMethods.Upsert(databaseContext, lastLoadedDate, token,
                                    new Dictionary<string, decimal>
                                    {
                                        {"AUD", priceAUD / 5},
                                        {"CAD", priceCAD / 5},
                                        {"CNY", priceCNY / 5},
                                        {"EUR", priceEUR / 5},
                                        {"GBP", priceGBP / 5},
                                        {"JPY", priceJPY / 5},
                                        {"RUB", priceRUB / 5},
                                        {"USD", priceUSD / 5}
                                    },
                                    false);
                            }

                            token = TokenMethods.Get(databaseContext, chain, "GOATI");
                            // Setting GOATI pegged to 0.1 USD.
                            TokenDailyPricesMethods.Upsert(databaseContext, lastLoadedDate, token,
                                new Dictionary<string, decimal>
                                {
                                    {"AUD", 0.1m * priceAUD / priceUSD},
                                    {"CAD", 0.1m * priceCAD / priceUSD},
                                    {"CNY", 0.1m * priceCNY / priceUSD},
                                    {"EUR", 0.1m * priceEUR / priceUSD},
                                    {"GBP", 0.1m * priceGBP / priceUSD},
                                    {"JPY", 0.1m * priceJPY / priceUSD},
                                    {"RUB", 0.1m * priceRUB / priceUSD},
                                    {"USD", 0.1m}
                                },
                                false);
                        }

                        token = TokenMethods.Get(databaseContext, chain, cryptoSymbol.NativeSymbol);
                        TokenDailyPricesMethods.Upsert(databaseContext, lastLoadedDate, token,
                            new Dictionary<string, decimal>
                            {
                                {"AUD", priceAUD},
                                {"CAD", priceCAD},
                                {"CNY", priceCNY},
                                {"EUR", priceEUR},
                                {"GBP", priceGBP},
                                {"JPY", priceJPY},
                                {"RUB", priceRUB},
                                {"USD", priceUSD}
                            },
                            false);

                        pricesUpdated += 8;
                    }
                    else
                    {
                        if ( ( lastLoadedDate >= 1601596800 || cryptoSymbol.NativeSymbol.ToUpper() != "KCAL" ) &&
                             cryptoSymbol.NativeSymbol.ToUpper() != "GOATI" )
                            Log.Information(
                                "[{Name}] plugin: Price for '{NativeSymbol}' crypto is not available", Name,
                                cryptoSymbol.NativeSymbol);
                    }
                }

                lastLoadedDate = UnixSeconds.AddDays(lastLoadedDate, 1);
            }

            databaseContext.SaveChanges();
        }

        // Second pass. Updating crypto pairs.
        using ( MainDbContext databaseContext = new() )
        {
            // Searching for fiat pairs that are not initialized.
            // Take earliest date with uninitialized price.
            var firstIncompleteDate = databaseContext.TokenDailyPrices
                .Where(x => x.PRICE_SOUL == 0 || x.PRICE_NEO == 0 || x.PRICE_ETH == 0)
                .OrderBy(x => x.DATE_UNIX_SECONDS).Select(x => x.DATE_UNIX_SECONDS).FirstOrDefault();

            if ( firstIncompleteDate == 0 )
                // Nothing to do.
                return;

            var chainEntry = ChainMethods.Get(databaseContext, "main");
            while ( firstIncompleteDate <= UnixSeconds.Now() )
            {
                Dictionary<string, decimal> tokenUsdPrices = new()
                {
                    ["SOUL"] = TokenDailyPricesMethods.Get(databaseContext, chainEntry, firstIncompleteDate, "SOUL",
                        "USD"),
                    ["KCAL"] = TokenDailyPricesMethods.Get(databaseContext, chainEntry, firstIncompleteDate, "KCAL",
                        "USD"),
                    ["GOATI"] = 0.1m,
                    ["NEO"] = TokenDailyPricesMethods.Get(databaseContext, chainEntry, firstIncompleteDate, "NEO",
                        "USD"),
                    ["ETH"] = TokenDailyPricesMethods.Get(databaseContext, chainEntry, firstIncompleteDate, "ETH",
                        "USD")
                };

                if ( tokenUsdPrices["SOUL"] == 0 || tokenUsdPrices["NEO"] == 0 || tokenUsdPrices["ETH"] == 0 )
                {
                    Log.Information(
                        "[{Name}] plugin: Prices for '{FirstIncompleteDate}' are not yet available", Name,
                        UnixSeconds.LogDate(firstIncompleteDate));
                    break;
                }


                // Taking every crypto token and calculating SOUL/NEO/ETH price for them using USD as common price.
                foreach ( var cryptoSymbol in cryptoSymbols.Where(
                             x => !string.IsNullOrEmpty(x.ApiSymbol)) )
                {
                    var chain = ChainMethods.Get(databaseContext, cryptoSymbol.ChainName);
                    var token = TokenMethods.Get(databaseContext, chain, cryptoSymbol.NativeSymbol);

                    var tokenUsdPrice = TokenDailyPricesMethods.Get(databaseContext, token, firstIncompleteDate, "USD");
                    TokenDailyPricesMethods.Upsert(databaseContext, firstIncompleteDate, token,
                        new Dictionary<string, decimal>
                        {
                            {"SOUL", tokenUsdPrice / tokenUsdPrices["SOUL"]},
                            {"NEO", tokenUsdPrice / tokenUsdPrices["NEO"]},
                            {"ETH", tokenUsdPrice / tokenUsdPrices["ETH"]}
                        },
                        false);
                }

                firstIncompleteDate = UnixSeconds.AddDays(firstIncompleteDate, 1);
            }

            databaseContext.SaveChanges();
        }

        Log.Information("[{Name}] plugin: {PricesUpdated} prices updated", Name, pricesUpdated);
    }
}
