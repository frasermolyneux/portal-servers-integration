using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Settings.Contracts.V1.Contracts.RulesEngine;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal sealed record RconMessageBatchResult(
    int CompletedFragments,
    int TotalFragments,
    int? FailedItemIndex,
    int? FailedFragmentIndex,
    bool OutcomeUnknown,
    bool NotImplemented,
    string? Detail,
    string? LastResponse)
{
    public bool IsSuccess => Detail is null;

    public string DescribeFailure() =>
        $"Dispatched {CompletedFragments} of {TotalFragments} fragments; stopped at item {FailedItemIndex}, fragment {FailedFragmentIndex}. " +
        $"{Detail} {(OutcomeUnknown ? "That fragment's outcome is unknown. " : string.Empty)}Do not retry the whole batch.";
}

internal static class RconMessageBatch
{
    private static readonly TimeSpan FragmentInterval = TimeSpan.FromMilliseconds(100);

    public static (IReadOnlyList<string>? Items, string? Error) Normalize(
        GameType gameType, string? message, IEnumerable<string>? messages, string? firstFragmentHeader = null)
    {
        var normalized = MessagePreparation.Normalize(message, messages);
        if (!normalized.IsValid)
        {
            return (null, string.Join(" ", normalized.Errors.Select(error => error.Message)));
        }

        if (normalized.Items.Any(string.IsNullOrWhiteSpace))
        {
            return (null, "Message items cannot be empty or whitespace.");
        }

        if (normalized.Items.Any(item => !MessageChannelBudgetResolver.IsRepresentable(gameType, item)))
        {
            return (null, "A message contains characters that cannot be safely represented by this RCON transport.");
        }

        if (firstFragmentHeader is { Length: > 0 } &&
            !MessageChannelBudgetResolver.IsRepresentable(gameType, firstFragmentHeader))
        {
            return (null, "The first-fragment header contains characters that cannot be safely represented by this RCON transport.");
        }

        return (normalized.Items, null);
    }

    public static (MessagePreparationResult? Prepared, string? Error) Prepare(
        GameType gameType, IReadOnlyList<string> items, MessageChannelBudget budget, string? firstFragmentHeader = null)
    {
        var prepared = firstFragmentHeader is null
            ? MessagePreparation.Prepare(null, items, budget)
            : MessagePreparation.Prepare(null, items, budget, firstFragmentHeader);
        if (!prepared.IsValid)
        {
            return (null, string.Join(" ", prepared.Errors.Select(error => error.Message)));
        }

        if (prepared.Fragments.Any(fragment => !MessageChannelBudgetResolver.IsRepresentable(gameType, fragment.Text)))
        {
            return (null, "A message contains characters that cannot be safely represented by this RCON transport.");
        }

        return (prepared, null);
    }

    public static async Task<RconMessageBatchResult> DispatchAsync(
        Guid gameServerId,
        GameType gameType,
        string channel,
        IRconClient client,
        MessagePreparationResult prepared,
        Func<string, CancellationToken, Task<string?>> dispatch,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var completed = 0;
        string? lastResponse = null;
        foreach (var fragment in prepared.Fragments)
        {
            var attempted = false;
            try
            {
                if (completed > 0)
                {
                    await Task.Delay(FragmentInterval, cancellationToken).ConfigureAwait(false);
                }

                // The first fragment uses the fresh budget that was used to prepare the batch.
                if (completed > 0 && gameType == GameType.CallOfDuty4x &&
                    channel is RuleMessageChannels.RconPublicChat or RuleMessageChannels.RconPrivateChat)
                {
                    var current = await MessageChannelBudgetResolver.ResolveAsync(gameType, channel, client, logger, cancellationToken).ConfigureAwait(false);
                    if (current.Status != RuleMessageBudgetStatus.Available || current.Budget is null)
                    {
                        return Failed(fragment, false, false, current.Detail ?? "The current message budget no longer fits the prepared fragment.");
                    }
                    if (fragment.Utf8ByteCount + current.Budget.ReservedPrefixUtf8Bytes > current.Budget.MaxPayloadBytes)
                    {
                        return Failed(fragment, false, false, "The current prefix leaves insufficient room for the prepared fragment.");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                attempted = true;
                lastResponse = await dispatch(fragment.Text, cancellationToken).ConfigureAwait(false);
                completed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (completed == 0 && !attempted)
                {
                    throw;
                }

                logger.LogWarning("Message batch cancelled after {Completed} of {Total} fragments for server {GameServerId}",
                    completed, prepared.Fragments.Count, gameServerId);
                return Failed(fragment, attempted, false, "The request was cancelled.");
            }
            catch (NotImplementedException ex)
            {
                logger.LogWarning(ex, "Message channel {Channel} is not implemented for server {GameServerId}", channel, gameServerId);
                return Failed(fragment, false, true, "The message transport is not implemented.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Message dispatch outcome is unknown for server {GameServerId}, item {Item}, fragment {Fragment}",
                    gameServerId, fragment.ItemIndex, fragment.FragmentIndex);
                return Failed(fragment, attempted, false, "The native command may have been sent.");
            }
        }

        return new(completed, prepared.Fragments.Count, null, null, false, false, null, lastResponse);

        RconMessageBatchResult Failed(PreparedMessageFragment fragment, bool outcomeUnknown, bool notImplemented, string detail) =>
            new(completed, prepared.Fragments.Count, fragment.ItemIndex, fragment.FragmentIndex, outcomeUnknown, notImplemented, detail, lastResponse);
    }
}
