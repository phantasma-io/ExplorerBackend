using System;
using System.Threading;
using Backend.Commons;
using Backend.PluginEngine;
using Serilog;

namespace Backend.Nft;

public class TTRS : Plugin, IDBAccessPlugin
{
    private bool _running = true;

    public override string Name => "Nft.TTRS";
    public string ExtendedDataId { get; } = "custom22RS";


    public void Startup()
    {
        Log.Information("{Name} plugin: Startup...", Name);

        if ( !Settings.Default.Enabled )
        {
            Log.Information("{Name} plugin is disabled, stopping", Name);
            return;
        }

        // Starting thread

        var mainThread = new Thread(() =>
        {
            Thread.Sleep(Settings.Default.StartDelay * 1000);

            Nft.Fetch.Init();

            while ( _running )
                try
                {
                    Nft.Fetch.LoadNfts();
                    //Nft.Fetch.LoadGAMENfts(); lets try without

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


    public void Fetch()
    {
    }
}
