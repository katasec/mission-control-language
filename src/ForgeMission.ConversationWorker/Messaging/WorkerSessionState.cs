using System.Text.Json.Serialization;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>Durable recovery state only.  It records the execution boundary and one outbox fact,
/// never a provider transcript, credential, project root, or capability handle.</summary>
public enum WorkerSessionPhase
{
    ExecutingProvider,
    WaitingForHands,
    Terminal,
}

public sealed record WorkerSessionState(
    Guid CurrentCommandId,
    Guid? RunId,
    WorkerSessionPhase Phase,
    int NextProgressOrdinal,
    string? PendingProgressJson,
    string? PackageHash = null,
    string? OpaqueContinuation = null);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(WorkerSessionState))]
internal partial class WorkerSessionStateJsonContext : JsonSerializerContext;
