using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.IntegrationTests.V1;

/// <summary>
/// Custom WebApplicationFactory that replaces external dependencies with mocks
/// and bypasses authentication for in-process integration testing.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IQueryClientFactory> MockQueryClientFactory { get; } = new();
    public Mock<IRconClientFactory> MockRconClientFactory { get; } = new();
    public Mock<IRepositoryApiClient> MockRepositoryApiClient { get; } = new();

    public void ResetMocks()
    {
        MockQueryClientFactory.Reset();
        MockRconClientFactory.Reset();
        MockRepositoryApiClient.Reset();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("RepositoryApi:BaseUrl", "https://localhost");
        builder.UseSetting("RepositoryApi:ApplicationAudience", "api://test");
        builder.UseSetting("ApplicationInsights:ConnectionString", "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/;LiveEndpoint=https://localhost/");

        builder.ConfigureTestServices(services =>
        {
            // Remove real factory and client registrations
            services.RemoveAll<IQueryClientFactory>();
            services.RemoveAll<IRconClientFactory>();
            services.RemoveAll<IRepositoryApiClient>();

            // Register mocks
            services.AddSingleton(MockQueryClientFactory.Object);
            services.AddSingleton(MockRconClientFactory.Object);
            services.AddSingleton(MockRepositoryApiClient.Object);

            // Replace authentication with a test scheme that always authenticates
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

            // Override the default authentication scheme
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            });
        });
    }
}

/// <summary>
/// Test authentication handler that creates an authenticated user with the ServiceAccount role.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.Role, "ServiceAccount")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

