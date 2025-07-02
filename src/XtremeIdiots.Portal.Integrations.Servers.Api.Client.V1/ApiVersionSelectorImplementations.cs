using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class VersionedMapsApi : IVersionedMapsApi
    {
        public VersionedMapsApi(IMapsApi v1Api)
        {
            V1 = v1Api;
        }

        public IMapsApi V1 { get; }
    }

    public class VersionedQueryApi : IVersionedQueryApi
    {
        public VersionedQueryApi(IQueryApi v1Api)
        {
            V1 = v1Api;
        }

        public IQueryApi V1 { get; }
    }

    public class VersionedRconApi : IVersionedRconApi
    {
        public VersionedRconApi(IRconApi v1Api)
        {
            V1 = v1Api;
        }

        public IRconApi V1 { get; }
    }
}