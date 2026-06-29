using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    internal sealed class NotSupportedVersionedCoD4xRconApi : IVersionedCoD4xRconApi
    {
        private static readonly ICoD4xRconApi NotSupportedApi = DispatchProxy.Create<ICoD4xRconApi, NotSupportedCoD4xRconApiProxy>();

        private NotSupportedVersionedCoD4xRconApi()
        {
        }

        public static IVersionedCoD4xRconApi Instance { get; } = new NotSupportedVersionedCoD4xRconApi();

        public ICoD4xRconApi V1 => NotSupportedApi;
    }

    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy types must remain unsealed.")]
    internal class NotSupportedCoD4xRconApiProxy : DispatchProxy
    {
        private const string ErrorCode = "OPERATION_NOT_IMPLEMENTED";
        private const string ErrorMessage = "CoD4x RCON is not available on this IServersApiClient implementation.";

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return Task.FromResult(new ApiResult<string>(
                HttpStatusCode.BadRequest,
                new ApiResponse<string>(new ApiError(ErrorCode, ErrorMessage))));
        }
    }
}
