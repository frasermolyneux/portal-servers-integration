using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Configuration;

/// <summary>
/// Configures the Swagger generation options for API versioning.
/// </summary>
public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureSwaggerOptions"/> class.
    /// </summary>
    /// <param name="provider">The <see cref="IApiVersionDescriptionProvider">provider</see> used to generate Swagger documents.</param>
    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) => this.provider = provider;

    /// <inheritdoc />
    public void Configure(SwaggerGenOptions options)
    {
        // Add a swagger document for each discovered API version
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(
                description.GroupName,
                new OpenApiInfo
                {
                    Title = $"Server Integration API V1 ({description.ApiVersion})",
                    Version = description.ApiVersion.ToString(),
                    Description = description.IsDeprecated
                        ? "This API version has been deprecated."
                        : GetVersionDescription(description.ApiVersion)
                });
        }

        // Include XML comments if available
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }

    private static string GetVersionDescription(Asp.Versioning.ApiVersion apiVersion)
    {
        return $"Server Integration API V1 {apiVersion}";
    }
}
