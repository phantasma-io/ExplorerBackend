using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json.Serialization;
using FluentValidation.AspNetCore;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Messaging;
using Backend.Service.Api.Caching;
using Backend.Service.Api.Converters;
using Backend.Service.Api.Events;
using Backend.Service.Api.Hosting;
using Backend.Service.Api.Metrics;
using Backend.Service.Api.Middleware;
using Backend.Service.Api.StartupActions;
using Backend.Service.Api.Swagger;
using Database.Main;
using Foundatio.Caching;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning.Conventions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Backend.Service.Api;

public class Startup
{
    public Startup(
        IConfiguration configuration
    )
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(
        IServiceCollection services
    )
    {
        services.AddDbContext<MainDbContext>();
        services.AddStartupActionToWaitForHealthChecks("Critical");
        services.AddHealthChecks()
            .AddCheckForStartupActions()
            .AddDbContextCheck<MainDbContext>(tags: new[]
            {
                "Critical",
            });

        services.AddHttpContextAccessor();
        services.AddTransient<IPrincipal>(sp => sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.User);
        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policyBuilder => { policyBuilder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); });
        });
        services.AddMvc()
            .AddJsonOptions(options =>
            {
                // Ensure settings here match GetDefaultSerializerOptions()
                options.JsonSerializerOptions.IncludeFields = true;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new EnumerableJsonConverterFactory());
            })
            .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<Startup>());
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));
        
        services.AddSingleton<ICacheClient>(sp => new InMemoryCacheClient(optionsBuilder =>
            optionsBuilder.CloneValues(true).MaxItems(10000)
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())));
        services.AddSingleton<IMessageBus>(sp => new InMemoryMessageBus(optionsBuilder =>
            optionsBuilder.LoggerFactory(sp.GetRequiredService<ILoggerFactory>())));

        services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<IMessageBus>());
        services.AddSingleton<IMessageSubscriber>(sp => sp.GetRequiredService<IMessageBus>());
        services.AddSingleton<IEndpointCacheManager, EndpointCacheManager>();
        services.AddSingleton<IEndpointMetrics, EndpointMetrics>();
        services.AddSingleton<IEventBus, EventBus>();
        services.AddHostedService<EventBusBackgroundService>();

        services.AddApiVersioning(options =>
        {
            options.ReportApiVersions = true;
            options.Conventions.Add(new VersionByNamespaceConvention());
            options.AssumeDefaultVersionWhenUnspecified = true;
        });
        services.AddVersionedApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        services.AddSwaggerGen(c =>
        {
            c.OperationFilter<SwaggerDefaultValues>();
            c.OperationFilter<ParameterObsoleteFilter>();
            c.DocumentFilter<InternalDocumentFilter>();
            c.EnableAnnotations();

            // Workaround, ref: https://github.com/swagger-api/swagger-ui/issues/7911
            //c.CustomSchemaIds(schema => schema.FullName);
            c.CustomSchemaIds(schema => schema.FullName!.Replace("+", "."));

            string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });

        services.AddStartupAction<DbConnectionWait>();
        services.AddStartupAction<LoadPlugins>();
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IApiVersionDescriptionProvider provider
    )
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        if (!env.EnvironmentName.Equals("Testing"))
        {
            app.UseWaitForStartupActionsBeforeServingRequests();
            app.UseSerilogRequestLogging();
        }

        app.UseCors();
        app.UseMiddleware<ErrorLoggingMiddleware>();
        app.UseMiddleware<PerformanceMiddleware>();
        app.UseRouting();
        app.UseMiddleware<CacheMiddleware>();

        // Public OpenAPI docs
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";

            // Present API version in descending order
            List<ApiVersionDescription> versionDescriptions = provider.ApiVersionDescriptions
                .OrderByDescending(desc => desc.ApiVersion)
                .ToList();

            foreach (ApiVersionDescription description in versionDescriptions)
            {
                options.SwaggerEndpoint($"{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
            }
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHealthChecks("/health");
            endpoints.MapControllers();
        });
        
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(AppContext.BaseDirectory, "../img")),
            RequestPath = "/img"
        });
    }
}
