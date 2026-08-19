namespace ForgeMission.Orchestration;

// The one readiness observation the Supervisor makes about a durable Conversation Runtime:
// GET {baseUrl}health. Health is what makes an endpoint safe to hand to Client Runtime; nothing
// else about it is inspected, and there is no configurable retry policy.
internal sealed class ConversationRuntimeReadinessProbe(HttpClient http)
{
    // One fixed Desktop startup budget for every endpoint: a just-started local tunnel needs a
    // moment to accept connections, and a configured endpoint must be healthy before the client
    // starts either way.
    internal static readonly TimeSpan StartupBudget = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    // A single observation: an unreachable endpoint is an answer ("not healthy"), not an error —
    // deciding what to do about it belongs to the bootstrap.
    public async Task<bool> IsHealthyAsync(string baseUrl, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync($"{baseUrl}health", ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task EnsureHealthyAsync(string baseUrl, TimeSpan budget, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + budget;
        while (true)
        {
            if (await IsHealthyAsync(baseUrl, ct))
                return;

            if (DateTime.UtcNow >= deadline)
                break;

            await Task.Delay(PollInterval, ct);
        }

        throw new InvalidOperationException(
            $"The durable Conversation Runtime at {baseUrl}health did not become healthy within "
            + $"{budget.TotalSeconds:0}s. Start the local runtime with "
            + "`make -C ../forge-infra 350-conversation-kind-up`, or set "
            + $"{ConversationRuntimeResolver.ConfigurationKey} to a healthy endpoint.");
    }
}
