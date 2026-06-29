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

    public class VersionedCoD4xRconApi : IVersionedCoD4xRconApi
    {
        public VersionedCoD4xRconApi(ICoD4xRconApi v1Api)
        {
            V1 = v1Api;
        }

        public ICoD4xRconApi V1 { get; }
    }

    public class VersionedApiHealthApi : IVersionedApiHealthApi
    {
        public VersionedApiHealthApi(IApiHealthApi v1Api)
        {
            V1 = v1Api;
        }

        public IApiHealthApi V1 { get; }
    }

    public class VersionedApiInfoApi : IVersionedApiInfoApi
    {
        public VersionedApiInfoApi(IApiInfoApi v1Api)
        {
            V1 = v1Api;
        }

        public IApiInfoApi V1 { get; }
    }

    public class VersionedConfigApi : IVersionedConfigApi
    {
        public VersionedConfigApi(IConfigApi v1Api)
        {
            V1 = v1Api;
        }

        public IConfigApi V1 { get; }
    }

    public class VersionedFileBrowseApi : IVersionedFileBrowseApi
    {
        public VersionedFileBrowseApi(IFileBrowseApi v1Api)
        {
            V1 = v1Api;
        }

        public IFileBrowseApi V1 { get; }
    }
}