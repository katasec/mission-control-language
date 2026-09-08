using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Application;

// The only Application class that knows the Task 6 HTTP/SSE projection: route formatting,
// ConversationContractsJsonContext (de)serialization, and SSE event:/id:/data: frame parsing.
// ConversationScope owns session state, reconnect policy, and tool hand-off; it never
// touches HttpClient itself.
internal sealed class ConversationHostClient(HttpClient httpClient)
{
    public Task<StartConversationResponse> StartAsync(StartConversationRequest request, CancellationToken ct) =>
        PostAsync("conversations", request,
            ConversationContractsJsonContext.Default.StartConversationRequest,
            ConversationContractsJsonContext.Default.StartConversationResponse, ct);

    public Task<SubmitConversationCommandResponse> SubmitCommandAsync(
        SubmitConversationCommandRequest request, CancellationToken ct) =>
        PostAsync($"conversations/{request.ConversationId}/commands", request,
            ConversationContractsJsonContext.Default.SubmitConversationCommandRequest,
            ConversationContractsJsonContext.Default.SubmitConversationCommandResponse, ct);

    public Task<SubmitToolResultResponse> SubmitToolResultAsync(
        SubmitToolResultRequest request, CancellationToken ct) =>
        PostAsync($"conversations/{request.ConversationId}/tool-results", request,
            ConversationContractsJsonContext.Default.SubmitToolResultRequest,
            ConversationContractsJsonContext.Default.SubmitToolResultResponse, ct);

    // 43.21 task 1 — the universal Project Mission pair. Same generic PostAsync, same typed
    // responses; nothing about the transport differs from the Janus start it replaces.
    public Task<CreateProjectMissionContainerResponse> CreateProjectMissionContainerAsync(
        CreateProjectMissionContainerRequest request, CancellationToken ct) =>
        PostProjectAsync("conversations/project-mission", request,
            ConversationContractsJsonContext.Default.CreateProjectMissionContainerRequest,
            ConversationContractsJsonContext.Default.CreateProjectMissionContainerResponse, ct);

    public Task<StartProjectMissionRunResponse> StartProjectMissionRunAsync(
        StartProjectMissionRunRequest request, CancellationToken ct) =>
        PostProjectAsync($"conversations/{request.ContainerId}/mission-runs", request,
            ConversationContractsJsonContext.Default.StartProjectMissionRunRequest,
            ConversationContractsJsonContext.Default.StartProjectMissionRunResponse, ct);

    public Task<ProjectRunPage> ReadProjectRunsAsync(Guid containerId, long? anchor, long? before, CancellationToken ct) =>
        GetProjectAsync($"conversations/{containerId}/runs{Cursor(anchor, before)}", ConversationContractsJsonContext.Default.ProjectRunPage, ct);

    public Task<ProjectRunDetail> ReadProjectRunAsync(Guid containerId, Guid runId, CancellationToken ct) =>
        GetProjectAsync($"conversations/{containerId}/runs/{runId}", ConversationContractsJsonContext.Default.ProjectRunDetail, ct);

    public Task<ProjectRunEventPage> ReadProjectRunEventsAsync(Guid containerId, Guid runId, long after, long? through, CancellationToken ct) =>
        GetProjectAsync($"conversations/{containerId}/runs/{runId}/events?after={after}{(through is null ? "" : $"&through={through}")}", ConversationContractsJsonContext.Default.ProjectRunEventPage, ct);

    public Task<ProjectCommandReceipt> ReadProjectCommandAsync(Guid containerId, Guid commandId, CancellationToken ct) =>
        GetProjectAsync($"conversations/{containerId}/project-commands/{commandId}", ConversationContractsJsonContext.Default.ProjectCommandReceipt, ct);

    public Task<GetConversationResponse> ReadConversationAsync(Guid conversationId, CancellationToken ct) =>
        GetProjectAsync($"conversations/{conversationId}", ConversationContractsJsonContext.Default.GetConversationResponse, ct);

    public Task<CreateMissionConversationResponse> CreateMissionConversationAsync(CreateMissionConversationRequest request, CancellationToken ct) =>
        PostProjectAsync("mission-conversations", request, ConversationContractsJsonContext.Default.CreateMissionConversationRequest,
            ConversationContractsJsonContext.Default.CreateMissionConversationResponse, ct);

