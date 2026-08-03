using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MX.Api.Client.Caching;

using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Registration;
using XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1;

/// <summary>
/// Startup-composition regression test for the Portal Repository API client.
/// </summary>
/// <remarks>
/// Repository client 4.2.21 (on MX.Api.Client 2.3.76) fanned a single cache
/// delegate across every typed sub-API registration. When library caching
/// defaults were enabled on the consumer, host DI resolution crashed at
/// startup with
/// <c>System.ArgumentException: The expression must invoke a method declared by ...IAdminActionsApi ...</c>.
/// PR #1021 hotfixed production by removing the consumer <c>.WithCaching(...)</c>.
/// <para>
/// Repository client 4.2.22 (on MX.Api.Client 2.3.77) resolves the root cause
/// via a reflection-free <c>SharedCacheConfiguration</c> that scopes each
/// cache policy to its matching typed sub-API. This test exercises two
/// registration entry points that would have failed against the crashing
/// 4.2.21 / 2.3.76 pair:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       Booting the real <c>Program.cs</c> host through
///       <see cref="WebApplicationFactory{TEntryPoint}"/> — the same code path
///       that took down production — and resolving <see cref="IRepositoryApiClient"/>
///       plus every typed sub-API this service consumes, including
///       <c>IAdminActionsApi</c> (the interface named in the historical
///       ArgumentException).
///     </description>
///   </item>
///   <item>
///     <description>
///       Building a lightweight <see cref="ServiceCollection"/> via the shared
///       <see cref="RepositoryApiClientRegistration.AddPortalRepositoryApiClient"/>
///       extension with <c>ValidateOnBuild = true</c> so a container-level
///       misconfiguration is caught even when the WebApplicationFactory path is
///       skipped (e.g. faster local dev loops).
///     </description>
///   </item>
/// </list>
/// <para>
/// Lives in the unit-tests project so it runs under the default CI filter
/// (<c>FullyQualifiedName!~IntegrationTests</c>).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public class RepositoryApiClientRegistrationTests : IClassFixture<RepositoryApiClientRegistrationTests.HostBootFactory>
{
    private readonly HostBootFactory _factory;

    public RepositoryApiClientRegistrationTests(HostBootFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void HostBoot_Resolves_IRepositoryApiClient()
    {
        // Materialising the factory's Services property drives the full Program.cs
        // pipeline. Any registration-time crash — including the historical
        // 4.2.21 / 2.3.76 ArgumentException against IAdminActionsApi — surfaces here.
        using var scope = _factory.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IRepositoryApiClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void HostBoot_Resolves_AllTypedSubClientsThisHostConsumes()
    {
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IRepositoryApiClient>();

        // IAdminActionsApi is the exact sub-API the 4.2.21 / 2.3.76 crash reported
        // in its ArgumentException — materialising it here is the regression assertion.
        Assert.NotNull(client.AdminActions);
        Assert.NotNull(client.AdminActions.V1);

        // Every typed sub-API this host actually calls from controllers / helpers.
        Assert.NotNull(client.GameServers.V1);
        Assert.NotNull(client.Maps.V1);
        Assert.NotNull(client.GameServerConfigurations.V1);
        Assert.NotNull(client.GameServersEvents.V1);
    }

    [Fact]
    public void HostBoot_Registers_LibraryCacheDefaults_ForCachedSubClients()
    {
        // The 4.2.22 library caching defaults register a DefaultCachePolicies<T>
        // per typed sub-API the Repository client caches. If a future refactor
        // silently drops the consumer .WithCaching(...) call these resolutions
        // return null and the L1 cache benefit is gone — this test catches that.
        using var scope = _factory.Services.CreateScope();

        var gameServersPolicies = scope.ServiceProvider.GetService<DefaultCachePolicies<IGameServersApi>>();
        var mapsPolicies = scope.ServiceProvider.GetService<DefaultCachePolicies<IMapsApi>>();

        Assert.NotNull(gameServersPolicies);
        Assert.NotEmpty(gameServersPolicies!.Policies);

        Assert.NotNull(mapsPolicies);
        Assert.NotEmpty(mapsPolicies!.Policies);
    }

    [Fact]
    public void SharedRegistration_Extension_BuildsValidatedContainer()
    {
        // Independent, host-free assertion: the shared extension that Program.cs
        // uses must produce a container that passes ValidateOnBuild, so a plain
        // dotnet-test invocation without the WebApplicationFactory path still
        // catches container-level misconfiguration in the client's DI wiring.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [RepositoryApiClientRegistration.BaseUrlConfigurationKey] = "https://localhost/repository",
                [RepositoryApiClientRegistration.AudienceConfigurationKey] = "api://test-repository"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddMemoryCache();

        services.AddPortalRepositoryApiClient(configuration, cachePartition: "boot-smoke-test");

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IRepositoryApiClient>();
        Assert.NotNull(client);
        Assert.NotNull(client.AdminActions.V1);
    }

    /// <summary>
    /// <see cref="WebApplicationFactory{TEntryPoint}"/> that keeps the real
    /// Repository API client registration intact so a crash in the client
    /// library's DI wiring reproduces here rather than in production.
    /// </summary>
    public sealed class HostBootFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.UseSetting("RepositoryApi:BaseUrl", "https://localhost/repository");
            builder.UseSetting("RepositoryApi:ApplicationAudience", "api://test-repository");
            builder.UseSetting(
                "ApplicationInsights:ConnectionString",
                "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/;LiveEndpoint=https://localhost/");
            builder.UseSetting("ServiceBusConnection:fullyQualifiedNamespace", "test.servicebus.windows.net");

            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, HostBootTestAuthHandler>("Test", _ => { });

                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                });
            });
        }
    }

    internal sealed class HostBootTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public HostBootTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.Name, "TestUser"),
                    new Claim(ClaimTypes.Role, "ServiceAccount")
                },
                "Test");
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
