using XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;

public interface IMapRotationRconClient
{
    Task<List<Quake3QueryMap>> GetMaps(CancellationToken cancellationToken);
}
