using System;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Backend.Service.Api.Swagger;

public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigureSwaggerOptions" /> class.
    /// </summary>
    /// <param name="provider">
    ///     The <see cref="IApiVersionDescriptionProvider">provider</see> used to generate Swagger
    ///     documents.
    /// </param>
    public ConfigureSwaggerOptions(
        IApiVersionDescriptionProvider provider
    )
    {
        _provider = provider;
    }

    /// <inheritdoc />
    public void Configure(
        SwaggerGenOptions options
    )
    {
        foreach (ApiVersionDescription description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, ConfigureSwaggerOptions.CreateInfoForApiVersion(description));
            options.SwaggerDoc($"{description.GroupName}-internal", ConfigureSwaggerOptions.CreateInfoForApiVersion(description, true));
        }
    }

    private static OpenApiInfo CreateInfoForApiVersion(
        ApiVersionDescription description,
        bool internalDoc = false
    )
    {
        OpenApiInfo info = new()
        {
            Title = "Phansatma Explorer API",
            Version = description.ApiVersion.ToString(),
            Description = "Explorer for Phansatma blockchain",
            Contact = new OpenApiContact { Name = "Phantasma", Url = new Uri("https://phantasma.info") }
        };

        if (internalDoc)
        {
            info.Title += " (Internal)";
        }

        if (description.IsDeprecated)
        {
            info.Description += " This API version has been deprecated.";
        }

        return info;
    }
}
