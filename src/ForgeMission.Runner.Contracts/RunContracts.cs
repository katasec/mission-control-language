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
    // Extra pipeline vars beyond the mission's first parameter (e.g. `today`). The runner adds
    // filesystem vars (source_pdf/work_dir) itself when an Input artifact is staged — the
    // orchestrator never names a /work path (38.9 D5, ownership split).
    IReadOnlyDictionary<string, string>? Vars,
    // Per-run trust policy hook (39.1: always RunPolicy.Trusted for built-ins).
    string Policy,
    // Optional file-in (38.9): inline bytes the runner materializes to a per-run work dir and
    // exposes to the mission as `source_pdf` (+ `work_dir`). Null for text-only runs.
    RunArtifact? Input = null);

/// <summary>A binary artifact carried inline (base64) over the runner wire (38.9). Deliberately
/// PDF-agnostic — just a named blob. Kept small (a few-MB PDF is fine inline); blob-by-reference is
/// the deferred alternative if size hurts (spec §7). Bytes never touch the orchestrator's DB.</summary>
public sealed record RunArtifact(
    string FileName,
    string ContentType,
    string Base64);

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
    RunUsage                   Usage,
    // Optional file-out (38.9): the produced file the runner collected from the work dir, inline
    // base64. Its FileName is the mission's display name (e.g. "Family Halaqa 04.07.2026.pdf") when a
    // producing step emitted `output_name`; else the on-disk name. Null when the run produced no file.
    RunArtifact?               Output = null);

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

/// <summary>An available mission the runner can execute — returned by <c>GET /missions</c> so the
/// orchestrator can bind only the handles whose mission is actually loadable (e.g. a provider whose
/// key is set on the runner).</summary>
public sealed record MissionInfo(string MissionRef, string Description);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RunRequest))]
[JsonSerializable(typeof(RunResponse))]
[JsonSerializable(typeof(RunArtifact))]
[JsonSerializable(typeof(IReadOnlyList<MissionInfo>))]
[JsonSerializable(typeof(MissionInfo))]
public partial class RunContractsContext : JsonSerializerContext { }
