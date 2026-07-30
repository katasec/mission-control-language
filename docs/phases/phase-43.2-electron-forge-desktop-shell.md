# Phase 43.2 — Electron Forge Desktop shell

**Status: In build — Task 1 (scaffold) done 2026-07-27, live-verified, merged to `main`. Task 2a
(HTTP loop) done 2026-07-30, live-verified; see [completed evidence](phase-43.2-electron-forge-desktop-shell_completed.md#task-2a--http-tool-loop-2026-07-30). Next: design and assign Task 2b (Docker packaging), including its non-terminal provider-key mechanism.**
Replaces
[phase-43.2-avalonia-vanilla-shell.md](phase-43.2-avalonia-vanilla-shell.md) (shelved — see that
doc for why). Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Depends on
[43.1](phase-43.1-tool-execution-engine.md) and [43.7](phase-43.7-workspace-provider.md), both done
and framework-agnostic (live in `ForgeMission.Core`), reused as-is.

Architecture and rationale live in
[docs/design/forge-desktop-client-runtime.md](../design/forge-desktop-client-runtime.md) — this
spoke links to it rather than re-explaining it. Read that doc first, in particular its **open
architecture question** (client-side vs. server-side tool-orchestration loop), which Task 2 below
must resolve, not silently default.

## Design

The "meet people where they are" surface, same brief as the shelved Avalonia spoke: a coding-agent
chat UI that feels like Claude Code / Codex. What changes is the substrate — a Client Runtime
(Electron shell, or a browser tab for `forge webui`) built on **Blazor Server**, talking to a
swappable Mission Runtime over [Phase 42](phase-42-forge-cloud.md)'s `/v1` wire protocol, per the
design doc.

This spoke does **not** attempt the debugger-style workbench — that stays
[43.4](phase-43.4-ide-trace-surface.md), an iteration on top of this shell once it's proven, exactly
as it was scoped under Avalonia.

## Locked decisions carried from the design doc

- **Client Runtime = Electron shell wrapping a local Blazor Server host**, or a plain browser tab
  for `forge webui` — same host, different chrome. UI components are freshly authored for this
  IDE-shaped surface, not reused from `ForgeUI`/Rooms' multi-user chat components. `forge.css`
  tokens are reused directly (no translation step).
- **Mission Runtime is swappable**: hosted Forge Cloud (`forge.katasec.com`) or a local Docker
  container running the same `/v1` image [42.4](phase-42.4-container-convergence.md) built.
  Docker is retained for local dev/test parity with the cloud contract and a genuine fully-local/
  private/no-account mode — not a legacy dependency.
- **Tool round-trip reuses [42.3](phase-42.3-tool-capable-enriching-responder.md) verbatim** —
  the Client Runtime plays the same role the real `claude` CLI already plays against `forge claude`:
  execute what the server asks locally (via `IWorkspace`/`ToolExecutorRegistry`), POST results back.
  No new protocol.
- **Workspace root** = whatever directory/directories the user opens the app against, constructed
  as a [43.7](phase-43.7-workspace-provider.md) provider (not a bare string). Multi-root ("Add
  folder") is a 43.7-level capability this spoke should build against, not hardcode around.

## Tasks

1. ✅ **Done 2026-07-27 — implemented by Codex, design-reviewed and independently re-verified by
   Claude (direct source read, not just the completion summary) before sign-off.** New project
   `src/ForgeMission.ClientRuntime/` (JIT `Microsoft.NET.Sdk.Web`, no AOT — distinct from the
   shelved `ForgeMission.Desktop`, which stays untouched in `src/ForgeMission.slnx`), referencing
   `ForgeMission.Core` directly (no serialization boundary). `Program.cs` binds an ephemeral
   loopback port (`http://127.0.0.1:0`) and exposes `/ready`; on startup prints
   `FORGE_CLIENT_RUNTIME_URL=<url>`. Electron's `main.cjs` spawns the host via `dotnet run` (dev
   scaffold only — packaging deferred), waits for that line, confirms `/ready`, then opens a
   `BrowserWindow` with `contextIsolation: true`, `nodeIntegration: false`, `sandbox: true`;
   `preload.cjs` exposes exactly one narrow API (`forgeDesktop.pickFolder`) via `contextBridge`.
   "Add folder" in `Pages/Index.razor` calls that API, hands the result to
   `Services/WorkspaceState.cs`, which constructs a [43.7](phase-43.7-workspace-provider.md)
   `LocalDiskWorkspace` and renders `"Workspace: {root}"` — live-verified end to end (screenshot
   after picking a real directory showed `Workspace: /Users/ameerdeen/progs/stuff`), not just the
   real-temp-directory unit test (`WorkspaceStateTests`) that covers the construction logic in
   isolation. `forge.css` linked directly from `ForgeUI`'s `wwwroot` (no copy/fork). `dotnet build`:
   0 warnings/errors; `dotnet test`: 423 passed / 6 skipped / 0 failed (same pre-existing
   environment-gated skips as 43.7's baseline, no new failures); `npm audit`: 0 vulnerabilities. No
   Mission Runtime/`AgenticSession`/streaming/tool-execution wiring — correctly out of scope, that's
   Task 2.
2. **Wire the Mission Runtime connection** — Task 2a is complete; Task 2b is next. Local Docker
   `/v1` remains the default dev target for 2b (hosted `forge.katasec.com` as the alternate target,
   same code path). Real streaming + tool round-trip, reusing
   [42.3](phase-42.3-tool-capable-enriching-responder.md)'s mechanism as-is.
   **Architecture question resolved 2026-07-27** — the orchestration loop lives in the Client
   Runtime, as a **new** lightweight loop component (not a revised `AgenticSession` — see the
   design doc's correction) that holds conversation history and calls `/v1/messages` over HTTP,
   executing `tool_use` responses locally via [43.1](phase-43.1-tool-execution-engine.md)'s
   `ToolExecutorRegistry`/`AgentToolDeclarations` and [43.7](phase-43.7-workspace-provider.md)'s
   `IWorkspace`, both reused verbatim. `AgenticSession` itself is untouched — it stays the
   in-process loop a Mission Runtime host uses, not something the Client Runtime calls. Full
   reasoning in
   [the design doc](../design/forge-desktop-client-runtime.md#architecture-decision--orchestration-loop-lives-in-the-client-runtime-2026-07-27).

   **Split into two sub-tasks (resolved 2026-07-27, after verifying all six pre-handoff items
   below against real code and walking each decision past the user)** — proving the HTTP loop and
   proving containerized packaging are separate concerns and should not be one task:

   - **Task 2a — HTTP loop correctness, in-process. ✅ Done 2026-07-30** — implementation and
     verification evidence are in the [completed record](phase-43.2-electron-forge-desktop-shell_completed.md#task-2a--http-tool-loop-2026-07-30). Build the new loop component and prove it
     against an **in-process** `AnthropicServer` host — no subprocess, no Docker. Reuse the exact
     pattern [`AnthropicServerFixture`](../../src/ForgeMission.Tests/Integration/AnthropicServerFixture.cs)
     already uses (`WebApplication.CreateSlimBuilder()` bound to a free loopback port via
     `app.StartAsync()`), which `AnthropicServerToolTests.cs` and `MockClaudeHostTests.cs` already
     drive with a plain `HttpClient` — i.e. non-CLI HTTP consumption of `/v1/messages`, both
     non-streaming and SSE, is proven prior art, not new ground. The Mission Runtime base URL is a
     **config value from the start** (not hardcoded) — since the loop's HTTP behavior is invariant
     regardless of target (confirmed below), taking it as config costs nothing and avoids a later
     rework. Minimal UI to make this testable: a plain unstyled prompt input, plain-text response
     area, plain tool-call log lines — enough to satisfy Done-when, no `forge.css` styling (that
     stays Task 3). **Done when:** a real prompt round-trips through the in-process host and
     executes at least one real tool call (file read/edit), visible in the unstyled UI.
     **Dev-launch decision (2026-07-30):** `make desktop` starts the real local
     `missions/vanilla` OpenAI Mission Runtime and Electron together; it passes the repository root
     as `Workspace:InitialRoot`, while leaving the normal folder picker available to change it. The
     launcher runs in `pwsh` so the existing `MCL_API_KEY` is inherited. No fake/demo Mission Runtime
     is part of the product or its development path.
   - **Task 2b — containerized packaging.** Wire the *same, already-proven* loop component against
     the real local Docker `/v1` container (`ghcr.io/katasec/forge-runner`), launched the way
     `forge claude --container` already does it: `DockerCli.RunContainerAsync` port-maps the
     container's fixed internal port to a free host port, and the caller just does plain HTTP to
     `http://127.0.0.1:{hostPort}/v1/messages` ([Program.cs:591](../../src/ForgeMission.Cli/Program.cs),
     [:618](../../src/ForgeMission.Cli/Program.cs)) — **no protocol difference from 2a's in-process
     target**, only the base URL config value changes. Two things Task 2b must land, resolved
     2026-07-27:
     - **`DockerCli` reuse** — extract [`DockerCli`](../../src/ForgeMission.Cli/Docker/DockerCli.cs)
       + `DockerPrereqChecker`/`PrereqCheck` (`src/ForgeMission.Cli/Docker/`) into a new shared
       project referenced by both `ForgeMission.Cli` and `ForgeMission.ClientRuntime`. `DockerCli`
       itself has zero CLI-specific dependencies (Spectre.Console/System.CommandLine live in
       `Program.cs`, not `DockerCli.cs`), so this is a mechanical move — a direct project reference
       to `ForgeMission.Cli` was rejected because it would drag that project's full AOT/Spectre/
       System.CommandLine/Anthropic dependency tree into a Blazor Server web project.
     - **Provider key sourcing — locked for Task 2b (2026-07-30).** The Client Runtime reads its
       own allow-listed dotenv file at `Environment.SpecialFolder.ApplicationData/Forge/provider.env`
       (macOS: `~/Library/Application Support/Forge/provider.env`) and forwards those values to the
       Docker container. `MCL_API_KEY` is sufficient for `missions/vanilla`; the file can also carry
       the established provider/model override names. It does **not** read provider keys from the
       Electron or `dotnet` process environment, so a Finder/Dock launch has the same key source as
       a terminal launch. Missing keys fail before the container starts, with the file path and
       required variable named. Full rationale and format live in the
       [Client Runtime design](../design/forge-desktop-client-runtime.md#local-docker-provider-keys).
     - **Done when:** the identical prompt/tool-call flow from 2a works against the real Docker
       target, only the base URL config value differs, no change to the loop component itself.
     - Hosted (`forge.katasec.com`) stays unproven after Task 2 — the config-value base URL leaves
       the door open, but proving it is a later task (it carries its own auth/billing questions,
       Phase 42.5/42.6), not part of this Done-when.

   **Pre-handoff clarity items — all six resolved 2026-07-27**, verified against real source (not
   assumed) and confirmed with the user one at a time, folded into Task 2a/2b above. Kept here as a
   short log rather than restated in full:
   1. Docker sequencing → split into 2a (in-process, no Docker) / 2b (Docker) above.
   2. `DockerCli` reuse → extract into a shared project (2b above).
   3. Provider key sourcing → locked for Task 2b as the Client Runtime-owned dotenv file above.
   4. Hosted vs. local target selection → base URL is a config value from Task 2a onward (no fork,
      since the wire is target-invariant); only local Docker needs to work for Task 2's Done-when.
   5. Streaming shape → confirmed real SSE (`text/event-stream`) but not token-level — the current
      `AnthropicServer` awaits the full response, then emits it as one start/delta/stop triple per
      content block (`AnthropicServer.cs:219-221` in `oai-server-dotnet`); non-CLI `HttpClient`
      consumption already proven by existing tests (see Task 2a above).
   6. Minimal UI scope → Task 2 (2a) builds bare-bones/unstyled elements sufficient for Done-when;
      Task 3 applies `forge.css` styling and indicator treatment on top, not from scratch.
3. **Tool-call indicators + basic visual polish**, using `forge.css` tokens directly — no XAML
   translation tax this time, since the surface is HTML/CSS natively. Mirrors the shelved spoke's
   Task 3 (indicator rows: running vs. done, muted metadata styling, per-tool copy) and folder-open
   affordance fix (progressive disclosure via a `+`-style composer control, no persistent chrome) —
   both worth re-grounding against
   [Desktop Interaction Principles](../design/desktop-interaction-principles.md) again, since the
   underlying interaction philosophy (progressive disclosure, honest affordances, no redundant entry
   points) didn't change with the framework, only how it's verified (browser tooling, not Avalonia
   DevTools MCP — see that doc's updated tooling section).

## Done when

The Electron app (and, sharing the same Client Runtime, `forge webui` in a browser tab) opens a
folder, accepts a prompt, streams a response from the configured Mission Runtime, executes at least
one real tool call (file read/edit) visibly, and produces a working result — verified against the
actually-running app via browser DevTools (Chrome DevTools Protocol through existing browser
tooling), not just a code diff.

## Open questions

- ~~The tool-orchestration-loop location (client vs. server)~~ — resolved 2026-07-27, see Task 2 and
  the [design doc](../design/forge-desktop-client-runtime.md#architecture-decision--orchestration-loop-lives-in-the-client-runtime-2026-07-27).
  A server-owned alternative was considered and deliberately deferred — see that doc's "Future
  consideration" section, not repeated here.
- Whether the Electron shell and `forge webui`'s browser-tab path share literally one build artifact
  or two thin wrappers over one Blazor Server project — decide once Task 1 scaffolding exists.
- Windows/Linux validation cadence for the Electron shell — likely lighter-weight than Avalonia's
  per-platform build concern, since Electron and a browser tab are both cross-platform by
  construction, but not yet confirmed against this repo's actual packaging needs.
