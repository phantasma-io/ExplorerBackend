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

        var logPath = "../logs";
        if ( !string.IsNullOrEmpty(loggingData.LogDirectoryPath) ) logPath = loggingData.LogDirectoryPath;

        Directory.CreateDirectory(logPath);
        LogEx.Init(Path.Combine(logPath, "data-fetcher-service-.log"), logLevel, loggingData.LogOverwrite);

        Log.Information("\n\n*********************************************************\n" +
                        "************** Data Fetcher Service Started *************\n" +
                        "*********************************************************\n" +
                        "Log level: {Level}, LogOverwrite: {Overwrite}, Path: {Path}, Config: {Config}", logLevel,
            loggingData.LogOverwrite, logPath, ConfigFile);

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
                    pgConnection = new PostgreSQLConnector(MainDbContext.GetConnectionString());
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

            if ( pgConnection != null ) Log.Information("PostgreSQL version: {Version}", pgConnection.GetVersion());

            // Add supported chains and tokens to the database.
            //supported tokens and chains are added by the plugin

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
