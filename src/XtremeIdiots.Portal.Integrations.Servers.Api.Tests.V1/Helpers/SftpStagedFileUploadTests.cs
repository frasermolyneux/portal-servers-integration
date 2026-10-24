using System.Reflection;
using Moq;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;

[Trait("Category", "Unit")]
public class SftpStagedFileUploadTests
{
    [Theory]
    [InlineData("server.cfg", 384, false, 384)]
    [InlineData("server.cfg", 416, false, 416)]
    [InlineData("server.cfg", 384, true, 384)]
    [InlineData("credentials.json", null, false, 384)]
    [InlineData(".env", null, false, 384)]
    [InlineData("server.cfg.bak", null, false, 384)]
    [InlineData("mp_custom.ff", null, false, 420)]
    [InlineData("mp_custom.iwd", null, false, 420)]
    public async Task StagingPreservesExistingAccessAndKeepsNewNonMapFilesPrivate(
        string destination, int? originalMode, bool accessUpdateFails, int expectedMode)
    {
        var files = new Dictionary<string, (byte[] Content, int Mode, int User, int Group)>();
        if (originalMode.HasValue)
        {
            files[destination] = ([9, 9], originalMode.Value, 12, 34);
        }
        var client = new Mock<ISftpClient>(MockBehavior.Strict);
        client.Setup(x => x.Exists(It.IsAny<string>())).Returns((string path) => files.ContainsKey(path));
        client.Setup(x => x.GetAttributes(It.IsAny<string>())).Returns((string path) =>
        {
            var file = files[path];
            return Attributes(file.Content.Length, file.Mode, file.User, file.Group);
        });
        client.Setup(x => x.UploadFile(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Action<ulong>>()))
            .Callback<Stream, string, bool, Action<ulong>>((source, path, overwrite, _) =>
            {
                if (!overwrite)
                {
                    Assert.False(files.ContainsKey(path));
                    files[path] = ([], 420, 1000, 1000);
                }
                else
                {
                    Assert.Equal(384, files[path].Mode);
                }
                using var copy = new MemoryStream();
                source.CopyTo(copy);
                files[path] = (copy.ToArray(), files[path].Mode, files[path].User, files[path].Group);
            });
        client.Setup(x => x.ChangePermissions(It.IsAny<string>(), It.IsAny<short>()))
            .Callback<string, short>((path, mode) =>
            {
                var file = files[path];
                files[path] = (file.Content, mode, file.User, file.Group);
            });
        client.Setup(x => x.SetAttributes(It.IsAny<string>(), It.IsAny<SftpFileAttributes>()))
            .Callback<string, SftpFileAttributes>((path, attributes) =>
            {
                if (originalMode is not int mode)
                {
                    throw new InvalidOperationException("New files have no original attributes to restore.");
                }
                Assert.Equal(3, attributes.Size);
                Assert.Equal(12, attributes.UserId);
                Assert.Equal(34, attributes.GroupId);
                Assert.True(attributes.OwnerCanRead && attributes.OwnerCanWrite);
                Assert.Equal(originalMode == 416, attributes.GroupCanRead);
                Assert.False(attributes.GroupCanWrite || attributes.OthersCanRead || attributes.OthersCanWrite);
                if (accessUpdateFails)
                {
                    throw new IOException("Ownership preservation rejected");
                }
                files[path] = (files[path].Content, mode, attributes.UserId, attributes.GroupId);
            });
        client.Setup(x => x.RenameFile(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((source, target) =>
            {
                Assert.False(files.ContainsKey(target));
                files[target] = files[source];
                _ = files.Remove(source);
            });
        client.Setup(x => x.DeleteFile(It.IsAny<string>())).Callback<string>(path => { _ = files.Remove(path); });
        using var content = new MemoryStream([1, 2, 3]);

        if (accessUpdateFails)
        {
            await Assert.ThrowsAsync<IOException>(() => SftpStagedFileUpload.Upload(client.Object, destination, content, CancellationToken.None));
        }
        else
        {
            await SftpStagedFileUpload.Upload(client.Object, destination, content, CancellationToken.None);
        }

        var retained = Assert.Single(files).Value;
        Assert.Equal(accessUpdateFails ? Enumerable.Repeat((byte)9, 2).ToArray() : content.ToArray(), retained.Content);
        Assert.Equal(expectedMode, retained.Mode);
        Assert.Equal(originalMode.HasValue ? 12 : 1000, retained.User);
        Assert.Equal(originalMode.HasValue ? 34 : 1000, retained.Group);
    }

    private static SftpFileAttributes Attributes(long size, int mode, int user, int group)
    {
        // SSH.NET creates attributes from protocol responses and exposes no public constructor.
        var constructor = typeof(SftpFileAttributes).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (SftpFileAttributes)constructor.Invoke(
            [DateTime.UtcNow, DateTime.UtcNow, size, user, group, (uint)mode | 0x8000U, new Dictionary<string, string>()]);
    }
}
