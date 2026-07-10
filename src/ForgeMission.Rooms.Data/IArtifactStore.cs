using ForgeMission.Rooms;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// Storage seam for room artifacts (Phase 38.9) — user files uploaded into a room and files a
/// mission produces. Bytes live behind this seam (local volume in dev, Azure Blob in prod — D2);
/// a lightweight <see cref="ArtifactRef"/> is what gets stored in the message jsonb. Never Postgres
/// large objects.
/// <para>
/// Retrieval is gated by <b>room membership</b> — the same confidentiality boundary that gates
/// messages (<see cref="IReadStore.IsMemberAsync"/>). Enforcement is one line: an artifact is bound
/// to a room at <see cref="PutAsync"/>, and <see cref="OpenAsync"/> refuses any requester who is not
/// a member of that room (D1/§4). There is no user-scoped ACL and no source-vs-output special-casing:
/// a file in a room is a room artifact.
/// </para>
/// </summary>
public interface IArtifactStore
{
    /// <summary>
    /// Store <paramref name="content"/> as an artifact of <paramref name="roomId"/> and return a
    /// reference to persist in the message payload. The room's membership is the only gate on later
    /// retrieval.
    /// </summary>
    Task<ArtifactRef> PutAsync(
        Guid roomId, string filename, string contentType, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Open an artifact by its room + id for <paramref name="requestingMemberId"/>. The download URL
    /// carries only those two ids; filename/content-type/size come from metadata the store persisted
    /// at <see cref="PutAsync"/> time — so the response is <b>authoritative from the store, never from
    /// caller-supplied values</b>. Returns <c>null</c> when the requester is not a member of the room
    /// (the confidentiality check) or the artifact is missing. Callers stream and dispose the content.
    /// </summary>
    Task<ArtifactContent?> OpenAsync(
        Guid roomId, Guid artifactId, Guid requestingMemberId, CancellationToken ct = default);
}

/// <summary>An opened artifact: the byte stream plus the metadata a download response needs.
/// Dispose <see cref="Content"/> after streaming.</summary>
public sealed record ArtifactContent(Stream Content, string Filename, string ContentType, long Size);
