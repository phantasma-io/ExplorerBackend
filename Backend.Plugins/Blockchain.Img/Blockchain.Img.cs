using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Backend.Commons;
using Backend.PluginEngine;
using Blockchain.Img;
using Castle.Core.Internal;
using Database.Main;
using Serilog;
using static System.IO.Path;


namespace Backend.Blockchain;

public class BlockChainImgPlugin : Plugin, IDBAccessPlugin
{
    private bool _running = true;
    public override string Name => "Blockchain.Img";


    public void Startup()
    {
        Log.Information("{Name} plugin: Startup ...", Name);

        if ( !Settings.Default.Enabled )
        {
            Log.Information("{Name} plugin is disabled, stopping", Name);
            return;
        }

        //TODO maybe use PhysicalFileProvider for that
        Thread mainThread = new(() =>
        {
            Thread.Sleep(Settings.Default.StartDelay * 1000);

            while ( _running )
                try
                {
                    CheckDirectory();

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


    private void CheckDirectory()
    {
        var directory = Combine(PluginsDirectory, "../..", Settings.Default.Folder);
        Log.Information("[{Name}] should check directory {Directory}", Name, directory);

        var directoryInfo = new DirectoryInfo(directory);
        var files = directoryInfo.GetFiles("*." + Settings.Default.FileEnding);

        var tokenUrlCount = 0;

        using MainDbContext databaseContext = new();

        //TODO fix
        var chain = ChainMethods.Get(databaseContext, "main");
        var defaultImg = "";

        var baseLink = Settings.Default.HostName + Settings.Default.Folder;
        foreach ( var file in files )
        {
            var token = GetFileNameWithoutExtension(file.Name);
            Log.Verbose("[{Name}] Processing Image for Token {Token}, File Name {File}", Name, token, file.Name);

            if ( token == Settings.Default.DefaultImage ) defaultImg = file.Name;

            if ( string.IsNullOrEmpty(token) ) continue;
            var tokenEntry = TokenMethods.Get(databaseContext, chain, token);

            if ( tokenEntry == null ) continue;

            Log.Verbose("[{Name}] building and adding Link for Token {Token}", Name, tokenEntry.SYMBOL);
            TokenLogoMethods.InsertIfNotExistList(databaseContext, tokenEntry,
                new Dictionary<string, string> {{"logo", baseLink + "/" + file.Name}}, false);
            tokenUrlCount++;
        }

        var tokensWithoutLogo = TokenMethods.GetTokensWithoutLogo(databaseContext);
        if ( !string.IsNullOrEmpty(defaultImg) )
        {
            Log.Verbose("[{Name}] building Links for Tokens with default Image", Name);
            foreach ( var token in tokensWithoutLogo )
            {
                Log.Verbose("[{Name}] building and adding Link for Token {Token}", Name, token.SYMBOL);
                TokenLogoMethods.InsertIfNotExistList(databaseContext, token,
                    new Dictionary<string, string> {{"logo", baseLink + "/" + defaultImg}}, false);
                tokenUrlCount++;
            }
        }

        if ( tokenUrlCount > 0 ) databaseContext.SaveChanges();
        Log.Information("[{Name}] plugin: {Count} Token for Urls processed", Name, tokenUrlCount);
    }
}
