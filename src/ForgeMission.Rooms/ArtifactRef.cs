namespace ForgeMission.Rooms;

/// <summary>
/// A lightweight reference to a room artifact stored behind <c>IArtifactStore</c> (Phase 38.9).
/// This — never the bytes — is what lives in the message jsonb (D2/D4). Room-scoped: <see cref="RoomId"/>
/// is the confidentiality boundary (the same one that gates messages), so retrieval needs only this
/// ref plus the requesting member. A file uploaded into a room and a file a mission produces are the
/// same kind of thing (D1, uniform): both are room artifacts.
/// </summary>
public sealed class ArtifactRef
{
    /// <summary>Stable id for the artifact (also the leaf of the storage key).</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The room this artifact belongs to — the only gate on retrieval.</summary>
    public Guid RoomId { get; set; }

    /// <summary>Original/display filename (e.g. "Family Halaqa 04.07.2026.pdf").</summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>MIME type, best-effort (defaults to opaque bytes).</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Backend storage key (e.g. "{roomId}/{id}"). Opaque to callers; the store owns its shape.</summary>
    public string Key { get; set; } = string.Empty;
}
