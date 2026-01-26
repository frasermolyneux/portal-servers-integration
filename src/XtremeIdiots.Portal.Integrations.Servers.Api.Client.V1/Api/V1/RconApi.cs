using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using MX.Api.Client.Extensions;
using RestSharp;

using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class RconApi : BaseApi<ServersApiClientOptions>, IRconApi
    {
        public RconApi(
            ILogger<BaseApi<ServersApiClientOptions>> logger,
            IApiTokenProvider? apiTokenProvider,
            IRestClientService restClientService,
            ServersApiClientOptions options)
            : base(logger, apiTokenProvider, restClientService, options)
        {
        }

        public async Task<ApiResult<ServerRconStatusResponseDto>> GetServerStatus(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/status", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResult<ServerRconStatusResponseDto>();
        }

        public async Task<ApiResult<RconMapCollectionDto>> GetServerMaps(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/maps", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResult<RconMapCollectionDto>();
        }

        public async Task<ApiResult> KickPlayer(Guid gameServerId, int clientId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/kick/{clientId}", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> BanPlayer(Guid gameServerId, int clientId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/ban/{clientId}", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> Restart(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/restart", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> RestartMap(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/restart-map", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> FastRestartMap(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/fast-restart-map", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> NextMap(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/next-map", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> Say(Guid gameServerId, string message)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/say", Method.Post);
            request.AddJsonBody(new { Message = message });
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> TellPlayer(Guid gameServerId, int clientId, string message)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/tell/{clientId}", Method.Post);
            request.AddJsonBody(new { Message = message });
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> ChangeMap(Guid gameServerId, string mapName)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/change-map", Method.Post);
            request.AddJsonBody(new { MapName = mapName });
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> KickPlayerByName(Guid gameServerId, string name)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/kick-player-by-name", Method.Post);
            request.AddJsonBody(new { Name = name });
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> KickAllPlayers(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/kick-all-players", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> BanPlayerByName(Guid gameServerId, string name)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/ban-player-by-name", Method.Post);
            request.AddJsonBody(new { Name = name });
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> TempBanPlayer(Guid gameServerId, int clientId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/tempban/{clientId}", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> TempBanPlayerByName(Guid gameServerId, string name)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/tempban-player-by-name", Method.Post);
            request.AddJsonBody(new { Name = name });
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> UnbanPlayer(Guid gameServerId, string name)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/unban-player", Method.Post);
            request.AddJsonBody(new { Name = name });
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult<string>> GetServerInfo(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/server-info", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResult<string>();
        }

        public async Task<ApiResult<string>> GetSystemInfo(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/system-info", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResult<string>();
        }

        public async Task<ApiResult<string>> GetCommandList(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/command-list", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResult<string>();
        }

        public async Task<ApiResult> KickPlayerWithVerification(Guid gameServerId, int clientId, string? expectedPlayerName)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/kick/{clientId}/verify", Method.Post);
            request.AddJsonBody(new { ExpectedPlayerName = expectedPlayerName });
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> BanPlayerWithVerification(Guid gameServerId, int clientId, string? expectedPlayerName)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/ban/{clientId}/verify", Method.Post);
            request.AddJsonBody(new { ExpectedPlayerName = expectedPlayerName });
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> TempBanPlayerWithVerification(Guid gameServerId, int clientId, string? expectedPlayerName)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/tempban/{clientId}/verify", Method.Post);
            request.AddJsonBody(new { ExpectedPlayerName = expectedPlayerName });
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> TellPlayerWithVerification(Guid gameServerId, int clientId, string message, string? expectedPlayerName)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/tell/{clientId}/verify", Method.Post);
            request.AddJsonBody(new { Message = message, ExpectedPlayerName = expectedPlayerName });
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }
    }
}
