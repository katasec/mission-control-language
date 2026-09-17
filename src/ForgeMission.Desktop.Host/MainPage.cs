namespace ForgeMission.Desktop.Host;

internal sealed class MainPage : ContentPage
{
    public MainPage()
    {
        var webView = new WebView();
        Host = new MauiDesktopHost(webView);
        Content = webView;
    }

    public MauiDesktopHost Host { get; }
}
