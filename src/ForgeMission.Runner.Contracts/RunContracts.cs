using System.Text.Json.Serialization;

namespace ForgeMission.Runner.Contracts;

/// <summary>
/// Transport contract between the orchestrator (ForgeUI) and the stateless mission runner
/// (Phase 39.1). Deliberately neutral of <c>ForgeUI.Models</c> and the engine's runtime types —
/// the wire only carries what a run needs (a mission reference + goal + vars + policy) and what
/// it produces (an answer + trace + cost signals). Identity, DB, broadcast and the ledger stay in
/// the orchestrator; the runner is pure compute.
/// </summary>
public sealed record RunRequest(
    // Registry label of a baked-in mission (e.g. "Forge", "Assistant", "Claude").
    string MissionRef,
    // The fully-assembled goal text (the orchestrator already folds in room context).
    string Goal,
    // Extra pipeline vars beyond the mission's first parameter (reserved; usually empty).
    IReadOnlyDictionary<string, string>? Vars,
    // Per-run trust policy hook (39.1: always RunPolicy.Trusted for built-ins).
    string Policy);

/// <summary>Trust policy carried per run. 39.1 sets every built-in to <see cref="Trusted"/>;
/// <see cref="Restricted"/> is the locked-down profile custom missions get in 39.5.</summary>
public static class RunPolicy
{
    public const string Trusted    = "trusted";
    public const string Restricted = "restricted";
}

/// <summary>
/// Result of a mission run. <see cref="Verified"/> is the pipeline's own pass/fail verdict; the
/// orchestrator additionally gates the user-visible green check on the agent descriptor's
/// <c>VerifiesAnswers</c> flag (a raw-model passthrough passes the pipeline but must never be
/// green-checked). Cost signals are emitted here and priced/debited by the orchestrator in 39.2.
/// </summary>
public sealed record RunResponse(
    string                     AgentText,
    bool                       Verified,
    int                        StepCount,
    int                        RetryCount,
    IReadOnlyList<RunTraceStep> Trace,
    RunUsage                   Usage);

/// <summary>One expert step in the run trace, mirrored from the engine's envelope.</summary>
public sealed record RunTraceStep(
    string  ExpertName,
    string  Status,
    string? Text,
    string? Reason,
    int     Attempt);

/// <summary>Cost signals the runner emits and the orchestrator prices (39.2). The meter is
/// <c>tokens + compute-seconds</c>; compute-seconds is wall-clock around the run. <see cref="Model"/>
/// is the mission's default provider model (the runner reports it so the orchestrator can price at
/// that model's rate); null/empty for exec-only missions with no LLM step.</summary>
public sealed record RunUsage(
    long    InputTokens,
    long    OutputTokens,
    double  ComputeSeconds,
    string? Model);

/// <summary>
/// One event on the streaming run leg (Phase 41.7). <c>POST /run/stream</c> emits these as NDJSON —
/// one JSON object per line — so progress reaches the room as it happens and continuous bytes keep the
/// runner→orchestrator connection alive (defeating idle timeouts). A run emits zero-or-more
/// <c>progress</c>/<c>heartbeat</c> events, then exactly one terminal <c>result</c> or <c>error</c>.
/// The buffered <c>POST /run</c> (returning <see cref="RunResponse"/>) stays for non-interactive callers.
/// </summary>
public sealed record RunStreamEvent(
    string        Type,               // "progress" | "heartbeat" | "result" | "error"
    RunProgress?  Progress = null,    // set when Type == "progress"
    RunResponse?  Result   = null,    // set when Type == "result"
    string?       Error    = null);   // set when Type == "error"

/// <summary>
/// A progress event, provider-agnostic (Phase 41.7). Two shapes share this record, distinguished by
/// <see cref="Kind"/>:
/// <list type="bullet">
/// <item><b>Step-start</b> — <see cref="Kind"/> is the engine expert kind (<c>llm</c>/<c>search</c>/
/// <c>json_extract</c>/…); <see cref="Detail"/>/<see cref="ResultCount"/> null.</item>
/// <item><b>Sub-search</b> (Task 2) — <see cref="Kind"/> is <c>searching_web</c>/<c>reading</c>/… from a
/// search backend's loop; <see cref="Detail"/> is the query or host, <see cref="ResultCount"/> the hit
/// count when known.</item>
/// </list>
/// The orchestrator maps <see cref="Kind"/> + detail to a human label. Carries no answer text — progress
/// is transient, not durable.
/// </summary>
public sealed record RunProgress(string ExpertName, string Kind, string? Detail = null, int? ResultCount = null);

/// <summary>An available mission the runner can execute — returned by <c>GET /missions</c> so the
/// orchestrator can bind only the handles whose mission is actually loadable (e.g. a provider whose
/// key is set on the runner).</summary>
public sealed record MissionInfo(string MissionRef, string Description);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RunRequest))]
[JsonSerializable(typeof(RunResponse))]
[JsonSerializable(typeof(RunStreamEvent))]
[JsonSerializable(typeof(IReadOnlyList<MissionInfo>))]
[JsonSerializable(typeof(MissionInfo))]
public partial class RunContractsContext : JsonSerializerContext { }
