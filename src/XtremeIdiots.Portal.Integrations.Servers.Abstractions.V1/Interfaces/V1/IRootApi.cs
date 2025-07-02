using MxIO.ApiClient.Abstractions;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1
{
    public interface IRootApi
    {
        Task<ApiResponseDto> GetRoot();
    }
}
