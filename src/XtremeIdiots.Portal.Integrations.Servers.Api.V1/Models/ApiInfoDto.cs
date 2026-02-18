namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Models;

/// <summary>
/// Represents API build and version information
/// </summary>
public class ApiInfoDto
{
    /// <summary>
    /// Gets or sets the full informational version including build metadata
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the build version without build metadata
    /// </summary>
    public string BuildVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly version
    /// </summary>
    public string AssemblyVersion { get; set; } = string.Empty;
}
