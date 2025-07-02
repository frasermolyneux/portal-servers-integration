using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.IntegrationTests.V1;

public class BaseApiTests
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

        // Add ServersApiClient with configuration
        services.AddServersApiClient(options =>
        {
            options.BaseUrl = baseUrl;
            options.PrimaryApiKey = apiKey;
            options.ApiAudience = apiAudience;
            options.ApiPathPrefix = configuration["api_path_prefix"] ?? "servers-integration";
        });

        var serviceProvider = services.BuildServiceProvider();
        serversApiClient = serviceProvider.GetRequiredService<IServersApiClient>();

        WarmUp().Wait();
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
                Thread.Sleep(5000);
            }
        }
    }
}
