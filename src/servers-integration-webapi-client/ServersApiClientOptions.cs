using MxIO.ApiClient;

namespace XtremeIdiots.Portal.ServersApiClient
{
    public class ServersApiClientOptions : IApiClientOptions
    {
        public string BaseUrl { get; }
        public string ApiKey { get; }
        public string ApiAudience { get; }
        public string? ApiPathPrefix { get; }

        public ServersApiClientOptions(string baseUrl, string apiKey, string apiAudience)
        {
            BaseUrl = baseUrl;
            ApiKey = apiKey;
            ApiAudience = apiAudience;
        }

        public ServersApiClientOptions(string baseUrl, string apiKey, string apiAudience, string apiPathPrefix) : this(baseUrl, apiKey, apiAudience)
        {
            ApiPathPrefix = apiPathPrefix;
        }
    }
}