namespace ForgeMission.Rooms;

/// <summary>
/// Fluid message payload stored as jsonb. In 38.1 this is human text only; agent
/// messages (38.2/38.3) extend it with trace, trust signal, and artifact
/// *references* (bytes go to blob storage, never jsonb). Large binary never
/// belongs here.
/// </summary>
public sealed class MessagePayload
{
    /// <summary>Payload schema version — bump when the shape changes.</summary>
    public int V { get; set; } = 1;

    /// <summary>Discriminator: "human" | "agent" (mirrors <see cref="Message.Kind"/>).</summary>
    public string Kind { get; set; } = MessagePayloadKinds.Human;

    public string? Text { get; set; }
}

public static class MessagePayloadKinds
{
    public const string Human = "human";
    public const string Agent = "agent";
}
