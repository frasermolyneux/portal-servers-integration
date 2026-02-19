# XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1

Abstractions for the XtremeIdiots Portal Servers API. Defines interfaces and DTOs for server queries, RCON commands, and map management.

## Installation

```shell
dotnet add package XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1
```

## Key Interfaces

### IQueryApi

```csharp
public interface IQueryApi
{
    Task<ApiResult<ServerQueryStatusResponseDto>> GetServerStatus(Guid serverId);
}
```

### IRconApi

Provides remote console operations for game servers:

```csharp
public interface IRconApi
{
    Task<ApiResult> KickPlayer(Guid serverId, string playerNum, string reason);
    Task<ApiResult> BanPlayer(Guid serverId, string playerNum, string reason);
    Task<ApiResult> TempBanPlayer(Guid serverId, string playerNum, string duration, string reason);
    Task<ApiResult> UnbanPlayer(Guid serverId, string playerGuid);
    Task<ApiResult> Say(Guid serverId, string message);
    Task<ApiResult> TellPlayer(Guid serverId, string playerNum, string message);
    Task<ApiResult> ChangeMap(Guid serverId, string mapName);
    Task<ApiResult> RestartMap(Guid serverId);
    Task<ApiResult> NextMap(Guid serverId);
    Task<ApiResult<ServerRconStatusResponseDto>> GetServerStatus(Guid serverId);
    Task<ApiResult<RconMapCollectionDto>> GetServerMaps(Guid serverId);
    Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid serverId);
}
```

### IMapsApi

```csharp
public interface IMapsApi
{
    Task<ApiResult<ServerMapsCollectionDto>> GetLoadedServerMapsFromHost(Guid serverId);
    Task<ApiResult> PushServerMapToHost(Guid serverId, string mapName);
    Task<ApiResult> DeleteServerMapFromHost(Guid serverId, string mapName);
}
```

## Data Models

| Model | Description |
|-------|-------------|
| `ServerQueryStatusResponseDto` | Server status with name, map, players, mod |
| `ServerQueryPlayerDto` | Player name and score from query |
| `ServerRconStatusResponseDto` | Detailed player list from RCON |
| `ServerRconPlayerDto` | Player num, GUID, name, IP, rate, ping |
| `RconMapDto` | Game type and map name |
| `RconCurrentMapDto` | Currently active map |
| `ServerMapDto` | Map file metadata (name, size, modified) |

## Usage

This package is consumed by:
- [`XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1`](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1) — live API client
- [`XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing`](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing) — test fakes

## License

This project is licensed under the [GPL-3.0-only](https://spdx.org/licenses/GPL-3.0-only.html) license.
