using MX.Api.Abstractions;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1
{
    public interface IRootApi
    {
        Task<ApiResult> GetRoot();
    }
}
