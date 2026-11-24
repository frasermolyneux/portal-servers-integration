using System.Collections.Concurrent;
using System.Net;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.RulesEngine;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

public class FakeRulesNativeActionApi : IRulesNativeActionApi
{
    private readonly ConcurrentBag<(Guid ServerId, RuleActionCommand Request)> _operations = [];
    public IReadOnlyCollection<(Guid ServerId, RuleActionCommand Request)> Operations => _operations.ToArray();
    public RuleActionExecutionOutcome Outcome { get; set; } = RuleActionExecutionOutcome.Dispatched;

    public Task<ApiResult<RuleActionResult>> Execute(Guid gameServerId, RuleActionCommand request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _operations.Add((gameServerId, request));
        var result = new RuleActionResult
        {
            GameServerId = gameServerId,
            ExecutionId = request.ExecutionId,
            ActionId = request.ActionId,
            LogicalEffectId = request.LogicalEffectId,
            Correlation = request.Correlation,
            Outcome = Outcome,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Detail = "Unconfirmed test dispatch."
        };
        return Task.FromResult(new ApiResult<RuleActionResult>(HttpStatusCode.OK, new ApiResponse<RuleActionResult>(result)));
    }

    public void Reset()
    {
        _operations.Clear();
        Outcome = RuleActionExecutionOutcome.Dispatched;
    }
}
