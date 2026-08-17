# Phase 43.17 — Responsive Desktop: completed work

> Verified, closed-out detail for [43.17 — Responsive Desktop lifecycle and UI](phase-43.17-responsive-desktop.md).
> The active spoke keeps one-line statuses and points here for the build narrative.

## Task 2 — Non-blocking supervised lifecycle (done 2026-08-17)

The Supervisor/Host process split from Task 1's locked design is implemented. `ForgeMission.Desktop`
is a windowless supervisor; `ForgeMission.Desktop.Host` is the disposable native host; the fixed
inherited-pipe protocol is the only control path between them.

### What was built

| Area | Detail |
|---|---|
| Contracts | `DesktopHostProtocol.cs` adds the four locked types plus one owner for the `[kind][len:i32 LE][payload]` framing, so neither side can drift on the wire format. `IDesktopHost` becomes `ShowLocalContent` / `Navigate` / `RegisterRetryRequestedHandler` / `Run`; the close-veto member is gone. |
| Host process | New `ForgeMission.Desktop.Host` executable: owns its Booting/Failed markup, the command-pipe reader thread, and the Retry web message. No `Core`, `Orchestration`, `ClientRuntime`, credential, or capability dependency. Exits when its window closes or when the Supervisor's pipe closes. |
| Photino adapter | Rewritten against the new contract. Owner-thread calls apply directly (the proven "configure, then `WaitForClose`" path); off-thread calls block on a window-created gate and then marshal through `PhotinoWindow.Invoke`. That gate is the pending-command safeguard — a command arriving mid-creation is applied when the window exists rather than dropped. |
| Supervisor | `DesktopLifecycle` (states + exactly-once cleanup), `HostProcess` (pipes, child, exit signal), `ClientRuntimeProcess` (async start + ready URL), `DesktopBoot` (credential, Mission Runtime resolution, runtime set), `ProcessTermination`, `SiblingExecutable`. `Program.cs` is a named entry class that parses args, installs signal handlers, and runs the lifecycle. |

### Decisions made during the build

- **The Supervisor is fully asynchronous now.** The old "no `await` anywhere" rule existed only
  because that process ran AppKit; it doesn't any more. The Host inherits the rule instead — its
  `Program.cs` is synchronous and all asynchronous work is on the command-reader thread.
- **A named entry class instead of top-level statements.** The test project compile-references the
  Supervisor, and a generated global `Program` type collided with `ForgeMission.Parser.Program` in
  existing tests — the same collision the CLI's project reference comment warns about.
- **The Host is killed, not asked to stop.** Measured live: the native host does not act on SIGTERM
  (its native loop never returns), so a graceful attempt cost the full 10s grace period before the
  kill that was always going to happen. A supervisor SIGTERM took 10–20s to complete. The Host owns
  no state worth draining, so cleanup kills it outright and does so *before* stopping runtimes, so
  the window goes away immediately on quit. Re-measured after the change: **1s**.
- **The native host publishes last.** Publishing several projects into one folder prunes files a
  project published before but no longer owns. The Supervisor's stale publish manifest (from when it
  still referenced Photino) deleted the Host's freshly-published `Photino.Native.dylib`, and the
  published app died with `DllNotFoundException` on first launch. `desktop-publish` now publishes the
  Host last so its native asset lands after any such pruning.
- **Cleanup awaits the boot it cancelled.** A window closed mid-boot must not leave behind a Client
  Runtime or container that finished starting a moment later, so cleanup cancels the boot and then
  awaits it before disposing whatever it produced.

### Verification

`dotnet build src/ForgeMission.slnx`: 0 warnings, 0 errors. `dotnet test src/ForgeMission.slnx`:
720 passed, 0 failed, 11 skipped (pre-existing live-LLM skips).

Boundary tests — `src/ForgeMission.Tests/Architecture/DesktopSupervisorHostBoundaryTests.cs`:
Supervisor has no Photino package and no project reference to the adapter or the Host, and no
Supervisor source file names `IDesktopHost` or Photino; Host and adapter reference no runtime,
credential, or capability project; Contracts has zero dependencies. The previous
`DesktopShellBoundaryTests` was passing vacuously — it ran `Path.GetFileNameWithoutExtension` over
`..\X\X.csproj`, and on macOS `\` is not a separator, so no reference ever matched. Separators are
now normalised before comparison.

Lifecycle tests — `src/ForgeMission.Tests/Desktop/DesktopLifecycleTests.cs`: Host starts before any
boot work and the state stays `Booting` with no command sent until runtimes are ready; `Navigate` is
sent only on `Ready`; a Host exit during boot cancels it and still stops what it started; runtimes
stop exactly once even with concurrent cleanup requests; a boot failure shows `ShowFailure` and only
a `RetryRequested` boots again; a stop signal stops runtimes and terminates the Host.

Published macOS app (`make desktop-publish`, `dist/forge-desktop`):

| Observation | Result |
|---|---|
| Booting before readiness | `MissionRuntime__Mode=docker` run, screenshot at t+2s: the window shows "Starting Forge — Preparing the mission and client runtimes…" while `docker ps` shows `forge-client-98cf293bb345 Up 2 seconds` and no Client Runtime process exists yet. |
| Navigate | Screenshot at t+14s: same window rendering the real Client Runtime UI. Host log: `LoadRawString(<!doctype html…)` then `Load(http://127.0.0.1:51140)`. |
| ShowFailure + Retry | With `ForgeMission.ClientRuntime` temporarily renamed, the window rendered "Forge could not start" with the resolver's message and a Retry button. The binary was restored and Retry clicked: host log shows a second `LoadRawString` followed by `Load(http://127.0.0.1:51253)` — the Supervisor re-booted and navigated. No native-thread misuse or crash on any of the three renders. |
| Normal window close | After that same run's window was closed: `pgrep ForgeMission` empty, no `forge-client` container, supervisor exited. |
| Host `kill -9` | Supervisor observed the child exit, stopped the Client Runtime and exited; `pgrep` empty and no container within 5s. |
| Supervisor SIGTERM | Docker-mode run: `kill -TERM` → supervisor exited in **1s**, Client Runtime gone, `forge-client-0dd980294acb` removed. |

Inherited-pipe transport was verified on macOS before building on it: a throwaway parent/child pair
exchanged a command frame and an event frame across `AnonymousPipeServerStream` handles passed as
process arguments, child exit code 0.

### Desktop Quality Gate result

| Required answer | Result |
|---|---|
| What product behaviour is required? | The window appears and is useful immediately at launch; closing it, killing it, or signalling the app leaves no runtime process or container behind. **PASS** |
| Who owns it? | The Desktop Supervisor process owns boot/stop state, credentials, runtime children and cleanup; the Host process owns only its window and local content. **PASS** |
| What has been verified about the adapter? | `Photino.NET` 4.0.16 exposes `Invoke(Action)`, `Load`, `LoadRawString`, `RegisterWindowCreatedHandler`, `RegisterWebMessageReceivedHandler`, `WaitForClose` (verified by reflecting the package). Whether `Load` before native creation defers to a start URL was treated as unknown and designed around. The Host's non-response to SIGTERM was measured, not assumed. No close veto is used. **PASS** |
| Why does the proposal preserve the replacement boundary? | Enforced by `DesktopSupervisorHostBoundaryTests`, not by convention. **PASS** |
| What proves it? | The boundary tests, the lifecycle tests, and the six published-app observations above. **PASS** |
