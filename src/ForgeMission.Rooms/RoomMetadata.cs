namespace ForgeMission.Rooms;

/// <summary>
/// Fluid room payload stored as jsonb. Read as a whole blob per room, never
/// filtered/sorted/joined on — a field that needs a cross-row query gets
/// promoted to a generated column instead (see phase-38.1 storage model).
/// </summary>
public sealed class RoomMetadata
{
    /// <summary>Payload schema version — bump when the shape changes.</summary>
    public int V { get; set; } = 1;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Avatar { get; set; }
}
