namespace ForgeMission.Desktop.Contracts;

// The seam ForgeMission.Desktop.Host — and only that process — programs against instead of a
// concrete native host. Same pattern as Model/Storage/Transport/Capability Providers elsewhere in
// Forge. Lives in its own dependency-free project so a future host implementation never needs to
// reference today's implementation just to see the contract.
//
// Scope is deliberately local window/WebView work: show Host-owned markup, show a ready URL, hear
// the one local Retry click, run the native loop. It owns no process, credential, runtime, cleanup,
// or general-purpose scheduler, and it has no close veto — the Desktop Supervisor observes the Host
// process exiting and owns every cleanup path. See
// docs/design/forge-architecture.md#desktop-host-abstraction-idesktophost.
public interface IDesktopHost
{
    // Host-owned local content (Booting/Failed); never requires a Client Runtime URL.
    void ShowLocalContent(string html);

    // The ready Client Runtime URL, supplied by the Supervisor's Navigate command.
    void Navigate(string url);

    // The single local event the Host understands: the user clicked Retry on the failure content.
    // Registered before Run; translated by the Host into the locked RetryRequested pipe event.
    void RegisterRetryRequestedHandler(Action onRetryRequested);

    // Blocks the calling thread — the Host's main thread — until the window closes.
    void Run();
}
