using ForgeMission.Desktop.Contracts;

namespace ForgeMission.Desktop.Host;

internal sealed class MauiDesktopHost : IDesktopHost
{
    internal const string RetryRequestUrl = "forge-retry://request";

    private readonly WebView _webView;
    private Action? _onRetryRequested;
    private Action? _pendingContent;
    private bool _webViewLoaded;

    public MauiDesktopHost(WebView webView)
    {
        _webView = webView;
        _webView.Navigating += OnNavigating;
        _webView.Loaded += OnWebViewLoaded;
    }

    public void ShowLocalContent(string html) => SetContent(() =>
        _webView.Source = new HtmlWebViewSource { Html = html });

    public void Navigate(string url) => SetContent(() =>
        _webView.Source = new UrlWebViewSource { Url = url });

    public void RegisterRetryRequestedHandler(Action onRetryRequested) => _onRetryRequested = onRetryRequested;

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!string.Equals(e.Url, RetryRequestUrl, StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;
        _onRetryRequested?.Invoke();
    }

    private void OnWebViewLoaded(object? sender, EventArgs e)
    {
        _webViewLoaded = true;
        ApplyPendingContent();
    }

    private void SetContent(Action content) => Dispatch(() =>
    {
        _pendingContent = content;
        ApplyPendingContent();
    });

    private void ApplyPendingContent()
    {
        if (!_webViewLoaded || _pendingContent is null)
            return;

        var content = _pendingContent;
        _pendingContent = null;
        content();
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
