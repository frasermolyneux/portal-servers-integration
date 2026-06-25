using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;

[Trait("Category", "Unit")]
public class FileTransportConfigResolverFixtureTests
{
    public static IEnumerable<object?[]> ValidTransportPayloads()
    {
        yield return
        [
            FileTransportType.Ftp,
            "FileTransport/ftp-valid-minimal.json",
            "ftp.example.local",
            21,
            "demo",
            string.Empty,
            null,
            null,
        ];

        yield return
        [
            FileTransportType.Ftp,
            "FileTransport/ftp-valid-full.json",
            "ftp.example.local",
            2121,
            "demo",
            "secret",
            null,
            "/mods/maps",
        ];

        yield return
        [
            FileTransportType.Sftp,
            "FileTransport/sftp-valid-full.json",
            "sftp.example.local",
            2022,
            "demo",
            "secret",
            "SHA256:abcdef",
            "/srv/game",
        ];
    }

    public static IEnumerable<object?[]> InvalidTransportPayloads()
    {
        yield return [FileTransportType.Ftp, "FileTransport/ftp-invalid-missing-hostname.json"];
        yield return [FileTransportType.Sftp, "FileTransport/sftp-invalid-missing-username.json"];
        // String ports are not tolerated by the current parser.
        yield return [FileTransportType.Sftp, "FileTransport/sftp-edge-string-port.json"];
    }

    [Theory]
    [MemberData(nameof(ValidTransportPayloads))]
    public void Parse_WithValidPayloads_ReturnsExpectedCredentials(
        FileTransportType transportType,
        string fixturePath,
        string expectedHostname,
        int expectedPort,
        string expectedUsername,
        string expectedPassword,
        string? expectedHostKeyFingerprint,
        string? expectedMapsRootPath)
    {
        var payload = ResolverFixtureLoader.Load(fixturePath);

        var result = FileTransportConfigResolver.Parse(transportType, payload);

        Assert.NotNull(result);
        Assert.Equal(expectedHostname, result!.Hostname);
        Assert.Equal(expectedPort, result.Port);
        Assert.Equal(expectedUsername, result.Username);
        Assert.Equal(expectedPassword, result.Password);
        Assert.Equal(expectedHostKeyFingerprint, result.HostKeyFingerprint);
        Assert.Equal(expectedMapsRootPath, result.MapsRootPath);
    }

    [Theory]
    [MemberData(nameof(InvalidTransportPayloads))]
    public void Parse_WithInvalidPayloads_ReturnsNull(FileTransportType transportType, string fixturePath)
    {
        var payload = ResolverFixtureLoader.Load(fixturePath);

        var result = FileTransportConfigResolver.Parse(transportType, payload);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_WithMalformedJson_ReturnsNull()
    {
        var result = FileTransportConfigResolver.Parse(FileTransportType.Ftp, "{\"hostname\":\"broken\"");

        Assert.Null(result);
    }

    [Fact]
    public void Parse_WithWhitespacePayload_ReturnsNull()
    {
        var result = FileTransportConfigResolver.Parse(FileTransportType.Ftp, "   ");

        Assert.Null(result);
    }

    [Fact]
    public void Parse_WithUnsupportedSchemaVersion_ReturnsNull()
    {
        const string payload = /*lang=json,strict*/ """
        {
            "schemaVersion": 999,
            "hostname": "ftp.example.local",
            "username": "demo"
        }
        """;

        var result = FileTransportConfigResolver.Parse(FileTransportType.Ftp, payload);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(FileTransportType.Ftp, "legacy-ftp.example.local", 2121, null)]
    [InlineData(FileTransportType.Sftp, "legacy-sftp.example.local", 2022, "SHA256:legacy")]
    public void Parse_WithLegacySupportedSchemaVersion_ReturnsCredentials(
        FileTransportType transportType,
        string expectedHostname,
        int expectedPort,
        string? expectedHostKeyFingerprint)
    {
        var payload = $$"""
        {
            "schemaVersion": 0,
            "hostname": "{{expectedHostname}}",
            "port": {{expectedPort}},
            "username": "demo",
            "password": "secret"{{(expectedHostKeyFingerprint is null ? string.Empty : $",\n            \"hostKeyFingerprint\": \"{expectedHostKeyFingerprint}\"")}}
        }
        """;

        var result = FileTransportConfigResolver.Parse(transportType, payload);

        Assert.NotNull(result);
        Assert.Equal(expectedHostname, result!.Hostname);
        Assert.Equal(expectedPort, result.Port);
        Assert.Equal("demo", result.Username);
        Assert.Equal("secret", result.Password);
        Assert.Equal(expectedHostKeyFingerprint, result.HostKeyFingerprint);
    }
}
