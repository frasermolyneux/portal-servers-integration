# XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1

Authenticated REST client for the XtremeIdiots Portal Servers API. Provides DI registration, token management, retry policies, and versioned access to server query, game-specific RCON, and map management endpoints.

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

    public async Task RestartCod4Server(Guid serverId)
    {
        await _client.Cod4Rcon.V1.Restart(serverId);
    }

    public async Task RotateCoD4xMap(Guid serverId)
    {
        await _client.CoD4xRcon.V1.MapRotate(serverId);
    }
}
```

## API Surface

The `IServersApiClient` exposes versioned APIs:

| Property                                                                                  | Description                                     |
| ----------------------------------------------------------------------------------------- | ----------------------------------------------- |
| `Query`                                                                                   | Server status queries (player counts, map info) |
| `CoD4xRcon`, `Cod2Rcon`, `Cod4Rcon`, `Cod5Rcon`, `InsurgencyRcon`, `RustRcon`, `L4d2Rcon` | Game-specific remote console commands           |
| `Maps`                                                                                    | Server map file management (load, push, delete) |
| `ApiHealth`, `ApiInfo`                                                                    | API health and metadata endpoints               |
| `Config`, `FileBrowse`, `Files`                                                           | Configuration and file-management endpoints     |

## Testing

Use the companion package [`XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing`](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing) for in-memory fakes and test helpers.

## License

This project is licensed under the [GPL-3.0-only](https://spdx.org/licenses/GPL-3.0-only.html) license.
