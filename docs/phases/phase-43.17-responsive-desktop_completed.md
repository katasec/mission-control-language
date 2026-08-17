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

## Task 3 — Session operation ownership and stale-result suppression (done 2026-08-17)

`Home.razor` now owns exactly one cancellable view operation. Replacing a workspace folder or an
attached mission cancels and awaits the previous subscription before the replacement exists, so two
subscribers can never overlap, and every long operation carries an identity a late result is checked
against before it may mutate anything.

### What was built

| Area | Detail |
|---|---|
| View operation | Four page-private fields replace the old `eventLoopCts`: `viewCts` (cancels this view's setup/prompt requests *and* its subscription), `eventLoopTask` (observed, no longer `_ = ...`), `viewGeneration` (stale-result identity), `connectionError`. `ViewOperation(Generation, Token)` is a private record struct carried by each long operation. |
| Replacement path | `BeginViewAsync()` → `StopViewAsync()` (cancel, await the loop, dispose) → bump generation → fresh CTS → clear session/turns/transcript/errors. `OnInitializedAsync`, `AddFolderAsync`, and `SelectMissionAsync` all start there; `AddFolderAsync` previously started a second loop without stopping the first. |
| Stale-result suppression | Setup and prompt results are assigned to locals, then discarded unless `IsCurrent(generation)`. Every `finally` guards `sending`/`settingUpSession` the same way, so a cancelled prompt cannot clear the replacement's state. `OperationCanceledException` is caught separately everywhere and is silent. |
| Disconnected state | `ConsumeEventsAsync` records its own failure as `connectionError` instead of faulting unobserved; the banner offers Retry. `RetryConnectionAsync` opens a new subscription on the same session, generation, and token. |
| Gap notice | A successful retry sets a persistent `.gap-notice` line ("Reconnected. Updates that arrived while disconnected are not shown."), cleared only by `BeginViewAsync`. |
| Prompt gating | `PromptsBlocked` disables the composer for DurableConversation while disconnected; Mission prompts stay enabled because their final answer arrives on the `PromptResponse`, not the stream. |

### Decisions made during the build

| Decision | Why |
|---|---|
| A normally-*ended* stream is treated as a disconnection, not a quiet finish. | The locked decision names an "unexpected subscription failure", but an SSE stream that completes without an exception leaves the same dead page with no further events. It now sets `connectionError` with "the Client Runtime event stream ended." and offers the same Retry. |
| `session` is cleared by `BeginViewAsync`, and `CanSend` requires a session. | The old code left the previous `SessionSetupResponse` in place when a replacement's setup failed, so the composer looked usable while pointing at a session whose subscription had already been cancelled. |
| Added one page-private `sessionError` banner. | `SelectMissionAsync` had no error handling at all — a failed mission switch threw out of a Blazor event handler. With the state now cleared before the request, that would have left a blank page and no message. |
| The gap notice is set when Retry is pressed, not when the first post-retry event arrives. | The gap is already real at that point; waiting for an event would leave a quiet conversation looking complete. Nothing below `IClientRuntimeChannel` signals "subscription established". |

### Verification

`dotnet build src/ForgeMission.slnx` — 0 warnings, 0 errors.

Ten bunit tests in `src/ForgeMission.Tests/Presentation/HomeSessionOperationTests.cs` drive the real
page through its real markup against a fake `IClientRuntimeChannel` that counts *concurrent*
subscriptions, so the central claim is a measured peak rather than a code-reading inference:

| Observation | Test |
|---|---|
| Four replacements (2 folders, 2 missions) start 5 subscriptions, leave 1 active, peak 1. | `RepeatedFolderAndMissionReplacement_LeavesExactlyOneSubscription` |
| One delta is applied once, not twice, after repeated replacement. | `Replacement_AwaitsTheOldSubscription_SoEachEventIsAppliedOnce` |
| A replacement issued while 200 deltas are rendering still completes. | `ReplacementWhileEventsAreRendering_CompletesWithoutDeadlock` |
| A prompt released *after* its session was replaced changes nothing, raises no banner, and does not clear the new view's sending state. | `StalePromptResult_AfterReplacement_CannotMutateTheNewSession` |
| Replacement produces no error/connection/gap banner. | `ExpectedCancellation_IsSilent` |
| A faulted stream shows its message and a Retry control. | `UnexpectedStreamFailure_BecomesVisibleRetryableState` |
| Retry opens exactly one new subscription, clears the banner, shows the gap notice, and the notice survives later events. | `Retry_OpensANewSubscriptionAndShowsThePersistentGapNotice` |
| The notice clears only on session replacement. | `GapNotice_ClearsOnlyWhenANewViewBegins` |
| Durable prompts are disabled while disconnected; Mission prompts are not. | `WhileDisconnected_DurableConversationPromptsAreBlocked_AndMissionPromptsAreNot` |
| Disposal leaves no active subscription. | `DisposeAsync_LeavesNoActiveSubscription` |

The deadlock probe exists because the design awaits the event loop from inside a UI event handler.
The first draft of the suite hung, which turned out to be the test awaiting a deliberately-held
prompt handler rather than a product deadlock; the probe was added so the distinction stays proven
rather than argued.

### Desktop Quality Gate result

| Required answer | Result |
|---|---|
| What product behaviour is required? | Replacing folder or mission shows only the new session: one live subscription, no event applied twice, no late result from the discarded session, no error flash from an intentional switch. A broken stream becomes a visible, retryable banner. **PASS** |
| Who owns it? | Presentation (`Home.razor`) in the Client Runtime Presentation WASM app inside the Host's WebView. It owns cancellation, stale-result identity, and rendering only. **PASS** |
| What has been verified about the adapter? | No adapter behaviour is relied on. `IClientRuntimeChannel.Subscribe` has no cursor, `ClientRuntimeEventHub` drops events with no live subscriber, and `ConversationRuntimeSession` publishes each event exactly once — all read in source, which is why the gap is stated rather than papered over. **PASS** |
| Why does the proposal preserve the replacement boundary? | The diff is one page plus one test file. No `IDesktopHost`, Supervisor, Host, transport-contract, or transcript-model change. **PASS** |
| What proves it? | The ten tests above plus a clean solution build/test. Process-level observation is **not applicable**: this task changes no process lifecycle (Task 2 owns that). **PASS** |
