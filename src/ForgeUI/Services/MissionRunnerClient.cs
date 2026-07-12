using System.Net.Http.Json;
using System.Text.Json;
using ForgeMission.Runner.Contracts;

namespace ForgeUI.Services;

/// <summary>
/// The orchestrator's thin client to the stateless mission runner (Phase 39.1). Replaces the old
/// in-process <c>new MissionService(registry).RunAsync(...)</c> call: the run now happens in the
/// containerised runner over HTTP (synchronous — the orchestrator awaits result + trace + cost, then
/// broadcasts). Identity, DB, broadcast, persistence and the ledger stay here; the runner is pure
/// compute. Configured as a typed <see cref="HttpClient"/> pointed at <c>RunnerBaseUrl</c>.
/// </summary>
public sealed class MissionRunnerClient(HttpClient http)
{
    /// <summary>Run a mission by its registry ref and return the result + trace + cost signals.</summary>
    public async Task<RunResponse> RunAsync(
        string missionRef, string goal, string policy, CancellationToken ct = default)
    {
        var request  = new RunRequest(missionRef, goal, Vars: null, policy);
        var response = await http.PostAsJsonAsync(
            "/run", request, RunContractsContext.Default.RunRequest, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
                   RunContractsContext.Default.RunResponse, ct)
               ?? throw new InvalidOperationException("Runner returned an empty run response.");
    }

    /// <summary>
    /// Streaming run (Phase 41.7): consume the runner's NDJSON <c>/run/stream</c>, invoking
    /// <paramref name="onProgress"/> as each step begins, and return the terminal result. Reading with
    /// <see cref="HttpCompletionOption.ResponseHeadersRead"/> keeps the body a live stream (the socket
    /// stays fed, so the run can't die on an idle timeout). A terminal <c>error</c> event surfaces as an
    /// exception — the same failure path as the buffered call.
    /// </summary>
    public async Task<RunResponse> RunStreamAsync(
        string missionRef, string goal, string policy,
        Func<RunProgress, Task> onProgress, CancellationToken ct = default)
    {
        var request = new RunRequest(missionRef, goal, Vars: null, policy);
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, "/run/stream")
        {
            Content = JsonContent.Create(request, RunContractsContext.Default.RunRequest),
        };

        using var response = await http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        RunResponse? result = null;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0) continue;
            var evt = JsonSerializer.Deserialize(line, RunContractsContext.Default.RunStreamEvent);
            switch (evt?.Type)
            {
                case "progress" when evt.Progress is not null:
                    await onProgress(evt.Progress);
                    break;
                case "result":
                    result = evt.Result;
                    break;
                case "error":
                    throw new InvalidOperationException(evt.Error ?? "The runner reported an error.");
                case "heartbeat":
                    break; // keep-alive only — nothing to relay
            }
        }

        return result ?? throw new InvalidOperationException("Runner stream ended without a result.");
    }

    /// <summary>Missions the runner can actually execute (provider key present) — used at boot to
    /// bind only the handles whose mission is loadable.</summary>
    public async Task<IReadOnlyList<MissionInfo>> ListMissionsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync(
               "/missions", RunContractsContext.Default.IReadOnlyListMissionInfo, ct)
           ?? [];
}
