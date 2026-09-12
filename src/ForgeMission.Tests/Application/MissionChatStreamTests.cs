using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.Application;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.Application;

/// <summary>
/// Phase 48. The one place a chat's history is assembled, so this is where the gapless rules are
/// proved: the bound is fixed before the first page, every page must advance the cursor, assembly
/// finishes only at that bound, and the live tail resumes from the same sequence.
/// </summary>
public sealed class MissionChatStreamTests : IDisposable
{
    private readonly string profile = Path.Combine(Path.GetTempPath(), "forge-chat-stream", Guid.NewGuid().ToString("N"));
    private readonly ProjectService projects;
    private readonly MissionVersionService versions;
    private static readonly Guid Chat = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Other = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public MissionChatStreamTests()
    {
        projects = new ProjectService(Path.Combine(profile, "Forge", "Projects"));
        versions = new MissionVersionService(projects);
    }

    [Fact]
    public async Task AMultiPageHistory_IsAssembledCompleteAndInOrder_WithTheCursorAdvancingEachTime()
    {
        var host = new FakeHost(history: 5, pageSize: 2);
        await using var stream = Stream(host);

        var history = await stream.OpenAsync(Chat, CancellationToken.None);

        Assert.Equal(5, history.ThroughSequence);
        Assert.Equal([1, 2, 3, 4, 5], history.Events.Select(evt => evt.Sequence));
        Assert.Equal(history.Events.Select(evt => evt.EventId).Distinct().Count(), history.Events.Count);
        // Each request started where the previous page actually ended, and every one carried the same
        // fixed upper bound.
        Assert.Equal([(0L, 5L), (2L, 5L), (4L, 5L)], host.PageRequests);
    }

    [Fact]
    public async Task TheTail_ResumesAtTheSameFixedSequence_SoTheNextLiveEventIsItsExpectedNext()
    {
        var host = new FakeHost(history: 3, pageSize: 10);
        await using var stream = Stream(host);

        var history = await stream.OpenAsync(Chat, CancellationToken.None);

        Assert.Equal(3, history.ThroughSequence);
        Assert.Equal(3L, host.TailAfter);
    }

    [Fact]
    public async Task ALiveEventAppendedMidAssembly_DoesNotMoveTheBound_AndNeverEntersTheSeed()
    {
        // The snapshot is read once, so an event that lands while paging stays outside this history and
        // arrives only through the live tail.
        var host = new FakeHost(history: 4, pageSize: 2) { AppendDuringPaging = true };
        await using var stream = Stream(host);

        var history = await stream.OpenAsync(Chat, CancellationToken.None);

        Assert.Equal(4, history.ThroughSequence);
        Assert.Equal([1, 2, 3, 4], history.Events.Select(evt => evt.Sequence));
        Assert.All(host.PageRequests, request => Assert.Equal(4L, request.Through));
        Assert.Equal(1, host.SnapshotReads);
    }

    [Fact]
    public async Task APageThatClaimsMoreWithoutAdvancing_FailsAsAProtocolFault_AndStartsNoTail()
    {
        var host = new FakeHost(history: 4, pageSize: 2) { StallAfterFirstPage = true };
        await using var stream = Stream(host);

        var failure = await Assert.ThrowsAsync<MissionChatHistoryException>(() => stream.OpenAsync(Chat, CancellationToken.None));

        Assert.Equal(MissionChatFailureCode.HistoryProtocol, failure.Code);
        Assert.Null(host.TailAfter);
    }

    [Fact]
    public async Task APageThatClaimsCompletenessShortOfTheBound_FailsTheSameWay_AndStartsNoTail()
    {
        var host = new FakeHost(history: 4, pageSize: 2) { ClaimCompleteEarly = true };
        await using var stream = Stream(host);

        var failure = await Assert.ThrowsAsync<MissionChatHistoryException>(() => stream.OpenAsync(Chat, CancellationToken.None));

        Assert.Equal(MissionChatFailureCode.HistoryProtocol, failure.Code);
        Assert.Null(host.TailAfter);
    }

    [Fact]
    public async Task AFailedPageRead_IsTypedUnavailable_AndStartsNoTail()
    {
        var host = new FakeHost(history: 4, pageSize: 2) { PageStatus = HttpStatusCode.ServiceUnavailable };
        await using var stream = Stream(host);

        var failure = await Assert.ThrowsAsync<MissionChatHistoryException>(() => stream.OpenAsync(Chat, CancellationToken.None));

        Assert.Equal(MissionChatFailureCode.HistoryUnavailable, failure.Code);
        Assert.Null(host.TailAfter);
    }

    [Fact]
    public async Task AnEmptyChat_NeedsNoPageAtAll_AndStillResumesFromItsOwnSequence()
    {
        var host = new FakeHost(history: 0, pageSize: 2);
        await using var stream = Stream(host);

        var history = await stream.OpenAsync(Chat, CancellationToken.None);

        Assert.Empty(history.Events);
        Assert.Equal(0, history.ThroughSequence);
        Assert.Empty(host.PageRequests);
        Assert.Equal(0L, host.TailAfter);
    }

    [Fact]
    public async Task Retargeting_DisposesThePreviousTail_BeforeFollowingTheNewChat()
    {
        var host = new FakeHost(history: 2, pageSize: 10);
        await using var stream = Stream(host);

        await stream.OpenAsync(Chat, CancellationToken.None);
        var first = host.TailStreams.Single();
        await stream.OpenAsync(Other, CancellationToken.None);

        Assert.Equal([Chat, Other], host.TailConversations);
        Assert.True(await Stopped(first));
    }

