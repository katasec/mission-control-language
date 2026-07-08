using System.Net.Http.Json;
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

    /// <summary>Missions the runner can actually execute (provider key present) — used at boot to
    /// bind only the handles whose mission is loadable.</summary>
    public async Task<IReadOnlyList<MissionInfo>> ListMissionsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync(
               "/missions", RunContractsContext.Default.IReadOnlyListMissionInfo, ct)
           ?? [];
}
