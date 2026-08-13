using System.Text;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>Store-level tests against real Azurite — no Orleans Silo needed. Each test uses a
/// fresh, unique <see cref="ConversationAddress"/> so tests never interfere with each other in the
/// shared event table/artifact container.</summary>
[Collection("Azurite")]
public class ConversationPersistenceTests(AzuriteFixture fixture)
{
    private IConversationEventStore CreateEventStore()
        => new AzureTableConversationEventStore(
            new TableServiceClient(fixture.ConnectionString),
            new ConversationStorageOptions { ConnectionString = fixture.ConnectionString });

    private IConversationArtifactStore CreateArtifactStore()
        => new AzureBlobConversationArtifactStore(
            new BlobServiceClient(fixture.ConnectionString),
            new ConversationStorageOptions { ConnectionString = fixture.ConnectionString });

    private static ConversationAddress NewAddress() => new("dev", Guid.NewGuid());

    private static ConversationEvent UserMessage(ConversationAddress address, Guid runId, long sequence, string text = "hello")
        => new(Guid.NewGuid(), 1, address.ConversationId, runId, sequence, ConversationEventKind.UserMessage,
               ConversationParticipant.User, null, text, null, null, null, null, null, null, DateTimeOffset.UtcNow);

    private static ConversationEvent RunStatusEvent(ConversationAddress address, Guid runId, long sequence, ConversationRunStatus status)
        => new(Guid.NewGuid(), 1, address.ConversationId, runId, sequence, ConversationEventKind.RunStatus,
               ConversationParticipant.Forge, null, null, null, null, null, null, null, status, DateTimeOffset.UtcNow);

    [Fact]
    public async Task AppendAsync_ThenReadAfter_ReturnsInOrder()
    {
        var store = CreateEventStore();
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var e1 = UserMessage(address, runId, 1, "first");
        var e2 = UserMessage(address, runId, 2, "second");

        await store.AppendAsync(address, e1, null, default);
        await store.AppendAsync(address, e2, null, default);

        var events = new List<ConversationEvent>();
        await foreach (var e in store.ReadAfterAsync(address, 0, default))
            events.Add(e);

        Assert.Equal([e1.EventId, e2.EventId], events.Select(e => e.EventId));
        Assert.Equal([1L, 2L], events.Select(e => e.Sequence));
    }

    [Fact]
    public async Task AppendAsync_RepeatingEventId_ReturnsTheSameStoredEvent_NoDuplicateRow()
    {
        var store = CreateEventStore();
        var address = NewAddress();
        var e1 = UserMessage(address, Guid.NewGuid(), 1);

        var first = await store.AppendAsync(address, e1, null, default);
        var second = await store.AppendAsync(address, e1, null, default);

        Assert.Equal(first.EventId, second.EventId);
        Assert.Equal(first.Sequence, second.Sequence);

        var count = 0;
        await foreach (var _ in store.ReadAfterAsync(address, 0, default)) count++;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AppendAsync_SameEventIdDifferentContent_ThrowsLoudly()
    {
        var store = CreateEventStore();
        var address = NewAddress();
        var original = UserMessage(address, Guid.NewGuid(), 1, "original text");
        await store.AppendAsync(address, original, null, default);

        var mismatched = original with { Text = "different text" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AppendAsync(address, mismatched, null, default));
    }

    [Fact]
    public async Task AppendAsync_OversizedInlinePayload_Throws()
    {
        var store = CreateEventStore();
        var address = NewAddress();
        var hugeText = new string('x', 60 * 1024); // 60 KiB > the 48 KiB inline limit
        var huge = UserMessage(address, Guid.NewGuid(), 1, hugeText);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AppendAsync(address, huge, null, default));
    }

    [Fact]
    public async Task ReadLatestForRunAsync_ReturnsTheLastRunStatusEvent_IgnoringOtherKinds()
    {
        var store = CreateEventStore();
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await store.AppendAsync(address, RunStatusEvent(address, runId, 1, ConversationRunStatus.Queued), null, default);
        await store.AppendAsync(address, RunStatusEvent(address, runId, 2, ConversationRunStatus.Running), null, default);
        await store.AppendAsync(address, UserMessage(address, runId, 3, "not a run status"), null, default);

        var latest = await store.ReadLatestForRunAsync(address, runId, default);

        Assert.NotNull(latest);
        Assert.Equal(ConversationRunStatus.Running, latest!.RunStatus);
    }

    [Fact]
    public async Task ArtifactStore_PutThenOpenRead_RoundTripsContentOverTheInlineLimit()
    {
        var store = CreateArtifactStore();
        var address = NewAddress();
        var artifactId = Guid.NewGuid();
        var payload = new byte[60 * 1024]; // over the 48 KiB inline event-JSON limit
        Random.Shared.NextBytes(payload);

        ConversationArtifactReference reference;
        using (var content = new MemoryStream(payload))
            reference = await store.PutAsync(address, null, artifactId, "application/octet-stream", "large.bin", content, default);

        await using var read = await store.OpenReadAsync(address, null, reference, default);
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);

        Assert.Equal(payload, buffer.ToArray());
        Assert.Equal(artifactId.ToString("N"), reference.ArtifactId);
    }

    // Confirmed live against this exact Azurite build (standalone diagnostic, outside this suite):
    // an IfNoneMatch=* create-only violation here surfaces as 409 Conflict / BlobAlreadyExists.
    // The store's catch clause also accepts 412 PreconditionFailed defensively, since real Azure
    // Storage can return either for this condition depending on implementation — both mean the
    // same thing and neither ever triggers a write, which this test verifies across three retries
    // with different (would-be-overwriting) content each time.
    [Fact]
    public async Task ArtifactStore_PutSameStableArtifactIdRepeatedly_AcceptsWithoutEverOverwriting()
    {
        var store = CreateArtifactStore();
        var address = NewAddress();
        var artifactId = Guid.NewGuid();

        using (var first = new MemoryStream(Encoding.UTF8.GetBytes("original")))
            await store.PutAsync(address, null, artifactId, "text/plain", "notes.txt", first, default);
        using (var retry1 = new MemoryStream(Encoding.UTF8.GetBytes("would-be overwrite #1")))
            await store.PutAsync(address, null, artifactId, "text/plain", "notes.txt", retry1, default);
        using (var retry2 = new MemoryStream(Encoding.UTF8.GetBytes("would-be overwrite #2")))
            await store.PutAsync(address, null, artifactId, "text/plain", "notes.txt", retry2, default);

        var reference = new ConversationArtifactReference(artifactId.ToString("N"), "text/plain", "notes.txt");
        await using var read = await store.OpenReadAsync(address, null, reference, default);
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);

        Assert.Equal("original", Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
