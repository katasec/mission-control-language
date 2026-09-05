namespace ForgeMission.ClientRuntime.Presentation.Components;

/// <summary>
/// Which document the workbench is showing (43.20 task 3). It is presentation state and nothing
/// else: changing it changes the rendered view only — never the Project, the Client Runtime
/// session, the durable Mission container, or the event subscription, all of which outlive every
/// switch.
/// </summary>
public enum WorkbenchView
{
    Explorer,

    /// <summary>The view a Project opens on: the selected mission and its one live run
    /// (43.21 task 2).</summary>
    Missions,

    Settings,

    /// <summary>One entry opened from the Explorer. Reached only by selecting an entry, never from
    /// the rail, which is why it is not a rail destination.</summary>
    Document,

    /// <summary>One run's expert trace, opened from its outcome in the Missions control thread
    /// (43.21 task 2). Like Document it is reached by selecting something, not from the rail, and
    /// the rail keeps saying Missions while it is open.</summary>
    RunTrace,
}
