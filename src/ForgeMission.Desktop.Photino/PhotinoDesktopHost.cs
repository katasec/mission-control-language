using ForgeMission.Desktop.Contracts;
using Photino.NET;

namespace ForgeMission.Desktop.Photino;

// Today's implementation of IDesktopHost. This project is deliberately the only place Photino.NET
// types are used — ForgeMission.Desktop's composition root only ever sees IDesktopHost.
public sealed class PhotinoDesktopHost : IDesktopHost
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
