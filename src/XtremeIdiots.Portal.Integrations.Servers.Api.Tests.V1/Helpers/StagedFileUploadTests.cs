using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;

[Trait("Category", "Unit")]
public class StagedFileUploadTests
{
    [Fact]
    public async Task ExplicitEmptyConfigurationUploadsRemainSupported()
    {
        using var source = new MemoryStream();
        var published = false;
        async Task Upload()
        {
            await StagedFileUpload.Upload("server.cfg", source,
                (_, stream, token) => stream.CopyToAsync(Stream.Null, token),
                (_, _) => Task.FromResult(0L),
                (_, _, _) =>
                {
                    published = true;
                    return Task.CompletedTask;
                },
                _ => Task.CompletedTask, CancellationToken.None);
        }
        await Upload();
        Assert.True(published);
    }

    [Fact]
    public async Task CancellationDuringPublicationStillRemovesTheBackup()
    {
        using var cancellation = new CancellationTokenSource();
        var files = new Dictionary<string, int> { ["server.cfg"] = 1, ["stage.tmp"] = 2 };
        await StagedFileUpload.ReplacePreservingOriginal("stage.tmp", "server.cfg",
            (path, _) => Task.FromResult(files.ContainsKey(path)),
            (source, destination, _) =>
            {
                files[destination] = files[source];
                Assert.True(files.Remove(source));
                if (source == "stage.tmp")
                {
                    cancellation.Cancel();
                }
                return Task.CompletedTask;
            },
            (path, token) =>
            {
                token.ThrowIfCancellationRequested();
                _ = files.Remove(path);
                return Task.CompletedTask;
            }, cancellation.Token);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(2, files["server.cfg"]);
        Assert.Single(files);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PortableReplacementRestoresOriginalIfPublicationFails(bool fails)
    {
        var files = new Dictionary<string, int> { ["map.ff"] = 1, ["stage.tmp"] = 2 };
        async Task Replace()
        {
            await StagedFileUpload.ReplacePreservingOriginal("stage.tmp", "map.ff",
                (path, _) => Task.FromResult(files.ContainsKey(path)),
                (source, target, _) =>
                {
                    if (source == "stage.tmp" && fails)
                    {
                        throw new IOException("Rename rejected");
                    }
                    Assert.False(files.ContainsKey(target));
                    files.Add(target, files[source]);
                    files.Remove(source);
                    return Task.CompletedTask;
                },
                (path, _) =>
                {
                    files.Remove(path);
                    return Task.CompletedTask;
                }, CancellationToken.None);
        }

        if (fails)
        {
            await Assert.ThrowsAsync<IOException>(Replace);
            Assert.Equal(1, files["map.ff"]);
            Assert.Equal(2, files.Count);
        }
        else
        {
            await Replace();
            Assert.Equal(2, files["map.ff"]);
            Assert.Single(files);
        }
        Assert.DoesNotContain(files.Keys, key => key.Contains("backup", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task PublishesOnlyCompleteSuccessfulUploads(long retainedSize, bool transferFails)
    {
        var files = new Dictionary<string, byte[]> { ["map.ff"] = [9, 9, 9] };
        using var source = new MemoryStream([1, 2, 3]);
        var published = false;
        async Task Upload()
        {
            await StagedFileUpload.Upload("map.ff", source,
            async (path, stream, token) =>
            {
                using var copy = new MemoryStream();
                await stream.CopyToAsync(copy, token);
                files[path] = copy.ToArray();
                if (transferFails)
                {
                    throw new IOException("Interrupted transfer");
                }
            },
            (_, _) => Task.FromResult(retainedSize),
            (path, destination, _) =>
            {
                files[destination] = files[path];
                files.Remove(path);
                published = true;
                return Task.CompletedTask;
            },
            path =>
            {
                files.Remove(path);
                return Task.CompletedTask;
            }, CancellationToken.None);
        }

        if (retainedSize == 3 && !transferFails)
        {
            await Upload();
            Assert.True(published);
            Assert.Equal(new byte[] { 1, 2, 3 }, files["map.ff"]);
        }
        else
        {
            await Assert.ThrowsAsync<IOException>(Upload);
            Assert.False(published);
            Assert.Equal(3, files["map.ff"].Length);
            Assert.All(files["map.ff"], value => Assert.Equal((byte)9, value));
        }
        Assert.Single(files);
    }

    [Fact]
    public async Task OverlappingPublicationRetainsBothTheOtherPublisherAndAnIdentifiedOriginalBackup()
    {
        var files = new Dictionary<string, int> { ["server.cfg"] = 1, ["stage.tmp"] = 2 };
        string? backup = null;

        var error = await Assert.ThrowsAsync<AggregateException>(() => StagedFileUpload.ReplacePreservingOriginal(
            "stage.tmp", "server.cfg",
            (path, _) => Task.FromResult(files.ContainsKey(path)),
            (source, target, _) =>
            {
                if (source == "stage.tmp")
                {
                    files.Add(target, 3);
                    throw new IOException("Another publisher filled the destination before rename.");
                }
                Assert.False(files.ContainsKey(target));
                files.Add(target, files[source]);
                files.Remove(source);
                backup = target;
                return Task.CompletedTask;
            },
            (path, _) =>
            {
                files.Remove(path);
                return Task.CompletedTask;
            }, CancellationToken.None));

        Assert.NotNull(backup);
        Assert.Contains(backup, error.Message);
        Assert.Equal(1, files[backup]);
        Assert.Equal(3, files["server.cfg"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/mods/sniper/configs/")]
    [InlineData("mods\\sniper\\configs\\")]
    public async Task StagingAndBackupNamesFitRemoteComponentLimitsInTheSameParent(string parent)
    {
        var destination = parent + new string('m', 230) + ".ff";
        var files = new Dictionary<string, int> { [destination] = 1 };
        using var source = new MemoryStream([2]);
        void CheckPath(string path)
        {
            Assert.StartsWith(parent, path);
            var name = path[parent.Length..];
            Assert.DoesNotContain('/', name);
            Assert.DoesNotContain('\\', name);
            Assert.InRange(System.Text.Encoding.UTF8.GetByteCount(name), 1, 255);
        }
        Task Rename(string from, string to, CancellationToken token)
        {
            CheckPath(from);
            CheckPath(to);
            Assert.False(files.ContainsKey(to));
            files.Add(to, files[from]);
            files.Remove(from);
            return Task.CompletedTask;
        }
        Task Delete(string path, CancellationToken token)
        {
            files.Remove(path);
            return Task.CompletedTask;
        }

        await StagedFileUpload.Upload(destination, source,
            async (path, stream, token) =>
            {
                CheckPath(path);
                await stream.CopyToAsync(Stream.Null, token);
                files.Add(path, 2);
            },
            (_, _) => Task.FromResult(1L),
            (from, to, token) => StagedFileUpload.ReplacePreservingOriginal(from, to,
                (path, _) => Task.FromResult(files.ContainsKey(path)), Rename, Delete, token),
            path => Delete(path, CancellationToken.None), CancellationToken.None);

        Assert.Single(files);
        Assert.Equal(2, files[destination]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task RejectsTruncatedOrOversizedSourceBeforePublishing(int actualSize)
    {
        using var source = new MemoryStream(new byte[actualSize]);
        using var validated = StagedFileUpload.WithExpectedLength(source, 3);
        var published = false;
        await Assert.ThrowsAsync<IOException>(() => StagedFileUpload.Upload("map.ff", validated,
            (_, stream, token) => stream.CopyToAsync(Stream.Null, token),
            (_, _) => Task.FromResult((long)actualSize),
            (_, _, _) =>
            {
                published = true;
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask, CancellationToken.None));
        Assert.False(published);
    }
}
