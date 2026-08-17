using ForgeMission.Desktop.Contracts;
using Photino.NET;

namespace ForgeMission.Desktop.Photino;

// Today's implementation of IDesktopHost. This project is deliberately the only place Photino.NET
// types are used; the Host composition root only ever sees IDesktopHost, and the Desktop Supervisor
// never sees either.
//
// Threading: the constructing thread owns the window. It is the Host's main thread, which is also
// the thread Photino runs its native loop on once Run() is called, and macOS AppKit requires all
// window/WebView work to happen there. Calls arriving from any other thread — in practice the Host's
// command-pipe reader — wait until the native window exists and are then marshalled onto that thread
// through Photino's own documented Invoke(Action). The wait is the pending-command safeguard: a
// Navigate or ShowFailure that arrives while the window is still being created is applied when it
// exists rather than being applied to a window that does not, or dropped.
public sealed class PhotinoDesktopHost : IDesktopHost
{
    // The Retry button in the Host's failure content posts exactly this string via Photino's
    // injected window.external.sendMessage. Nothing else is treated as a message.
    public const string RetryMessage = "retry";

    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly ManualResetEventSlim _windowCreated = new(initialState: false);
    private readonly PhotinoWindow _window;

    public PhotinoDesktopHost() =>
        _window = new PhotinoWindow()
            .SetTitle("Forge")
            .SetUseOsDefaultSize(true)
            .Center()
            .RegisterWindowCreatedHandler((_, _) => _windowCreated.Set());

    public void ShowLocalContent(string html) => Apply(window => window.LoadRawString(html));

    public void Navigate(string url) => Apply(window => window.Load(url));

    public void RegisterRetryRequestedHandler(Action onRetryRequested) =>
        _window.RegisterWebMessageReceivedHandler((_, message) =>
        {
            if (message == RetryMessage)
                onRetryRequested();
        });

    public void Run() => _window.WaitForClose();

    // On the owner thread this is either pre-Run start content (Photino's documented "configure the
    // window, then WaitForClose" pattern) or a call already on the native loop's thread; both apply
    // directly. Off the owner thread, block until the window exists, then hand the work to Photino's
    // Invoke. The wait is unbounded on purpose: the only caller is the Host's background command
    // reader, and if the window never comes up the Host process is going away regardless.
    private void Apply(Action<PhotinoWindow> operation)
    {
        if (Environment.CurrentManagedThreadId == _ownerThreadId)
        {
            operation(_window);
            return;
        }

        _windowCreated.Wait();
        _window.Invoke(() => operation(_window));
    }
}
