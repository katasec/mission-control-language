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
// command-pipe reader — wait until the initial Booting document has run in the WebView and are then
// marshalled onto that thread through Photino's own documented Invoke(Action). Native-window
// creation alone is not sufficient: on Windows ARM64 Photino can create the window before the
// WebView accepts Invoke/LoadRawString work.
public sealed class PhotinoDesktopHost : IDesktopHost
{
    // The Retry button in the Host's failure content posts exactly this string via Photino's
    // injected window.external.sendMessage. Nothing else is treated as a message.
    public const string RetryMessage = "retry";

    // The Host's initial local document posts this exact literal when its inline script reaches the
    // native message bridge. It proves the WebView is ready for off-owner-thread work; it is an
    // adapter-internal bootstrap signal, not a Supervisor event.
    public const string WebViewReadyMessage = "webview-ready";

    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly ManualResetEventSlim _webViewReady = new(initialState: false);
    private readonly PhotinoWindow _window;

    public PhotinoDesktopHost() =>
        _window = new PhotinoWindow()
            .SetTitle("Forge")
            .SetUseOsDefaultSize(true)
            .Center();

    public void ShowLocalContent(string html) => Apply(window => window.LoadRawString(html));

    public void Navigate(string url) => Apply(window => window.Load(url));

    public void RegisterRetryRequestedHandler(Action onRetryRequested) =>
        _window.RegisterWebMessageReceivedHandler((_, message) =>
            DispatchWebMessage(message, onRetryRequested));

    public void Run() => _window.WaitForClose();

    // On the owner thread this is either pre-Run start content (Photino's documented "configure the
    // window, then WaitForClose" pattern) or a call already on the native loop's thread; both apply
    // directly. Off the owner thread, block until the Booting document proves the WebView is ready,
    // then hand the work to Photino's Invoke. The wait is unbounded on purpose: the only caller is
    // the Host's background command reader, and if the initial WebView never comes up the Host
    // process is going away regardless.
    private void Apply(Action<PhotinoWindow> operation)
    {
        if (Environment.CurrentManagedThreadId == _ownerThreadId)
        {
            operation(_window);
            return;
        }

        _webViewReady.Wait();
        Console.Error.WriteLine("Photino WebView ready; applying queued Host command.");
        _window.Invoke(() => operation(_window));
    }

    // Both messages remain literal and adapter-owned. Bootstrap readiness never crosses the Host
    // process boundary; Retry remains the one local event the Host translates for the Supervisor.
    private void DispatchWebMessage(string message, Action onRetryRequested)
    {
        if (message == WebViewReadyMessage)
        {
            Console.Error.WriteLine("Photino WebView ready message received.");
            _webViewReady.Set();
            return;
        }

        if (message == RetryMessage)
            onRetryRequested();
    }
}
