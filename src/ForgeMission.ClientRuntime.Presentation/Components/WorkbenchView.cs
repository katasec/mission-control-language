namespace ForgeMission.ClientRuntime.Presentation.Components;

/// <summary>
/// Which document the workbench is showing (43.20 task 3). It is presentation state and nothing
/// else: changing it changes the rendered view only — never the Project, the Client Runtime
/// session, the Mission Control conversation, or the event subscription, all of which outlive
/// every switch.
/// </summary>
public enum WorkbenchView
{
    Explorer,

    /// <summary>The view a Project opens on.</summary>
    MissionControl,

    Settings,

    /// <summary>One entry opened from the Explorer. Reached only by selecting an entry, never from
    /// the rail, which is why it is not a rail destination.</summary>
    Document,
}
