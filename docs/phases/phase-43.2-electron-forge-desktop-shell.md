# Phase 43.2 — Electron Forge Desktop shell

**Status: Design — architecture question resolved 2026-07-27 (see below); implementation not
started, next up is Task 1 (scaffold).** Replaces
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

1. **Scaffold the Electron app + local Blazor Server Client Runtime host.** New project (naming TBD
   at implementation time — mirrors the old `ForgeMission.Desktop` naming precedent), Blazor Server
   host referencing `ForgeMission.Core`/`ForgeMission.Cli`'s shared pieces directly (no
   serialization boundary), Electron shell pointing a native window at the local host's URL. Wire
   the "Add folder" flow onto [43.7](phase-43.7-workspace-provider.md)'s `IWorkspace` — this is the
   direct Electron equivalent of the shelved spoke's Task 1+2 folder-picker work, this time via a
   browser-native file/folder picker (or an Electron `dialog` API call) instead of Avalonia's
   `StorageProvider`.
2. **Wire the Mission Runtime connection**, local Docker `/v1` image as the default dev target
   (hosted `forge.katasec.com` as the alternate target, same code path). Real streaming + tool
   round-trip, reusing [42.3](phase-42.3-tool-capable-enriching-responder.md)'s mechanism as-is.
   **Architecture question resolved 2026-07-27** — the orchestration loop lives in the Client
   Runtime; `AgenticSession` ([43.1](phase-43.1-tool-execution-engine.md)) treats the Mission
   Runtime as a remote `IChatClient` over `/v1`. Full reasoning in
   [the design doc](../design/forge-desktop-client-runtime.md#architecture-decision--orchestration-loop-lives-in-the-client-runtime-2026-07-27).
   Build to that decision — no remaining architecture choice in this task.
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
