using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ForgeMission.ClientRuntime.Transport;

public sealed class HttpClientRuntimeChannel(HttpClient httpClient) : IClientRuntimeChannel
{
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
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            var message = JsonSerializer.Deserialize(line["data: ".Length..], ClientRuntimeJsonContext.Default.ClientRuntimeEvent)
                ?? throw new InvalidOperationException("Client Runtime sent an invalid event.");
            yield return message;
        }
    }

    private static string RouteFor<TRequest>(TRequest request) => request switch
    {
        SessionSetupRequest => "transport/session/setup",
        CapabilityDispatchRequest => "transport/capability/dispatch",
        PromptRequest => "transport/prompt",
        ConfirmationResponseRequest => "transport/confirmation/respond",
        _ => throw new InvalidOperationException($"Unsupported Client Runtime request: {typeof(TRequest).Name}."),
    };
}
