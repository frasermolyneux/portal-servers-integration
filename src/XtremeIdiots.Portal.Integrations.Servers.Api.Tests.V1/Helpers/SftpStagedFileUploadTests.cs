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
        client.Setup(x => x.ListDirectory(".", It.IsAny<Action<int>>())).Returns(() =>
            files.Select(file => DirectoryEntry(file.Key,
                Attributes(file.Value.Content.Length, file.Value.Mode, file.Value.User, file.Value.Group))));
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
                var attributes = Attributes(file.Content.Length, file.Mode, file.User, file.Group);
                attributes.SetPermissions(mode);
                var permissions =
                    (attributes.OwnerCanRead ? 0x100 : 0) |
                    (attributes.OwnerCanWrite ? 0x080 : 0) |
                    (attributes.OwnerCanExecute ? 0x040 : 0) |
                    (attributes.GroupCanRead ? 0x020 : 0) |
                    (attributes.GroupCanWrite ? 0x010 : 0) |
                    (attributes.GroupCanExecute ? 0x008 : 0) |
                    (attributes.OthersCanRead ? 0x004 : 0) |
                    (attributes.OthersCanWrite ? 0x002 : 0) |
                    (attributes.OthersCanExecute ? 0x001 : 0);
                files[path] = (file.Content, permissions, file.User, file.Group);
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

    [Theory]
    [InlineData(0x4000U)]
    [InlineData(0xA000U)]
    [InlineData(0x1000U)]
    public async Task ExistingNonRegularDestinationsAreRejectedBeforeAnyRemoteMutation(uint fileType)
    {
        const string destination = "occupied.ff";
        var client = new Mock<ISftpClient>(MockBehavior.Strict);
        client.Setup(x => x.Exists(destination)).Returns(true);
        client.Setup(x => x.GetAttributes(destination)).Returns(Attributes(0, 448, 12, 34,
            fileType == 0xA000U ? 0x8000U : fileType));
        client.Setup(x => x.ListDirectory(".", It.IsAny<Action<int>>()))
            .Returns([DirectoryEntry(destination, Attributes(0, 448, 12, 34, fileType))]);
        using var content = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<IOException>(() => SftpStagedFileUpload.Upload(
            client.Object, destination, content, CancellationToken.None));

        client.Verify(x => x.UploadFile(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Action<ulong>>()), Times.Never);
        client.Verify(x => x.RenameFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        client.Verify(x => x.GetAttributes(destination), Times.Never);
    }

    [Theory]
    [InlineData("/server.cfg", "/")]
    [InlineData("/configs/server.cfg", "/configs")]
    [InlineData("configs/server.cfg", "configs")]
    public async Task FinalSymlinkIsInspectedThroughItsParentRatherThanItsRegularTarget(string destination, string parent)
    {
        var client = new Mock<ISftpClient>(MockBehavior.Strict);
        client.Setup(x => x.Exists(destination)).Returns(true);
        client.Setup(x => x.GetAttributes(destination)).Returns(Attributes(123, 420, 12, 34));
        client.Setup(x => x.ListDirectory(parent, It.IsAny<Action<int>>()))
            .Returns([DirectoryEntry("server.cfg", Attributes(10, 511, 12, 34, 0xA000U))]);
        using var content = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<IOException>(() => SftpStagedFileUpload.Upload(
            client.Object, destination, content, CancellationToken.None));

        client.Verify(x => x.GetAttributes(destination), Times.Never);
        client.Verify(x => x.UploadFile(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Action<ulong>>()), Times.Never);
        client.Verify(x => x.RenameFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("configs/")]
    [InlineData(".")]
    [InlineData("configs/..")]
    public async Task DirectoryOnlyDestinationsAreRejectedBeforeRemoteCalls(string destination)
    {
        var client = new Mock<ISftpClient>(MockBehavior.Strict);
        using var content = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<IOException>(() => SftpStagedFileUpload.Upload(
            client.Object, destination, content, CancellationToken.None));

        Assert.Empty(client.Invocations);
    }

    private static ISftpFile DirectoryEntry(string name, SftpFileAttributes attributes) =>
        Mock.Of<ISftpFile>(file => file.Name == name && file.Attributes == attributes);

    private static SftpFileAttributes Attributes(long size, int mode, int user, int group, uint fileType = 0x8000U)
    {
        // SSH.NET creates attributes from protocol responses and exposes no public constructor.
        var constructor = typeof(SftpFileAttributes).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (SftpFileAttributes)constructor.Invoke(
            [DateTime.UtcNow, DateTime.UtcNow, size, user, group, (uint)mode | fileType, new Dictionary<string, string>()]);
    }
}
