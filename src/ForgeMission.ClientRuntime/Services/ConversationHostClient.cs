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
            throw new HttpRequestException($"ConversationHost returned HTTP {(int)response.StatusCode}: {body}");

        return JsonSerializer.Deserialize(body, responseType)
            ?? throw new InvalidOperationException("ConversationHost returned an empty response.");
    }
}
