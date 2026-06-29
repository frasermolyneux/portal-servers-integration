using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    internal sealed class NotSupportedVersionedCoD4xRconApi : IVersionedCoD4xRconApi
    {
        private static readonly ICoD4xRconApi NotSupportedApi = NotSupportedApiProxyFactory.Create<ICoD4xRconApi>();

        private NotSupportedVersionedCoD4xRconApi()
        {
        }

        public static IVersionedCoD4xRconApi Instance { get; } = new NotSupportedVersionedCoD4xRconApi();

        public ICoD4xRconApi V1 => NotSupportedApi;
    }

    internal sealed class NotSupportedVersionedCod2RconApi : IVersionedCod2RconApi
    {
        private static readonly ICod2RconApi NotSupportedApi = NotSupportedApiProxyFactory.Create<ICod2RconApi>();

        private NotSupportedVersionedCod2RconApi()
        {
        }

        public static IVersionedCod2RconApi Instance { get; } = new NotSupportedVersionedCod2RconApi();

        public ICod2RconApi V1 => NotSupportedApi;
    }

    internal sealed class NotSupportedVersionedCod4RconApi : IVersionedCod4RconApi
    {
        private static readonly ICod4RconApi NotSupportedApi = NotSupportedApiProxyFactory.Create<ICod4RconApi>();

        private NotSupportedVersionedCod4RconApi()
        {
        }

        public static IVersionedCod4RconApi Instance { get; } = new NotSupportedVersionedCod4RconApi();

        public ICod4RconApi V1 => NotSupportedApi;
    }

    internal sealed class NotSupportedVersionedCod5RconApi : IVersionedCod5RconApi
    {
        private static readonly ICod5RconApi NotSupportedApi = NotSupportedApiProxyFactory.Create<ICod5RconApi>();

        private NotSupportedVersionedCod5RconApi()
        {
        }

        public static IVersionedCod5RconApi Instance { get; } = new NotSupportedVersionedCod5RconApi();

        public ICod5RconApi V1 => NotSupportedApi;
    }

    internal sealed class NotSupportedVersionedInsurgencyRconApi : IVersionedInsurgencyRconApi
    {
        private static readonly IInsurgencyRconApi NotSupportedApi = NotSupportedApiProxyFactory.Create<IInsurgencyRconApi>();

        private NotSupportedVersionedInsurgencyRconApi()
        {
        }

        public static IVersionedInsurgencyRconApi Instance { get; } = new NotSupportedVersionedInsurgencyRconApi();

        public IInsurgencyRconApi V1 => NotSupportedApi;
    }

    internal sealed class NotSupportedVersionedRustRconApi : IVersionedRustRconApi
    {
        private static readonly IRustRconApi NotSupportedApi = NotSupportedApiProxyFactory.Create<IRustRconApi>();

        private NotSupportedVersionedRustRconApi()
        {
        }

        public static IVersionedRustRconApi Instance { get; } = new NotSupportedVersionedRustRconApi();

        public IRustRconApi V1 => NotSupportedApi;
    }

    internal sealed class NotSupportedVersionedL4d2RconApi : IVersionedL4d2RconApi
    {
        private static readonly IL4d2RconApi NotSupportedApi = NotSupportedApiProxyFactory.Create<IL4d2RconApi>();

        private NotSupportedVersionedL4d2RconApi()
        {
        }

        public static IVersionedL4d2RconApi Instance { get; } = new NotSupportedVersionedL4d2RconApi();

        public IL4d2RconApi V1 => NotSupportedApi;
    }

    internal static class NotSupportedApiProxyFactory
    {
        public static TApi Create<TApi>() where TApi : class
        {
            return DispatchProxy.Create<TApi, NotSupportedCoD4xRconApiProxy>();
        }
    }

    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy types must remain unsealed.")]
    internal class NotSupportedCoD4xRconApiProxy : DispatchProxy
    {
        private const string ErrorCode = "OPERATION_NOT_IMPLEMENTED";
        private const string ErrorMessage = "This game-specific RCON API is not available on this IServersApiClient implementation.";

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

                if (taskInnerType == typeof(ApiResult))
                {
                    var apiResult = new ApiResult(
                        HttpStatusCode.BadRequest,
                        new ApiResponse(new ApiError(ErrorCode, ErrorMessage)));

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
