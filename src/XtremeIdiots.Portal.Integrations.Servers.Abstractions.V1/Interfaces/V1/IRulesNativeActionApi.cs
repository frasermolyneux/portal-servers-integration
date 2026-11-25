using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.RulesEngine;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

public interface IRulesNativeActionApi
{
    Task<ApiResult<RuleActionResult>> Execute(Guid gameServerId, RuleActionCommand request, CancellationToken cancellationToken = default);

    Task<ApiResult<RuleMessageChannelBudgetDto>> GetMessageChannelBudget(Guid gameServerId, string channel, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This client does not implement message channel budget lookup.");
}
