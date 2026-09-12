using System.Text.Json;

namespace ForgeMission.Application;

/// <summary>
/// The only reader and writer of the managed-chat marker (Phase 48). The marker sits beside the
/// Projects root rather than inside a Project, and names no directory, asset, or manifest, which is
/// why it is not <see cref="ProjectService"/>'s to write: ProjectService remains the sole writer of
/// a Project's own directory, assets, and manifest.
/// </summary>
internal sealed class ManagedChatProjectMarkerFile(string projectsRoot)
{
    internal const string FileName = "mission-chat-project.json";

    private string Path => System.IO.Path.Combine(projectsRoot, FileName);

    /// <summary>Reads the marker without throwing. An unreadable or malformed file is reported as
    /// <see cref="ManagedChatProjectMarkerState.Invalid"/> so the caller can repair the marker
    /// rather than having to interpret an exception.</summary>
    internal ManagedChatProjectMarkerRead Read()
    {
        var path = Path;
        if (!File.Exists(path))
            return new ManagedChatProjectMarkerRead(ManagedChatProjectMarkerState.Missing, Guid.Empty);

        try
        {
            var marker = JsonSerializer.Deserialize(File.ReadAllBytes(path), ManagedChatProjectJsonContext.Default.ManagedChatProjectMarker);
            return marker is null || marker.ProjectId == Guid.Empty
                ? new ManagedChatProjectMarkerRead(ManagedChatProjectMarkerState.Invalid, Guid.Empty)
                : new ManagedChatProjectMarkerRead(ManagedChatProjectMarkerState.Found, marker.ProjectId);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new ManagedChatProjectMarkerRead(ManagedChatProjectMarkerState.Invalid, Guid.Empty);
        }
    }

    /// <summary>Writes the marker last in a provisioning sequence, replacing any earlier one. The
    /// write is a temp file plus a replace, so a crash leaves either the old marker or the new one
    /// and never a half-written file.</summary>
    internal async Task WriteAsync(Guid projectId, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(projectsRoot);
        var path = Path;
        var temporary = path + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new ManagedChatProjectMarker(projectId),
            ManagedChatProjectJsonContext.Default.ManagedChatProjectMarker);
        await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
        File.Move(temporary, path, overwrite: true);
    }
}
