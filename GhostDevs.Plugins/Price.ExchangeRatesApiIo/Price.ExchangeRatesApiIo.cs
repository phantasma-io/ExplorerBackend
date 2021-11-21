using GhostDevs.PluginEngine;
using Database.Main;
using GhostDevs.Api;
using System.Threading;
using System;
using GhostDevs.Commons;
using Serilog;
using System.Text.Json;

namespace GhostDevs.Price
{
    public class ExchangeRatesApiIo : Plugin, IDBAccessPlugin
    {
        public override string Name => "Price.ExchangeRatesApiIo";
        private bool _running = true;
        private static Random rnd = new Random();
        public ExchangeRatesApiIo()
        {
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
        public void Startup()
        {
            Log.Information($"{Name} plugin: Startup...");

            if (!Settings.Default.Enabled)
            {
                Log.Information($"{Name} plugin is disabled, stopping.");
                return;
            }

            // Starting thread

            Thread mainThread = new Thread(() =>
            {
                Thread.Sleep(Settings.Default.StartDelay * 1000);

                while (_running)
                {
                    try
                    {
                        LoadPrices();

                        Thread.Sleep((int)Settings.Default.RunInterval * 1000); // We repeat task every RunInterval seconds.
                    }
                    catch (Exception e)
                    {
                        LogEx.Exception($"{Name} plugin", e);

                        Thread.Sleep((int)Settings.Default.RunInterval * 1000);
                    }
                }
            });
            mainThread.Start();

            Log.Information($"{Name} plugin: Startup finished");
        }
        public void Shutdown()
        {
            Log.Information($"{Name} plugin: Shutdown command received.");
            _running = false;
        }
        // Loads token prices from https://exchangeratesapi.io/.
        // API documentation: https://exchangeratesapi.io/
        public void LoadPrices()
        {
            var url = "https://api.exchangeratesapi.io/latest?base=USD&access_key=" + Settings.Default.ApiKeys.GetValue(rnd.Next(Settings.Default.ApiKeys.Length));

            var response = Client.APIRequest<JsonDocument>(url, out var stringResponse);
            if (response == null)
            {
                return;
            }

            int pricesUpdated = 0;
            using (var databaseContext = new MainDbContext())
            {
                foreach (var fiatSymbol in TokenMethods.GetSupportedFiatSymbols())
                {
                    var price = response.RootElement.GetProperty("rates").GetProperty(fiatSymbol).GetDecimal();

                    FiatExchangeRateMethods.Upsert(databaseContext, fiatSymbol, price, false);

                    // Setting pegged token prices.

                    // GOATI. 1 GOATI = 0.1 USD
                    if (fiatSymbol.ToUpper() == "USD")
                    {
                        TokenMethods.SetPrice(databaseContext, ChainMethods.GetId(databaseContext, "main"), "GOATI", fiatSymbol, 0.1m, false);
                    }
                    else
                    {
                        TokenMethods.SetPrice(databaseContext, ChainMethods.GetId(databaseContext, "main"), "GOATI", fiatSymbol, price * 0.1m, false);
                    }
                    pricesUpdated++;
                }

                if(pricesUpdated > 0)
                    databaseContext.SaveChanges();
            }
            Log.Information($"{Name} plugin: {pricesUpdated} prices updated.");
        }
    }
}
