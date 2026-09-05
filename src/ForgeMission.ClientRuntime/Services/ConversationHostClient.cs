using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Services;

// The only Client Runtime class that knows the Task 6 HTTP/SSE projection: route formatting,
// ConversationContractsJsonContext (de)serialization, and SSE event:/id:/data: frame parsing.
// ConversationRuntimeSession owns session state, reconnect policy, and tool hand-off; it never
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

    // 43.20 task 2 — the Project-control pair. They reuse the same generic PostAsync and the same
    // source-generated context as the Janus methods above; only their routes and message types
    // differ. StreamEventsAsync below is NOT duplicated for them: a control conversation replays
    // and tails through that identical SSE projection.

    public Task<CreateProjectControlConversationResponse> CreateProjectControlAsync(
        CreateProjectControlConversationRequest request, CancellationToken ct) =>
        PostAsync("conversations/project-control", request,
            ConversationContractsJsonContext.Default.CreateProjectControlConversationRequest,
            ConversationContractsJsonContext.Default.CreateProjectControlConversationResponse, ct);

    public Task<SubmitProjectControlMessageResponse> SubmitProjectControlMessageAsync(
        SubmitProjectControlMessageRequest request, CancellationToken ct) =>
        PostAsync($"conversations/{request.ConversationId}/control-messages", request,
            ConversationContractsJsonContext.Default.SubmitProjectControlMessageRequest,
            ConversationContractsJsonContext.Default.SubmitProjectControlMessageResponse, ct);

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

    public async IAsyncEnumerable<ConversationEvent> StreamEventsAsync(
        Guid conversationId, long after, [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"conversations/{conversationId}/events?after={after}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"ConversationHost returned HTTP {(int)response.StatusCode}: {errorBody}");
        }

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
