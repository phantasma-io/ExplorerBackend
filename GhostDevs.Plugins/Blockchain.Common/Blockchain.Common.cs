using System;
using GhostDevs.PluginEngine;
using System.Threading;
using Serilog;
using GhostDevs.Commons;

namespace GhostDevs.Blockchain
{
    public partial class BlockchainCommonPlugin: Plugin, IDBAccessPlugin
    {
        public override string Name => "Blockchain.Common";
        private bool _running = true;

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }
        public void Startup()
        {
            Log.Information($"{Name} plugin: Startup...");

            if (!Settings.Default.Enabled)
            {
                Log.Information($"{Name} plugin is disabled, stopping.");
                return;
            }

            // Starting threads

            Thread eventsProcessThread = new Thread(() =>
            {
                Thread.Sleep(Settings.Default.StartDelay * 1000);

                while (_running)
                {
                    try
                    {
                        MarkBurnedNfts();
                        EventUsdPricesFill();

                        Thread.Sleep(Settings.Default.EventsProcessingInterval * 1000); // We process events every EventsProcessingInterval seconds
                    }
                    catch (Exception e)
                    {
                        LogEx.Exception($"{Name} plugin: Events processing", e);

                        Thread.Sleep(Settings.Default.EventsProcessingInterval * 1000);
                    }
                }
            });
            eventsProcessThread.Start();
            
            Log.Information($"{Name} plugin: Startup finished");
        }
        public void Shutdown()
        {
            Log.Information($"{Name} plugin: Shutdown command received.");
            _running = false;
        }
    }
}
