using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;
using Microsoft.Extensions.AI;

namespace ForgeMission.Application;

/// <summary>Owns prompt routing and the scoped local lifetime of a legacy conversation. The
/// Conversation Host remains authoritative for durable state; Bob remains authoritative for
/// every local capability decision.</summary>
internal sealed class ConversationService(
    ApplicationSessionService sessions,
    IHttpClientFactory clients,
    string? missionRuntimeMode,
    Action<ApplicationEvent> publish,
    CancellationToken applicationStopping) : IConversationService
{
    public async Task<PromptResponse> PromptAsync(PromptRequest request, CancellationToken ct)
    {
        if (!sessions.TryGet(request.SessionId, out var session) || session is null)
            throw new KeyNotFoundException();

        try
        {
            return session.Runtime == SessionRuntimeKind.DurableConversation
                ? await SendDurablePromptAsync(session, request, ct)
                : await SendMissionPromptAsync(session, request, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            publish(new ApplicationEvent(ApplicationEventKind.Error, request.SessionId, Error: exception.Message));
            return new PromptResponse(exception.Message, IsError: true);
        }
    }

    private async Task<PromptResponse> SendDurablePromptAsync(
        ApplicationSession session, PromptRequest request, CancellationToken ct)
    {
        var conversationId = await session.Conversation.SendPromptAsync(
            () => new ConversationScope(
                request.SessionId,
                session.Mission ?? "Janus",
                new ConversationHostClient(clients.CreateClient("conversation-host")),
                session.Execution.Capabilities,
                session.Execution,
                publish,
                applicationStopping),
            request.Prompt,
            ct);
        return new PromptResponse(string.Empty, ConversationId: conversationId);
    }

    private async Task<PromptResponse> SendMissionPromptAsync(
        ApplicationSession session, PromptRequest request, CancellationToken ct)
    {
        var client = clients.CreateClient("mission-runtime");
        var publishText = (string text) =>
            publish(new ApplicationEvent(ApplicationEventKind.MissionTextDelta, request.SessionId, Text: text));
        var publishTool = (FunctionCallContent call, ToolCallNotificationState state) =>
            publish(new ApplicationEvent(
                ApplicationEventKind.ToolCallStatus,
                request.SessionId,
                ToolName: call.Name,
                ToolStatus: state.ToString(),
                ToolTarget: ExtractTarget(call)));

        var answer = UsesCloudMissionRuntime(missionRuntimeMode)
            ? NewCloudClient(client, session.Mission).SendAsync(request.Prompt, session.Execution.Capabilities,
                session.Execution, publishText, update => publishTool(update.Call, update.State), ct)
            : NewLocalClient(client, session.Mission).SendAsync(request.Prompt, session.Execution.Capabilities,
                session.Execution, publishText, update => publishTool(update.Call, update.State), ct);
        return new PromptResponse(await answer);
    }

    internal static bool UsesCloudMissionRuntime(string? mode) =>
        mode is null || mode.Equals("cloud", StringComparison.OrdinalIgnoreCase);

    private static LegacyCloudMissionProtocolClient NewCloudClient(HttpClient client, string? mission) =>
        mission is null ? new LegacyCloudMissionProtocolClient(client) : new LegacyCloudMissionProtocolClient(client, mission);

    private static LegacyMissionProtocolClient NewLocalClient(HttpClient client, string? mission) =>
        mission is null ? new LegacyMissionProtocolClient(client) : new LegacyMissionProtocolClient(client, mission);

    private static string? ExtractTarget(FunctionCallContent call) =>
        call.Name switch
        {
            "Read" or "Edit" or "Write" =>
                call.Arguments?.TryGetValue("file_path", out var file) is true ? file?.ToString() : null,
            "Bash" =>
                call.Arguments?.TryGetValue("command", out var command) is true ? command?.ToString() : null,
            _ => null,
        };
}

// The scope is an attachment-lifetime detail of ConversationService, not another durable owner.
// It carries the retained conversation ID and tail for one selected Application session only.
internal sealed class ConversationScope : IAsyncDisposable
{
    private readonly string _sessionId;
    private readonly string _missionRef;
    private readonly ConversationHostClient _hostClient;
    private readonly CapabilityRegistry _capabilities;
    private readonly LegacyJanusToolDelivery _delivery;
    private readonly ConversationTailReader _tail;

    private Guid? _conversationId;

    public ConversationScope(
        string sessionId,
        string missionRef,
        ConversationHostClient hostClient,
        CapabilityRegistry capabilities,
        ICapabilityDispatcher dispatcher,
        Action<ApplicationEvent> publish,
        CancellationToken applicationStopping,
        ToolExecutorRegistry? toolExecutors = null)
    {
        _sessionId = sessionId;
        _missionRef = missionRef;
        _hostClient = hostClient;
        _capabilities = capabilities;
        _delivery = new LegacyJanusToolDelivery(sessionId, hostClient, dispatcher, publish, toolExecutors);
        _tail = new ConversationTailReader(sessionId, hostClient, publish, applicationStopping, OnTailEventAsync);
    }

    public async Task<Guid> SendAsync(string prompt, CancellationToken ct)
    {
        if (_conversationId is null)
        {
            var response = await _hostClient.StartAsync(
                new StartConversationRequest(Guid.NewGuid(), _missionRef, prompt, ToCapabilityDeclarations(_capabilities)), ct);
            _conversationId = response.ConversationId;
            _tail.Start(_conversationId.Value);
        }
        else
        {
            await _hostClient.SubmitCommandAsync(
                new SubmitConversationCommandRequest(_conversationId.Value, Guid.NewGuid(), prompt), ct);
        }

        return _conversationId.Value;
    }

    private async Task OnTailEventAsync(ConversationEvent evt, CancellationToken ct)
        => await _delivery.ApplyAsync(evt, _conversationId!.Value, ct);

    private static ConversationCapabilityDeclaration[] ToCapabilityDeclarations(CapabilityRegistry capabilities) =>
        capabilities.ToolDeclarations.Select(tool =>
        {
            var function = (AIFunction)tool;
            return new ConversationCapabilityDeclaration(function.Name, function.Description, function.JsonSchema);
        }).ToArray();

    public ValueTask DisposeAsync() => _tail.DisposeAsync();
}
