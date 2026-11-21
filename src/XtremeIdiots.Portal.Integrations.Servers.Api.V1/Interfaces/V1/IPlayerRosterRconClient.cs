using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;

public interface IPlayerRosterRconClient
{
    (List<IRconPlayer> Players, RconRosterObservationDto Roster) GetPlayersWithRoster(CancellationToken cancellationToken);
}
