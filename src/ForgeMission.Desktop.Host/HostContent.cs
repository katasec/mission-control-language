using System.Net;
using ForgeMission.Desktop.Photino;

namespace ForgeMission.Desktop.Host;

// The Host's own local content. It renders without a Client Runtime URL, a credential, or any
// runtime being up — that is the entire point of starting the Host first. Kept as two small literal
// documents rather than an asset pipeline or template engine: this is the whole of the Host's UI.
internal static class HostContent
{
    public static string Booting { get; } = Page("""
        <div class="spinner"></div>
        <h1>Starting Forge</h1>
        <p>Preparing the mission and client runtimes&hellip;</p>
        """);

    public static string Failed(string message) => Page($"""
        <h1>Forge could not start</h1>
        <p class="detail">{WebUtility.HtmlEncode(message)}</p>
        <button onclick="window.external.sendMessage('{PhotinoDesktopHost.RetryMessage}')">Retry</button>
        """);

    private static string Page(string body) => $$"""
        <!doctype html>
        <html>
          <head>
            <meta charset="utf-8">
            <style>
              :root { color-scheme: dark; }
              body {
                margin: 0; height: 100vh; display: flex; flex-direction: column;
                align-items: center; justify-content: center; gap: 0.75rem;
                background: #14161a; color: #e6e8eb; text-align: center;
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
              }
              h1 { font-size: 1.05rem; font-weight: 600; margin: 0; }
              p { font-size: 0.85rem; color: #9aa1a9; margin: 0; max-width: 32rem; }
              p.detail { white-space: pre-wrap; }
              button {
                margin-top: 0.5rem; padding: 0.4rem 1.1rem; border: 0; border-radius: 6px;
                background: #3b6ea5; color: #fff; font-size: 0.85rem; cursor: pointer;
              }
              .spinner {
                width: 22px; height: 22px; border-radius: 50%;
                border: 2px solid #2c3138; border-top-color: #6f7c8a;
                animation: spin 0.9s linear infinite;
              }
              @keyframes spin { to { transform: rotate(360deg); } }
            </style>
          </head>
          <body>
        {{body}}
          </body>
        </html>
        """;
}
