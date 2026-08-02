using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.IntegrationTests.V1;

/// <summary>
/// Boots the API host with the real Repository API client registration intact so that
/// a regression in the client library's default DI wiring (for example, an
/// expression/interface mismatch for <c>IAdminActionsApi</c> introduced by enabling
/// library caching defaults) surfaces as a resolution failure here rather than at
/// production startup.
/// </summary>
public class RealRepositoryApiClientWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("RepositoryApi:BaseUrl", "https://localhost");
        builder.UseSetting("RepositoryApi:ApplicationAudience", "api://test");
        builder.UseSetting("ApplicationInsights:ConnectionString", "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/;LiveEndpoint=https://localhost/");

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, RealClientTestAuthHandler>("Test", _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            });
        });
    }
}

internal sealed class RealClientTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public RealClientTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
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

/// <summary>
/// End-to-end DI resolution regression test for the Repository API client registration.
///
/// Historically, enabling the 4.2.21 Repository client library caching defaults
/// (via <c>.WithCachePartition(...).WithCaching(c =&gt; c.UseLibraryDefaults())</c>)
/// caused an expression/interface mismatch for <see cref="Repository.Abstractions.Interfaces.V1.IAdminActionsApi"/>
/// that crashed portal-sync and portal-repository-func at startup. The hotfix leaves the
/// Repository client wired with base URL + Entra ID auth only. These tests boot the real host
/// and force the DI container to materialize <see cref="IRepositoryApiClient"/> and each
/// representative subclient exposed by it, so any re-introduction of the crashing defaults
/// is caught by CI instead of production.
/// </summary>
[Trait("Category", "Integration")]
public class RepositoryClientResolutionTests : IClassFixture<RealRepositoryApiClientWebApplicationFactory>
{
    private readonly RealRepositoryApiClientWebApplicationFactory _factory;

    public RepositoryClientResolutionTests(RealRepositoryApiClientWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Host_Builds_AndResolvesRepositoryApiClient()
    {
        // Forces WebApplicationFactory to build the host; a startup crash (as seen on
        // portal-sync / portal-repository-func with the caching defaults enabled) would
        // throw here before we ever reached the assertion.
        using var scope = _factory.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IRepositoryApiClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void RepositoryApiClient_Exposes_IAdminActionsApi_AndRepresentativeSubclients()
    {
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IRepositoryApiClient>();

        // IAdminActionsApi is the interface that triggered the original expression/interface
        // mismatch when library caching defaults were enabled. Accessing it here proves the
        // container can materialize the subclient without crashing.
        Assert.NotNull(client.AdminActions);
        Assert.NotNull(client.AdminActions.V1);

        // Representative subclients consumed by this API's controllers and helpers.
        Assert.NotNull(client.GameServers.V1);
        Assert.NotNull(client.Maps.V1);
        Assert.NotNull(client.GameServerConfigurations.V1);
        Assert.NotNull(client.GameServersEvents.V1);
    }
}
