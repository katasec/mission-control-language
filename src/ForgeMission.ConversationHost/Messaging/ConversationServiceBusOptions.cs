namespace ForgeMission.ConversationHost.Messaging;

/// <summary>
/// Bound from the "ConversationServiceBus" configuration section (the locked
/// <c>ConversationServiceBus__*</c> env var contract). Host constructs only the
/// mission-command Send and conversation-progress Listen directions from this; Worker has its own
/// identical copy of this shape (Worker cannot reference Host) and constructs only the opposite
/// two directions. A plain settings POCO — <see cref="Validate"/> is the composition root's own
/// per-direction credential selection, not a shared library concern.
/// </summary>
public sealed class ConversationServiceBusOptions
{
    public string? FullyQualifiedNamespace { get; set; }

    public string MissionCommandQueueName { get; set; } = "mission-command";
    public string ProgressQueueName { get; set; } = "conversation-progress";

    public string? MissionCommandSendConnectionString { get; set; }
    public string? MissionCommandListenConnectionString { get; set; }
    public string? ProgressSendConnectionString { get; set; }
    public string? ProgressListenConnectionString { get; set; }

    /// <summary>For each required direction, selects the scoped connection string or
    /// <see cref="FullyQualifiedNamespace"/> with <c>DefaultAzureCredential</c> — throws if a
    /// required direction has neither or both, so the Host fails at startup rather than at first
    /// use.</summary>
    public void ValidateDirection(string? scopedConnectionString, string directionName)
    {
        var hasScoped = !string.IsNullOrWhiteSpace(scopedConnectionString);
        var hasNamespace = !string.IsNullOrWhiteSpace(FullyQualifiedNamespace);

        if (hasScoped == hasNamespace)
            throw new InvalidOperationException(
                $"ConversationServiceBus:{directionName} requires exactly one of a scoped connection " +
                "string or FullyQualifiedNamespace (for DefaultAzureCredential) — never both, never neither.");
    }
}
