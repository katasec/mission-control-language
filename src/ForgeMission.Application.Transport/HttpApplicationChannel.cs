using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ForgeMission.Application.Transport;

public sealed class HttpApplicationChannel : IApplicationChannel, IDisposable
{
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly Action<HttpRequestMessage>? prepareStreamingRequest;

    // Blazor WebAssembly's Fetch-based HttpClient buffers the whole response by default, which
    // never completes for a long-lived SSE stream. The WASM host opts in via
    // Microsoft.AspNetCore.Components.WebAssembly.Http.WebAssemblyHttpRequestMessageExtensions.
    // SetBrowserResponseStreamingEnabled — a WASM-only extension this framework-agnostic project
    // can't reference directly, so the host supplies it as a hook instead.
    public HttpApplicationChannel(HttpClient httpClient, Action<HttpRequestMessage>? prepareStreamingRequest = null)
    {
        this.httpClient = httpClient;
        this.prepareStreamingRequest = prepareStreamingRequest;
    }

    public HttpApplicationChannel(Uri baseAddress, Action<HttpRequestMessage>? prepareStreamingRequest = null)
    {
        httpClient = new HttpClient { BaseAddress = baseAddress };
        ownsHttpClient = true;
        this.prepareStreamingRequest = prepareStreamingRequest;
    }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
    {
        var route = RouteFor(request);
        var requestType = TypeInfoFor(typeof(TRequest));
        var responseType = TypeInfoFor(typeof(TResponse));
        if (requestType is null || responseType is null)
            throw new InvalidOperationException($"Unsupported application transport type: {typeof(TRequest).Name}.");

        var json = JsonSerializer.Serialize(request, requestType);
        using var response = await httpClient.PostAsync(route,
            new StringContent(json, Encoding.UTF8, "application/json"), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();
        return (TResponse)(JsonSerializer.Deserialize(body, responseType)
            ?? throw new InvalidOperationException("The Application Host returned an empty response."));
    }

    public async IAsyncEnumerable<ApplicationEvent> Subscribe(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "transport/events");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        prepareStreamingRequest?.Invoke(request);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            var message = JsonSerializer.Deserialize(line["data: ".Length..], ConversationRelayJsonContext.Default.ApplicationEvent)
                ?? throw new InvalidOperationException("The Application Host sent an invalid event.");
            yield return message;
        }
    }

    private static string RouteFor<TRequest>(TRequest request) => request switch
    {
        SessionSetupRequest => "transport/session/setup",
        ProjectDraftRequest => "transport/project/draft",
        ProjectCreateRequest => "transport/project/create",
        ProjectOpenRequest => "transport/project/open",
        StartProjectMissionRunRequest => "transport/project/mission/run",
        RetryProjectMissionSubmissionRequest => "transport/project/mission/retry",
        GetProjectMissionStateRequest => "transport/project/mission/state",
        GetProjectRunsRequest => "transport/project/runs",
        GetProjectRunRequest => "transport/project/run",
        GetProjectRunEventsRequest => "transport/project/run/events",
        SelectProjectMissionRequest => "transport/project/mission/select",
        GetProjectWorkbenchRequest => "transport/project/workbench",
        OpenProjectDocumentRequest => "transport/project/document",
        CapabilityDispatchRequest => "transport/capability/dispatch",
        PromptRequest => "transport/prompt",
        ConfirmationResponseRequest => "transport/confirmation/respond",
        AcknowledgeMissionHandsRequest => "transport/mission-hands/acknowledge",
        DetachMissionHandsRequest => "transport/mission-hands/detach",
        GetMissionHandsStatusRequest => "transport/mission-hands/status",
        ExecuteMissionHandsRequest => "transport/mission-hands/execute",
        CancelMissionHandsRequest => "transport/mission-hands/cancel",
        RecoverMissionHandsRequest => "transport/mission-hands/recover",
        ListMissionConversationsRequest => "transport/mission-conversations/list",
        ListApprovedMissionVersionsRequest => "transport/mission-conversations/approved-versions",
        CreateMissionConversationRequest => "transport/mission-conversations/create",
        GetMissionAuthoringRequest => "transport/mission-authoring/get",
        CreateMissionDraftRequest => "transport/mission-authoring/draft",
        SaveMissionDraftRequest => "transport/mission-authoring/save-draft",
        PromoteMissionCandidateRequest => "transport/mission-authoring/promote",
        AddEvaluationCaseRequest => "transport/mission-authoring/case/add",
        UpdateEvaluationCaseRequest => "transport/mission-authoring/case/update",
        RunEvaluationCaseRequest => "transport/mission-authoring/evaluate",
        PublishMissionVersionRequest => "transport/mission-authoring/publish",
        StartMissionChatRequest => "transport/mission-chat/start",
        CreateMissionChatRequest => "transport/mission-chat/new",
        OpenMissionChatRequest => "transport/mission-chat/open",
        SubmitMissionChatTurnRequest => "transport/mission-chat/turn",
        _ => throw new InvalidOperationException($"Unsupported application request: {typeof(TRequest).Name}."),
    };

    // The application vocabulary keeps its own (numeric-enum) serialization, and the Mission Chat
    // family keeps the conversation relay's — because it carries durable ConversationEvent values.
    // Both are source-generated; this picks the owning context rather than merging their options.
    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo? TypeInfoFor(Type type) =>
        ApplicationJsonContext.Default.GetTypeInfo(type) ?? ConversationRelayJsonContext.Default.GetTypeInfo(type);

    public void Dispose()
    {
        if (ownsHttpClient)
            httpClient.Dispose();
    }
}
