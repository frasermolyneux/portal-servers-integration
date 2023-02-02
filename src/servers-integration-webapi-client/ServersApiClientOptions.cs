using MxIO.ApiClient;

namespace XtremeIdiots.Portal.ServersApiClient
{
    public class ServersApiClientOptions : ApiClientOptions
    {
        public ServersApiClientOptions(string baseUrl, string apiKey, string apiAudience) : base(baseUrl, apiKey, apiAudience)
        {

        }

        public ServersApiClientOptions(string baseUrl, string apiKey, string apiAudience, string apiPathPrefix) : base(baseUrl, apiKey, apiAudience, apiPathPrefix)
        {

        }
    }
}