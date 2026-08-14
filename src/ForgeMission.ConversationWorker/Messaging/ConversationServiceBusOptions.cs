namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>
/// Bound from the "ConversationServiceBus" configuration section (the locked
/// <c>ConversationServiceBus__*</c> env var contract) — an exact duplicate of Host's identical
/// type (approved: Worker cannot reference Host, and this shape is too small to justify a shared
/// project). Worker constructs only the mission-command Listen and conversation-progress Send
/// directions from this; Host constructs the opposite two.
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
    /// required direction has neither or both, so the Worker fails at startup rather than at first
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
