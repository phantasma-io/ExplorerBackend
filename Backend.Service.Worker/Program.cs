using System;
using System.IO;
using System.Threading;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Microsoft.Extensions.Configuration;
using Serilog;

// ReSharper disable LoopVariableIsNeverChangedInsideLoop

namespace Backend.Service.Worker;

public static class Worker
{
    private static int _fetchInterval = 30;

    private static readonly string configDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..");

    private static string ConfigFile => Path.Combine(configDirectory, "explorer-backend-config.json");

	public static IConfiguration Configuration { get; private set; } = new ConfigurationBuilder()
		.AddJsonFile(ConfigFile)
        .Build();

    private static void Main()
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(Configuration)
            .CreateLogger();

        Log.Information("Initializing worker service... Configuration file: {ConfigFile}", ConfigFile);

        Settings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, false).Build()
            .GetSection("FetcherServiceConfiguration"));

        using ( var database = new MainDbContext() )
        {
            PostgreSQLConnector.PostgreSQLConnector pgConnection = null;
            var max = MainDbContext.GetConnectionMaxRetries();
            var timeout = MainDbContext.GetConnectionRetryTimeout();

            Log.Debug("Getting Database Connection, MaxRetries {Max}, Timeout {Timeout}", max, timeout);

            for ( var i = 1; i <= max; i++ )
                try
                {
                    pgConnection = new PostgreSQLConnector.PostgreSQLConnector(MainDbContext.GetConnectionString());
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
        Log.Information("Worker Service is ready, Interval {Interval}", _fetchInterval);

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
