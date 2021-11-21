using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Database.Main;
using GhostDevs.PluginEngine;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;
using Foundatio.Caching;
using Foundatio.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using GhostDevs.Commons;
using GhostDevs.Service.Caching;
using GhostDevs.Service.Converters;
using GhostDevs.Service.Events;
using GhostDevs.Service.Hosting;
using GhostDevs.Service.Infrastructure;
using GhostDevs.Service.Middleware;
using GhostDevs.Service.Swagger;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Serilog;

namespace GhostDevs.Service
{
    public class Api
    {
        public static readonly int ApiVersion = 1;
        public static int RpcPort = 0;
        public static int RestPort = 0;
        public static int RestPortNoCompression = 0;
        public static string EndPoint;
        public static Endpoints API;
        private static string SslCertificatePath;
        private static string SslCertificatePassword;
        private static bool AllowSelfSignedCertificates;

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
                                if (!Enum.TryParse<Serilog.Events.LogEventLevel>(args[i + 1], true, out _logLevel))
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
                    case "--rpc-port":
                        {
                            if (i + 1 < args.Length)
                            {
                                RpcPort = Int32.Parse(args[i + 1]);
                            }

                            break;
                        }
                    case "--rest-port":
                        {
                            if (i + 1 < args.Length)
                            {
                                RestPort = Int32.Parse(args[i + 1]);
                            }

                            break;
                        }
                    case "--rest-port-no-compression":
                        {
                            if (i + 1 < args.Length)
                            {
                                RestPortNoCompression = Int32.Parse(args[i + 1]);
                            }

                            break;
                        }
                    case "--ssl-certificate-path":
                        {
                            if (i + 1 < args.Length)
                            {
                                SslCertificatePath = args[i + 1];
                            }
                            break;
                        }
                    case "--ssl-certificate-password":
                        {
                            if (i + 1 < args.Length)
                            {
                                SslCertificatePassword = args[i + 1];
                            }
                            break;
                        }
                    case "--allow-self-signed-certificates":
                        {
                            AllowSelfSignedCertificates = true;
                            break;
                        }
                }
            }

            System.IO.Directory.CreateDirectory("../logs");
            LogEx.Init($"../logs/api-service-.log", _logLevel, _logOverwriteMode);
            Log.Information("\n\n*********************************************************\n" +
                      "************** API Service Started **************\n" +
                      "*********************************************************\n" +
                      "Log level: " + _logLevel.ToString());

            Log.Information("Initializing APIService...");

            Settings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, optional: false).Build().GetSection("ApiServiceConfiguration"));

            PostgreSQLConnector pgConnection = null;
            using (var Database = new MainDatabaseContext())
            {
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
            }

            Log.Information($"PostgreSQL version: {pgConnection.GetVersion()}");

            if (!String.IsNullOrEmpty(SslCertificatePath))
            {
                Log.Information($"SSL is enabled. Certificate: {SslCertificatePath}, AllowSelfSignedCertificates: {AllowSelfSignedCertificates}");
            }

            Plugin.LoadPlugins();

            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddJsonFile(ConfigFile);
            builder.WebHost.UseSerilog();
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
                    new OpenApiInfo
                    {
                        Title = "Phantasma Explorer API",
                        Description = "",
                        Version = "v1"
                    });
                c.DocumentFilter<InternalDocumentFilter>();
            });
            var app = builder.Build();

            const string basePath = "/api/v1";
            var httpMethods = new List<Type>
            {
                typeof(HttpDeleteAttribute),
                typeof(HttpGetAttribute),
                //typeof(HttpHeadAttribute),
                //typeof(HttpOptionsAttribute),
                //typeof(HttpPatchAttribute),
                typeof(HttpPostAttribute),
                typeof(HttpPutAttribute)
            };

            var type = typeof(Endpoints);
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m =>
                m.GetCustomAttributes<APIInfoAttribute>().Any()).ToArray();

            using var scope = app.Services.CreateScope();
            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttributes().FirstOrDefault(a => httpMethods.Contains(a.GetType())) ??
                                new HttpGetAttribute();

                var methodName = method.Name.ToLowerInvariant();
                var path = $"{basePath}/{methodName}";

                var handler = Delegate.CreateDelegate(
                    Expression.GetDelegateType(method.GetParameters().Select(parameter => parameter.ParameterType)
                        .Concat(new[] { method.ReturnType }).ToArray()),
                    scope.ServiceProvider.GetRequiredService<IApiEndpoint>(), method);

                switch (attribute)
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

            Log.Information($"Phantasma Explorer API enabled. {methods.Length} methods available.");

            app.UseSerilogRequestLogging();
            app.UseCors();
            app.UseSwagger();
            app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1"));
            app.UseMiddleware<ErrorLoggingMiddleware>();
            app.UseMiddleware<CacheMiddleware>();
            app.Run();
        }
    }
}
