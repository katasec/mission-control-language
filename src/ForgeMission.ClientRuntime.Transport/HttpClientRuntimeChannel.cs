using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ForgeMission.ClientRuntime.Transport;

public sealed class HttpClientRuntimeChannel : IClientRuntimeChannel, IDisposable
{
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly Action<HttpRequestMessage>? prepareStreamingRequest;

    // Blazor WebAssembly's Fetch-based HttpClient buffers the whole response by default, which
    // never completes for a long-lived SSE stream. The WASM host opts in via
    // Microsoft.AspNetCore.Components.WebAssembly.Http.WebAssemblyHttpRequestMessageExtensions.
    // SetBrowserResponseStreamingEnabled — a WASM-only extension this framework-agnostic project
    // can't reference directly, so the host supplies it as a hook instead.
    public HttpClientRuntimeChannel(HttpClient httpClient, Action<HttpRequestMessage>? prepareStreamingRequest = null)
    {
        this.httpClient = httpClient;
        this.prepareStreamingRequest = prepareStreamingRequest;
    }

    public HttpClientRuntimeChannel(Uri baseAddress, Action<HttpRequestMessage>? prepareStreamingRequest = null)
    {
        httpClient = new HttpClient { BaseAddress = baseAddress };
        ownsHttpClient = true;
        this.prepareStreamingRequest = prepareStreamingRequest;
    }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
    {
        var route = RouteFor(request);
        var requestType = ClientRuntimeJsonContext.Default.GetTypeInfo(typeof(TRequest));
        var responseType = ClientRuntimeJsonContext.Default.GetTypeInfo(typeof(TResponse));
        if (requestType is null || responseType is null)
            throw new InvalidOperationException($"Unsupported Client Runtime transport type: {typeof(TRequest).Name}.");

        var json = JsonSerializer.Serialize(request, requestType);
        using var response = await httpClient.PostAsync(route,
            new StringContent(json, Encoding.UTF8, "application/json"), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();
        return (TResponse)(JsonSerializer.Deserialize(body, responseType)
            ?? throw new InvalidOperationException("Client Runtime returned an empty response."));
    }

    public async IAsyncEnumerable<ClientRuntimeEvent> Subscribe(
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

            var message = JsonSerializer.Deserialize(line["data: ".Length..], ConversationRelayJsonContext.Default.ClientRuntimeEvent)
                ?? throw new InvalidOperationException("Client Runtime sent an invalid event.");
            yield return message;
        }
    }

    private static string RouteFor<TRequest>(TRequest request) => request switch
    {
        SessionSetupRequest => "transport/session/setup",
        ProjectDraftRequest => "transport/project/draft",
        ProjectCreateRequest => "transport/project/create",
        ProjectOpenRequest => "transport/project/open",
        OpenProjectMissionControlRequest => "transport/project/mission-control/open",
        SubmitProjectMissionControlTurnRequest => "transport/project/mission-control/submit",
        GetProjectMissionsRequest => "transport/project/missions",
        SelectProjectMissionRequest => "transport/project/mission/select",
        StartProjectMissionRunRequest => "transport/project/mission/run",
        GetProjectWorkbenchRequest => "transport/project/workbench",
        OpenProjectDocumentRequest => "transport/project/document",
        CapabilityDispatchRequest => "transport/capability/dispatch",
        PromptRequest => "transport/prompt",
        ConfirmationResponseRequest => "transport/confirmation/respond",
        _ => throw new InvalidOperationException($"Unsupported Client Runtime request: {typeof(TRequest).Name}."),
    };

    public void Dispose()
    {
        if (ownsHttpClient)
            httpClient.Dispose();
    }
}
