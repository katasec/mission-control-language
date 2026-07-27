# Phase 43.2 — Electron Forge Desktop shell

**Status: In build — Task 1 (scaffold) done 2026-07-27, live-verified; next up is Task 2 (wire the
Mission Runtime connection).** Replaces
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
2. **Wire the Mission Runtime connection**, local Docker `/v1` image as the default dev target
   (hosted `forge.katasec.com` as the alternate target, same code path). Real streaming + tool
   round-trip, reusing [42.3](phase-42.3-tool-capable-enriching-responder.md)'s mechanism as-is.
   **Architecture question resolved 2026-07-27** — the orchestration loop lives in the Client
   Runtime, as a **new** lightweight loop component (not a revised `AgenticSession` — see the
   design doc's correction) that holds conversation history and calls `/v1/messages` over HTTP,
   executing `tool_use` responses locally via [43.1](phase-43.1-tool-execution-engine.md)'s
   `ToolExecutorRegistry`/`AgentToolDeclarations` and [43.7](phase-43.7-workspace-provider.md)'s
   `IWorkspace`, both reused verbatim. `AgenticSession` itself is untouched — it stays the
   in-process loop a Mission Runtime host uses, not something the Client Runtime calls. Full
   reasoning in
   [the design doc](../design/forge-desktop-client-runtime.md#architecture-decision--orchestration-loop-lives-in-the-client-runtime-2026-07-27).
   Build to that decision — no remaining architecture choice in this task.

   **Pre-handoff clarity items (raised 2026-07-27, not yet resolved).** The architecture question
   above needed a same-day correction once checked against 43.1's actual code (see the design doc's
   correction) — these six were surfaced by the same scrutiny and must be resolved or verified
   before Task 2's assignment is written, not discovered by Codex mid-build:
   1. **Docker sequencing.** Local Docker `/v1` (this task's default dev target) means launching a
      real container — separate work from the core HTTP loop. Leaning toward: state Docker as the
      end goal, let Codex's plan propose build order (e.g. `forge serve` first, Docker layered in
      after) rather than mandating it in Done-when — not yet confirmed with the user.
   2. **`DockerCli` reuse feasibility.** `forge claude --container`'s container-launch logic
      ([`DockerCli`](../../src/ForgeMission.Cli/Docker/DockerCli.cs)) lives in `ForgeMission.Cli`, a
      different project than `ForgeMission.ClientRuntime`. Unchecked whether it's cleanly
      referenceable or needs porting/extracting.
   3. **Provider key sourcing for local Docker.** The local Mission Runtime container needs a real
      provider key. [deploy.md](../design/deploy.md#local-dev-environment--shell--provider-keys-read-this-before-running-anything-locally)
      already documents a live gotcha (keys live in the maintainer's `pwsh` env, not inherited by a
      `bash`-backed tool) — unclear whether `ClientRuntime` reads the same shell env or needs its
      own config/keychain path.
   4. **Hosted vs. local target selection.** Unclear whether Task 2 needs an actual UI/config toggle
      between `forge.katasec.com` and local Docker, or whether proving one target is enough for this
      task.
   5. **Streaming shape.** "Real streaming" is asserted as a requirement but the actual `/v1` wire
      streaming shape (SSE, chunked, etc.) hasn't been confirmed, nor whether it's ever been
      consumed by a non-CLI HTTP client before.
   6. **Minimal UI scope for Task 2 vs. Task 3.** Task 3 owns visual polish, but Task 2 needs some
      functional prompt input + response/tool-activity display to be testable. Line not yet drawn.
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
