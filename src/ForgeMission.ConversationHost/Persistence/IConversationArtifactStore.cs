using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Persistence;

/// <summary>
/// The only application-level reader/writer of the <c>forgeconversationartifacts</c> Blob
/// container. Derives the complete Blob path from the address/reference only — never accepts a
/// Blob path or SAS URI from an event/request.
/// </summary>
public interface IConversationArtifactStore
{
    /// <summary>
    /// Create-only: <paramref name="artifactId"/> is supplied by the grain/caller and remains
    /// stable on retry — an existing blob is accepted as that artifact and never overwritten. The
    /// caller writes the Blob before appending its <c>Artifact</c> reference event.
    /// </summary>
    Task<ConversationArtifactReference> PutAsync(
        ConversationAddress address, Guid? runId, Guid artifactId,
        string contentType, string? fileName, Stream content, CancellationToken ct);

    Task<Stream> OpenReadAsync(
        ConversationAddress address, Guid? runId, ConversationArtifactReference artifact, CancellationToken ct);
}
