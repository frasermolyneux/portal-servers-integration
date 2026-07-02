namespace XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;

/// <summary>
/// Publishes ban lifecycle events to the shared server-events pipeline.
/// </summary>
public interface IBanLifecycleEventPublisher
{
    Task PublishBanLiftAppliedAsync(
        Guid serverId,
        string gameType,
        string playerGuid,
        string playerName,
        string source,
        string liftReason,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
