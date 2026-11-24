using MX.Api.Abstractions;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.RulesEngine;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

public interface IRulesNativeActionApi
{
    Task<ApiResult<RuleActionResult>> Execute(Guid gameServerId, RuleActionCommand request, CancellationToken cancellationToken = default);
}
