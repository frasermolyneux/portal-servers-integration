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
            if (targetMethod?.ReturnType.IsGenericType == true
                && targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var taskInnerType = targetMethod.ReturnType.GenericTypeArguments[0];
                if (taskInnerType.IsGenericType && taskInnerType.GetGenericTypeDefinition() == typeof(ApiResult<>))
                {
                    var responseType = taskInnerType.GenericTypeArguments[0];
                    var apiResponseType = typeof(ApiResponse<>).MakeGenericType(responseType);
                    var apiResultType = typeof(ApiResult<>).MakeGenericType(responseType);

                    var apiError = new ApiError(ErrorCode, ErrorMessage);
                    var apiResponse = Activator.CreateInstance(apiResponseType, apiError);
                    var apiResult = Activator.CreateInstance(apiResultType, HttpStatusCode.BadRequest, apiResponse);

                    var fromResult = typeof(Task)
                        .GetMethod(nameof(Task.FromResult), BindingFlags.Public | BindingFlags.Static)!
                        .MakeGenericMethod(taskInnerType);

                    return fromResult.Invoke(null, [apiResult]);
                }
            }

            return Task.FromResult(new ApiResult<string>(
                HttpStatusCode.BadRequest,
                new ApiResponse<string>(new ApiError(ErrorCode, ErrorMessage))));
        }
    }
}
