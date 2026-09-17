using ForgeMission.Desktop.Contracts;

namespace ForgeMission.Desktop.Host;

internal sealed class MauiDesktopHost : IDesktopHost
{
    internal const string RetryRequestUrl = "forge-retry://request";

    private readonly WebView _webView;
    private Action? _onRetryRequested;

    public MauiDesktopHost(WebView webView)
    {
        _webView = webView;
        _webView.Navigating += OnNavigating;
    }

    public void ShowLocalContent(string html) => Dispatch(() =>
        _webView.Source = new HtmlWebViewSource { Html = html });

    public void Navigate(string url) => Dispatch(() =>
        _webView.Source = new UrlWebViewSource { Url = url });

    public void RegisterRetryRequestedHandler(Action onRetryRequested) => _onRetryRequested = onRetryRequested;

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!string.Equals(e.Url, RetryRequestUrl, StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;
        _onRetryRequested?.Invoke();
    }

    private static void Dispatch(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
            return;
        }

        MainThread.BeginInvokeOnMainThread(action);
    }
}
