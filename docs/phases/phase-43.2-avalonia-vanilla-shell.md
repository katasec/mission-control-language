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
- **Native AOT — locked 2026-07-25.** Confirmed against
  [Avalonia's own Native AOT deployment guide](https://docs.avaloniaui.net/docs/deployment/native-aot):
  does not reduce UI/UX design options (full styling/theming/animation/Skia rendering all work),
  only constrains *how* the app is wired — same AOT-first discipline [AGENTS.md](../../AGENTS.md#aot-first--standing-rules-for-all-new-code)
  already mandates elsewhere in this repo, not a new burden:
  - Compiled bindings (`x:CompileBindings="True"`), not reflection bindings.
  - **CommunityToolkit.Mvvm, not ReactiveUI** — ReactiveUI's expression-tree/reflection-heavy
    bindings have documented AOT friction; Avalonia's own official template ships a
    CommunityToolkit.Mvvm variant for this reason.
  - No runtime-loaded/dynamic XAML, static resources, assets as embedded resources. Worth
    remembering for [43.4](phase-43.4-ide-trace-surface.md)'s dockable-panel workbench later if it
    ever wants user-customizable layouts loaded from disk — not a v1 concern.
  - Compile-time DI (register view models at startup), not reflection-based service location.
  - Third-party Avalonia controls need AOT vetting case-by-case; first-party `Avalonia.Controls` is
    fine. Matches this repo's existing "right-size deps" bias (Phase 40.1 design-system doc).
  - Design-time XAML preview/hot-reload is limited under AOT — a dev-workflow cost only, mitigated
    by developing in JIT/Debug (fast inner loop, hot reload works) and only AOT-publishing the
    release binary, same split .NET already does generally.

## Tasks

1. Implementation complete 2026-07-26: scaffolded `ForgeMission.Desktop` from the official
   CommunityToolkit.Mvvm Avalonia template, added it to `ForgeMission.slnx`, and built a compiled-binding
   chat placeholder (message list + compose box, no agentic wiring). `dotnet build src/ForgeMission.slnx`
   passed with 0 warnings / 0 errors; Native AOT `win-arm64` publish and launch succeeded. The task is not
   marked done yet because the full test command is blocked by pre-existing Windows `ExecExpertRunnerTests`
   failures plus this machine's unavailable Docker daemon for Rooms tests; see current session evidence.
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
