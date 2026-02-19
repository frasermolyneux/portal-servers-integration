# XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1

Authenticated REST client for the XtremeIdiots Portal Servers API. Provides DI registration, token management, retry policies, and versioned access to server query, RCON, and map management endpoints.

## Installation

```shell
dotnet add package XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
```

## Quick Start

### Register Services

```csharp
builder.Services.AddServersApiClient(options =>
{
    options.ConfigureBaseUrl("https://servers-api.example.com");
});
```

### Inject and Use

```csharp
public class ServerMonitorService
{
    private readonly IServersApiClient _client;

    public ServerMonitorService(IServersApiClient client)
    {
        _client = client;
    }

    public async Task<ServerQueryStatusResponseDto?> GetStatus(Guid serverId)
    {
        var result = await _client.Query.V1.GetServerStatus(serverId);

        if (result.IsSuccess)
            return result.Result;

        return null;
    }

    public async Task KickPlayer(Guid serverId, string playerNum, string reason)
    {
        await _client.Rcon.V1.KickPlayer(serverId, playerNum, reason);
    }

    public async Task ChangeMap(Guid serverId, string mapName)
    {
        await _client.Rcon.V1.ChangeMap(serverId, mapName);
    }
}
```

## API Surface

The `IServersApiClient` exposes versioned APIs:

| Property | Description |
|----------|-------------|
| `Query` | Server status queries (player counts, map info) |
| `Rcon` | Remote console commands (kick, ban, say, map changes) |
| `Maps` | Server map file management (load, push, delete) |
| `Root` | API metadata endpoint |

## Testing

Use the companion package [`XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing`](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing) for in-memory fakes and test helpers.

## License

This project is licensed under the [GPL-3.0-only](https://spdx.org/licenses/GPL-3.0-only.html) license.
