namespace ForgeMission.Rooms;

/// <summary>
/// A participant that can belong to rooms and send messages.
/// Agents are members, not tools — the two kinds share one identity space
/// so a message sender is always a Member regardless of kind.
/// </summary>
public sealed class Member
{
    public Guid Id { get; set; }
    public MemberKind Kind { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
