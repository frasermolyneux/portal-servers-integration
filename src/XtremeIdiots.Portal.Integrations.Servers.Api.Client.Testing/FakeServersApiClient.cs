using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// In-memory fake of <see cref="IServersApiClient"/> composing individual API fakes.
/// Provides a single entry point for configuring all fake APIs in tests.
/// </summary>
public class FakeServersApiClient : IServersApiClient
{
    public FakeQueryApi FakeQuery { get; } = new();
    public FakeRconApi FakeRcon { get; } = new();
    public FakeMapsApi FakeMaps { get; } = new();
    public FakeRootApi FakeRoot { get; } = new();

    private readonly Lazy<IVersionedQueryApi> _versionedQuery;
    private readonly Lazy<IVersionedRconApi> _versionedRcon;
    private readonly Lazy<IVersionedMapsApi> _versionedMaps;
    private readonly Lazy<IVersionedRootApi> _versionedRoot;

    public FakeServersApiClient()
    {
        _versionedQuery = new Lazy<IVersionedQueryApi>(() => new FakeVersionedQueryApi(FakeQuery));
        _versionedRcon = new Lazy<IVersionedRconApi>(() => new FakeVersionedRconApi(FakeRcon));
        _versionedMaps = new Lazy<IVersionedMapsApi>(() => new FakeVersionedMapsApi(FakeMaps));
        _versionedRoot = new Lazy<IVersionedRootApi>(() => new FakeVersionedRootApi(FakeRoot));
    }

    public IVersionedQueryApi Query => _versionedQuery.Value;
    public IVersionedRconApi Rcon => _versionedRcon.Value;
    public IVersionedMapsApi Maps => _versionedMaps.Value;
    public IVersionedRootApi Root => _versionedRoot.Value;

    public void Reset()
    {
        FakeQuery.Reset();
        FakeRcon.Reset();
        FakeMaps.Reset();
        FakeRoot.Reset();
    }

    private sealed class FakeVersionedQueryApi(IQueryApi v1) : IVersionedQueryApi
    {
        public IQueryApi V1 => v1;
    }

    private sealed class FakeVersionedRconApi(IRconApi v1) : IVersionedRconApi
    {
        public IRconApi V1 => v1;
    }

    private sealed class FakeVersionedMapsApi(IMapsApi v1) : IVersionedMapsApi
    {
        public IMapsApi V1 => v1;
    }

    private sealed class FakeVersionedRootApi(IRootApi v1) : IVersionedRootApi
    {
        public IRootApi V1 => v1;
    }
}
