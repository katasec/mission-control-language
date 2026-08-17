namespace ForgeMission.ConversationPresentation;

/// <summary>
/// The three conversation activity states a Forge surface can already describe from facts it
/// holds. Fixed by design (43.18): a caller that wants a fourth visual is asking for a different
/// component, not another member here.
/// </summary>
public enum ConversationActivityKind
{
    /// <summary>An actor has the turn but has produced nothing yet.</summary>
    Thinking,

    /// <summary>An actor is running a tool or a named step.</summary>
    Working,

    /// <summary>An actor's response text is arriving.</summary>
    Streaming,
}