    [Fact]
    public async Task DisposingTheStream_StopsFollowingAtAll()
    {
        var host = new FakeHost(history: 1, pageSize: 10);
        var stream = Stream(host);

        await stream.OpenAsync(Chat, CancellationToken.None);
        await stream.DisposeAsync();

        Assert.True(await Stopped(host.TailStreams.Single()));
    }

    // --- helpers ---------------------------------------------------------------------------------

    private MissionChatStream Stream(FakeHost host)
    {
        var factory = new SingleClientFactory(host);
        return new MissionChatStream("session", new MissionConversationService(versions, factory),
            new ConversationHostClient(factory.CreateClient("conversation-host")), _ => { }, CancellationToken.None);
    }

    private static async Task<bool> Stopped(OpenStream tail) =>
        await Task.WhenAny(tail.Stopped, Task.Delay(TimeSpan.FromSeconds(5))) == tail.Stopped;

    /// <summary>A Conversation Host that answers exactly the three reads this path makes: one snapshot,
    /// finite bounded pages, and one SSE tail. Each flag below bends one of those answers so the
    /// assembly rules can be held to.</summary>
    private sealed class FakeHost : HttpMessageHandler
    {
        private readonly List<ConversationEvent> events;
        private readonly int pageSize;

        public FakeHost(int history, int pageSize)
        {
            this.pageSize = pageSize;
            events = [.. Enumerable.Range(1, history).Select(sequence => Event(Chat, sequence))];
        }

        public List<(long After, long Through)> PageRequests { get; } = [];
        public List<Guid> TailConversations { get; } = [];
        public List<OpenStream> TailStreams { get; } = [];
        public long? TailAfter { get; private set; }
        public int SnapshotReads { get; private set; }
        public bool AppendDuringPaging { get; set; }
        public bool StallAfterFirstPage { get; set; }
        public bool ClaimCompleteEarly { get; set; }
        public HttpStatusCode PageStatus { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.StartsWith("/mission-conversations/", StringComparison.Ordinal) && path.EndsWith("/events", StringComparison.Ordinal))
                return Task.FromResult(Page(request));
            if (path.EndsWith("/events", StringComparison.Ordinal))
                return Task.FromResult(Tail(request));
            SnapshotReads++;
            var conversation = Guid.Parse(path.Split('/')[^1]);
            var snapshot = new ConversationSnapshot(conversation, "Durable", null,
                conversation == Chat ? events.Count : 0, ConversationRunStatus.Queued, null, DateTimeOffset.UtcNow,
                ConversationPurpose.MissionConversation, Guid.NewGuid(), null, null, null, null, null, null, "New chat");
            return Task.FromResult(Json(new GetConversationResponse(snapshot),
                ConversationContractsJsonContext.Default.GetConversationResponse, HttpStatusCode.OK));
        }

        private HttpResponseMessage Page(HttpRequestMessage request)
        {
            if (PageStatus != HttpStatusCode.OK)
                return Json(new ConversationApiError("serviceUnavailable", "unavailable"),
                    ConversationContractsJsonContext.Default.ConversationApiError, PageStatus);

            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            var after = long.Parse(query["after"]!);
            var through = long.Parse(query["through"]!);
            PageRequests.Add((after, through));

            if (AppendDuringPaging)
                events.Add(Event(Chat, events.Count + 1));

            var page = events.Where(evt => evt.Sequence > after && evt.Sequence <= through).Take(pageSize).ToArray();
            var returned = page.Length == 0 ? after : page[^1].Sequence;
            var hasMore = returned < through;
            if (StallAfterFirstPage && PageRequests.Count >= 1)
                returned = after; // claims more, stands still
            if (ClaimCompleteEarly)
                hasMore = false; // claims the range is finished while short of the bound

            return Json(new MissionConversationEventPage(Chat, page, after, through, returned, hasMore),
                ConversationContractsJsonContext.Default.MissionConversationEventPage, HttpStatusCode.OK);
        }

        private HttpResponseMessage Tail(HttpRequestMessage request)
        {
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            TailAfter = long.Parse(query["after"]!);
            TailConversations.Add(Guid.Parse(request.RequestUri!.AbsolutePath.Split('/')[^2]));
            // A live stream that stays open until the reader's own token cancels it, which is what
            // retargeting and disposal must do.
            var open = new OpenStream();
            TailStreams.Add(open);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamingContent(open) };
        }

        private static ConversationEvent Event(Guid conversation, int sequence) =>
            new(Guid.NewGuid(), 1, conversation, null, sequence, ConversationEventKind.ParticipantMessage,
                ConversationParticipant.Forge, 1, $"line {sequence}", null, null, null, null, null, null,
                DateTimeOffset.UnixEpoch, null, null, "Proposer");

        private static HttpResponseMessage Json<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, HttpStatusCode status) =>
            new(status) { Content = new StringContent(JsonSerializer.Serialize(value, type), Encoding.UTF8, "application/json") };
    }

    /// <summary>Content whose read stream is handed straight to the caller, so the tail reads a live
    /// stream rather than a buffered body.</summary>
    private sealed class StreamingContent(System.IO.Stream content) : HttpContent
    {
        protected override Task SerializeToStreamAsync(System.IO.Stream stream, TransportContext? context) => content.CopyToAsync(stream);

        protected override Task<System.IO.Stream> CreateContentReadStreamAsync() => Task.FromResult(content);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>A stream that never ends on its own: each read waits for the reader's own cancellation,
    /// recording the token so a test can prove the tail really was stopped.</summary>
    private sealed class OpenStream : System.IO.Stream
    {
        private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Stopped => stopped.Task;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => stopped.TrySetResult());
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                stopped.TrySetResult();
                throw;
            }

            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://conversation-host/") };
    }

    public void Dispose()
    {
        if (Directory.Exists(profile)) Directory.Delete(profile, recursive: true);
    }
}
