using System.Text.Json;

namespace ForgeMission.Core.Runtime;

/// <summary>
/// Awaited, structured pipeline lifecycle facts (Phase 43.16 Task 3). Execution facts only — Core
/// neither persists nor references Conversation contracts, Orleans, Azure, Service Bus, HTTP,
/// Client Runtime, or Presentation. <see cref="MissionPath"/> is a fresh array created by the
/// runner, root mission first, ending with <see cref="MissionName"/> — it is how a nested Janus
/// sub-mission (e.g. <c>[Janus, Negotiate]</c>) is distinguished from its parent in the trace.
/// <see cref="ExpertName"/> is the MCL expert name — the future Janus participant-mapping input;
/// Core does not depend on <c>ConversationParticipant</c>.
/// </summary>
public abstract record PipelineTraceEvent(
    string MissionName,
    IReadOnlyList<string> MissionPath,
    string ExpertName,
    string ExpertKind,
    int Attempt);

/// <summary>A step is about to invoke its expert. <see cref="PipelineTraceEvent.Attempt"/> is the
/// enclosing mission's current loop attempt.</summary>
public sealed record PipelineStepStarted(
    string MissionName,
    IReadOnlyList<string> MissionPath,
    string ExpertName,
    string ExpertKind,
    int Attempt)
    : PipelineTraceEvent(MissionName, MissionPath, ExpertName, ExpertKind, Attempt);

/// <summary>A non-empty raw streaming chunk from the writer-driven streaming path, in write order.
/// Transient: not resumable, and never becomes a <c>ConversationEvent</c> in this task.</summary>
public sealed record PipelineStepDelta(
    string MissionName,
    IReadOnlyList<string> MissionPath,
    string ExpertName,
    string ExpertKind,
    int Attempt,
    string Text)
    : PipelineTraceEvent(MissionName, MissionPath, ExpertName, ExpertKind, Attempt);

/// <summary>A step finished — pass or fail. Emitted after the output/history context update and
/// before the runner decides whether the envelope fails the mission.</summary>
public sealed record PipelineStepCompleted(
    string MissionName,
    IReadOnlyList<string> MissionPath,
    string ExpertName,
    string ExpertKind,
    int Attempt,
    StepEnvelope Envelope)
    : PipelineTraceEvent(MissionName, MissionPath, ExpertName, ExpertKind, Attempt);

/// <summary>The agent expert's non-empty set of client tool calls that makes
/// <c>PipelineRunner</c> return early. Emitted once, after that step's
/// <see cref="PipelineStepCompleted"/> fact.</summary>
public sealed record PipelineToolRequested(
    string MissionName,
    IReadOnlyList<string> MissionPath,
    string ExpertName,
    string ExpertKind,
    int Attempt,
    IReadOnlyList<PipelineToolCall> Calls)
    : PipelineTraceEvent(MissionName, MissionPath, ExpertName, ExpertKind, Attempt);

/// <summary>Closed tool-call shape — carries no provider SDK object.</summary>
public sealed record PipelineToolCall(string CallId, string Name, JsonElement Arguments);
