namespace ForgeMission.ConversationHost.Persistence;

/// <summary>
/// Bound from the "ConversationStorage" configuration section (the already-locked
/// <c>ConversationStorage__*</c> Container Apps env var contract — see
/// docs/design/durable-conversations.md). A plain settings POCO with public get/set properties so
/// <c>IConfiguration.Get&lt;T&gt;()</c> binds it directly; the composition root (Program.cs) owns
/// credential selection and client construction, not this type.
/// </summary>
public sealed class ConversationStorageOptions
{
    /// <summary>Kind/Azurite only — a plain Storage account connection string.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Production managed-identity path.</summary>
    public string? TableEndpoint { get; set; }

    /// <summary>Production managed-identity path.</summary>
    public string? BlobEndpoint { get; set; }

    public string EventTableName { get; set; } = "forgeconversationevents";

    public string ArtifactContainerName { get; set; } = "forgeconversationartifacts";

    public string OrleansClusterId { get; set; } = "forge-conversation-dev";

    public string OrleansServiceId { get; set; } = "forge-conversation";

    /// <summary>
    /// Selects exactly one credential path: a non-empty <see cref="ConnectionString"/>, otherwise
    /// both endpoints (for <c>DefaultAzureCredential</c>). Throws — never silently falls back —
    /// when the selected path is incomplete, so the Host fails at startup rather than at first use.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EventTableName))
            throw new InvalidOperationException("ConversationStorage:EventTableName must be set.");
        if (string.IsNullOrWhiteSpace(ArtifactContainerName))
            throw new InvalidOperationException("ConversationStorage:ArtifactContainerName must be set.");
        if (string.IsNullOrWhiteSpace(OrleansClusterId))
            throw new InvalidOperationException("ConversationStorage:OrleansClusterId must be set.");
        if (string.IsNullOrWhiteSpace(OrleansServiceId))
            throw new InvalidOperationException("ConversationStorage:OrleansServiceId must be set.");

        if (!string.IsNullOrWhiteSpace(ConnectionString))
            return; // Kind/Azurite path — endpoints are not consulted.

        if (string.IsNullOrWhiteSpace(TableEndpoint) || string.IsNullOrWhiteSpace(BlobEndpoint))
            throw new InvalidOperationException(
                "ConversationStorage requires either a non-empty ConnectionString, or both " +
                "TableEndpoint and BlobEndpoint set together for the DefaultAzureCredential path.");
    }
}
