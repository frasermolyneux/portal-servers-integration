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

    public class VersionedCoD4xRconApi : IVersionedCoD4xRconApi
    {
        public VersionedCoD4xRconApi(ICoD4xRconApi v1Api)
        {
            V1 = v1Api;
        }

        public ICoD4xRconApi V1 { get; }
    }

    public class VersionedCod2RconApi : IVersionedCod2RconApi
    {
        public VersionedCod2RconApi(ICod2RconApi v1Api)
        {
            V1 = v1Api;
        }

        public ICod2RconApi V1 { get; }
    }

    public class VersionedCod4RconApi : IVersionedCod4RconApi
    {
        public VersionedCod4RconApi(ICod4RconApi v1Api)
        {
            V1 = v1Api;
        }

        public ICod4RconApi V1 { get; }
    }

    public class VersionedCod5RconApi : IVersionedCod5RconApi
    {
        public VersionedCod5RconApi(ICod5RconApi v1Api)
        {
            V1 = v1Api;
        }

        public ICod5RconApi V1 { get; }
    }

    public class VersionedInsurgencyRconApi : IVersionedInsurgencyRconApi
    {
        public VersionedInsurgencyRconApi(IInsurgencyRconApi v1Api)
        {
            V1 = v1Api;
        }

        public IInsurgencyRconApi V1 { get; }
    }

    public class VersionedRustRconApi : IVersionedRustRconApi
    {
        public VersionedRustRconApi(IRustRconApi v1Api)
        {
            V1 = v1Api;
        }

        public IRustRconApi V1 { get; }
    }

    public class VersionedL4d2RconApi : IVersionedL4d2RconApi
    {
        public VersionedL4d2RconApi(IL4d2RconApi v1Api)
        {
            V1 = v1Api;
        }

        public IL4d2RconApi V1 { get; }
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

    public class VersionedFilesApi : IVersionedFilesApi
    {
        public VersionedFilesApi(IFilesApi v1Api)
        {
            V1 = v1Api;
        }

        public IFilesApi V1 { get; }
    }
}