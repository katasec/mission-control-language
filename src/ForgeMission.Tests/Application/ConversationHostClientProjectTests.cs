using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.Application;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.Application;

public sealed class ConversationHostClientProjectTests
{
    [Fact]
    public async Task ProjectErrorDecoder_PreservesTypedErrorAndRejectsMalformedBodies()
    {
        using var http = new HttpClient(new ErrorHandler("{\"code\":\"historySynchronizing\",\"message\":\"Retry shortly.\"}"))
        { BaseAddress = new Uri("http://localhost/") };
        var client = new ConversationHostClient(http);

        var error = await Assert.ThrowsAsync<ConversationHostProjectException>(() =>
            client.ReadProjectRunsAsync(Guid.NewGuid(), null, null, CancellationToken.None));
        Assert.Equal("historySynchronizing", error.Error.Code);

        using var malformed = new HttpClient(new ErrorHandler("not-json")) { BaseAddress = new Uri("http://localhost/") };
        var malformedClient = new ConversationHostClient(malformed);
        await Assert.ThrowsAsync<ConversationHostProtocolException>(() =>
            malformedClient.ReadProjectRunsAsync(Guid.NewGuid(), null, null, CancellationToken.None));
    }

    [Fact]
    public async Task TheBoundedHistoryRead_SendsBothBoundsAndDecodesAllFourOfThePagesOwnFacts()
    {
        var conversation = Guid.NewGuid();
        var page = new MissionConversationEventPage(conversation, [], 2, 9, 4, true);
        var handler = new CapturingHandler(System.Text.Json.JsonSerializer.Serialize(page,
            ConversationContractsJsonContext.Default.MissionConversationEventPage));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var read = await new ConversationHostClient(http).ReadMissionConversationEventsAsync(conversation, 2, 9, CancellationToken.None);

        Assert.Equal($"/mission-conversations/{conversation}/events", handler.Path);
        Assert.Equal("?after=2&through=9", handler.Query);
        Assert.Equal(2, read.AfterSequence);
        Assert.Equal(9, read.RequestedThroughSequence);
        Assert.Equal(4, read.ReturnedThroughSequence);
        Assert.True(read.HasMore);
    }

    [Fact]
    public async Task AFailedBoundedRead_StaysTheSameTypedHostFailure()
    {
        using var http = new HttpClient(new ErrorHandler("{\"code\":\"invalidRequest\",\"message\":\"The history range is invalid.\"}"))
        { BaseAddress = new Uri("http://localhost/") };

        var error = await Assert.ThrowsAsync<ConversationHostProjectException>(() =>
            new ConversationHostClient(http).ReadMissionConversationEventsAsync(Guid.NewGuid(), 0, 3, CancellationToken.None));

        Assert.Equal("invalidRequest", error.Error.Code);
    }

    private sealed class CapturingHandler(string body) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? Query { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsolutePath;
            Query = request.RequestUri.Query;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class ErrorHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
