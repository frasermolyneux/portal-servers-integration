namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// Controls how fake APIs respond to unconfigured requests.
/// </summary>
public enum DefaultBehavior
{
    /// <summary>
    /// Return a generic successful response with default DTOs.
    /// </summary>
    ReturnGenericSuccess,

    /// <summary>
    /// Return an error response (e.g., 404 Not Found).
    /// </summary>
    ReturnError
}
