namespace XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;

public interface IRulesNativeRconClient
{
    Task DispatchKick(int clientId, CancellationToken cancellationToken);
    Task DispatchPublicMessage(string message, CancellationToken cancellationToken);
    Task DispatchPrivateMessage(int clientId, string message, CancellationToken cancellationToken);
}
