using Azure.Identity;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Identity.Web;

using System.Text.Json.Serialization;

using XtremeIdiots.Portal.Integrations.Servers.Api.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Factories.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using Asp.Versioning;
using XtremeIdiots.Portal.Repository.Api.Client.V1;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Scalar.AspNetCore;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var appConfigEndpoint = builder.Configuration["AzureAppConfiguration:Endpoint"];
var isAzureAppConfigurationEnabled = false;

if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
{
    var managedIdentityClientId = builder.Configuration["AzureAppConfiguration:ManagedIdentityClientId"];
    var environmentLabel = builder.Configuration["AzureAppConfiguration:Environment"];

    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ManagedIdentityClientId = managedIdentityClientId,
    });

    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(new Uri(appConfigEndpoint), credential)
            .Select("XtremeIdiots.Portal.Integrations.Servers.Api.V1:*", environmentLabel)
            .TrimKeyPrefix("XtremeIdiots.Portal.Integrations.Servers.Api.V1:")
            .Select("RepositoryApi:*", environmentLabel);

        options.ConfigureKeyVault(kv => kv.SetCredential(credential));
    });

    builder.Services.AddAzureAppConfiguration();
    isAzureAppConfigurationEnabled = true;
}

builder.Services.AddSingleton<ITelemetryInitializer, TelemetryInitializer>();
builder.Services.AddLogging();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

//https://learn.microsoft.com/en-us/azure/azure-monitor/app/sampling-classic-api#configure-sampling-settings
builder.Services.Configure<TelemetryConfiguration>(telemetryConfiguration =>
{
    var telemetryProcessorChainBuilder = telemetryConfiguration.DefaultTelemetrySink.TelemetryProcessorChainBuilder;
    telemetryProcessorChainBuilder.UseAdaptiveSampling(
        settings: new SamplingPercentageEstimatorSettings
        {
            InitialSamplingPercentage = double.TryParse(builder.Configuration["ApplicationInsights:InitialSamplingPercentage"], out var initPct) ? initPct : 5,
            MinSamplingPercentage = double.TryParse(builder.Configuration["ApplicationInsights:MinSamplingPercentage"], out var minPct) ? minPct : 5,
            MaxSamplingPercentage = double.TryParse(builder.Configuration["ApplicationInsights:MaxSamplingPercentage"], out var maxPct) ? maxPct : 60
        },
        callback: null,
        excludedTypes: "Exception");
    telemetryProcessorChainBuilder.Build();
});

builder.Services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions
{
    EnableAdaptiveSampling = false,
});

builder.Services.AddServiceProfiler();

// Add services to the container.
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// Configure API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    // Configure URL path versioning
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    // Format the version as "'v'major[.minor]" (e.g. v1.0)
    options.GroupNameFormat = "'v'VV";
    options.SubstituteApiVersionInUrl = true;
});

// Configure OpenAPI
builder.Services.AddOpenApi("v1.0", options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddDocumentTransformer<StripVersionPrefixTransformer>();
});

builder.Services.AddSingleton<IQueryClientFactory, QueryClientFactory>();
builder.Services.AddSingleton<IRconClientFactory, RconClientFactory>();

builder.Services.AddRepositoryApiClient(options => options
    .WithBaseUrl(builder.Configuration["RepositoryApi:BaseUrl"] ?? throw new InvalidOperationException("RepositoryApi:BaseUrl configuration is required"))
    .WithEntraIdAuthentication(builder.Configuration["RepositoryApi:ApplicationAudience"] ?? throw new InvalidOperationException("RepositoryApi:ApplicationAudience configuration is required")));

builder.Services.AddHealthChecks()
    .AddCheck<XtremeIdiots.Portal.Integrations.Servers.Api.V1.HealthChecks.RepositoryApiHealthCheck>(
        name: "repository-api",
        tags: ["dependency"]);

var app = builder.Build();

if (isAzureAppConfigurationEnabled)
{
    app.UseAzureAppConfiguration();
}

// Configure the HTTP request pipeline.
app.MapOpenApi();
app.MapScalarApiReference();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Ok()).ExcludeFromDescription();

app.Run();

// Required for WebApplicationFactory integration tests
public partial class Program { }
