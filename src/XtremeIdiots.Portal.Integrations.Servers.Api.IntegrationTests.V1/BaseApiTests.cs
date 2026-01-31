using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MX.Api.Client.Extensions;
using MX.Api.Client.Configuration;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.IntegrationTests.V1;

public class BaseApiTests : IAsyncLifetime
{
    protected IServersApiClient serversApiClient;

    public BaseApiTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
            .AddEnvironmentVariables()
            .Build();

        Console.WriteLine($"Using API Base URL: {configuration["api_base_url"]}");

        string baseUrl = configuration["api_base_url"] ?? throw new Exception("Environment variable 'api_base_url' is null - this needs to be set to invoke tests");
        string apiKey = configuration["api_key"] ?? throw new Exception("Environment variable 'api_key' is null - this needs to be set to invoke tests");
        string apiAudience = configuration["api_audience"] ?? throw new Exception("Environment variable 'api_audience' is null - this needs to be set to invoke tests");

        // Set up dependency injection using the service collection extension
        var services = new ServiceCollection();

        // Add console logging
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.SetMinimumLevel(LogLevel.Debug);
            loggingBuilder.AddProvider(new ConsoleLoggerProvider());
        });

        // Add ServersApiClient with conditional authentication
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl(baseUrl);

            // Check if running in GitHub Actions workflow
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
            {
                Console.WriteLine("Detected GitHub Actions environment - using client credentials authentication");

                string tenantId = configuration["tenant_id"] ?? throw new Exception("Environment variable 'tenant_id' is null - this needs to be set for GitHub Actions authentication");
                string clientId = configuration["client_id"] ?? throw new Exception("Environment variable 'client_id' is null - this needs to be set for GitHub Actions authentication");
                string clientSecret = configuration["client_secret"] ?? throw new Exception("Environment variable 'client_secret' is null - this needs to be set for GitHub Actions authentication");

                // Create client credential authentication options
                var clientCredOptions = new ClientCredentialAuthenticationOptions
                {
                    ApiAudience = apiAudience,
                    TenantId = tenantId,
                    ClientId = clientId
                };
                clientCredOptions.SetClientSecret(clientSecret);
                options.WithAuthentication(clientCredOptions);
            }
            else
            {
                Console.WriteLine("Detected local environment - using Azure credentials authentication");
                options.WithEntraIdAuthentication(apiAudience);
            }
        });

        var serviceProvider = services.BuildServiceProvider();
        serversApiClient = serviceProvider.GetRequiredService<IServersApiClient>();
    }

    public async Task InitializeAsync()
    {
        await WarmUp();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private async Task WarmUp()
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                _ = await serversApiClient.Root.V1.GetRoot();
                // Successfully warmed up, break out of the loop
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error performing warmup request");
                Console.WriteLine(ex);

                // Sleep for five seconds before trying again.
                await Task.Delay(5000);
            }
        }
    }
}
