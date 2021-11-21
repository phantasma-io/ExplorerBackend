using GhostDevs.Commons;
using GhostDevs.PluginEngine;
using Serilog;
using System;
using System.Threading;

namespace GhostDevs.Nft
{
    public class TTRS: Plugin, IDBAccessPlugin
    {
        public override string Name => "Nft.TTRS";
        private bool _running = true;
        public string ExtendedDataId { get; } = "custom22RS";
        public TTRS()
        {
        }
        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }
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

                Nft.Fetch.Init();

                while (_running)
                {
                    try
                    {
                        Nft.Fetch.LoadNfts();
                        Nft.Fetch.LoadGAMENfts();

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
    }
}
