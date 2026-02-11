using System;
using System.Linq;
using System.Net.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Backend.Service.Api.Swagger;

public class InternalDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var description in context.ApiDescriptions)
        {
            var attribute = description.ActionDescriptor.EndpointMetadata.OfType<ApiInfoAttribute>()
                .FirstOrDefault();

            if (attribute is not { InternalEndpoint: true }) continue;

            var key = "/" + description.RelativePath?.TrimEnd('/');
            if (description.HttpMethod != null)
            {
                var operation = new HttpMethod(description.HttpMethod.ToUpperInvariant());

                swaggerDoc.Paths[key].Operations.Remove(operation);
            }

            // Drop the entire route of there are no operations left
            if (!swaggerDoc.Paths[key].Operations.Any()) swaggerDoc.Paths.Remove(key);

        }
    }
}
