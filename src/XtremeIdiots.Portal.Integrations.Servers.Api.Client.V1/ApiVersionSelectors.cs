using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public interface IVersionedMapsApi
    {
        IMapsApi V1 { get; }
    }

    public interface IVersionedQueryApi
    {
        IQueryApi V1 { get; }
    }

    public interface IVersionedCoD4xRconApi
    {
        ICoD4xRconApi V1 { get; }
    }

    public interface IVersionedCod2RconApi
    {
        ICod2RconApi V1 { get; }
    }

    public interface IVersionedCod4RconApi
    {
        ICod4RconApi V1 { get; }
    }

    public interface IVersionedCod5RconApi
    {
        ICod5RconApi V1 { get; }
    }

    public interface IVersionedInsurgencyRconApi
    {
        IInsurgencyRconApi V1 { get; }
    }

    public interface IVersionedRustRconApi
    {
        IRustRconApi V1 { get; }
    }

    public interface IVersionedL4d2RconApi
    {
        IL4d2RconApi V1 { get; }
    }

    public interface IVersionedApiHealthApi
    {
        IApiHealthApi V1 { get; }
    }

    public interface IVersionedApiInfoApi
    {
        IApiInfoApi V1 { get; }
    }

    public interface IVersionedConfigApi
    {
        IConfigApi V1 { get; }
    }

    public interface IVersionedFileBrowseApi
    {
        IFileBrowseApi V1 { get; }
    }

    public interface IVersionedFilesApi
    {
        IFilesApi V1 { get; }
    }
}