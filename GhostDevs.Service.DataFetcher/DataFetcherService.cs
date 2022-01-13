using System;
using System.IO;
using System.Threading;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.PluginEngine;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

// ReSharper disable LoopVariableIsNeverChangedInsideLoop

namespace GhostDevs.Service.DataFetcher;

public static class DataFetcher
{
    private static int _fetchInterval = 30;

    private static readonly string ConfigDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..");

    private static string ConfigFile => Path.Combine(ConfigDirectory, "explorer-backend-config.json");


    private static void Main()
    {
        LoggingSettings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, false).Build()
            .GetSection("Logging"));

        //load cfg now, process loglevel then rest
        var loggingData = LoggingSettings.Default;
        if ( !Enum.TryParse(loggingData.Level, true, out LogEventLevel logLevel) ) logLevel = LogEventLevel.Information;

        Directory.CreateDirectory("../logs");
        LogEx.Init("../logs/data-fetcher-service-.log", logLevel, loggingData.LogOverwrite);
        Log.Information("\n\n*********************************************************\n" +
                        "************** Data Fetcher Service Started *************\n" +
                        "*********************************************************\n" +
                        "Log level: {Level}, LogOverwrite: {Overwrite}", logLevel,
            loggingData.LogOverwrite);

        Log.Information("Initializing Data Fetcher Service...");

        Settings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, false).Build()
            .GetSection("FetcherServiceConfiguration"));

        using ( var database = new MainDbContext() )
        {
            PostgreSQLConnector pgConnection = null;
            var max = MainDbContext.GetConnectionMaxRetries();
            var timeout = MainDbContext.GetConnectionRetryTimeout();

            Log.Debug("Getting Database Connection, MaxRetries {Max}, Timeout {Timeout}", max, timeout);

            for ( var i = 1; i <= max; i++ )
                try
                {
                    pgConnection = new PostgreSQLConnector(database.GetConnectionString());
                }
                catch ( Exception e )
                {
                    Log.Warning("Database connection error: {Message}", e.Message);
                    if ( i < max )
                    {
                        Thread.Sleep(timeout * i);
                        Log.Warning("Database connection: Trying again ({Index}/{Max})...", i, max);
                    }
                    else
                        throw;
                }

            if ( pgConnection != null ) Log.Information("PostgresSQL version: {Version}", pgConnection.GetVersion());

            // Add supported chains and tokens to the database.
            foreach ( var chain in Settings.Default.Chains )
            {
                Log.Information("Registering chain {Name}", chain.Name);

                ChainMethods.Upsert(database, chain.Name);
            }

            database.SaveChanges();

            foreach ( var symbol in Settings.Default.Tokens )
            {
                Log.Information("Registering token {Chain} {Symbol}", symbol.Chain, symbol.Symbol);

                var chainId = ChainMethods.GetId(database, symbol.Chain);
                TokenMethods.Upsert(database, chainId, symbol.Contract, symbol.Symbol);
            }

            database.SaveChanges();
        }

        Plugin.LoadPlugins();

        // Start up self contained plugins
        foreach ( var plugin in Plugin.DBAPlugins ) plugin.Startup();

        _fetchInterval = Settings.Default.FetchInterval;
        Log.Information("Data Fetcher Service is ready, Interval {Interval}", _fetchInterval);

        var running = true;

        new Thread(() =>
        {
            while ( running )
            {
                try
                {
                    foreach ( var plugin in Plugin.BlockchainPlugins ) plugin.Fetch();
                }
                catch ( Exception e )
                {
                    LogEx.Exception("Fetch", e);
                }

                Thread.Sleep(1000 * _fetchInterval);
            }
        }).Start();

        Console.CancelKeyPress += delegate
        {
            Log.Information("Terminating service...");
            running = false;
            try
            {
                // Stopping code.
                foreach ( var plugin in Plugin.DBAPlugins ) plugin.Shutdown();
            }
            catch ( Exception e )
            {
                Log.Error(e, "Termination service exception");
            }

            Environment.Exit(0);
        };

        Log.Information("Service is running...");
    }
}