    public Task<ListMissionConversationsResponse> ListMissionConversationsAsync(Guid projectId, CancellationToken ct) =>
        GetProjectAsync($"mission-conversations/{projectId}", ConversationContractsJsonContext.Default.ListMissionConversationsResponse, ct);

    public Task<SubmitMissionTurnResponse> SubmitMissionTurnAsync(SubmitMissionTurnRequest request, CancellationToken ct) =>
        PostProjectAsync($"mission-conversations/{request.ConversationId}/turns", request,
            ConversationContractsJsonContext.Default.SubmitMissionTurnRequest, ConversationContractsJsonContext.Default.SubmitMissionTurnResponse, ct);

    public Task<SubmitMissionTurnResponse> RetryMissionTurnAsync(RetryMissionTurnRequest request, CancellationToken ct) =>
        PostProjectAsync($"mission-conversations/{request.ConversationId}/turns/retry", request,
            ConversationContractsJsonContext.Default.RetryMissionTurnRequest, ConversationContractsJsonContext.Default.SubmitMissionTurnResponse, ct);

    public Task<CancelMissionTurnResponse> CancelMissionTurnAsync(CancelMissionTurnRequest request, CancellationToken ct) =>
        PostProjectAsync($"mission-conversations/{request.ConversationId}/turns/cancel", request,
            ConversationContractsJsonContext.Default.CancelMissionTurnRequest, ConversationContractsJsonContext.Default.CancelMissionTurnResponse, ct);

    public Task<StartEvaluationResponse> StartEvaluationAsync(StartEvaluationRequest request, CancellationToken ct) =>
        PostProjectAsync("evaluations", request, ConversationContractsJsonContext.Default.StartEvaluationRequest,
            ConversationContractsJsonContext.Default.StartEvaluationResponse, ct);

    public Task<GetEvaluationResponse> GetEvaluationAsync(Guid evaluationResultId, CancellationToken ct) =>
        GetProjectAsync($"evaluations/{evaluationResultId}", ConversationContractsJsonContext.Default.GetEvaluationResponse, ct);

    public Task<MissionHandsResult> AttachMissionHandsAsync(AttachMissionHandsRequest request, CancellationToken ct) =>
        PostAsync("mission-hands/attach", request,
            ConversationContractsJsonContext.Default.AttachMissionHandsRequest,
            ConversationContractsJsonContext.Default.MissionHandsResult, ct);

    public Task<MissionHandsResult> DetachMissionHandsAsync(ForgeMission.Conversations.Contracts.DetachMissionHandsRequest request, CancellationToken ct) =>
        PostAsync("mission-hands/detach", request,
            ConversationContractsJsonContext.Default.DetachMissionHandsRequest,
            ConversationContractsJsonContext.Default.MissionHandsResult, ct);

    public Task<MissionHandsResult> SubmitMissionHandsResultAsync(SubmitMissionToolResultRequest request, CancellationToken ct) =>
        PostAsync("mission-hands/result", request,
            ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest,
            ConversationContractsJsonContext.Default.MissionHandsResult, ct);

    public Task<MissionHandsWorkItem> GetMissionHandsWorkAsync(GetMissionHandsWorkRequest request, CancellationToken ct) =>
        PostAsync("mission-hands/work", request,
            ConversationContractsJsonContext.Default.GetMissionHandsWorkRequest,
            ConversationContractsJsonContext.Default.MissionHandsWorkItem, ct);

    public Task<MissionHandsWorkItem> ClaimMissionHandsWorkAsync(ClaimMissionHandsWorkRequest request, CancellationToken ct) =>
        PostAsync("mission-hands/claim", request,
            ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest,
            ConversationContractsJsonContext.Default.MissionHandsWorkItem, ct);

    public Task<MissionHandsResult> BeginMissionHandsConfirmationAsync(BeginMissionHandsConfirmationRequest request, CancellationToken ct) =>
        PostAsync("mission-hands/confirmation", request,
            ConversationContractsJsonContext.Default.BeginMissionHandsConfirmationRequest,
            ConversationContractsJsonContext.Default.MissionHandsResult, ct);

    public Task<MissionHandsResult> CancelMissionHandsAttemptAsync(CancelMissionHandsAttemptRequest request, CancellationToken ct) =>
        PostAsync("mission-hands/cancel", request,
            ConversationContractsJsonContext.Default.CancelMissionHandsAttemptRequest,
            ConversationContractsJsonContext.Default.MissionHandsResult, ct);

