namespace ForgeMission.Desktop;

// The seam the rest of this project programs against instead of Photino directly — same pattern
// as Model/Storage/Transport/Capability Providers elsewhere in Forge. Program.cs's Client Runtime
// orchestration (spawn, wait for ready URL, signal-driven teardown) is host-agnostic and never
// touches this interface's implementation; only the one composition-root line that constructs a
// concrete host would change if Photino were ever replaced. See
// docs/design/forge-architecture.md#desktop-host-abstraction-idesktophost.
internal interface IDesktopHost
{
    void Load(string url);

    // Handler returns true to veto the close (keep the window open), false to let it proceed —
    // matches Photino.NET's own RegisterWindowClosingHandler contract.
    void RegisterClosingHandler(Func<bool> onClosing);

    // Blocks the calling thread until the window closes.
    void Run();
}
