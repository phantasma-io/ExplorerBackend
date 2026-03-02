using System;
using System.Text.Json;
using System.Threading;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Serilog;

namespace Backend.Price;

public class ExchangeRatesApiIo : Plugin, IDBAccessPlugin
{
    private static readonly Random rnd = new();
    private bool _running = true;
    private bool _deferredDueCatchup = false;

    public override string Name => "Price.ExchangeRatesApiIo";

    private bool ShouldDeferForCatchup()
    {
        // Price refresh is non-critical during initial catch-up and can compete on DB/CPU.
        try
        {
            using MainDbContext databaseContext = new();
            var chainId = ChainMethods.GetId(databaseContext, "main");
            if (chainId <= 0)
                return false;

            return CatchupGateMethods.TryGetCatchupReady(databaseContext, chainId, out var isCatchupReady) &&
                   !isCatchupReady;
        }
        catch (Exception e)
        {
            Log.Warning("[{Name}] plugin: Failed to check catch-up state, proceeding with price refresh: {Reason}",
                Name, e.Message);
            return false;
        }
    }


    public void Startup()
    {
        Log.Information("{Name} plugin: Startup ...", Name);

        if (!Settings.Default.Enabled)
        {
            Log.Information("{Name} plugin is disabled, stopping", Name);
            return;
        }

        // Starting thread

        Thread mainThread = new(() =>
        {
            Thread.Sleep(Settings.Default.StartDelay * 1000);

            while (_running)
                try
                {
                    if (ShouldDeferForCatchup())
                    {
                        if (!_deferredDueCatchup)
                        {
                            Log.Information(
                                "{Name} plugin: Deferring price refresh while explorer catch-up is in progress",
                                Name);
                            _deferredDueCatchup = true;
                        }

                        Thread.Sleep((int)Settings.Default.RunInterval * 1000);
                        continue;
                    }

                    if (_deferredDueCatchup)
                    {
                        Log.Information(
                            "{Name} plugin: Resuming price refresh after explorer reached zero-lag",
                            Name);
                        _deferredDueCatchup = false;
                    }

                    LoadPrices();

                    Thread.Sleep((int)Settings.Default.RunInterval *
                                 1000); // We repeat task every RunInterval seconds.
                }
                catch (Exception e)
                {
                    LogEx.Exception($"{Name} plugin", e);

                    Thread.Sleep((int)Settings.Default.RunInterval * 1000);
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
    private void LoadPrices()
    {
        var url = "https://api.exchangeratesapi.io/latest?base=USD&access_key=" +
                  Settings.Default.ApiKeys.GetValue(rnd.Next(Settings.Default.ApiKeys.Length));

        var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse);
        if (response == null) return;

        var pricesUpdated = 0;
        using (MainDbContext databaseContext = new())
        {
            foreach (var fiatSymbol in TokenMethods.GetSupportedFiatSymbols())
            {
                decimal price;
                if (response.RootElement.TryGetProperty("rates", out var element))
                    price = element.GetProperty(fiatSymbol).GetDecimal();
                else
                {
                    Log.Warning("[{Name}] failed to get 'rates' element, for fiatSymbol {Symbol}", Name, fiatSymbol);
                    continue;
                }

                FiatExchangeRateMethods.Upsert(databaseContext, fiatSymbol, price);

                // Setting pegged token prices.

                // GOATI. 1 GOATI = 0.1 USD
                if (fiatSymbol.ToUpper() == "USD")
                    TokenMethods.SetPrice(databaseContext, ChainMethods.GetId(databaseContext, "main"), "GOATI",
                        fiatSymbol, 0.1m, false);
                else
                    TokenMethods.SetPrice(databaseContext, ChainMethods.GetId(databaseContext, "main"), "GOATI",
                        fiatSymbol, price * 0.1m, false);

                pricesUpdated++;
            }

            if (pricesUpdated > 0) databaseContext.SaveChanges();
        }

        Log.Information("{Name} plugin: {PricesUpdated} prices updated", Name, pricesUpdated);
    }
}
