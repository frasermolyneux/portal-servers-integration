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
    [InlineData(384, false)]
    [InlineData(416, false)]
    [InlineData(384, true)]
    public async Task ConfigurationStagingProtectsContentAndPreservesAccessOrLeavesOriginalIntact(int originalMode, bool accessUpdateFails)
    {
        const string destination = "server.cfg";
        var files = new Dictionary<string, (byte[] Content, int Mode, int User, int Group)>
        {
            [destination] = ([9, 9], originalMode, 12, 34)
        };
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
                files[path] = (files[path].Content, originalMode, attributes.UserId, attributes.GroupId);
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
        Assert.Equal(originalMode, retained.Mode);
        Assert.Equal(12, retained.User);
        Assert.Equal(34, retained.Group);
    }

    private static SftpFileAttributes Attributes(long size, int mode, int user, int group)
    {
        // SSH.NET creates attributes from protocol responses and exposes no public constructor.
        var constructor = typeof(SftpFileAttributes).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (SftpFileAttributes)constructor.Invoke(
            [DateTime.UtcNow, DateTime.UtcNow, size, user, group, (uint)mode | 0x8000U, new Dictionary<string, string>()]);
    }
}
