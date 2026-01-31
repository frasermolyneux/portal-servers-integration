using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.OpenApiOperationFilters;

public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema model, SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            model.Type = "string";
            model.Enum.Clear();

            foreach (var name in Enum.GetNames(context.Type))
            {
                model.Enum.Add(new OpenApiString(name));
            }
        }
    }
}
