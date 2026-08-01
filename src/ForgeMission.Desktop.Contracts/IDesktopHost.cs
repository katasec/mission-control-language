namespace ForgeMission.Desktop.Contracts;

// The seam ForgeMission.Desktop's composition root programs against instead of Photino directly —
// same pattern as Model/Storage/Transport/Capability Providers elsewhere in Forge. Lives in its own
// project (no dependency on Photino.NET, ForgeMission.Desktop, or anything else) so a future host
// implementation never needs to reference today's implementation just to see the contract. Program
// .cs's Client Runtime orchestration (spawn, wait for ready URL, signal-driven teardown) depends
// only on this interface; the composition root's single `new PhotinoDesktopHost()` line is the only
// place a concrete host is named. See
// docs/design/forge-architecture.md#desktop-host-abstraction-idesktophost.
public interface IDesktopHost
{
    void Load(string url);

    // Handler returns true to veto the close (keep the window open), false to let it proceed —
    // matches Photino.NET's own RegisterWindowClosingHandler contract.
    void RegisterClosingHandler(Func<bool> onClosing);

    // Blocks the calling thread until the window closes.
    void Run();
}
