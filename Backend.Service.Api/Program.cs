using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Backend.Service.Api;

public static class Program
{
    private static readonly string configDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..");

    private static string ConfigFile => Path.Combine(configDirectory, "explorer-backend-config.json");

    private static IConfiguration _configuration;

    public static async Task<int> Main(
        string[] args
    )
    {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile(ConfigFile, false, true)
            .AddEnvironmentVariables()
            .Build();

        IConfigurationSection configSection = Program._configuration.GetSection("ApiServiceConfiguration");

        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();
        
        
        try
        {
            Settings.Load(configSection);
            Log.Information("Starting host");

            await Program.BootstrapHostBuilder(args).Build().RunAsync();

            return 0;
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Host terminated unexpectedly");

            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
    
    private static IHostBuilder BootstrapHostBuilder(
        string[] args
    )
    {
        IHostBuilder builder = Host.CreateDefaultBuilder(args);

        if (!Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")?.Equals("Testing") ?? true)
        {
            builder = builder.UseSerilog((
                _,
                _,
                configuration
            ) => configuration.ReadFrom.Configuration(Program._configuration));
        }

        return builder.ConfigureWebHostDefaults(webBuilder =>
            webBuilder.UseConfiguration(Program._configuration).UseStartup<Startup>());
    }
}
