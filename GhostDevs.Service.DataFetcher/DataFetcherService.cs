using System;
using System.Threading;
using Database.Main;
using GhostDevs.PluginEngine;
using Microsoft.Extensions.Configuration;
using GhostDevs.Commons;
using Serilog;

namespace GhostDevs.Service
{
    public class DataFetcher
    {
        private static int FetchInterval = 30;

        private static readonly string ConfigDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..");
        private static string ConfigFile => System.IO.Path.Combine(ConfigDirectory, "explorer-backend-config.json");

        static void Main(string[] args)
        {
            Serilog.Events.LogEventLevel _logLevel = Serilog.Events.LogEventLevel.Information;
            bool _logOverwriteMode = true;

            // Checking if log options are set in command line.
            // They override settings (for debug purposes).
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--log-level":
                        {
                            if (i + 1 < args.Length)
                            {
                                if(!Enum.TryParse<Serilog.Events.LogEventLevel>(args[i + 1], true, out _logLevel))
                                    _logLevel = Serilog.Events.LogEventLevel.Information;
                            }

                            break;
                        }
                    case "--log-overwrite-mode":
                        {
                            if (i + 1 < args.Length)
                            {
                                _logOverwriteMode = args[i + 1] == "1";
                            }

                            break;
                        }
                    case "--fetch-interval":
                        {
                            if (i + 1 < args.Length)
                            {
                                FetchInterval = Int32.Parse(args[i + 1]);
                            }

                            break;
                        }
                }
            }

            System.IO.Directory.CreateDirectory("../logs");
            LogEx.Init($"../logs/data-fetcher-service-.log", _logLevel, _logOverwriteMode);
            Log.Information("\n\n*********************************************************\n" +
                      "************** Data Fetcher Service Started *************\n" +
                      "*********************************************************\n" +
                      "Log level: " + _logLevel.ToString());

            Log.Information("Initializing Data Fetcher Service...");

            Settings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, optional: false).Build().GetSection("FetcherServiceConfiguration"));

            using (var Database = new MainDatabaseContext())
            {

                PostgreSQLConnector pgConnection = null;
                int max = 6;
                for (int i = 1; i <= max; i++)
                {
                    try
                    {
                        pgConnection = new PostgreSQLConnector(Database.GetConnectionString());
                    }
                    catch (Exception e)
                    {
                        Log.Warning($"Database connection error: {e.Message}");
                        if (i < max)
                        {
                            Thread.Sleep(5000 * i);
                            Log.Warning($"Database connection: Trying again...");
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                Log.Information($"PostgreSQL version: {pgConnection.GetVersion()}");

                // Add supported chains and tokens to the database.
                foreach (var chain in Settings.Default.Chains)
                {
                    Log.Information($"Registering chain {chain.Name}");

                    ChainMethods.Upsert(Database, chain.Name);
                }
                Database.SaveChanges();

                foreach (var symbol in Settings.Default.Tokens)
                {
                    Log.Information($"Registering token {symbol.Chain} {symbol.Symbol}");
                    
                    var chainId = ChainMethods.GetId(Database, symbol.Chain);
                    TokenMethods.Upsert(Database, chainId, symbol.Contract, symbol.Symbol);
                }
                Database.SaveChanges();
            }

            Plugin.LoadPlugins();

            // Start up self contained plugins
            foreach (IDBAccessPlugin plugin in Plugin.DBAPlugins)
            {
                plugin.Startup();
            }

            Log.Information("Data Fetcher Service is ready");

            bool running = true;

            new Thread(() =>
            {
                while (running)
                {
                    try
                    {
                        foreach (IBlockchainPlugin plugin in Plugin.BlockchainPlugins)
                        {
                            plugin.Fetch();
                        }
                    }
                    catch(Exception e)
                    {
                        LogEx.Exception("Fetch", e);
                    }

                    Thread.Sleep(1000 * FetchInterval);
                }
            }).Start();

            Console.CancelKeyPress += delegate {
                Log.Information("Terminating service...");
                running = false;
                try
                {
                    // Stopping code.
                    foreach (IDBAccessPlugin plugin in Plugin.DBAPlugins)
                    {
                        plugin.Shutdown();
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Termination service exception");
                }

                Environment.Exit(0);
            };

            Log.Information($"Service is running...");
        }
    }
}
