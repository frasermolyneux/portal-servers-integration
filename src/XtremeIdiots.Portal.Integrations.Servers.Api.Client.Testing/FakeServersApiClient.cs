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
    public FakeCoD4xRconApi FakeCoD4xRcon { get; } = new();
    public FakeCod2RconApi FakeCod2Rcon { get; } = new();
    public FakeCod4RconApi FakeCod4Rcon { get; } = new();
    public FakeCod5RconApi FakeCod5Rcon { get; } = new();
    public FakeInsurgencyRconApi FakeInsurgencyRcon { get; } = new();
    public FakeRustRconApi FakeRustRcon { get; } = new();
    public FakeL4d2RconApi FakeL4d2Rcon { get; } = new();
    public FakeMapsApi FakeMaps { get; } = new();
    public FakeApiHealthApi FakeApiHealth { get; } = new();
    public FakeApiInfoApi FakeApiInfo { get; } = new();
    public FakeConfigApi FakeConfig { get; } = new();
    public FakeFileBrowseApi FakeFileBrowse { get; } = new();

    private readonly Lazy<IVersionedQueryApi> _versionedQuery;
    private readonly Lazy<IVersionedRconApi> _versionedRcon;
    private readonly Lazy<IVersionedCoD4xRconApi> _versionedCoD4xRcon;
    private readonly Lazy<IVersionedCod2RconApi> _versionedCod2Rcon;
    private readonly Lazy<IVersionedCod4RconApi> _versionedCod4Rcon;
    private readonly Lazy<IVersionedCod5RconApi> _versionedCod5Rcon;
    private readonly Lazy<IVersionedInsurgencyRconApi> _versionedInsurgencyRcon;
    private readonly Lazy<IVersionedRustRconApi> _versionedRustRcon;
    private readonly Lazy<IVersionedL4d2RconApi> _versionedL4d2Rcon;
    private readonly Lazy<IVersionedMapsApi> _versionedMaps;
    private readonly Lazy<IVersionedApiHealthApi> _versionedApiHealth;
    private readonly Lazy<IVersionedApiInfoApi> _versionedApiInfo;
    private readonly Lazy<IVersionedConfigApi> _versionedConfig;
    private readonly Lazy<IVersionedFileBrowseApi> _versionedFileBrowse;

    public FakeServersApiClient()
    {
        _versionedQuery = new Lazy<IVersionedQueryApi>(() => new FakeVersionedQueryApi(FakeQuery));
        _versionedRcon = new Lazy<IVersionedRconApi>(() => new FakeVersionedRconApi(FakeRcon));
        _versionedCoD4xRcon = new Lazy<IVersionedCoD4xRconApi>(() => new FakeVersionedCoD4xRconApi(FakeCoD4xRcon));
        _versionedCod2Rcon = new Lazy<IVersionedCod2RconApi>(() => new FakeVersionedCod2RconApi(FakeCod2Rcon));
        _versionedCod4Rcon = new Lazy<IVersionedCod4RconApi>(() => new FakeVersionedCod4RconApi(FakeCod4Rcon));
        _versionedCod5Rcon = new Lazy<IVersionedCod5RconApi>(() => new FakeVersionedCod5RconApi(FakeCod5Rcon));
        _versionedInsurgencyRcon = new Lazy<IVersionedInsurgencyRconApi>(() => new FakeVersionedInsurgencyRconApi(FakeInsurgencyRcon));
        _versionedRustRcon = new Lazy<IVersionedRustRconApi>(() => new FakeVersionedRustRconApi(FakeRustRcon));
        _versionedL4d2Rcon = new Lazy<IVersionedL4d2RconApi>(() => new FakeVersionedL4d2RconApi(FakeL4d2Rcon));
        _versionedMaps = new Lazy<IVersionedMapsApi>(() => new FakeVersionedMapsApi(FakeMaps));
        _versionedApiHealth = new Lazy<IVersionedApiHealthApi>(() => new FakeVersionedApiHealthApi(FakeApiHealth));
        _versionedApiInfo = new Lazy<IVersionedApiInfoApi>(() => new FakeVersionedApiInfoApi(FakeApiInfo));
        _versionedConfig = new Lazy<IVersionedConfigApi>(() => new FakeVersionedConfigApi(FakeConfig));
        _versionedFileBrowse = new Lazy<IVersionedFileBrowseApi>(() => new FakeVersionedFileBrowseApi(FakeFileBrowse));
    }

    public IVersionedQueryApi Query => _versionedQuery.Value;
    public IVersionedRconApi Rcon => _versionedRcon.Value;
    public IVersionedCoD4xRconApi CoD4xRcon => _versionedCoD4xRcon.Value;
    public IVersionedCod2RconApi Cod2Rcon => _versionedCod2Rcon.Value;
    public IVersionedCod4RconApi Cod4Rcon => _versionedCod4Rcon.Value;
    public IVersionedCod5RconApi Cod5Rcon => _versionedCod5Rcon.Value;
    public IVersionedInsurgencyRconApi InsurgencyRcon => _versionedInsurgencyRcon.Value;
    public IVersionedRustRconApi RustRcon => _versionedRustRcon.Value;
    public IVersionedL4d2RconApi L4d2Rcon => _versionedL4d2Rcon.Value;
    public IVersionedMapsApi Maps => _versionedMaps.Value;
    public IVersionedApiHealthApi ApiHealth => _versionedApiHealth.Value;
    public IVersionedApiInfoApi ApiInfo => _versionedApiInfo.Value;
    public IVersionedConfigApi Config => _versionedConfig.Value;
    public IVersionedFileBrowseApi FileBrowse => _versionedFileBrowse.Value;

    public void Reset()
    {
        FakeQuery.Reset();
        FakeRcon.Reset();
        FakeCoD4xRcon.Reset();
        FakeCod2Rcon.Reset();
        FakeCod4Rcon.Reset();
        FakeCod5Rcon.Reset();
        FakeInsurgencyRcon.Reset();
        FakeRustRcon.Reset();
        FakeL4d2Rcon.Reset();
        FakeMaps.Reset();
        FakeApiHealth.Reset();
        FakeApiInfo.Reset();
        FakeConfig.Reset();
        FakeFileBrowse.Reset();
    }

    private sealed class FakeVersionedQueryApi(IQueryApi v1) : IVersionedQueryApi
    {
        public IQueryApi V1 => v1;
    }

    private sealed class FakeVersionedRconApi(IRconApi v1) : IVersionedRconApi
    {
        public IRconApi V1 => v1;
    }

    private sealed class FakeVersionedCoD4xRconApi(ICoD4xRconApi v1) : IVersionedCoD4xRconApi
    {
        public ICoD4xRconApi V1 => v1;
    }

    private sealed class FakeVersionedCod2RconApi(ICod2RconApi v1) : IVersionedCod2RconApi
    {
        public ICod2RconApi V1 => v1;
    }

    private sealed class FakeVersionedCod4RconApi(ICod4RconApi v1) : IVersionedCod4RconApi
    {
        public ICod4RconApi V1 => v1;
    }

    private sealed class FakeVersionedCod5RconApi(ICod5RconApi v1) : IVersionedCod5RconApi
    {
        public ICod5RconApi V1 => v1;
    }

    private sealed class FakeVersionedInsurgencyRconApi(IInsurgencyRconApi v1) : IVersionedInsurgencyRconApi
    {
        public IInsurgencyRconApi V1 => v1;
    }

    private sealed class FakeVersionedRustRconApi(IRustRconApi v1) : IVersionedRustRconApi
    {
        public IRustRconApi V1 => v1;
    }

    private sealed class FakeVersionedL4d2RconApi(IL4d2RconApi v1) : IVersionedL4d2RconApi
    {
        public IL4d2RconApi V1 => v1;
    }

    private sealed class FakeVersionedMapsApi(IMapsApi v1) : IVersionedMapsApi
    {
        public IMapsApi V1 => v1;
    }

    private sealed class FakeVersionedApiHealthApi(IApiHealthApi v1) : IVersionedApiHealthApi
    {
        public IApiHealthApi V1 => v1;
    }

    private sealed class FakeVersionedApiInfoApi(IApiInfoApi v1) : IVersionedApiInfoApi
    {
        public IApiInfoApi V1 => v1;
    }

    private sealed class FakeVersionedConfigApi(IConfigApi v1) : IVersionedConfigApi
    {
        public IConfigApi V1 => v1;
    }

    private sealed class FakeVersionedFileBrowseApi(IFileBrowseApi v1) : IVersionedFileBrowseApi
    {
        public IFileBrowseApi V1 => v1;
    }
}
