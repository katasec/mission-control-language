using System.Net;
using ForgeMission.Orchestration;

namespace ForgeMission.Tests.Orchestration;

// A scripted /health endpoint standing in for ConversationHost: the durable-runtime bootstrap tests
// need no Kind cluster, cloud credential, or provider — only an answer to one GET.
internal sealed class StubHealthEndpoint(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHealthEndpoint Always(HttpStatusCode status) =>
        new(_ => new HttpResponseMessage(status));

    // Answers 503 until the given call number, then 200 — how a tunnel that has just started looks.
    public static StubHealthEndpoint HealthyFromCall(int call) =>
        new(n => new HttpResponseMessage(n >= call ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable));

    public static StubHealthEndpoint Unreachable() =>
        new(_ => throw new HttpRequestException("Connection refused."));

    public ConversationRuntimeReadinessProbe NewProbe() => new(new HttpClient(this));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(respond(Requests.Count));
    }
}

// Stands in for the local tunnel so ownership and disposal are provable without kubectl.
internal sealed class FakeTunnel : IAsyncDisposable
{
    public int DisposeCount { get; private set; }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
