namespace ForgeMission.Api;

/// <summary>
/// Streaming reverse-proxy of the <c>/v1</c> wire to the internal runner's door (42.6 task 3). The
/// gateway is a pass-through for <b>both</b> verbs — a buffered <c>/v1/messages</c> and a streaming
/// SSE turn — because an agentic <c>forge claude</c> turn is N+1 requests (the client's tool loop
/// resumes the turn), which the runner's single-shot <c>/run</c> RPC can't carry. So we forward the
/// raw wire and never buffer: request body streams up, response body streams straight back with
/// headers flushed first (<see cref="HttpCompletionOption.ResponseHeadersRead"/>), so SSE chunks
/// reach the client as the runner emits them.
///
/// <para>Thin gateway only: no mission logic here. Auth (task 4), per-handle routing (task 5), and
/// metering (task 6) wrap this — the relay itself stays dumb.</para>
/// </summary>
public static class WireProxy
{
    // Request headers we must not copy upstream: Host is rewritten by the client, and the
    // hop-by-hop headers describe this connection, not the forwarded one.
    private static readonly string[] StrippedRequestHeaders =
        ["Host", "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade", "Proxy-Connection"];

    // Response headers the framework sets itself from the streamed body; copying them conflicts.
    private static readonly string[] StrippedResponseHeaders =
        ["Transfer-Encoding", "Content-Length", "Connection", "Keep-Alive"];

    /// <summary>Forward the current request to <paramref name="targetPath"/> on the runner, streaming
    /// both directions.</summary>
    public static async Task ForwardAsync(HttpContext ctx, HttpClient runner, string targetPath)
    {
        using var upstream = BuildUpstreamRequest(ctx, targetPath);
        using var response = await runner.SendAsync(
            upstream, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
        await CopyResponseAsync(ctx, response);
    }

    private static HttpRequestMessage BuildUpstreamRequest(HttpContext ctx, string targetPath)
    {
        var request = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), targetPath);

        if (HasBody(ctx.Request))
        {
            request.Content = new StreamContent(ctx.Request.Body);
            if (ctx.Request.ContentType is { Length: > 0 } contentType)
                request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        }

        foreach (var (name, values) in ctx.Request.Headers)
            if (!IsStripped(StrippedRequestHeaders, name) && !name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                request.Headers.TryAddWithoutValidation(name, (IEnumerable<string?>)values);

        return request;
    }

    private static async Task CopyResponseAsync(HttpContext ctx, HttpResponseMessage response)
    {
        ctx.Response.StatusCode = (int)response.StatusCode;

        foreach (var (name, values) in response.Headers)
            if (!IsStripped(StrippedResponseHeaders, name))
                ctx.Response.Headers[name] = values.ToArray();
        foreach (var (name, values) in response.Content.Headers)
            if (!IsStripped(StrippedResponseHeaders, name))
                ctx.Response.Headers[name] = values.ToArray();

        // Never buffer — SSE progress must reach the client as it arrives.
        ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();

        await using var upstreamBody = await response.Content.ReadAsStreamAsync(ctx.RequestAborted);
        await upstreamBody.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
    }

    private static bool HasBody(HttpRequest request) =>
        request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding");

    private static bool IsStripped(string[] set, string name) =>
        Array.Exists(set, h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
}
