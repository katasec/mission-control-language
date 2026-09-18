namespace ForgeMission.Desktop.Host;

public sealed class App(DesktopHostController controller) : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var page = new MainPage();

        // A WebView cannot create its native WebView2 controller until MAUI has mounted the page.
        // Start the pipe protocol once the page is mounted, rather than queueing content before
        // the native handler exists. Detaching makes this an exactly-once startup action.
        void StartController(object? sender, EventArgs args)
        {
            page.Loaded -= StartController;
            controller.Start(page.Host);
        }

        page.Loaded += StartController;
        return new Window(page) { Title = "Forge" };
    }
}
