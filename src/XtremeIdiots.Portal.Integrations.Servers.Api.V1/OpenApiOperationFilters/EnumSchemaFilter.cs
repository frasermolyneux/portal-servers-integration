﻿using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.OpenApiOperationFilters
{
    public class EnumSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema model, SchemaFilterContext context)
        {
            if (context.Type.IsEnum)
            {
                model.Type = "string";
                model.Enum.Clear();

                Enum.GetNames(context.Type)
                    .ToList()
                    .ForEach(n => model.Enum.Add(new OpenApiString(n)));
            }
        }
    }
}
