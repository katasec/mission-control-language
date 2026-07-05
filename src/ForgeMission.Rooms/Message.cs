namespace ForgeMission.Rooms;

/// <summary>
/// Append-only: rows are INSERTed and never UPDATEd. Edits/reactions arrive as new
/// rows referencing the original via <see cref="ReplyTo"/>-style links. Reads are
/// always (RoomId, CreatedAt) paginated — no code path loads a whole room.
/// </summary>
public sealed class Message
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }

    /// <summary>Attribution: every message has a sender (a Member).</summary>
    public Guid SenderId { get; set; }
    public MemberKind SenderKind { get; set; }

    /// <summary>
    /// Payload-shape discriminator mirrored from <see cref="MessagePayload.Kind"/>
    /// (human | agent), kept as a column so future queries can filter without
    /// touching jsonb.
    /// </summary>
    public MessageKind Kind { get; set; }

    public Guid? ReplyTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fluid content — jsonb. See <see cref="MessagePayload"/>.</summary>
    public MessagePayload Payload { get; set; } = new();
}
