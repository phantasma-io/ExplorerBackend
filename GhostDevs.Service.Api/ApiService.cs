using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using Database.Main;
using Foundatio.Caching;
using Foundatio.Messaging;
using GhostDevs.Commons;
using GhostDevs.PluginEngine;
using GhostDevs.Service.Caching;
using GhostDevs.Service.Converters;
using GhostDevs.Service.Events;
using GhostDevs.Service.Hosting;
using GhostDevs.Service.Infrastructure;
using GhostDevs.Service.Middleware;
using GhostDevs.Service.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace GhostDevs.Service.Api;

public static class Api
{
    private static readonly string ConfigDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..");

    private static string ConfigFile => Path.Combine(ConfigDirectory, "explorer-backend-config.json");


    private static void Main(string[] args)
    {
        LoggingSettings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, false).Build()
            .GetSection("Logging"));

        var loggingData = LoggingSettings.Default;
        if ( !Enum.TryParse(loggingData.Level, true, out LogEventLevel logLevel) ) logLevel = LogEventLevel.Information;

        var logPath = "../logs";
        if ( !string.IsNullOrEmpty(loggingData.LogDirectoryPath) ) logPath = loggingData.LogDirectoryPath;

        Directory.CreateDirectory(logPath);
        LogEx.Init(Path.Combine(logPath, "api-service-.log"), logLevel, loggingData.LogOverwrite);

        Log.Information("\n\n*********************************************************\n" +
                        "************** API Service Started **************\n" +
                        "*********************************************************\n" +
                        "Log level: {Level}, LogOverwrite: {Overwrite}, Path: {Path}, Config: {Config}", logLevel,
            loggingData.LogOverwrite, logPath, ConfigFile);

        Log.Information("Initializing APIService...");

        Settings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, false).Build()
            .GetSection("ApiServiceConfiguration"));

        PostgreSQLConnector pgConnection = null;

        var max = MainDbContext.GetConnectionMaxRetries();
        var timeout = MainDbContext.GetConnectionRetryTimeout();
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

        Plugin.LoadPlugins();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Configuration.AddJsonFile(ConfigFile);
        //builder.WebHost.UseSerilog();
        //obsolete note told me I should use IHostBuilder, instead of IWebHostBuilder for serilog
        builder.Host.UseSerilog();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.IncludeFields = true;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new EnumerableJsonConverterFactory());
        });
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policyBuilder => { policyBuilder.AllowAnyOrigin(); });
        });
        builder.Services.AddTransient<IApiEndpoint, Endpoints>();
        builder.Services.AddSingleton<ICacheClient>(sp => new InMemoryCacheClient(optionsBuilder =>
            optionsBuilder.CloneValues(true).MaxItems(10000)
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())));
        builder.Services.AddSingleton<IMessageBus>(sp => new InMemoryMessageBus(optionsBuilder =>
            optionsBuilder.LoggerFactory(sp.GetRequiredService<ILoggerFactory>())));
        builder.Services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<IMessageBus>());
        builder.Services.AddSingleton<IMessageSubscriber>(sp => sp.GetRequiredService<IMessageBus>());
        builder.Services.AddScoped<IEndpointCacheManager, EndpointCacheManager>();
        builder.Services.AddSingleton<IEventBus, EventBus>();
        builder.Services.AddHostedService<EventBusBackgroundService>();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1",
                new OpenApiInfo {Title = "Phantasma Explorer API", Description = "", Version = "v1"});
            c.DocumentFilter<InternalDocumentFilter>();
        });
        var app = builder.Build();

        const string basePath = "/api/v1";
        var httpMethods = new List<Type>
        {
            //typeof(HttpDeleteAttribute),
            typeof(HttpGetAttribute),
            //typeof(HttpHeadAttribute),
            //typeof(HttpOptionsAttribute),
            //typeof(HttpPatchAttribute),
            typeof(HttpPostAttribute)
            //typeof(HttpPutAttribute)
        };

        var type = typeof(Endpoints);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m =>
            m.GetCustomAttributes<APIInfoAttribute>().Any()).ToArray();

        using var scope = app.Services.CreateScope();
        foreach ( var method in methods )
        {
            var attribute = method.GetCustomAttributes().FirstOrDefault(a => httpMethods.Contains(a.GetType())) ??
                            new HttpGetAttribute();

            var methodName = method.Name.ToLowerInvariant();
            var path = $"{basePath}/{methodName}";

            var handler = Delegate.CreateDelegate(
                Expression.GetDelegateType(method.GetParameters().Select(parameter => parameter.ParameterType)
                    .Concat(new[] {method.ReturnType}).ToArray()),
                scope.ServiceProvider.GetRequiredService<IApiEndpoint>(), method);

            switch ( attribute )
            {
                case HttpDeleteAttribute:
                    app.MapDelete(path, handler);
                    break;
                case HttpPostAttribute:
                    app.MapPost(path, handler);
                    break;
                case HttpPutAttribute:
                    app.MapPut(path, handler);
                    break;
                default:
                    // Assume GET
                    app.MapGet(path, handler);
                    break;
            }
        }

        Log.Information("Phantasma Explorer API enabled. {Methods} methods available", methods.Length);

        app.UseSerilogRequestLogging();
        app.UseCors();
        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1"));
        app.UseMiddleware<ErrorLoggingMiddleware>();
        app.UseMiddleware<CacheMiddleware>();
        app.Run();
    }
}
