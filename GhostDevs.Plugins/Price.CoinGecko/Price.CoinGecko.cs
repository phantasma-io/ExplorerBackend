using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Database.Main;
using GhostDevs.Api;
using GhostDevs.Commons;
using GhostDevs.PluginEngine;
using Serilog;

namespace GhostDevs.Price;

public class CoinGecko : Plugin, IDBAccessPlugin
{
    private bool _running = true;

    public override string Name => "Price.CoinGecko";


    public void Startup()
    {
        Log.Information("{Name} plugin: Startup...", Name);

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
                    List<TokenMethods.Symbol> cryptoSymbols = null;
                    using ( MainDbContext databaseContext = new() )
                    {
                        cryptoSymbols = TokenMethods.GetSupportedTokens(databaseContext);
                    }

                    // Set tokens API IDs that are compatible with CoinGecko.
                    // CoinGecko's id for SOUL, for example, is "phantasma".
                    // Also we skip those symbols that are unavailable on CoinGecko.
                    for ( var i = 0; i < cryptoSymbols.Count(); i++ )
                        switch ( cryptoSymbols[i].NativeSymbol.ToUpper() )
                        {
                            case "SOUL":
                                // Using "PHANTASMA" id for "SOUL" tokens.
                                cryptoSymbols[i].ApiSymbol = "phantasma"; // Case-sensitive!
                                break;
                            case "KCAL":
                                cryptoSymbols[i].ApiSymbol = "phantasma-energy";
                                break;
                            case "ETH":
                                cryptoSymbols[i].ApiSymbol = "ethereum";
                                break;
                            case "WETH":
                                cryptoSymbols[i].ApiSymbol = "weth";
                                break;
                            case "USDC":
                                cryptoSymbols[i].ApiSymbol = "usd-coin";
                                break;
                            case "DAI":
                                cryptoSymbols[i].ApiSymbol = "dai";
                                break;
                            case "BNB":
                                cryptoSymbols[i].ApiSymbol = "binancecoin";
                                break;
                            case "WBNB":
                                cryptoSymbols[i].ApiSymbol = "wbnb";
                                break;
                            case "BUSD":
                                cryptoSymbols[i].ApiSymbol = "binance-usd";
                                break;
                            case "SWTH":
                                cryptoSymbols[i].ApiSymbol = "switcheo";
                                break;
                            case "CAKE":
                                cryptoSymbols[i].ApiSymbol = "pancakeswap-token";
                                break;
                            case "MATIC":
                                cryptoSymbols[i].ApiSymbol = "matic-network";
                                break;
                            case "DYT":
                                cryptoSymbols[i].ApiSymbol = "dynamite";
                                break;
                            case "NEO":
                            case "BNEO":
                                cryptoSymbols[i].ApiSymbol = "neo";
                                break;
                            case "AVAX":
                                cryptoSymbols[i].ApiSymbol = "avalanche-2";
                                break;
                            case "WAVAX":
                                cryptoSymbols[i].ApiSymbol = "wrapped-avax";
                                break;
                            case "GOATI":
                            case "TTRS":
                            case "MKNI":
                            case "CROWN":
                            case "GHOST":
                            case "SEM":
                                // Just ignore, can't get this price from CoinGecko.
                                break;
                            default:
                                // Here goes NEO, GAS, and all other tokens.
                                cryptoSymbols[i].ApiSymbol = cryptoSymbols[i].NativeSymbol.ToLower(); // Case-sensitive!
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
    public void LoadPrices(List<TokenMethods.Symbol> cryptoSymbols)
    {
        var separator = "%2C";

        // Fiat currency symbols, supported by backend.
        var fiatSymbols = TokenMethods.GetSupportedFiatSymbols();

        var url = "https://api.coingecko.com/api/v3/simple/price?ids=" +
                  string.Join(separator,
                      cryptoSymbols.Where(x => !string.IsNullOrEmpty(x.ApiSymbol)).Select(x => x.ApiSymbol)
                          .Distinct()
                          .ToList()) + "&vs_currencies=" + string.Join(separator, fiatSymbols);

        var response = Client.APIRequest<JsonDocument>(url, out var stringResponse);
        if ( response == null ) return;

        var pricesUpdated = 0;

        using ( MainDbContext databaseContext = new() )
        {
            foreach ( var cryptoSymbol in cryptoSymbols )
                if ( !string.IsNullOrEmpty(cryptoSymbol.ApiSymbol) &&
                     response.RootElement.TryGetProperty(cryptoSymbol.ApiSymbol.ToLower(), out var priceNode) )
                    foreach ( var fiatSymbol in fiatSymbols )
                        if ( priceNode.TryGetProperty(fiatSymbol.ToLower(), out var priceElement) )
                        {
                            var price = priceElement.GetDecimal();

                            TokenMethods.SetPrice(databaseContext,
                                ChainMethods.GetId(databaseContext, cryptoSymbol.ChainName), cryptoSymbol.NativeSymbol,
                                fiatSymbol, price, false);
                            pricesUpdated++;
                        }
                        else
                            Log.Warning(
                                "{Name} plugin: Can't find price for '{NativeSymbol}'/'{ApiSymbol}'/'{FiatSymbol}', url: {Url}",
                                Name, cryptoSymbol.NativeSymbol, cryptoSymbol.ApiSymbol, fiatSymbol, url);
                else
                {
                    // TODO check fungibility through db when will be available
                    if ( cryptoSymbol.NativeSymbol.ToUpper() != "GOATI" &&
                         cryptoSymbol.NativeSymbol.ToUpper() != "MKNI" &&
                         cryptoSymbol.NativeSymbol.ToUpper() != "CROWN" &&
                         cryptoSymbol.NativeSymbol.ToUpper() != "GHOST" &&
                         cryptoSymbol.NativeSymbol.ToUpper() != "SEM" &&
                         cryptoSymbol.NativeSymbol.ToUpper() != "TTRS" &&
                         cryptoSymbol.NativeSymbol.ToUpper() != "LEET" &&
                         cryptoSymbol.NativeSymbol.ToUpper() != "NOMI" )
                        Log.Warning("{Name} plugin: Price for '{NativeSymbol}' crypto is not available", Name,
                            cryptoSymbol.NativeSymbol);
                }

            databaseContext.SaveChanges();
        }

        Log.Information("{Name} plugin: {PricesUpdated} prices updated", Name, pricesUpdated);
    }


    public void LoadPricesHistory(List<TokenMethods.Symbol> cryptoSymbols)
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

            while ( lastLoadedDate <= UnixSeconds.Now() )
            {
                foreach ( var cryptoSymbol in cryptoSymbols.Where(
                             x => !string.IsNullOrEmpty(x.ApiSymbol)) )
                {
                    var url = "https://api.coingecko.com/api/v3/coins/" + cryptoSymbol.ApiSymbol + "/history?date=" +
                              lastLoadedDate.ToString("dd-MM-yyyy");

                    var response = Client.APIRequest<JsonDocument>(url, out var stringResponse, error =>
                    {
                        if ( !error.ToUpper().Contains("Too Many Requests".ToUpper()) ) return;

                        Log.Information(
                            "{Name} plugin: We reached request limit, saving changes and sleep for 5 seconds", Name);
                        databaseContext.SaveChanges();
                        Thread.Sleep(5000);
                    });
                    if ( response == null )
                    {
                        Log.Error(
                            "{Name} plugin: Stuck on '{ApiSymbol}' price update for '{LastLoadedDate}'", Name,
                            cryptoSymbol.ApiSymbol, lastLoadedDate.ToString("dd-MM-yyyy"));
                        return;
                    }

                    if ( !response.RootElement.TryGetProperty("market_data", out var marketProperty) ) continue;

                    if ( marketProperty.TryGetProperty("current_price", out var priceNode) )
                    {
                        var priceAUD = priceNode.GetProperty("AUD").GetDecimal();
                        var priceCAD = priceNode.GetProperty("CAD").GetDecimal();
                        var priceCNY = priceNode.GetProperty("CNY").GetDecimal();
                        var priceEUR = priceNode.GetProperty("EUR").GetDecimal();
                        var priceGBP = priceNode.GetProperty("GBP").GetDecimal();
                        var priceJPY = priceNode.GetProperty("JPY").GetDecimal();
                        var priceRUB = priceNode.GetProperty("RUB").GetDecimal();
                        var priceUSD = priceNode.GetProperty("USD").GetDecimal();

                        if ( cryptoSymbol.NativeSymbol.ToUpper() == "SOUL" )
                        {
                            // Calculating KCAL price as 1/5 of SOUL price for dates before 02.10.2020 (1601596800).
                            if ( lastLoadedDate < 1601596800 )
                                TokenDailyPricesMethods.Upsert(databaseContext, lastLoadedDate,
                                    ChainMethods.GetId(databaseContext, cryptoSymbol.ChainName),
                                    "KCAL",
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

                            // Setting GOATI pegged to 0.1 USD.
                            TokenDailyPricesMethods.Upsert(databaseContext, lastLoadedDate,
                                ChainMethods.GetId(databaseContext, cryptoSymbol.ChainName),
                                "GOATI",
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

                        TokenDailyPricesMethods.Upsert(databaseContext, lastLoadedDate,
                            ChainMethods.GetId(databaseContext, cryptoSymbol.ChainName),
                            cryptoSymbol.NativeSymbol,
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
                                "{Name} plugin: Price for '{NativeSymbol}' crypto is not available", Name,
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

            while ( firstIncompleteDate <= UnixSeconds.Now() )
            {
                Dictionary<string, decimal> tokenUsdPrices = new()
                {
                    ["SOUL"] = TokenDailyPricesMethods.Get(databaseContext, ChainMethods.GetId(databaseContext, "main"),
                        firstIncompleteDate, "SOUL", "USD"),
                    ["KCAL"] = TokenDailyPricesMethods.Get(databaseContext, ChainMethods.GetId(databaseContext, "main"),
                        firstIncompleteDate, "KCAL", "USD"),
                    ["GOATI"] = 0.1m
                };

                if ( tokenUsdPrices["SOUL"] == 0 || tokenUsdPrices["NEO"] == 0 || tokenUsdPrices["ETH"] == 0 )
                {
                    Log.Information(
                        "{Name} plugin: Prices for '{FirstIncompleteDate}' are not yet available", Name,
                        UnixSeconds.LogDate(firstIncompleteDate));
                    break;
                }

                // Taking every crypto token and calculating SOUL/NEO/ETH price for them using USD as common price.
                foreach ( var cryptoSymbol in cryptoSymbols.Where(
                             x => !string.IsNullOrEmpty(x.ApiSymbol)) )
                {
                    var symbol = cryptoSymbol.NativeSymbol.ToUpper();

                    var tokenUsdPrice = TokenDailyPricesMethods.Get(databaseContext,
                        ChainMethods.GetId(databaseContext, cryptoSymbol.ChainName), firstIncompleteDate,
                        cryptoSymbol.NativeSymbol, "USD");
                    TokenDailyPricesMethods.Upsert(databaseContext, firstIncompleteDate,
                        ChainMethods.GetId(databaseContext, cryptoSymbol.ChainName),
                        cryptoSymbol.NativeSymbol,
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

        Log.Information("{Name} plugin: {PricesUpdated} prices updated", Name, pricesUpdated);
    }
}
