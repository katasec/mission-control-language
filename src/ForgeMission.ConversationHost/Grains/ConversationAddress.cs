namespace ForgeMission.ConversationHost.Grains;

/// <summary>
/// The server-trusted internal conversation identity a future authenticated Tier-1 adapter
/// supplies. Has no public HTTP/Contracts representation. Never crosses an Orleans grain-interface
/// boundary — it is only ever used to build a grain's string key or a Table partition key, both
/// plain Host-internal service calls, so it needs no Orleans serializer attribute.
/// </summary>
public sealed record ConversationAddress
{
    public string TenantId { get; }
    public Guid ConversationId { get; }

    public ConversationAddress(string tenantId, Guid conversationId)
    {
        if (string.IsNullOrEmpty(tenantId))
            throw new ArgumentException("TenantId must be non-empty.", nameof(tenantId));
        if (tenantId.Contains('|'))
            throw new ArgumentException("TenantId cannot contain '|'.", nameof(tenantId));

        TenantId = tenantId;
        ConversationId = conversationId;
    }

    /// <summary>Canonical grain/Table partition key: <c>v1|{TenantId}|{ConversationId:N}</c>.
    /// This is also the <c>IConversationGrain</c> string key.</summary>
    public string PartitionKey => $"v1|{TenantId}|{ConversationId:N}";

    public override string ToString() => PartitionKey;

    /// <summary>Parses a canonical <see cref="PartitionKey"/> back into its address — used by
    /// <c>ConversationGrain</c> to recover its own identity from its Orleans grain key.</summary>
    public static ConversationAddress Parse(string partitionKey)
    {
        var parts = partitionKey.Split('|', 3);
        if (parts.Length != 3 || parts[0] != "v1")
            throw new FormatException($"'{partitionKey}' is not a valid v1 ConversationAddress key.");

        return new ConversationAddress(parts[1], Guid.ParseExact(parts[2], "N"));
    }
}
