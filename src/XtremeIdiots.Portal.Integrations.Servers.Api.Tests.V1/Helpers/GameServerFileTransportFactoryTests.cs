using System.Security.Cryptography;

using Renci.SshNet;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Settings.Contracts.V1.Contracts.FileTransport;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;

[Trait("Category", "Unit")]
public class GameServerFileTransportFactoryTests
{
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

    [Fact]
    public void CreateSftpConnectionInfo_WithEncryptedPrivateKey_UsesPrivateKeyMethod()
    {
        const string passphrase = "test-passphrase";
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportEncryptedPkcs8PrivateKeyPem(
            passphrase,
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1_000));
        var credentials = new FileTransportCredentials(
            "sftp.example.local",
            22,
            "demo",
            string.Empty,
            AuthenticationType: SftpAuthenticationType.PrivateKey,
            PrivateKey: privateKey,
            PrivateKeyPassphrase: passphrase);

        var connectionInfo = GameServerFileTransportFactory.CreateSftpConnectionInfo(credentials);

        using var authenticationMethod = Assert.IsType<PrivateKeyAuthenticationMethod>(
            Assert.Single(connectionInfo.AuthenticationMethods));
    }

    [Fact]
    public void CreateSftpConnectionInfo_WithMissingPrivateKey_Throws()
    {
        var credentials = new FileTransportCredentials(
            "sftp.example.local",
            22,
            "demo",
            string.Empty,
            AuthenticationType: SftpAuthenticationType.PrivateKey);

        var exception = Assert.Throws<InvalidOperationException>(
            () => GameServerFileTransportFactory.CreateSftpConnectionInfo(credentials));

        Assert.Equal("The sftp.privateKey setting is required for private-key authentication.", exception.Message);
    }
}
