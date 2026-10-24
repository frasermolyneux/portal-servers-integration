using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class SftpStagedFileUpload
{
    // SSH.NET expects chmod notation, not a UnixFileMode bitmask.
    private const short OwnerReadWrite = 600;
    private const short PublicMapRead = 644;

    internal static async Task Upload(ISftpClient client, string path, Stream content, CancellationToken cancellationToken)
    {
        var separator = path.LastIndexOf('/');
        var name = path[(separator + 1)..];
        if (name is "" or "." or "..")
        {
            throw new IOException("The SFTP destination must identify a file, not a directory.");
        }
        var parent = separator < 0 ? "." : separator == 0 ? "/" : path[..separator];
        // GetAttributes canonicalizes the final entry and can hide a link to a regular file.
        var original = await Task.Run(() => client.ListDirectory(parent)
            .SingleOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal))?.Attributes,
            cancellationToken).ConfigureAwait(false);
        if (original is { IsRegularFile: false })
        {
            throw new IOException("The SFTP destination is not a regular file and cannot be replaced safely.");
        }

        await StagedFileUpload.Upload(path, content,
            (temporary, stream, token) => Task.Run(() =>
            {
                client.UploadFile(Stream.Null, temporary, false);
                client.ChangePermissions(temporary, OwnerReadWrite);
                client.UploadFile(stream, temporary, true);
            }, token),
            (temporary, token) => Task.Run(() => client.GetAttributes(temporary).Size, token),
            async (temporary, destination, token) =>
            {
                await Task.Run(() =>
                {
                    if (original is null)
                    {
                        var isMapFile = path.EndsWith(".ff", StringComparison.OrdinalIgnoreCase) ||
                            path.EndsWith(".iwd", StringComparison.OrdinalIgnoreCase);
                        client.ChangePermissions(temporary, isMapFile ? PublicMapRead : OwnerReadWrite);
                    }
                    else
                    {
                        var attributes = client.GetAttributes(temporary);
                        CopyAccess(original, attributes);
                        client.SetAttributes(temporary, attributes);
                    }
                }, token).ConfigureAwait(false);
                await StagedFileUpload.ReplacePreservingOriginal(temporary, destination,
                    (file, ct) => Task.Run(() => client.Exists(file), ct),
                    (source, target, ct) => Task.Run(() => client.RenameFile(source, target), ct),
                    (file, ct) => Task.Run(() => client.DeleteFile(file), ct), token).ConfigureAwait(false);
            },
            temporary => Task.Run(() =>
            {
                if (client.Exists(temporary))
                {
                    client.DeleteFile(temporary);
                }
            }, CancellationToken.None), cancellationToken).ConfigureAwait(false);
    }

    private static void CopyAccess(SftpFileAttributes original, SftpFileAttributes staged)
    {
        staged.UserId = original.UserId;
        staged.GroupId = original.GroupId;
        staged.OwnerCanRead = original.OwnerCanRead;
        staged.OwnerCanWrite = original.OwnerCanWrite;
        staged.OwnerCanExecute = original.OwnerCanExecute;
        staged.GroupCanRead = original.GroupCanRead;
        staged.GroupCanWrite = original.GroupCanWrite;
        staged.GroupCanExecute = original.GroupCanExecute;
        staged.OthersCanRead = original.OthersCanRead;
        staged.OthersCanWrite = original.OthersCanWrite;
        staged.OthersCanExecute = original.OthersCanExecute;
        staged.IsUIDBitSet = original.IsUIDBitSet;
        staged.IsGroupIDBitSet = original.IsGroupIDBitSet;
        staged.IsStickyBitSet = original.IsStickyBitSet;
    }
}
