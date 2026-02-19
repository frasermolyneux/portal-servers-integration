# XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing

Test helpers for consumer applications that depend on the Portal Servers API client. Provides in-memory fakes of `IServersApiClient`, DTO factory methods, and DI extensions for integration tests.

## Installation

```shell
dotnet add package XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing
```

## Quick Start — Integration Tests

Replace the real client with fakes in your test DI container:

```csharp
var serverId = Guid.NewGuid();

services.AddFakeServersApiClient(client =>
{
    client.FakeQueryApi.AddResponse(serverId,
        ServersDtoFactory.CreateQueryStatusResponse(
            serverName: "Test Server",
            map: "mp_crossfire",
            playerCount: 12,
            maxPlayers: 24));

    client.FakeRconApi.AddStatusResponse(serverId,
        ServersDtoFactory.CreateRconStatusResponse());

    client.FakeRconApi.AddMapsResponse(serverId,
        ServersDtoFactory.CreateRconMapCollection());
});
```

## Quick Start — Unit Tests

Create and configure the fake client directly:

```csharp
var fakeClient = new FakeServersApiClient();
var serverId = Guid.NewGuid();

fakeClient.FakeQueryApi.AddResponse(serverId,
    ServersDtoFactory.CreateQueryStatusResponse(
        serverName: "My Server",
        map: "mp_crash",
        playerCount: 8));

var sut = new ServerMonitor(fakeClient);
var status = await sut.GetStatus(serverId);

Assert.Equal("My Server", status?.ServerName);
Assert.Equal(8, status?.PlayerCount);
```

## DTO Factories

`ServersDtoFactory` provides static factory methods with sensible defaults:

```csharp
ServersDtoFactory.CreateQueryStatusResponse(serverName: "Server", map: "mp_crossfire", playerCount: 10);
ServersDtoFactory.CreateQueryPlayer(name: "Player1", score: 25);
ServersDtoFactory.CreateRconStatusResponse();
ServersDtoFactory.CreateRconPlayer(name: "Admin", ping: 30);
ServersDtoFactory.CreateRconMapCollection();
ServersDtoFactory.CreateRconMap(mapName: "mp_crash", gameType: "war");
ServersDtoFactory.CreateRconCurrentMap(mapName: "mp_crossfire");
ServersDtoFactory.CreateServerMapsCollection();
ServersDtoFactory.CreateServerMap(name: "mp_broadcast");
```

## Configuring Error Responses

```csharp
fakeClient.FakeQueryApi.AddErrorResponse(serverId,
    HttpStatusCode.NotFound, "ServerNotFound", "Server does not exist");

fakeClient.FakeQueryApi.SetDefaultBehavior(DefaultBehavior.ReturnError);
```

## Tracking Calls

The fake APIs expose operation logs to verify interactions:

```csharp
Assert.Contains(serverId, fakeClient.FakeQueryApi.QueriedServerIds);
Assert.NotEmpty(fakeClient.FakeRconApi.OperationLog);
Assert.NotEmpty(fakeClient.FakeMapsApi.OperationLog);
```

## Resetting State Between Tests

```csharp
fakeClient.Reset(); // Clears all configured responses and tracked operations
```

## License

This project is licensed under the [GPL-3.0-only](https://spdx.org/licenses/GPL-3.0-only.html) license.
