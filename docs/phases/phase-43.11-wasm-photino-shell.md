# Phase 43.11 — Blazor WASM UI shell + Photino native packaging

> **Operational correction (2026-08-17):** This completed spoke records the original single-process
> implementation. It is historical evidence only. New work must follow the locked
> [Desktop Supervisor/Host process boundary](../design/forge-architecture.md#desktop-supervisor-and-native-host-are-separate-processes):
> `ForgeMission.Desktop` is the Supervisor; `ForgeMission.Desktop.Host` is the disposable native
> host. In particular, do not use this spoke's `Program.cs` orchestration or its host-close handling
> as precedent.

> **Historical naming note (2026-08-01):** the native packaging project this spoke built was renamed
> `ForgeMission.ClientRuntime.Photino` → `ForgeMission.Desktop` (and moved out from under the
> `ClientRuntime.` prefix). The current process names are fixed in
> [Forge Architecture](../design/forge-architecture.md#naming-the-desktop-processes). Below,
> literal code references (project name, `PhotinoShellBoundaryTests`) have been updated to the new
> names; "Photino" elsewhere still correctly names the underlying `Photino.NET` library the shell is
> built on, which hasn't changed. **Same day, follow-up:** the shell contract was also formalized as
> an actual interface, `IDesktopHost`, split into its own project (`ForgeMission.Desktop.Contracts`)
> with the implementation (`PhotinoDesktopHost`) in a third project (`ForgeMission.Desktop.Photino`)
> — see [forge-architecture.md](../design/forge-architecture.md#desktop-host-abstraction-idesktophost)
> for the current three-project layout and why it isn't just inside `ForgeMission.Desktop`.
> The former `Program.cs` Client Runtime orchestration depended only on `IDesktopHost`, not
> `Photino.NET` directly. That same-process composition is superseded by the correction above.

**Status: ✅ DONE (2026-08-08) — both Batch A and Batch B complete and verified.** Part of
[Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Depended on
[43.10](phase-43.10-transport-contract.md) (the UI needs a real channel to the Client Runtime before
it can do anything). Replaces the now-superseded Electron + Blazor Server shell
([phase-43.2-electron-forge-desktop-shell.md](phase-43.2-electron-forge-desktop-shell.md)) as the
active desktop-shell spoke. The later **Janus Desktop PoC** is recorded in the
[Phase 43 completed record](phase-43-forge-desktop_completed.md); 43.3's remaining `sdlc-agent`
catalog work is independent follow-up.

## Design

Per [forge-architecture.md](../design/forge-architecture.md#native-host-ui-framework-and-the-verification-constraint):
the UI is Blazor WebAssembly, developed and verified primarily against a plain browser tab (CDP,
DevTools, Playwright, screenshots, hot reload — the same zero-setup loop already proven for
`ForgeUI`/Rooms). Photino wraps that same application for native packaging only — it is not the UI
framework and holds no business logic.

**What carries forward from the Electron/Blazor Server shell, unchanged:** the actual UI component
design work already done — the `forge.css` token application, the tool-call indicator states
(running/done glyphs, exact per-tool verb copy), the folder-open `+` composer menu — is UX/visual
design knowledge, not framework-specific code. [Task 3's target mockup](phase-43.2-electron-forge-desktop-shell.md)
and its component spec remain the correct visual target; only the rendering technology underneath
changes.

**What does not carry forward:** the Electron `main.cjs` process-spawning code
(`startHost`/`waitForHostAddress`/`confirmReady`/`hostProcess.kill()`), Blazor Server's SignalR
circuit, and the combined UI+tool-execution process shape. Those are specific to the superseded
architecture and are not migrated — this spoke builds their Photino/WASM equivalents fresh, per the
new layer split.

## Locked decisions

- **Development happens against a plain browser tab, always.** Any visual-verification task in this
  spoke (or downstream, e.g. 43.3/43.4) is done against the WASM app running as a normal localhost
  page — the same browser tooling already proven throughout this project. Photino is checked
  periodically for "does it still look right when packaged," not used as the primary dev-loop
  target — that would reintroduce the exact WKWebView/CDP verification gap this whole decision
  exists to avoid.
- **Photino's responsibilities are strictly limited to:** native window, native WebView, desktop
  lifecycle, packaging, and OS integration (menus, updater). Business logic, capability execution,
  and authorization all live in the Client Runtime ([43.8](phase-43.8-capability-provider-pattern.md)/
  [43.9](phase-43.9-client-runtime-authorization.md)), not in the Photino host.
- **Native dialogs (folder picker, etc.) are the Native Desktop Host's job**, not the WASM UI's and
  not the Client Runtime's — the WASM UI requests one through the Client Runtime API
  ([43.10](phase-43.10-transport-contract.md)'s channel), which is the same relationship the old
  Electron `contextBridge`/`forgeDesktop.pickFolder` pattern already established; only the
  implementation underneath changes.
- **`forge webui` and the packaged Photino app share the identical WASM UI and Client Runtime** —
  per [forge-desktop-client-runtime.md](../design/forge-desktop-client-runtime.md#forge-webui-redefinition),
  the only difference is chrome (native window vs. browser tab).
- **Photino maturity is a due-diligence gate before this spoke starts, not after.** Check current
  maintenance activity, Linux WebKitGTK support quality, and issue backlog health before committing
  implementation time — same bar already applied to Avalonia's Pro-tier control risk. If Photino
  turns out to be a poor bet, that's a design-stage finding, not something to discover mid-build.
  **Finding (2026-08-01):** genuine yellow flag, not a blocker. `photino.NET`/`Photino.Native` last
  merged PR was 2025-06-19; last functional commit 2025-01-23 (v4.0), with one README-only push in
  March 2026. `Photino.Blazor` — the package this spoke actually depends on — has had zero pushes of
  any kind since 2025-01-23. Ten community PRs sit unmerged, several since 2025-10, including
  Linux/GTK contributions; an open Linux segfault report (`SetIconFile`, filed 2026-03) has no
  maintainer response. The maintainer (CODE Magazine, a side project) has publicly acknowledged
  reduced bandwidth and is moving to Copilot-assisted triage. **Accepted, not deferred**, because the
  mitigation here is structural rather than time-boxed: the architecture already requires Photino be
  a thin, swappable shell (see the next locked decision) — if Photino stalls further or a blocking bug
  goes unaddressed, only the shell project needs replacing, not Forge itself. Near-term verification
  target is macOS only (WKWebView, Apple's own native webview — the thinnest, lowest-risk path
  through this dependency); the Linux-specific staleness above isn't load-bearing for this spoke.
- **The shell-swappability guarantee is enforced by one simple test, not an architecture-tooling
  framework.** A single test asserts the Photino host project has no project reference to
  `ForgeMission.Core`, `ForgeMission.ClientRuntime`, or `ForgeMission.ClientRuntime.Transport` — it
  only loads the WASM UI's built static output and does native windowing/lifecycle/packaging. This
  is deliberately a one-off check (in the spirit of 43.10's `ClientRuntimePresentationBoundaryTests`,
  not necessarily the same mechanism), not a generalized architecture-test framework — the goal is
  making an accidental boundary violation obvious during development, not building tooling for its
  own sake. If boundary-enforcement needs ever grow past a handful of checks like this, that's a
  future reconsideration, not something to build speculatively now.
- **The WASM static bundle is served by the Client Runtime's existing Kestrel host, at the same
  origin as the transport API — not by a separate static server, and not by `Photino.Blazor`'s own
  scheme-handler hosting mechanism.** Blazor WebAssembly itself needs no ASP.NET Core — it's static
  output any static file server could serve. ASP.NET Core is unavoidable here only because
  [43.10](phase-43.10-transport-contract.md)'s transport API already needs Kestrel (routing, request
  binding, SSE) — that requirement exists independent of how the UI gets served. Given that Kestrel
  process already exists, serving the WASM assets from it too (one origin, one port) is not just
  convenient, it's the reason `IClientRuntimeChannel`'s calls need no CORS handling at all — same
  origin means plain relative-URL requests, and specifically avoids cross-origin `EventSource` for
  the SSE subscription, which is meaningfully worse-behaved than same-origin. It also means the
  Photino native host stays on plain `Photino.NET` — a window + native WebView pointed at
  `http://localhost:<port>` — with no dependency on `Photino.Blazor` at all, which matters because
  `Photino.Blazor` was the single most stale of the three Photino repos checked in the due-diligence
  finding above (zero pushes of any kind since 2025-01-23, versus a cosmetic README push on the other
  two). This also means the Photino host project never touches
  `ForgeMission.ClientRuntime.Transport` directly — it just opens a URL — which is exactly what the
  shell-boundary test above expects to find.

## Sequencing: plumbing now (Codex), visual work deferred (Claude paired with Ameer)

Per working preference (UI/UX work pairs directly with Ameer; functional/plumbing work stays a good
fit for the Claude-architect/Codex-developer pipeline in
[claude-codex-workflow.md](../design/claude-codex-workflow.md)), this spoke splits into two batches:

- **Batch A — plumbing, assigned to Codex now:** task 2's bare project scaffold (new WASM project,
  `forge.css` included, no styled components yet), task 3 (channel consumption), task 5 (Photino
  host scaffold), task 7 (boundary test). A minimal, unstyled page is enough to prove task 3's
  channel round-trip — it does not need to look like anything yet.
- **Batch B — visual work, deferred to a separate paired session:** task 2's actual UI components,
  task 4 (composer/tool-call-indicator/response-card rebuild against Task 3's mockup — see
  [phase-43.2-electron-forge-desktop-shell.md](phase-43.2-electron-forge-desktop-shell.md)), and
  task 6 (visual verification, browser tab then packaged Photino/macOS). Batch B starts once Batch A
  is merged.

## Tasks

1. ~~Due diligence: verify Photino's maturity~~ — **done**, see the finding recorded in the locked
   decision above.
2. Scaffold the Blazor WebAssembly project. **Batch A (now):** the bare project — new `.csproj`,
   `forge.css` wired in as a static asset, a minimal unstyled page sufficient for task 3 to prove the
   channel round-trip — plus wiring its published static output into `ForgeMission.ClientRuntime`'s
   existing Kestrel host (same origin as the `/transport/*` API, per the hosting locked decision
   above), not a separate static server. **Batch B (deferred):** the actual UI components (composer,
   tool-call indicator, response card) — not a port of the Electron shell's Razor markup (the
   underlying rendering model differs enough that a port risks carrying over Server-specific
   assumptions).
3. **Batch A.** Implement the WASM side of [43.10](phase-43.10-transport-contract.md)'s
   `IClientRuntimeChannel` consumption — the UI's only path to the Client Runtime. Provable against
   task 2's minimal page; does not require task 4's visual polish to exist first.
4. **Batch B (deferred).** Rebuild the composer, tool-call indicator, and response-card treatment
   against Task 3's existing target mockup and component spec (visual design carries forward; only
   the implementation technology changes).
5. **Batch A.** Scaffold the Photino native host using plain `Photino.NET` (not `Photino.Blazor` —
   per the hosting locked decision above): window lifecycle, opening a native window pointed at the
   Client Runtime's own `http://localhost:<port>` (where task 2 wired the WASM static output). Can
   load task 2's minimal page for now — doesn't need task 4's polish to prove the host works.
   **The native folder-picker dialog bridge is deferred to Batch B** (raised as an open question
   during Batch A planning, 2026-08-01): Batch A's Done-when doesn't require it —
   `SessionSetupRequest` already takes a plain `string WorkspaceRoot`, so the Batch A proof page uses
   a typed-in path, no dialog needed. The folder-open affordance is UX/visual carryover work per the
   "What carries forward" section above, not plumbing. When Batch B needs it, the intended shape
   (recorded here so it isn't lost): reuse `PendingConfirmationHandler`'s exact pattern — Client
   Runtime publishes a `PickFolderRequested`-style event over the existing SSE hub, the Photino host
   subscribes as a plain `HttpClient` consumer (no `Transport` project reference needed) and posts
   the chosen path back, Client Runtime completes a `TaskCompletionSource` exactly like the
   confirmation flow already does.
6. **Batch B (deferred).** **Verify against a plain browser first** (screenshot/inspect the WASM app
   running as a normal localhost page — full loop: open folder, send a prompt, see a real tool call,
   see the styled response). Only after that passes, verify the identical app once more through the
   packaged Photino build on macOS, to confirm the WKWebView residual-risk note in
   [forge-architecture.md](../design/forge-architecture.md) doesn't manifest in practice.
7. **Batch A.** Add the single shell-boundary test described above (no project reference from the
   Photino host to `ForgeMission.Core`/`ForgeMission.ClientRuntime`/`ForgeMission.ClientRuntime.Transport`).

## Done when

**Batch A (Codex, now) done when:** the WASM project exists and loads (unstyled), served from the
Client Runtime's own Kestrel host at the same origin as the transport API; a capability request
round-trips through `IClientRuntimeChannel` from that page (using a typed-in workspace path — no
native folder-picker needed yet, see task 5) to a real provider and back; the Photino host (plain
`Photino.NET`) loads that same page in a native window; and the shell-boundary test (task 7) passes.

**✅ Batch A DONE (2026-08-01)**, branch `codex/phase-43.11-wasm-photino-shell-batch-a` (commits
`0c8908c`/`8929943`/`744338e`). Implemented by Codex, verified independently: build clean, full
suite 446 passed/0 failed/0 warnings; browser proof (unstyled page reads a real file through the
channel); published-host proof (`/`, `/css/forge.css`, `/_framework/blazor.webassembly.js` all 200);
Photino proof (native window loads the same URL); `DesktopShellBoundaryTests` +
`ClientRuntimePresentationBoundaryTests` passing (5 tests). Cleanup folded in: removed the
now-fully-orphaned Blazor Server scaffold (`App.razor`/`Shared/MainLayout.razor`/`Pages/Index.razor`/
`Pages/_Host.cshtml`) and the dead Electron shell scaffold (`electron/main.cjs` + friends, superseded
by this batch's WASM/Photino replacement).

**AOT validated and enabled**, not just designed: both `ForgeMission.ClientRuntime` and
`ForgeMission.Desktop` now publish as genuine single self-contained Native AOT
executables (~16MB / ~1.8MB on osx-arm64, same Homebrew-OpenSSL linker precedent as
`ForgeMission.Cli`), sub-second startup, confirmed by an actual `dotnet publish` + runtime smoke test
against the produced binaries — not just a clean build. Found and fixed two real AOT gaps in the
process (both pre-existing code, not introduced by this batch): the minimal API endpoints had no
source-gen JSON metadata for their own request binding (threw `NotSupportedException` on `GET /`
specifically, since that's the first request forcing endpoint-table initialization — static files
were unaffected, different code path); `MissionRuntimeSession` had three reflection-based
`JsonSerializer` calls plus one anonymous-type serialization with no source-gen context. This directly
serves the product requirement raised alongside Batch A: **a single shippable executable per host
that starts fast** — confirmed, not assumed.

**Single-exe orchestration built and verified (2026-08-01, commits `d195ff3`/`26282fe`):** the two
AOT binaries above were still separately-published with no orchestration between them — running the
real app required manually starting `ClientRuntime`, reading its URL off stdout, and passing it to
`Photino` by hand. `ForgeMission.Desktop` now spawns `ClientRuntime` as a child process
itself when run with no arguments (the real desktop experience), reads its `FORGE_CLIENT_RUNTIME_URL=`
line, loads that URL, and tears the child down on exit — the explicit-URL argument mode is kept only
for dev/test convenience. Two real bugs surfaced by actual runtime testing, not review: top-level
`await` resumes on a thread-pool thread after the first await, which crashed macOS AppKit ("API
misuse: setting the main menu on a non-main thread") the moment `PhotinoWindow` was constructed —
fixed by keeping the whole child-wait path synchronous; `AppDomain.ProcessExit` does not reliably
fire on external `kill -TERM` (confirmed — the child was still orphaned with a `ProcessExit` handler
registered) — fixed with `PosixSignalRegistration`, verified clean teardown afterward. `make
desktop-publish` publishes both binaries into one `dist/forge-desktop` folder; `make desktop`
publishes and launches in one command. Verified end-to-end: running only the Photino exe with zero
arguments from the published folder spawns the co-located native `ClientRuntime` (confirmed via `ps`
— no `dotnet` prefix, the native path was used), opens the window, loads the UI, and SIGTERM cleanly
tears down both processes. The Makefile's previously-broken `desktop` target (pointed at the deleted
Electron directory) and the dead `scripts/desktop.ps1` were replaced/removed in the same pass.

**Third termination path found and fixed the same day, via an actual native window close, not just
signal tests (commit `232c8d7`):** the two paths above don't cover clicking the window's own close
button. Confirmed live, twice, by actually opening the published app and closing the window: the
`ClientRuntime` child was left orphaned both times — code placed after `window.WaitForClose()`
never ran, because closing the last window on macOS tears the process down via native AppKit
machinery before control returns to managed code. Root-caused against Photino.NET's own source
(`PhotinoNetDelegates.cs`): `RegisterWindowClosingHandler`'s callback runs synchronously from native
code *before* the close is allowed to proceed, which is the correct hook — not `WaitForClose()`'s
return. Fixed and reverified live, twice more: closing the real window now leaves zero processes
behind. All three termination paths (normal close, external `SIGTERM`, and the un-catchable `SIGKILL`
that no process can intercept) are now accounted for, not assumed.

**Full spoke done when** (batch B, later): the Blazor WASM UI, reached through
`IClientRuntimeChannel`, opens a folder, accepts a prompt, streams a response from the configured
Mission Runtime, executes at least one real tool call visibly (with correct per-tool glyphs/copy,
matching Task 3's already-proven design), and produces a working result — verified against a plain
browser tab first, then confirmed once more through the actual packaged Photino app on macOS. The
Photino package has no business logic in it — everything it does is window/lifecycle/packaging,
proven by the shell-boundary test (task 7), not just by the app happening to work or by code review
alone.

**✅ Batch B MET (2026-08-08), branch `codex/phase-43.11-batch-b-chat-ui`.** Implemented by Claude,
paired directly with Ameer per the UI/UX convention — Task 2's real components, Task 4's
composer/tool-call-indicator/response-card rebuild, and Task 6's two-stage verification are all
done. `Home.razor` rewritten from the Batch A test page into the actual chat surface: header +
workspace label, a progressive-disclosure `+` → "Add folder" flyout (inline path entry, since the
native OS folder-picker bridge — noted below as deferred in task 5 — still doesn't exist; this
avoids the persistent top-level field the design doc's Principle 1 explicitly flags), a scrollable
turn list (user prompt pill, tool-call indicator rows, assistant response card), and a composer
matching [the Task 3 target mockup](../images/phase-43.2/task3-electron-visual-polish-after.png)
pixel-for-pixel in spirit (forge.css tokens used directly, no translation layer). Tool-call copy
mapping reuses the exact verb pairs from the already-implemented Avalonia
[Task 3 component spec](phase-43.2-avalonia-vanilla-shell_completed.md#task-3-design-tool-call-indicators)
(Reading/Read, Editing/Edited, Writing/Wrote, Running/Ran, Using/Used fallback), extended with a
`ToolTarget` field threaded onto `ClientRuntimeEvent` (server-side `ClientRuntimeEndpoints.cs`
extracts `file_path`/`command` from the tool call's arguments) so rows read "Read README.md" /
"Ran sleep 2 && echo done" instead of the verb alone.

Two real bugs found and fixed during verification, not by review:
- **Blazor WASM buffers streamed HTTP responses by default** — a long-lived SSE connection
  (`/transport/events`) never delivers anything to `await foreach` until the connection closes,
  which for this endpoint is never. Confirmed by a raw `fetch()` probe in the same browser context
  receiving chunks in real time while the Blazor `HttpClient` path received nothing. Fixed by
  threading an `Action<HttpRequestMessage>` hook through `HttpClientRuntimeChannel` (kept
  WASM-agnostic — it's also used by the non-browser `ClientRuntime.TransportProbe` — so the actual
  `SetBrowserResponseStreamingEnabled(true)` call lives in the WASM host's `Program.cs`, not the
  shared Transport project).
- **A casing bug, found only after the streaming fix above ruled out the transport layer**: the
  prompt-loop path publishes `ToolStatus` from `ToolCallNotificationState.ToString()`
  ("Running"/"Done", PascalCase), but the older capability-dispatch test path publishes literal
  lowercase `"running"/"done"`. `Home.razor`'s event switch matched only the lowercase form (copied
  from the wrong precedent), so tool-call rows silently never rendered even once transport worked.
  Fixed with an ordinal case-insensitive comparison.

Verified two ways, per the Done-when bar above: (1) real interactive round-trips against a plain
browser tab pointed at a live `dotnet run` Client Runtime + local `forge serve` (`missions/vanilla`)
— two separate real prompts, one triggering `Read` and one triggering `Bash`, both showing the
correct `✓ Read README.md` / `✓ Ran sleep 2 && echo done` indicator rows and a real answer in the
response card; (2) `make desktop-publish` + launching the actual packaged
`dist/forge-desktop/ForgeMission.Desktop` binary (native Photino window, explicit-URL dev mode) —
confirmed via `screencapture` (not just process/log inspection) that the identical styled UI renders
correctly in the real native window, closing the WKWebView-residual-risk question this doc's design
section raised. Full interactive flow (folder-open, tool call, response) was proven in the browser
tab per the documented CDP-attachment gotcha (Photino's window still isn't debuggable directly);
the Photino pass confirms visual parity, not a second independent interaction test.
`dotnet build`: 0 warnings/errors. `dotnet test`: 470 passed / 0 failed / 11 skipped (same
environment-gated skips as baseline).

**Still open, not silently dropped:** the native OS folder-picker dialog bridge (task 5's deferred
item) remains unbuilt — the inline path-entry flyout is the interim, not the final shape. The
completed shell now hosts the Janus Desktop PoC; 43.3's remaining `sdlc-agent` catalog work is
independent follow-up.

**Not the only live next step — read [43.13](phase-43.13-mission-runtime-orchestration.md) too.**
Batch B here (UI/visual work, pairs with Ameer) and 43.13 (Mission Runtime orchestration layer,
design fully locked, hand off to Codex) are two independent threads, not a sequence — neither blocks
the other. Don't treat finishing this spoke's Batch B as the only thing left in Phase 43; check
[plan.md](../plan.md)'s top pointer or the [phase hub](phase-43-forge-desktop.md)'s task table for
the current state of both before assuming.
