# Phase 48 — MAUI Desktop Host spike

> **Status:** implementation in progress. This is a Windows-only host replacement spike; it does
> not change the Application Host, Presentation UI, supervisor, or pipe protocol.

## Why this phase exists

Forge Desktop needs a dependable Windows-native shell. The existing Photino shell has not produced
an accepted Windows default-path result, while the local `m2` reference proves a minimal MAUI
Windows wrapper can own a normal WebView. This spike tests MAUI only as that thin host; it is not a
Blazor UI migration.

## Locked design

| Area | Decision |
|---|---|
| Native executable | `ForgeMission.Desktop.Host` becomes the ordinary Windows MAUI executable. Its name stays unchanged, so the Desktop Supervisor and its child-process wiring stay unchanged. |
| Rendered content | MAUI `WebView` shows Host-owned Booting/Failed HTML first and navigates to the existing dynamic loopback URL only when the Supervisor sends `Navigate`. `ForgeMission.Application.Host` remains the sole embedded web server and continues serving `ForgeMission.Presentation` unchanged. |
| Host seam | `MauiDesktopHost` implements the existing `IDesktopHost` content/navigation/retry seam. MAUI owns its own application loop, so `IDesktopHost.Run()` is removed rather than simulated. |
| Retry | The Failed page navigates to the reserved `forge-retry://request` URL. The MAUI WebView cancels that navigation and emits the existing `RetryRequested` pipe event. No additional IPC or WebView message bridge is introduced. |
| Scope | Windows only (`net10.0-windows10.0.19041.0`, `win-arm64` on this machine). The MAUI host is managed/self-contained, not Native AOT. Existing non-Windows host support is outside this spike. |
| Installer trial | A WiX MSI packages the already-published `dist/forge-desktop` folder without changing its files. It is an installation/distribution experiment only: it neither signs the package nor establishes Smart App Control trust. |
| In-process trial | `ForgeMission.Desktop.Maui` is a separate Windows-only executable that owns the MAUI window and starts the loopback Application Host in-process. It replaces neither the accepted legacy process topology nor the pipe Host during the spike. Reversal is deleting this project and its installer selection. |

## Architecture and gates

| Gate | Disposition |
|---|---|
| Component fit | **PASS.** `ForgeMission.Desktop.Host` owns the disposable native window and fixed command application. `ForgeMission.Desktop.Contracts` retains its dependency-free local-window seam. The Supervisor, Application Host, and Presentation retain their existing ownership. |
| Security Architecture | **PASS.** Type-2 local native-adapter replacement. The new host receives only inherited pipe handles and the supervisor-owned loopback URL; it holds no identity, credential, datastore, or new public entry point. Reversal is restoring the Photino project reference and host composition. |
| Engineering Philosophy | **PASS.** One MAUI host owns UI-thread dispatch and WebView behavior. The fixed two-command/one-event protocol is unchanged; no generic IPC, configuration switch, embedded server, or lifecycle abstraction is added. |
| Desktop interaction principles / UI system | **PASS.** No ForgeUI markup, interaction, design token, or reference surface changes. The existing application URL is rendered through the native WebView; packaged default-path inspection remains required. |
| Native AOT | **Exception, bounded.** MAUI Windows runs managed CoreCLR/ReadyToRun rather than Native AOT. The AOT rule remains unchanged for the Forge CLI, Supervisor, and Application Host. This spike adds no reflection-based application code. |
| Default path | **Applies.** Publish the three sibling executables for `win-arm64` into `dist/forge-desktop`; run `ForgeMission.Desktop.exe` with zero arguments and no `FORGE_*` overrides. Passing evidence must show boot content, dynamic-loopback navigation to the existing UI, and cleanup when the window closes. On 2026-09-17, Windows Application Control blocked the unsigned published Supervisor before process launch; this is an environment-policy observation, not MAUI acceptance. |

## Tasks

| Task | Status | Done when |
|---|---|---|
| Replace the Photino implementation with the MAUI host | In progress | The existing host executable builds and runs the unchanged inherited-pipe protocol with an ordinary MAUI WebView. |
| Default-path acceptance | Blocked by WDAC | The published zero-argument Windows artifact visibly reaches the existing Forge UI and exits without orphaned children. |
