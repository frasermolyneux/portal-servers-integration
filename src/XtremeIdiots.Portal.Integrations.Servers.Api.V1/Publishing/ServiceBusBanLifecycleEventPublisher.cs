using System.Text.Json;
using System.Text.Json.Serialization;

using Azure.Messaging.ServiceBus;

using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Server.Events.Abstractions.V1;

using SbEvents = XtremeIdiots.Portal.Server.Events.Abstractions.V1.Events;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Publishing;

public sealed class ServiceBusBanLifecycleEventPublisher(
    ServiceBusClient serviceBusClient,
    ILogger<ServiceBusBanLifecycleEventPublisher> logger) : IBanLifecycleEventPublisher, IAsyncDisposable
{
    private const long DefaultSequenceId = 0;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ServiceBusSender _banLiftSender = serviceBusClient.CreateSender(Queues.BanLiftApplied);

    public async Task PublishBanLiftAppliedAsync(
        Guid serverId,
        string gameType,
        string playerGuid,
        string playerName,
        string source,
        string liftReason,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var evt = new SbEvents.BanLiftAppliedEvent
        {
            EventGeneratedUtc = now,
            EventPublishedUtc = now,
            ServerId = serverId,
            GameType = gameType,
            SequenceId = DefaultSequenceId,
            PlayerGuid = playerGuid,
            PlayerName = playerName,
            Source = source,
            LiftReason = liftReason,
            CorrelationId = correlationId
        };

        var body = JsonSerializer.Serialize(evt, JsonOptions);
        var message = new ServiceBusMessage(BinaryData.FromString(body));
        message.ApplicationProperties["eventType"] = nameof(SbEvents.BanLiftAppliedEvent);

        await _banLiftSender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Published BanLiftApplied for server {ServerId} and player {PlayerGuid}", serverId, playerGuid);
    }

    public async ValueTask DisposeAsync()
    {
        await _banLiftSender.DisposeAsync().ConfigureAwait(false);
    }
}
