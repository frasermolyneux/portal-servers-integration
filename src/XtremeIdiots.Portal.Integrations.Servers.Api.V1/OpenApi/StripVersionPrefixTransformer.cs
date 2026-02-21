using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.OpenApi;

/// <summary>
/// Strips the version prefix (e.g. /v1.0) from OpenAPI spec paths so that
/// APIM segment versioning can manage the version prefix. Without this, APIM
/// produces double-versioned paths like /v1/v1/...
/// </summary>
public partial class StripVersionPrefixTransformer : IOpenApiDocumentTransformer
{
    [GeneratedRegex(@"^/v\d+(\.\d+)?", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPrefixRegex();

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var updatedPaths = new OpenApiPaths();

        foreach (var (path, pathItem) in document.Paths)
        {
            var newPath = VersionPrefixRegex().Replace(path, string.Empty);

            // Ensure the path still starts with /
            if (!newPath.StartsWith('/'))
                newPath = "/" + newPath;

            updatedPaths.Add(newPath, pathItem);
        }

        document.Paths = updatedPaths;

        return Task.CompletedTask;
    }
}
