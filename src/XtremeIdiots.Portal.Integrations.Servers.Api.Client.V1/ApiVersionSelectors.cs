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

    public interface IVersionedRconApi
    {
        IRconApi V1 { get; }
    }
}