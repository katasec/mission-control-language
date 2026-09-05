using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ProjectMissionToolRefusalTests
{
    [Fact]
    public async Task ToolRequest_IsRejectedWithStableIdentity_AndNoCapabilityDispatchPath()
    {
        var conversationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var handler = new RefusalHandler(conversationId);
        var host = new ConversationHostClient(new HttpClient(handler) { BaseAddress = new Uri("https://conversation-host.test/") });
        var refusal = new ProjectMissionToolRefusal(host);
        var evt = new ConversationEvent(Guid.NewGuid(), 1, conversationId, Guid.NewGuid(), 1,
            ConversationEventKind.ToolRequested, ConversationParticipant.Implementer, null, null, null, null,
            new ConversationToolRequest(requestId, "Bash", JsonDocument.Parse("{}").RootElement.Clone()), null, null, null,
            DateTimeOffset.UtcNow);

        await refusal.ApplyAsync(evt, CancellationToken.None);

        Assert.Equal(ConversationDeterministicIds.ClientToolResult(requestId), handler.CommandId);
        Assert.Equal(requestId, handler.ToolRequestId);
        Assert.True(handler.IsError);
        Assert.Contains("cannot execute local tools", handler.Content);
    }

    private sealed class RefusalHandler(Guid conversationId) : HttpMessageHandler
    {
        public Guid CommandId { get; private set; }
        public Guid ToolRequestId { get; private set; }
        public bool IsError { get; private set; }
        public string Content { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method != HttpMethod.Post || request.RequestUri!.AbsolutePath != $"/conversations/{conversationId}/tool-results")
                throw new InvalidOperationException("No filesystem, terminal, or other capability route is reachable from refusal.");
            await using var stream = await request.Content!.ReadAsStreamAsync(ct);
            using var body = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            CommandId = body.RootElement.GetProperty("commandId").GetGuid();
            ToolRequestId = body.RootElement.GetProperty("toolRequestId").GetGuid();
            IsError = body.RootElement.GetProperty("isError").GetBoolean();
            Content = body.RootElement.GetProperty("content").GetString()!;
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            { Content = new StringContent($"{{\"conversationId\":\"{conversationId}\",\"runId\":\"{Guid.NewGuid()}\",\"acceptedSequence\":2,\"status\":\"waitingForTool\"}}", Encoding.UTF8, "application/json") };
        }
    }
}
