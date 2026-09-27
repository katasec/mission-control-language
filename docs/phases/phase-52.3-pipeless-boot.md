# Phase 52.3 — Pipeless Boot

> **Status: design (2026-09-28). Not build-ready** — see [Open questions](#open-questions).
> Starts after [52.2](phase-52.2-trim-orchestration.md). Hub: [Phase 52](phase-52-desktop-simplification.md).

**Goal:** replace, not remove, the Supervisor's function. The MAUI app becomes the entry point and
performs startup on a background task. The Supervisor process, the pipe protocol, and the
Orchestration project are deleted.

## Why

The Supervisor/pipe split was introduced to avoid startup freezes and locking. Freezes come from
blocking the UI thread, which async startup fixes inside one process.

## Locked design

| Area | Decision |
|---|---|
| Entry point | `ForgeMission.Desktop.Host` (MAUI) is the launched artifact. |
| UI thread | Shows the local Booting content immediately; does no I/O. |
| Background startup | 1. `GET {missionUrl}/health`. 2. `GET {conversationUrl}/health`. 3. Start Application Host; wait for its ready marker. 4. Marshal back to the UI thread and navigate. Both dependencies are always checked, each at its own configured URL, local or remote. |
| Failure | Any step's failure or timeout shows the existing Failed content with Retry. Retry reruns background startup as a method call. |
| Cancellation | Window close cancels startup and stops the Application Host. |
| Orphan cleanup | Application Host exits when its parent process dies (parent-owned stdin closes). |
| Absorbed code | URL resolution and the health probe move from Orchestration into Desktop.Host. |
| Deleted | `ForgeMission.Desktop` (Supervisor), `ForgeMission.Desktop.Contracts`, `ForgeMission.Orchestration`. |
| Unchanged | Application Host, Transport, Presentation, the Booting/Failed visuals. |

## Gates

| Gate | Result |
|---|---|
| Architecture / security | N/A — local process topology only; no entry point, store, or credential changes. |
| Engineering philosophy | PASS — removes one process, one protocol, and two projects; one owner for startup. Accepted trade-off: a native WebView crash takes down the app; the Application Host still exits with it. |
| Desktop quality / visual | Booting and Failed content are unchanged; read [Desktop Interaction Principles](../design/desktop-interaction-principles.md) before implementation. |
| Default path (new) | Launched artifact becomes the MAUI app bundle, zero arguments, no overrides. Action: launch → Booting → application; send a conversation message and receive a streamed reply. Update [Default-Path Acceptance](../design/default-path-acceptance.md) and the canonical build workflow. |

## Done when

1. Launching the published MAUI app reaches the application with both health checks observed.
2. Stopping either runtime shows Failed; restoring it and pressing Retry reaches the application.
3. Force-quitting the app leaves no Application Host process.
4. The three projects are deleted; build and tests pass; canonical workflow produces the bundle.

## Open questions

1. **Parent-death signal on Windows.** Confirm stdin-close works for the Application Host on both
   macOS and Windows, or name the Windows equivalent.
2. **Mission `/health` through ForgeAPI.** ForgeAPI exposes `/health`; confirm it represents
   mission-run readiness, or name the correct endpoint.
3. **Bundle layout.** Name the published artifact and update `Publish-Desktop.ps1` and the
   workflow accordingly.
