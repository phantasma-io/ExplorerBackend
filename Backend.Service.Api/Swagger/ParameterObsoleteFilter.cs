using System;
using System.Linq;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Backend.Service.Api.Swagger;

public class ParameterObsoleteFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation == null || context == null || context.ApiDescription?.ParameterDescriptions == null)
        {
            return;
        }

        var parametersToObsolete = context.ApiDescription.ParameterDescriptions
            .Where(parameterDescription => ParameterObsoleteFilter.ParameterHasDepreciate(parameterDescription))
            .ToList();
        foreach (var parameterToObsolete in parametersToObsolete)
        {
            var parameter = operation.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, parameterToObsolete.Name, System.StringComparison.Ordinal));
            if (parameter is OpenApiParameter mutableParameter)
            {
                mutableParameter.Deprecated = true;
            }
        }

    }
    private static bool ParameterHasDepreciate(Microsoft.AspNetCore.Mvc.ApiExplorer.ApiParameterDescription parameterDescription)
    {
        if (parameterDescription.ModelMetadata is Microsoft.AspNetCore.Mvc.ModelBinding.Metadata.DefaultModelMetadata metadata)
        {
            return
                (metadata.Attributes.Attributes?.Any(attribute => attribute is ObsoleteAttribute) ?? false);
        }

        return false;
    }
}
