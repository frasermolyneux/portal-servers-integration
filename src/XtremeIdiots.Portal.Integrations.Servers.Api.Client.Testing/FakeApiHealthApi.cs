using System.Net;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// In-memory fake of <see cref="IApiHealthApi"/> for unit and integration testing.
/// </summary>
public class FakeApiHealthApi : IApiHealthApi
{
    public HttpStatusCode StatusCode { get; private set; } = HttpStatusCode.OK;

    public FakeApiHealthApi WithStatusCode(HttpStatusCode statusCode)
    {
        StatusCode = statusCode;
        return this;
    }

    public void Reset()
    {
        StatusCode = HttpStatusCode.OK;
    }

    public Task<ApiResult> CheckHealth(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(StatusCode == HttpStatusCode.OK
            ? new ApiResult(HttpStatusCode.OK, new ApiResponse())
            : new ApiResult(StatusCode, new ApiResponse(new ApiError("ERROR", "Error"))));
    }
}
