using System.Security.Cryptography;

using FluentFTP;
using Renci.SshNet;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Settings.Contracts.V1.Contracts.FileTransport;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;

/// <summary>
/// Verifies SFTP authentication method construction without opening network connections.
/// </summary>
[Trait("Category", "Unit")]
public class GameServerFileTransportFactoryTests
{
    private static readonly ResolvedFileTransport FtpTransport = new(
        FileTransportType.Ftp,
        "ftp",
        new FileTransportCredentials("ftp.example.local", 21, "demo", "secret"));

    [Fact]
    public async Task FtpSession_WhenAutoConnectFails_AwaitsAsyncDisposalBeforeRethrowing()
    {
        var disposalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDisposal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new TestAsyncFtpClient(
            autoConnect: () => Task.FromException<FtpProfile>(new InvalidOperationException("connect failed")),
            disposeAsync: async () =>
            {
                disposalStarted.SetResult();
                await releaseDisposal.Task;
            });

        var creation = GameServerFileTransportFactory.FtpGameServerFileTransportSession.CreateAsync(
            FtpTransport,
            new ConfigurationBuilder().Build(),
            CancellationToken.None,
            _ => client);

        await disposalStarted.Task;
        Assert.False(creation.IsCompleted);

        releaseDisposal.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => creation);
        Assert.True(client.DisposalCompleted);
    }

    [Fact]
    public async Task FtpSession_WhenAutoConnectSucceeds_TransfersClientOwnershipToSession()
    {
        var clientDisposalCompleted = false;
        var client = new TestAsyncFtpClient(
            autoConnect: () => Task.FromResult(new FtpProfile()),
            disposeAsync: () =>
            {
                clientDisposalCompleted = true;
                return ValueTask.CompletedTask;
            });

        var session = await GameServerFileTransportFactory.FtpGameServerFileTransportSession.CreateAsync(
            FtpTransport,
            new ConfigurationBuilder().Build(),
            CancellationToken.None,
            _ => client);

        Assert.False(clientDisposalCompleted);
        await session.DisposeAsync();
        Assert.True(clientDisposalCompleted);
    }

    /// <summary>
    /// Verifies password settings select SSH.NET password authentication.
    /// </summary>
    [Fact]
    public void CreateSftpConnectionInfo_WithPasswordAuthentication_UsesPasswordMethod()
    {
        var credentials = new FileTransportCredentials(
            "sftp.example.local",
            22,
            "demo",
            "secret");

        var connectionInfo = GameServerFileTransportFactory.CreateSftpConnectionInfo(credentials);

        using var authenticationMethod = Assert.IsType<PasswordAuthenticationMethod>(
            Assert.Single(connectionInfo.AuthenticationMethods));
    }

    /// <summary>
    /// Verifies encrypted and unencrypted private keys select SSH.NET private-key authentication.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateSftpConnectionInfo_WithPrivateKey_UsesPrivateKeyMethod(bool encrypted)
    {
        const string passphrase = "test-passphrase";
        using var rsa = RSA.Create(2048);
        var privateKey = encrypted
            ? rsa.ExportEncryptedPkcs8PrivateKeyPem(
                passphrase,
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1_000))
            : rsa.ExportPkcs8PrivateKeyPem();
        var credentials = new FileTransportCredentials(
            "sftp.example.local",
            22,
            "demo",
            string.Empty)
        {
            AuthenticationType = SftpAuthenticationType.PrivateKey,
            PrivateKey = privateKey,
            PrivateKeyPassphrase = encrypted ? passphrase : null,
        };

        var connectionInfo = GameServerFileTransportFactory.CreateSftpConnectionInfo(credentials);

        using var authenticationMethod = Assert.IsType<PrivateKeyAuthenticationMethod>(
            Assert.Single(connectionInfo.AuthenticationMethods));
    }

    /// <summary>
    /// Verifies private-key authentication rejects missing key material.
    /// </summary>
    [Fact]
    public void CreateSftpConnectionInfo_WithMissingPrivateKey_Throws()
    {
        var credentials = new FileTransportCredentials(
            "sftp.example.local",
            22,
            "demo",
            string.Empty)
        {
            AuthenticationType = SftpAuthenticationType.PrivateKey,
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => GameServerFileTransportFactory.CreateSftpConnectionInfo(credentials));

        Assert.Equal("The sftp.privateKey setting is required for private-key authentication.", exception.Message);
    }

    private sealed class TestAsyncFtpClient(
        Func<Task<FtpProfile>> autoConnect,
        Func<ValueTask> disposeAsync) : AsyncFtpClient()
    {
        public bool DisposalCompleted { get; private set; }

        public override Task<FtpProfile> AutoConnect(CancellationToken token)
            => autoConnect();

        public override async ValueTask DisposeAsync()
        {
            await disposeAsync();
            DisposalCompleted = true;
        }
    }
}
