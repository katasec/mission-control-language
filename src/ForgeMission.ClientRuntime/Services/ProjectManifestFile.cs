using ForgeMission.ClientRuntime.Transport;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>
/// Owns the filesystem part of a Project manifest transaction: one stable OS lease, bounded byte
/// reads, and atomic publication. It deliberately knows nothing about manifest meaning, missions,
/// or network work; <see cref="ProjectStore"/> supplies the pure transformation while this lease is held.
/// </summary>
internal sealed class ProjectManifestFile
{
    internal const string LockFileName = ".forge-project.lock";
    internal const int MaximumManifestBytes = 2 * 1024 * 1024;

    private static readonly TimeSpan LeaseRetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(5);
    private readonly Func<Guid> _temporaryId;
    private readonly Action<ProjectManifestPublicationBoundary>? _publicationBoundary;

    // These internal seams make the three actual publication boundaries reproducible. Production
    // uses a fresh GUID and no callback; callers cannot configure persistence behaviour.
    internal ProjectManifestFile(
        Func<Guid>? temporaryId = null,
        Action<ProjectManifestPublicationBoundary>? publicationBoundary = null)
    {
        _temporaryId = temporaryId ?? Guid.NewGuid;
        _publicationBoundary = publicationBoundary;
    }

    public ProjectManifestFileSnapshot Read(string home)
    {
        var manifestPath = Path.Combine(home, ProjectStore.ManifestFileName);
        return new ProjectManifestFileSnapshot(manifestPath, ReadBytes(manifestPath));
    }

    public async Task<T> UpdateAsync<T>(
        string home,
        Func<ProjectManifestFileSnapshot, ProjectManifestFileUpdate<T>> transform,
        CancellationToken cancellationToken)
    {
        await using var lease = await AcquireLeaseAsync(home, cancellationToken);
        var current = Read(home);
        var update = transform(current);
        if (update.Contents is not null)
            await PublishAsync(home, current.Bytes, update.Contents, cancellationToken);

        return update.Value;
    }

    public async Task<bool> CreateIfAbsentAsync(string home, byte[] contents, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(home);
        await using var lease = await AcquireLeaseAsync(home, cancellationToken);
        var manifestPath = Path.Combine(home, ProjectStore.ManifestFileName);
        if (File.Exists(manifestPath))
            return false;

        await PublishAsync(home, expectedBytes: null, contents, cancellationToken);
        return true;
    }

    private static async Task<FileStream> AcquireLeaseAsync(string home, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(home);
        var lockPath = Path.Combine(home, LockFileName);
        var deadline = DateTime.UtcNow + LeaseTimeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception) when (IsLeaseContention(exception) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(LeaseRetryDelay, cancellationToken);
            }
            catch (IOException exception) when (IsLeaseContention(exception))
            {
                throw new ProjectOperationException(ProjectOperationErrorCode.ProjectBusy,
                    $"The Project at {home} is busy. Try again after the other operation finishes.");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw WriteFailure(lockPath, exception);
            }
        }
    }

    // Win32 sharing/lock violations are 32/33. Unix advisory/open contention reports EAGAIN or
    // EBUSY through IOException. Permission denied is deliberately absent: it is a write failure,
    // not a condition that can become safe by waiting.
    private static bool IsLeaseContention(IOException exception) =>
        (exception.HResult & 0xFFFF) is 11 or 16 or 32 or 33 ||
        exception.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase);

    private async Task PublishAsync(
        string home,
        byte[]? expectedBytes,
        byte[] contents,
        CancellationToken cancellationToken)
    {
        if (contents.Length > MaximumManifestBytes)
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                $"The Project manifest at {home} exceeds {MaximumManifestBytes} bytes.");

        var manifestPath = Path.Combine(home, ProjectStore.ManifestFileName);
        var temporaryPath = Path.Combine(home, $".forge-project.{_temporaryId():N}.tmp");
        try
        {
            await WriteTemporaryAsync(temporaryPath, contents, cancellationToken);
            EnsureUnchanged(manifestPath, expectedBytes);
            cancellationToken.ThrowIfCancellationRequested();
            _publicationBoundary?.Invoke(ProjectManifestPublicationBoundary.BeforeRename);
            File.Move(temporaryPath, manifestPath, overwrite: true);
            _publicationBoundary?.Invoke(ProjectManifestPublicationBoundary.AfterRename);
        }
        catch (ProjectOperationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or EndOfStreamException)
        {
            throw WriteFailure(manifestPath, exception);
        }
        finally
        {
            TryDeleteOwnTemporaryFile(temporaryPath);
        }
    }

    private async Task WriteTemporaryAsync(string path, byte[] contents, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        await file.WriteAsync(contents, cancellationToken);
        _publicationBoundary?.Invoke(ProjectManifestPublicationBoundary.BeforeFlush);
        await file.FlushAsync(cancellationToken);
        file.Flush(flushToDisk: true);
    }

    private static void EnsureUnchanged(string manifestPath, byte[]? expectedBytes)
    {
        byte[]? current;
        try
        {
            current = File.Exists(manifestPath) ? ReadBytes(manifestPath) : null;
        }
        catch (ProjectOperationException exception) when (exception.Code == ProjectOperationErrorCode.HomeNotFound)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.ProjectChanged,
                $"The Project manifest at {manifestPath} changed outside this operation. Refresh and retry.");
        }
        if (expectedBytes is null ? current is not null : current is null || !current.AsSpan().SequenceEqual(expectedBytes))
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.ProjectChanged,
                $"The Project manifest at {manifestPath} changed outside this operation. Refresh and retry.");
        }
    }

    private static byte[] ReadBytes(string manifestPath)
    {
        try
        {
            using var file = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (file.Length > MaximumManifestBytes)
            {
                throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                    $"The Project manifest at {manifestPath} exceeds {MaximumManifestBytes} bytes.");
            }

            var bytes = new byte[file.Length];
            file.ReadExactly(bytes);
            return bytes;
        }
        catch (ProjectOperationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.HomeNotFound,
                $"No Forge Project manifest exists at {manifestPath}: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.ManifestReadFailed,
                $"Could not read {manifestPath}: {exception.Message}");
        }
    }

    private static ProjectOperationException WriteFailure(string path, Exception exception) =>
        new(ProjectOperationErrorCode.ManifestWriteFailed, $"Could not update {path}: {exception.Message}");

    private static void TryDeleteOwnTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A unique stale temporary file is never read or promoted. The operation's primary
            // failure is preserved, and cleanup cannot touch another writer's file.
        }
    }
}

internal sealed record ProjectManifestFileSnapshot(string Path, byte[] Bytes);

internal sealed record ProjectManifestFileUpdate<T>(T Value, byte[]? Contents);

internal enum ProjectManifestPublicationBoundary
{
    BeforeFlush,
    BeforeRename,
    AfterRename,
}
