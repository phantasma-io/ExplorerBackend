using System;
using System.Text.Json;
using System.Threading;
using Database.Main;
using GhostDevs.Api;
using GhostDevs.Commons;
using GhostDevs.PluginEngine;
using Serilog;

namespace GhostDevs.Price;

public class ExchangeRatesApiIo : Plugin, IDBAccessPlugin
{
    private static readonly Random rnd = new();
    private bool _running = true;

    public override string Name => "Price.ExchangeRatesApiIo";


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
                    LoadPrices();

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


    // Fetching fiat currency prices from https://exchangeratesapi.io/.
    // Using them to calculate prices for pegged tokens.
    public void Fetch()
    {
    }


    // Loads token prices from https://exchangeratesapi.io/.
    // API documentation: https://exchangeratesapi.io/
    public void LoadPrices()
    {
        var url = "https://api.exchangeratesapi.io/latest?base=USD&access_key=" +
                  Settings.Default.ApiKeys.GetValue(rnd.Next(Settings.Default.ApiKeys.Length));

        var response = Client.APIRequest<JsonDocument>(url, out var stringResponse);
        if ( response == null ) return;

        var pricesUpdated = 0;
        using ( MainDbContext databaseContext = new() )
        {
            foreach ( var fiatSymbol in TokenMethods.GetSupportedFiatSymbols() )
            {
                decimal price;
                if ( response.RootElement.TryGetProperty("rates", out var element) )
                    price = element.GetProperty(fiatSymbol).GetDecimal();
                else
                {
                    Log.Warning("[{Name}] failed to get 'rates' element, for fiatSymbol {Symbol}", Name, fiatSymbol);
                    continue;
                }

                FiatExchangeRateMethods.Upsert(databaseContext, fiatSymbol, price, false);

                // Setting pegged token prices.

                // GOATI. 1 GOATI = 0.1 USD
                if ( fiatSymbol.ToUpper() == "USD" )
                    TokenMethods.SetPrice(databaseContext, ChainMethods.GetId(databaseContext, "main"), "GOATI",
                        fiatSymbol, 0.1m, false);
                else
                    TokenMethods.SetPrice(databaseContext, ChainMethods.GetId(databaseContext, "main"), "GOATI",
                        fiatSymbol, price * 0.1m, false);

                pricesUpdated++;
            }

            if ( pricesUpdated > 0 ) databaseContext.SaveChanges();
        }

        Log.Information("{Name} plugin: {PricesUpdated} prices updated", Name, pricesUpdated);
    }
}
