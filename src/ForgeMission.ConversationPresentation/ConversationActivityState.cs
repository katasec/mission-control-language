namespace ForgeMission.ConversationPresentation;

/// <summary>
/// One in-transcript activity state, mapped by each surface from facts it already has.
/// </summary>
/// <param name="Actor">Who is working — a room handle, mission name, or participant label.</param>
/// <param name="Kind">Which of the three fixed states to show.</param>
/// <param name="Detail">
/// An existing progress/tool label to show instead of the default phrase for <paramref name="Kind"/>
/// (for example Rooms' step label, or Desktop's running tool name). Null means "use the default".
/// </param>
public sealed record ConversationActivityState(
    string Actor,
    ConversationActivityKind Kind,
    string? Detail);