    public Task<MissionHandsResult> RecoverMissionHandsInFlightAsync(RecoverMissionHandsInFlightRequest request, CancellationToken ct) =>
        PostAsync("mission-hands/recover", request,
            ConversationContractsJsonContext.Default.RecoverMissionHandsInFlightRequest,
            ConversationContractsJsonContext.Default.MissionHandsResult, ct);

    public async IAsyncEnumerable<ConversationEvent> StreamEventsAsync(
        Guid conversationId, long after, Action? onConnected = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"conversations/{conversationId}/events?after={after}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"ConversationHost returned HTTP {(int)response.StatusCode}: {errorBody}");
        }

        onConnected?.Invoke();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var data = new StringBuilder();

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    yield return JsonSerializer.Deserialize(data.ToString(), ConversationContractsJsonContext.Default.ConversationEvent)
                        ?? throw new InvalidOperationException("ConversationHost sent an invalid conversation event.");
                    data.Clear();
                }

                continue;
            }

            if (line.StartsWith("data: ", StringComparison.Ordinal))
                data.Append(line["data: ".Length..]);
        }
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string route,
        TRequest request,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<TResponse> responseType,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request, requestType);
        using var response = await httpClient.PostAsync(route, new StringContent(json, Encoding.UTF8, "application/json"), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            // The status code is carried on the exception itself (the BCL overload), so a caller
            // can map an EXPECTED outcome — invalid/not-found/conflict — to its own typed result
            // instead of parsing this message string.
            throw new HttpRequestException(
                $"ConversationHost returned HTTP {(int)response.StatusCode}: {body}", null, response.StatusCode);

        return JsonSerializer.Deserialize(body, responseType)
            ?? throw new InvalidOperationException("ConversationHost returned an empty response.");
    }

    private async Task<TResponse> PostProjectAsync<TRequest, TResponse>(
        string route, TRequest request, JsonTypeInfo<TRequest> requestType, JsonTypeInfo<TResponse> responseType, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request, requestType);
        using var response = await httpClient.PostAsync(route, new StringContent(json, Encoding.UTF8, "application/json"), ct);
        return await DecodeProjectAsync(response, responseType, ct);
    }

    private async Task<TResponse> GetProjectAsync<TResponse>(string route, JsonTypeInfo<TResponse> responseType, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(route, ct);
        return await DecodeProjectAsync(response, responseType, ct);
    }

    private static async Task<TResponse> DecodeProjectAsync<TResponse>(HttpResponseMessage response, JsonTypeInfo<TResponse> responseType, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.IsSuccessStatusCode)
        {
            try
            {
                return JsonSerializer.Deserialize(body, responseType)
                    ?? throw new ConversationHostProtocolException("ConversationHost returned an empty or invalid Project response.");
            }
            catch (JsonException ex)
            {
                throw new ConversationHostProtocolException("ConversationHost returned an invalid Project response.", ex);
            }
        }

        ConversationApiError? error;
        try { error = JsonSerializer.Deserialize(body, ConversationContractsJsonContext.Default.ConversationApiError); }
        catch (JsonException ex) { throw new ConversationHostProtocolException($"ConversationHost returned malformed Project error HTTP {(int)response.StatusCode}.", ex); }
        if (error is null || string.IsNullOrWhiteSpace(error.Code) || string.IsNullOrWhiteSpace(error.Message))
            throw new ConversationHostProtocolException($"ConversationHost returned malformed Project error HTTP {(int)response.StatusCode}.");
        throw new ConversationHostProjectException(error, response.StatusCode);
    }

    private static string Cursor(long? anchor, long? before) => anchor is null && before is null ? "" : $"?anchor={anchor}&before={before}";
}

internal sealed class ConversationHostProjectException(ConversationApiError error, System.Net.HttpStatusCode status)
    : HttpRequestException(error.Message, null, status)
{
    public ConversationApiError Error { get; } = error;
}

internal sealed class ConversationHostProtocolException : InvalidOperationException
{
    public ConversationHostProtocolException(string message) : base(message) { }
    public ConversationHostProtocolException(string message, Exception inner) : base(message, inner) { }
}
