namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class StagedFileUpload
{
    internal static Stream WithExpectedLength(Stream stream, long expectedBytes) => new CountingReadStream(stream, expectedBytes);

    internal static async Task ReplacePreservingOriginal(
        string temporary, string destination,
        Func<string, CancellationToken, Task<bool>> exists,
        Func<string, string, CancellationToken, Task> rename,
        Func<string, CancellationToken, Task> delete,
        CancellationToken cancellationToken)
    {
        if (!await exists(destination, cancellationToken).ConfigureAwait(false))
        {
            await rename(temporary, destination, cancellationToken).ConfigureAwait(false);
            return;
        }

        var backup = $"{destination}.portal-backup-{Guid.NewGuid():N}";
        try
        {
            await rename(destination, backup, cancellationToken).ConfigureAwait(false);
            await rename(temporary, destination, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception publishError)
        {
            try
            {
                if (await exists(backup, CancellationToken.None).ConfigureAwait(false) &&
                    !await exists(destination, CancellationToken.None).ConfigureAwait(false))
                {
                    await rename(backup, destination, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception restoreError)
            {
                throw new AggregateException($"Publication failed; the original file may require recovery from '{backup}'.", publishError, restoreError);
            }
            throw;
        }
        await delete(backup, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task Upload(
        string destination,
        Stream content,
        Func<string, Stream, CancellationToken, Task> upload,
        Func<string, CancellationToken, Task<long>> getSize,
        Func<string, string, CancellationToken, Task> replace,
        Func<string, Task> cleanup,
        CancellationToken cancellationToken)
    {
        var temporary = $"{destination}.portal-{Guid.NewGuid():N}.tmp";
        using var counted = new CountingReadStream(content);
        try
        {
            await upload(temporary, counted, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (counted.BytesRead == 0 || await getSize(temporary, cancellationToken).ConfigureAwait(false) != counted.BytesRead)
            {
                throw new IOException("The staged upload is empty or its retained size differs from the transferred bytes.");
            }

            await replace(temporary, destination, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception uploadError)
        {
            try
            {
                await cleanup(temporary).ConfigureAwait(false);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("The upload failed and its temporary file could not be cleaned up.", uploadError, cleanupError);
            }
            throw;
        }
    }

    private sealed class CountingReadStream(Stream inner, long? expectedBytes = null) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            Count(read, count != 0);
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            Count(read, buffer.Length != 0);
            return read;
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
            Count(read, count != 0);
            return read;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private void Count(int read, bool requestedBytes)
        {
            BytesRead += read;
            if (expectedBytes.HasValue && (BytesRead > expectedBytes || (read == 0 && requestedBytes && BytesRead != expectedBytes)))
            {
                throw new IOException("The redirect stream does not match the manifest's expected file size.");
            }
        }
    }
}
