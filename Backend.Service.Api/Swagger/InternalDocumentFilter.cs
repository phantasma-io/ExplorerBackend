﻿using System;
using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Backend.Service.Api.Swagger;

public class InternalDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach ( var description in context.ApiDescriptions )
        {
            var attribute = description.ActionDescriptor.EndpointMetadata.OfType<ApiInfoAttribute>()
                .FirstOrDefault();

            if ( attribute is not {InternalEndpoint: true} ) continue;

            var key = "/" + description.RelativePath?.TrimEnd('/');
            if ( description.HttpMethod != null )
            {
                var operation = ( OperationType ) Enum.Parse(typeof(OperationType), description.HttpMethod, true);

                swaggerDoc.Paths[key].Operations.Remove(operation);
            }

            // Drop the entire route of there are no operations left
            if ( !swaggerDoc.Paths[key].Operations.Any() ) swaggerDoc.Paths.Remove(key);

            var referenceSchema = swaggerDoc.Paths.SelectMany(p => p.Value.Operations.Values)
                .SelectMany(o => o.Responses.Values).SelectMany(r => r.Content.Values).Select(c => c.Schema)
                .SelectMany(x => x.EnumerateSchema(swaggerDoc.Components.Schemas)).ToArray();

            var list1 = referenceSchema.Where(s => s.Reference != null).Select(s => s.Reference.Id).ToList();
            var list2 = referenceSchema.Where(s => s.Items?.Reference != null)
                .Select(s => s.Items.Reference.Id)
                .ToList();
            var list3 = list1.Concat(list2).Distinct().ToArray();

            var listOfUnreferencedDefinition = swaggerDoc.Components.Schemas
                .Where(x => list3.All(y => y != x.Key)).ToList();

            foreach ( var unreferencedDefinition in listOfUnreferencedDefinition )
                swaggerDoc.Components.Schemas.Remove(unreferencedDefinition.Key);
        }
    }
}
