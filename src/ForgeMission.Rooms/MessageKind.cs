namespace ForgeMission.Rooms;

/// <summary>Shape of the message payload. Matches the jsonb "kind" discriminator.</summary>
public enum MessageKind
{
    Human,
    Agent
}
