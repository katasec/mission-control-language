# Phase 43 — Forge Desktop (native coding-agent client)

**Status: Design — top priority (2026-07-25).** Supersedes the standing "Phase 39 TOP PRIORITY"
tag as the active focus; 39 and 42.6 are paused, not abandoned (see [plan.md](../plan.md)).

## The problem

Every existing coding-agent surface (Claude Code, Codex, Copilot) attaches to a **model**. MCL's
actual differentiator — a mission (multiple roles/experts coordinating, e.g. an architect and a
critic reviewing each other) — has no equivalent first-class attach point. Today the only way to
get that shape of collaboration is copy-pasting between separate chat surfaces by hand.

## The core idea

A native desktop coding agent, built and felt exactly like the tools people already use daily
(Claude Code / Codex), except the thing you attach in the model picker is a **mission**, not a
model. Picking "SDLC" doesn't call one smarter model — it runs `Architect -> CriticalReviewer ->
Synthesiser -> QualityJudge` ([missions/sdlc-agent/mission.mcl](../../missions/sdlc-agent/mission.mcl))
behind one attach point, indistinguishable in gesture from picking a model today.

Sequencing is deliberately **meet-people-where-they-are, then evolve**: ship the vanilla,
familiar coding-agent UX first (proves the mission-attach idea with zero learning curve), then
iterate the same client toward the richer debugger-style surface already explored in
[docs/brainstorm/forge-trace-ide-surface.md](../brainstorm/forge-trace-ide-surface.md) — outline /
thread / gate / code-pane, dockable workbench. Both brainstorm docs
([forge-trace-ide-surface.md](../brainstorm/forge-trace-ide-surface.md),
[human-in-the-loop.md](../brainstorm/human-in-the-loop.md)) are promoted into this phase as of
this doc — their content now lives in [43.4](phase-43.4-ide-trace-surface.md) and
[43.5](phase-43.5-human-in-the-loop.md) respectively; the brainstorm originals are left as stubs
pointing here.

## Locked decisions (2026-07-25)

- **Own the tool-execution loop — stop passing through to the real `claude` CLI.** Today's
  `forge claude` ([42.2](phase-42.2-forge-claude-launcher.md)) launches the actual Anthropic
  `claude` binary and lets *it* execute tools; forge only brokers the wire
  ([42.3](phase-42.3-tool-capable-enriching-responder.md)). Forge Desktop executes `Read` / `Edit`
  / `Write` / `Bash` itself — no external CLI dependency, no `npm install -g @anthropic-ai/claude-code`
  prerequisite ([Program.cs:582](../../src/ForgeMission.Cli/Program.cs:582)).
- **Missions are the attach point, not models.** Extends the raw-model passthrough pattern already
  shipped in [Phase 38.5](phase-38.5-registry-save-as-agent.md) (`@claude`/`@openai` as thin
  `vanilla`-shape missions, see [missions/claude/](../../missions/claude/)) from "one model wrapped
  as a mission" to "any real multi-role mission is attachable the same way." First flagship:
  [missions/sdlc-agent/](../../missions/sdlc-agent/) (Architect/CriticalReviewer/Synthesiser/
  QualityJudge, `DesignMode`, `loop(2)`).
- **UI framework: Avalonia**, not the SwiftUI/WinUI split explored in the brainstorm mockups.
  MIT-licensed, Skia-rendered, one XAML/MVVM C# codebase for macOS + Windows (+ Linux for free).
  Chosen over true-native (two codebases) and browser/Electron (weaker desktop feel, extra
  runtime). Known tradeoff: not pixel-identical to native Fluent/AppKit widgets — acceptable.
- **Backend stays `ForgeMission.Core`**, referenced either in-process (same .NET runtime as
  Avalonia) or via local `forge serve` loopback — decide per-spoke; in-process is simpler for v1,
  the server split stays available if a browser/hosted surface needs the same backend later.
  AOT-safety rules from [AGENTS.md](../../AGENTS.md#aot-first--standing-rules-for-all-new-code)
  apply to the desktop project too if it's AOT-published, not only the CLI.
- **Platform order: Mac first**, Windows validated periodically (not continuously) by pulling the
  built binary onto a physical Surface (ARM64) and/or the Parallels Windows-11-ARM64 VM — both
  already match the existing `win-arm64` release RID
  ([release.yml:55](../../.github/workflows/release.yml:55)), so no new RID work is needed.
  Platform choice no longer gates architecture decisions (Avalonia is x-plat by construction); it's
  a dogfooding-order choice, not a technical one.
- **This is explicitly iterative, not a linear build.** [43.4](phase-43.4-ide-trace-surface.md) in
  particular is a design-and-mockup loop against real Avalonia code, not a fixed task checklist —
  "done when it feels right" is a real completion criterion here, tracked as iteration rounds, not
  a single technical gate.

## Dependency-ordered task list

| Spoke | What | Depends on | Status |
|---|---|---|---|
| [43.1 — Tool-execution engine](phase-43.1-tool-execution-engine.md) | Forge executes `Read`/`Edit`/`Write`/`Bash` itself; agentic loop (`AgenticSession`, in-memory, no cache) inside `ForgeMission.Core`, replacing client-side execution. | None (builds on existing `FunctionCallContent`/`MissionResult.ToolCalls` plumbing from [42.3](phase-42.3-tool-capable-enriching-responder.md)) | **✅ DONE 2026-07-25** — all 4 tasks, end-to-end verified, no mocks |
| [43.7 — Workspace provider abstraction](phase-43.7-workspace-provider.md) | Revises 43.1's `workspaceRoot: string` into one `IWorkspace` interface (`LocalDiskWorkspace` v1; container backend documented, deferred) before 43.2 builds a shell around the narrower assumption. Cross-references [39.7](phase-39.7-exec-secret-isolation.md) (shares the interface shape, not the timeline). | 43.1 | **✅ DONE 2026-07-25** — implemented by Codex, design + build reviewed and independently re-verified by Claude |
| [43.2 — Avalonia vanilla shell](phase-43.2-avalonia-vanilla-shell.md) | The familiar coding-agent chat UI (Claude Code/Codex-shaped), native Mac + Windows from one Avalonia codebase, talking to Core. | 43.1, 43.7 | **In build** — Tasks 1–3 done 2026-07-26; Tasks 4–5 (packaging, dogfood) remaining |
| [43.3 — Mission-as-attach-point](phase-43.3-mission-attach-point.md) | Swap the model picker for a mission picker; wire `missions/sdlc-agent/` in as the flagship; decide intermediate-role-switch visibility. | 43.2 | Design |
| [43.4 — IDE trace surface (iterative)](phase-43.4-ide-trace-surface.md) | Evolve the vanilla shell toward the debugger-style workbench (outline/thread/gate/code-pane, dockable panels) from the forge-trace brainstorm. Explicitly a mockup-iterate-refine loop, not a fixed deliverable. | 43.3 | Design — iteration not started |
| [43.5 — Human-in-the-loop (suspend/resume)](phase-43.5-human-in-the-loop.md) | `kind: human` pipeline step + `Suspended` `StepEnvelope` outcome + resume-at-step-N — the mechanical prerequisite for 43.4's "Gate" concept and for approval-gated missions generally. | None (parallel-buildable with 43.1–43.3; blocks only 43.4's Gate feature) | Design |
| 43.6 — Windows validation checkpoints | Periodic: pull the built binary/app onto the physical Surface (ARM64) and/or Parallels VM, confirm parity. Not a spoke — a recurring checklist item against whichever spoke just shipped. | Each prior spoke | Ongoing |

## Relationship to existing phases

- **[Phase 42](phase-42-forge-cloud.md)** stays the *hosted/passthrough* leg — `forge claude`
  wiring a real external `claude` CLI against a hosted mission. Not superseded; Forge Desktop is a
  parallel, self-contained client that doesn't need an external CLI at all.
- **[Phase 38.5](phase-38.5-registry-save-as-agent.md)** raw-model passthrough agents are the
  direct ancestor of the mission-attach-point idea (43.3) — same pattern, generalized from one
  model to any mission.
- **[Phase 39](phase-39-metered-runtime-marketplace.md)** (metered runtime/marketplace) is paused
  in priority, not cancelled — its ledger/billing work is orthogonal and can resume once Forge
  Desktop's core loop (43.1–43.3) proves out.

## Open questions / not yet decided

- In-process `ForgeMission.Core` reference vs. local `forge serve` loopback for the desktop app's
  backend — decide in 43.2.
- Whether intermediate mission role-switches (Architect proposes → Critic pushes back) surface
  inline in the vanilla shell, or stay hidden until 43.4's richer surface exists — decide in 43.3.
- Exact iteration cadence/exit criteria for 43.4 — "feels right" needs at least a rough rubric
  before that spoke starts, or it never converges.
- **Longer-term aspiration (not scoped to any spoke yet, 2026-07-26):** the user wants to eventually
  eliminate the manual copy-paste cycle currently used to hand work between Claude (architect/
  reviewer) and Codex (implementor) during this project's own build process — one surface instead.
  Forge Desktop's "missions attach instead of models" thesis is the natural candidate for that
  eventual tool (Forge Desktop itself, or a mission built on it), but this is explicitly a future
  idea, not a current task — revisit if/when Forge Desktop's multi-agent orchestration capabilities
  mature enough to make it concrete.
