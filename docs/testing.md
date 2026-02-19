# Testing

This project publishes the `XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing` NuGet package to help consumers write tests against the Servers API client without requiring a running API instance.

## What's Included

The testing package provides:

- **`FakeServersApiClient`** — In-memory fake of `IServersApiClient` composing individual API fakes
- **`FakeQueryApi`** — Fake implementation of `IQueryApi` with canned responses and call tracking
- **`FakeRconApi`** — Fake implementation of `IRconApi` with operation logging
- **`FakeMapsApi`** — Fake implementation of `IMapsApi` with operation logging
- **`FakeRootApi`** — Fake implementation of `IRootApi` with configurable status codes
- **`ServersDtoFactory`** — Static factory methods for creating test DTOs with sensible defaults
- **`ServiceCollectionExtensions`** — `AddFakeServersApiClient()` DI extension for integration tests

## Quick Start

### Unit Tests (Manual Construction)

```csharp
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

var fakeClient = new FakeServersApiClient();

// Configure a canned query response
var serverId = Guid.NewGuid();
fakeClient.FakeQuery.AddResponse(serverId,
    ServersDtoFactory.CreateQueryStatusResponse(serverName: "My Server", map: "mp_crash"));

// Use the client
var result = await fakeClient.Query.V1.GetServerStatus(serverId);
Assert.Equal("My Server", result.Result!.Data!.ServerName);

// Check what was queried
Assert.Contains(serverId, fakeClient.FakeQuery.QueriedServerIds);
```

### Integration Tests (DI Registration)

Use `AddFakeServersApiClient()` to replace the real client in a `WebApplicationFactory`:

```csharp
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

builder.ConfigureTestServices(services =>
{
    services.AddFakeServersApiClient(fake =>
    {
        fake.FakeQuery.AddResponse(serverId,
            ServersDtoFactory.CreateQueryStatusResponse(serverName: "Test Server"));
        fake.FakeRcon.AddStatusResponse(serverId,
            ServersDtoFactory.CreateRconStatusResponse());
    });
});
```

## Error Simulation

Configure error responses for specific server IDs:

```csharp
fakeClient.FakeQuery.AddErrorResponse(serverId,
    HttpStatusCode.ServiceUnavailable, "OFFLINE", "Server is offline");
```

Or set a default error behavior for all unconfigured servers:

```csharp
fakeClient.FakeQuery.SetDefaultBehavior(DefaultBehavior.ReturnError);
```

## Operation Tracking

`FakeRconApi` and `FakeMapsApi` log all operations for verification:

```csharp
await fakeClient.Rcon.V1.KickPlayer(serverId, 5);
await fakeClient.Rcon.V1.Say(serverId, "Hello");

Assert.Equal(2, fakeClient.FakeRcon.OperationLog.Count);
Assert.Equal("KickPlayer", fakeClient.FakeRcon.OperationLog.First().Operation);
```

## DTO Factories

`ServersDtoFactory` provides factory methods with sensible defaults:

```csharp
// All have optional parameters for customisation
var queryStatus = ServersDtoFactory.CreateQueryStatusResponse(serverName: "Custom", maxPlayers: 64);
var rconStatus = ServersDtoFactory.CreateRconStatusResponse();
var player = ServersDtoFactory.CreateRconPlayer(name: "TestPlayer", ping: 50);
var map = ServersDtoFactory.CreateRconMap(gameType: "dm", mapName: "mp_crash");
var serverMap = ServersDtoFactory.CreateServerMap(name: "mp_backlot");
```

## Reset Between Tests

Call `Reset()` to clear all state between tests:

```csharp
fakeClient.Reset(); // Clears all fakes, operation logs, and reverts default behaviors
```
