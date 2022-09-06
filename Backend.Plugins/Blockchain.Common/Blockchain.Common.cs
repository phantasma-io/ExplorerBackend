using System;
using System.Threading;
using Backend.Commons;
using Backend.PluginEngine;
using Serilog;

namespace Backend.Blockchain;

public partial class BlockchainCommonPlugin : Plugin, IDBAccessPlugin
{
    private bool _running = true;
    public override string Name => "Blockchain.Common";


    public void Startup()
    {
        Log.Information("{Name} plugin: Startup ...", Name);

        if ( !Settings.Default.Enabled )
        {
            Log.Information("{Name} plugin is disabled, stopping", Name);
            return;
        }

        // Starting threads

        var eventsProcessThread = new Thread(() =>
        {
            Thread.Sleep(Settings.Default.StartDelay * 1000);

            while ( _running )
                try
                {
                    MarkBurnedNfts();
                    EventUsdPricesFill();

                    Thread.Sleep(Settings.Default.EventsProcessingInterval *
                                 1000); // We process events every EventsProcessingInterval seconds
                }
                catch ( Exception e )
                {
                    LogEx.Exception($"{Name} plugin: Events processing", e);

                    Thread.Sleep(Settings.Default.EventsProcessingInterval * 1000);
                }
        });
        eventsProcessThread.Start();

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
}
