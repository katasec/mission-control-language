# Phase 51 Desktop publish script — completed records

## Task 8 — First ready-to-navigate handoff

**Completed:** 2026-09-28

The published zero-argument Desktop bundle could keep its native Host on `Starting Forge` even
after the Application Host reported readiness. The failure was not a Kind-service restart,
credential, pipe, readiness-marker, or UI-thread-dispatch problem: the Host received `Navigate`,
but its initial `HtmlWebViewSource` and ready `UrlWebViewSource` overlapped. WebKit discarded the
ready request and completed the stale local Booting document.

`MauiDesktopHost` now owns that ordering boundary. It queues Host-issued source assignments FIFO,
marks an assignment in flight before setting `WebView.Source`, and advances only after
`WebView.Navigated` reports completion. The fixed Supervisor-to-Host pipe contract, normal
credential route, loopback-only Application Host, and retry action are unchanged.

The Mac Catalyst bundle declares only `NSAppTransportSecurity.NSAllowsLocalNetworking=true`; its
parsed source guard rejects `NSAllowsArbitraryLoads` and
`NSAllowsArbitraryLoadsInWebContent`.

**Verification:**

- Focused architecture, lifecycle, and Application Host process tests: 17/17 passed.
- `git diff --check` passed.
- A clean Host release-artifact build followed by `scripts/Publish-Desktop.ps1` produced the fresh
  bundle; its generated `Info.plist` contains only the local-network ATS exception.
- The exact zero-argument executable at `dist/forge-desktop/ForgeMission.Desktop` launched its
  Application Host at a dynamic `127.0.0.1` port. `/ready` returned the corresponding URL and the
  WebContent process held established TCP connections to that port.
- The default Kind conversation health route `http://127.0.0.1:18080/health` returned 200. The
  operator accepted the fresh launch.
