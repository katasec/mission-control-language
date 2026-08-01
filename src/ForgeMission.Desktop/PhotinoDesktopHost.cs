using Photino.NET;

namespace ForgeMission.Desktop;

// Today's implementation of IDesktopHost. Deliberately the only file in this project allowed to
// touch Photino.NET types — everything else programs against the interface.
internal sealed class PhotinoDesktopHost : IDesktopHost
{
    private readonly PhotinoWindow _window = new PhotinoWindow()
        .SetTitle("Forge")
        .SetUseOsDefaultSize(true)
        .Center();

    public void Load(string url) => _window.Load(url);

    public void RegisterClosingHandler(Func<bool> onClosing) =>
        _window.RegisterWindowClosingHandler((_, _) => onClosing());

    public void Run() => _window.WaitForClose();
}
