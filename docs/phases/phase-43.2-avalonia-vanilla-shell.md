# Phase 43.2 — Avalonia vanilla shell

**Status: Design.** Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Depends on
[43.1](phase-43.1-tool-execution-engine.md) and
[43.7](phase-43.7-workspace-provider.md) (the workspace-root shape this spoke builds against).

## Design

The "meet people where they are" surface: a coding-agent chat UI that feels like Claude Code /
Codex, built once in Avalonia, running natively on macOS and Windows from one codebase. This spoke
deliberately does **not** attempt the debugger-style workbench yet — that's
[43.4](phase-43.4-ide-trace-surface.md), an iteration on top of this shell once it's proven.

Reference UX (what "vanilla" means here): a single chat pane, a compose box, streaming assistant
output, tool-call indicators (e.g. "Reading `Foo.cs`...", "Editing `Bar.cs`..." — the same
minimal-chrome signal every existing coding-agent TUI already gives), and a picker in the same
visual slot as a model dropdown — see [screenshot in the design conversation, 2026-07-25] — except
listing missions instead of models (wired for real in [43.3](phase-43.3-mission-attach-point.md);
this spoke can ship with a single hardcoded mission to prove the shell itself).

## Locked decisions carried from the hub

- New Avalonia project, `ForgeMission.Desktop` (or similar), referencing `ForgeMission.Core`
  directly (in-process — no `forge serve` hop for v1; revisit only if a browser/hosted surface
  needs to share the same backend later).
- Workspace root = whatever directory the user opens the app against, constructed as a
  [43.7](phase-43.7-workspace-provider.md) local-disk provider (not a bare string) and passed into
  [43.1](phase-43.1-tool-execution-engine.md)'s `AgenticSession`. Multi-root ("Add folder") is a
  43.7 open question, not decided here — this spoke can ship single-root as long as it's built
  against 43.7's interface, not a hardcoded path.
- AOT: decide per [AGENTS.md](../../AGENTS.md#aot-first--standing-rules-for-all-new-code) whether
  the desktop app itself is Native-AOT-published (smaller/faster startup) or JIT (matches the
  `ForgeMission.Runner`/`ForgeUI` precedent of not fighting Avalonia's AOT maturity if it's not
  fully there yet — check current Avalonia AOT support before committing).

## Tasks

1. Scaffold `ForgeMission.Desktop` (Avalonia project, added to `ForgeMission.slnx`), minimal
   window + chat view, referencing `ForgeMission.Core`.
2. Wire the compose box → [43.1](phase-43.1-tool-execution-engine.md)'s agentic loop, streaming
   assistant output back into the chat pane (reuse the existing `IAsyncEnumerable<string>`
   streaming contract from [Phase 15](phase-15-streaming.md) if it fits the loop shape; adapt if
   not).
3. Tool-call indicators — minimal inline rendering when a tool executes mid-turn (no need for a
   full diff view yet — that's 43.4's code pane).
4. Package for macOS (dev-signed is fine locally; defer notarization) and Windows (win-arm64,
   matching the existing release RID).
5. Dogfood checkpoint: run a real multi-tool coding task end-to-end in the shell on Mac.

## Done when

The Avalonia app opens a folder, accepts a prompt, streams a response, executes at least one real
tool call (file read/edit) visibly, and produces a working result — on macOS first, with a Windows
build (Surface ARM64 / Parallels) confirmed working within the same iteration (43.6 checkpoint).

## Open questions

- In-process `ForgeMission.Core` vs. `forge serve` loopback — locked toward in-process above, but
  worth a final check once streaming/cancellation semantics are prototyped (in-process gives free
  cancellation via `CancellationToken`; a server hop would need its own cancel-request wire-up).
