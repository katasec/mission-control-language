using System.Net;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Persistence;

/// <summary>
/// Writes create-only to <c>{escaped-tenant}/{conversationId:N}/{run-or-conversation}/
/// {artifactId:N}</c> in the private <c>forgeconversationartifacts</c> container. Derives the
/// complete Blob path from the address/reference only — never accepts a Blob path or SAS URI from
/// an event/request. No delete or retention policy.
/// </summary>
public sealed class AzureBlobConversationArtifactStore : IConversationArtifactStore
{
    private readonly BlobContainerClient _container;

    public AzureBlobConversationArtifactStore(BlobServiceClient blobServiceClient, ConversationStorageOptions options)
    {
        _container = blobServiceClient.GetBlobContainerClient(options.ArtifactContainerName);
    }

    public async Task<ConversationArtifactReference> PutAsync(
        ConversationAddress address, Guid? runId, Guid artifactId,
        string contentType, string? fileName, Stream content, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(BlobPath(address, runId, artifactId));

        try
        {
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                // Create-only: fail rather than overwrite if a blob already exists at this path.
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            }, ct);
        }
        catch (RequestFailedException ex) when (ex.Status is (int)HttpStatusCode.Conflict or (int)HttpStatusCode.PreconditionFailed)
        {
            // artifactId is stable on retry — an existing blob is accepted as that artifact.
            // An IfNoneMatch=* create-only violation can surface as either 409 (Conflict,
            // "BlobAlreadyExists") or 412 (PreconditionFailed) depending on the Azure Storage
            // implementation — both mean the same thing here, and neither ever triggers a write.
        }

        return new ConversationArtifactReference(artifactId.ToString("N"), contentType, fileName);
    }

    public async Task<Stream> OpenReadAsync(
        ConversationAddress address, Guid? runId, ConversationArtifactReference artifact, CancellationToken ct)
    {
        var artifactId = Guid.ParseExact(artifact.ArtifactId, "N");
        var blob = _container.GetBlobClient(BlobPath(address, runId, artifactId));
        return await blob.OpenReadAsync(cancellationToken: ct);
    }

    private static string BlobPath(ConversationAddress address, Guid? runId, Guid artifactId)
    {
        var runSegment = runId is { } id ? id.ToString("N") : "conversation";
        return $"{Uri.EscapeDataString(address.TenantId)}/{address.ConversationId:N}/{runSegment}/{artifactId:N}";
    }
}
