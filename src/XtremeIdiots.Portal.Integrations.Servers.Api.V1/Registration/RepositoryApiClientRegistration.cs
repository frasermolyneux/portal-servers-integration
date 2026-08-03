using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Registration;

/// <summary>
/// Encapsulates the Portal Repository API client DI registration for this host so that
/// Program.cs and the startup-composition regression tests exercise the exact same
/// registration path.
/// </summary>
/// <remarks>
/// The client is registered with base URL + Entra ID auth and opts into the
/// Repository client (4.2.22) library caching defaults, which are backed by
/// MX.Api.Client 2.3.77's reflection-free <c>SharedCacheConfiguration</c>. Each
/// cache policy is scoped to the typed sub-API it targets, so this no longer
/// reproduces the 4.2.21 / MX.Api.Client 2.3.76 cross-sub-API expression crash
/// that took down consumers with an <c>ArgumentException</c> against
/// <c>IAdminActionsApi</c> at startup.
/// <para>
/// Cached surface (per the Repository client's library defaults):
/// short-TTL single/list reads on <c>IGameServersApi</c> and longer-TTL reads on
/// <c>IMapsApi</c>. Every method this host invokes on those sub-APIs is a plain
/// read with no in-request follow-up write, so cached responses can never mask a
/// mutation performed by this service (this service never writes game servers or
/// maps — it only reads them to look up transport settings, resolve map metadata,
/// and drive RCON/query flows).
/// </para>
/// </remarks>
internal static class RepositoryApiClientRegistration
{
    /// <summary>
    /// Configuration key holding the Repository API base URL.
    /// </summary>
    internal const string BaseUrlConfigurationKey = "RepositoryApi:BaseUrl";

    /// <summary>
    /// Configuration key holding the Repository API Entra ID audience.
    /// </summary>
    internal const string AudienceConfigurationKey = "RepositoryApi:ApplicationAudience";

    /// <summary>
    /// Registers the Portal Repository API client with the same options wiring used by
    /// <c>Program.cs</c>. The <paramref name="cachePartition"/> should be the host
    /// application name so cache entries are isolated per consumer.
    /// </summary>
    public static IServiceCollection AddPortalRepositoryApiClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string cachePartition)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePartition);

        var baseUrl = configuration[BaseUrlConfigurationKey]
            ?? throw new InvalidOperationException($"{BaseUrlConfigurationKey} configuration is required");
        var audience = configuration[AudienceConfigurationKey]
            ?? throw new InvalidOperationException($"{AudienceConfigurationKey} configuration is required");

        services.AddRepositoryApiClient(options => options
            .WithBaseUrl(baseUrl)
            .WithEntraIdAuthentication(audience)
            .WithCachePartition(cachePartition)
            .WithCaching(c => c.UseLibraryDefaults()));

        return services;
    }
}
