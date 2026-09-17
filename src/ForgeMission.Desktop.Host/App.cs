namespace ForgeMission.Desktop.Host;

public sealed class App(DesktopHostController controller) : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var page = new MainPage();
        controller.Start(page.Host);
        return new Window(page) { Title = "Forge" };
    }
}
