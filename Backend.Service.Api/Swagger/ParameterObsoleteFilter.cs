using System;
using System.Linq;

namespace Backend.Service.Api.Swagger;

public class ParameterObsoleteFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(Microsoft.OpenApi.Models.OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
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
            if (parameter != null)
            {
                parameter.Deprecated = true;
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
