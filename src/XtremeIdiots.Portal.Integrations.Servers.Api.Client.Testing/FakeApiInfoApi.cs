using System.Net;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// In-memory fake of <see cref="IApiInfoApi"/> for unit and integration testing.
/// </summary>
public class FakeApiInfoApi : IApiInfoApi
{
    public HttpStatusCode StatusCode { get; private set; } = HttpStatusCode.OK;

    public FakeApiInfoApi WithStatusCode(HttpStatusCode statusCode)
    {
        StatusCode = statusCode;
        return this;
    }

    public void Reset()
    {
        StatusCode = HttpStatusCode.OK;
    }

    public Task<ApiResult<ApiInfoDto>> GetApiInfo(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(StatusCode == HttpStatusCode.OK
            ? new ApiResult<ApiInfoDto>(HttpStatusCode.OK, new ApiResponse<ApiInfoDto>(new ApiInfoDto
            {
                Version = "1.0.0",
                BuildVersion = "1.0.0",
                AssemblyVersion = "1.0.0.0"
            }))
            : new ApiResult<ApiInfoDto>(StatusCode, new ApiResponse<ApiInfoDto>(new ApiError("ERROR", "Error"))));
    }
}
