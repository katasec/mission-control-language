# Phase 43 — Forge Desktop (coding-agent client)

**Status: Design — top priority (2026-07-25).** Supersedes the standing "Phase 39 TOP PRIORITY"
tag as the active focus; 39 and 42.6 are paused, not abandoned (see [plan.md](../plan.md)).

## The problem

Every existing coding-agent surface (Claude Code, Codex, Copilot) attaches to a **model**. MCL's
actual differentiator — a mission (multiple roles/experts coordinating, e.g. an architect and a
critic reviewing each other) — has no equivalent first-class attach point. Today the only way to
get that shape of collaboration is copy-pasting between separate chat surfaces by hand.

## The core idea

A native-feeling desktop coding agent, built and felt exactly like the tools people already use
daily (Claude Code / Codex), except the thing you attach in the model picker is a **mission**, not a
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

## Client Runtime / Mission Runtime architecture (2026-07-27 — supersedes the Avalonia decision below)

The client pivoted from Avalonia to a web-rendered UI (Electron shell, or a plain browser tab for
`forge webui`) built on Blazor Server, split into a **Client Runtime** (UI + local tool execution —
the "hands") and a swappable **Mission Runtime** (the "brain," hosted Forge Cloud or a local Docker
`/v1` image) talking over [Phase 42](phase-42-forge-cloud.md)'s wire protocol. The Client Runtime
is the sole holder of the opened workspace: a brain may receive explicit mission inputs and return
tool instructions, but it must never receive a general local-workspace mount. Full rationale (why
Avalonia was dropped, why Docker is retained, the `forge webui` redefinition, and the settled
client-owned tool loop) is written up in
[docs/design/forge-desktop-client-runtime.md](../design/forge-desktop-client-runtime.md) — read
that doc rather than expecting the architecture re-explained here. The active build spoke is
[43.2 — Electron Forge Desktop shell](phase-43.2-electron-forge-desktop-shell.md), with
[43.2a — Client Runtime capability boundary](phase-43.2a-client-runtime-capability-boundary.md)
as its next design gate; the shelved
Avalonia spike is [phase-43.2-avalonia-vanilla-shell.md](phase-43.2-avalonia-vanilla-shell.md).

## Locked decisions (2026-07-25, UI framework decision superseded 2026-07-27)

- **Own the tool-execution loop — stop passing through to the real `claude` CLI.** Today's
  `forge claude` ([42.2](phase-42.2-forge-claude-launcher.md)) launches the actual Anthropic
  `claude` binary and lets *it* execute tools; forge only brokers the wire
  ([42.3](phase-42.3-tool-capable-enriching-responder.md)). Forge Desktop executes `Read` / `Edit`
  / `Write` / `Bash` itself — no external CLI dependency, no `npm install -g @anthropic-ai/claude-code`
  prerequisite ([Program.cs:582](../../src/ForgeMission.Cli/Program.cs:582)). Unaffected by the
  Electron pivot — this loop lives in `ForgeMission.Core` (43.1/43.7), reused as-is.
- **Missions are the attach point, not models.** Extends the raw-model passthrough pattern already
  shipped in [Phase 38.5](phase-38.5-registry-save-as-agent.md) (`@claude`/`@openai` as thin
  `vanilla`-shape missions, see [missions/claude/](../../missions/claude/)) from "one model wrapped
  as a mission" to "any real multi-role mission is attachable the same way." First flagship:
  [missions/sdlc-agent/](../../missions/sdlc-agent/) (Architect/CriticalReviewer/Synthesiser/
  QualityJudge, `DesignMode`, `loop(2)`).
- **UI framework: SUPERSEDED 2026-07-27 — was Avalonia, now Electron + Blazor Server.** Avalonia
  (MIT-licensed, Skia-rendered, one XAML/MVVM C# codebase for macOS + Windows) was chosen 2026-07-25
  over true-native (two codebases) and browser/Electron (then judged weaker desktop feel, extra
  runtime). [Phase 43.2](phase-43.2-avalonia-vanilla-shell.md)'s Tasks 1–3 proved the shell
  genuinely worked, but Task 4 (visual identity) surfaced that verifying an Avalonia visual change
  needs a paid DevTools tier, per-machine license setup, and a multi-day environment saga — while
  the existing Blazor `ForgeUI` app has been fast to iterate on with zero-setup browser-based visual
  verification already available. See
  [forge-desktop-client-runtime.md](../design/forge-desktop-client-runtime.md) for the full
  reasoning; the tradeoff accepted now is Electron's extra runtime/less-native feel, in exchange for
  removing the theming-verification tax entirely.
- **Backend stays `ForgeMission.Core`**, referenced directly by the Blazor Server Client Runtime
  host — no serialization boundary, same reasoning [Phase 35](phase-35-forge-ui-blazor.md) already
  used for `ForgeUI`. **The HTTP conversation/tool loop is client-owned and target-invariant:** it
  calls the configured `/v1/messages` endpoint, executes requested tools locally, and posts each
  result back; the Mission Runtime handles one model turn per request. The existing Core tool
  machinery is reused, but `AgenticSession` is not repurposed across the HTTP boundary. See the
  [Client Runtime design](../design/forge-desktop-client-runtime.md#architecture-decision--orchestration-loop-lives-in-the-client-runtime-2026-07-27).
  AOT-safety rules from [AGENTS.md](../../AGENTS.md#aot-first--standing-rules-for-all-new-code)
  still apply to whichever pieces are AOT-published (the CLI, and any Core-referencing host).
- **Platform order: Mac first**, Windows/Linux validated periodically. Electron and a browser tab
  are both cross-platform by construction, so this is even less of a gating concern than it was
  under Avalonia — exact packaging/validation cadence to be confirmed once
  [43.2 (Electron)](phase-43.2-electron-forge-desktop-shell.md) Task 1 exists.
- **This is explicitly iterative, not a linear build.** [43.4](phase-43.4-ide-trace-surface.md) in
  particular is a design-and-mockup loop against the real running client, not a fixed task
  checklist — "done when it feels right" is a real completion criterion here, tracked as iteration
  rounds, not a single technical gate.

## Dependency-ordered task list

| Spoke | What | Depends on | Status |
|---|---|---|---|
| [43.1 — Tool-execution engine](phase-43.1-tool-execution-engine.md) | Forge executes `Read`/`Edit`/`Write`/`Bash` itself; agentic loop (`AgenticSession`, in-memory, no cache) inside `ForgeMission.Core`, replacing client-side execution. Framework-agnostic — unaffected by the Electron pivot. | None (builds on existing `FunctionCallContent`/`MissionResult.ToolCalls` plumbing from [42.3](phase-42.3-tool-capable-enriching-responder.md)) | **✅ DONE 2026-07-25** — all 4 tasks, end-to-end verified, no mocks |
| [43.7 — Workspace provider abstraction](phase-43.7-workspace-provider.md) | Revises 43.1's `workspaceRoot: string` into one `IWorkspace` interface (`LocalDiskWorkspace` v1; container backend documented, deferred) before 43.2 builds a shell around the narrower assumption. Cross-references [39.7](phase-39.7-exec-secret-isolation.md) (shares the interface shape, not the timeline). Framework-agnostic — unaffected by the Electron pivot. | 43.1 | **✅ DONE 2026-07-25** — implemented by Codex, design + build reviewed and independently re-verified by Claude |
| ~~43.2 — Avalonia vanilla shell~~ [(shelved)](phase-43.2-avalonia-vanilla-shell.md) | Superseded below. Tasks 1–3 (streaming, real tool execution, indicators) genuinely worked and are preserved as evidence in the [_completed doc](phase-43.2-avalonia-vanilla-shell_completed.md); Task 4 (visual identity) was abandoned mid-flight, merged into `main` via PR 2026-07-27 for historical reference (dead/superseded code, not active). | 43.1, 43.7 | **Shelved 2026-07-27** |
| [43.2 — Electron Forge Desktop shell](phase-43.2-electron-forge-desktop-shell.md) | The familiar coding-agent chat UI (Claude Code/Codex-shaped), Electron shell (+ browser tab for `forge webui`) over a Blazor Server Client Runtime, talking to a swappable Mission Runtime. Replaces the shelved Avalonia spoke above. | 43.1, 43.7 | Active — Task 1 + 2a live-verified; Task 2b proved against the local Docker `/v1` runtime, pending the boundary hardening in 43.2a |
| [43.2a — Client Runtime capability boundary](phase-43.2a-client-runtime-capability-boundary.md) | Enforce the hands/brain boundary: Docker and hosted Mission Runtimes receive explicit mission inputs and protocol messages, never the opened local workspace; keep the existing Client Runtime loop target-invariant. | 43.2 Task 2b proof | **Design — decision gate** |
| [43.3 — Mission-as-attach-point](phase-43.3-mission-attach-point.md) | Swap the model picker for a mission picker; wire `missions/sdlc-agent/` in as the flagship; decide intermediate-role-switch visibility. Now builds on the Electron shell — vision unchanged. | 43.2 (Electron) | Design |
| [43.4 — IDE trace surface (iterative)](phase-43.4-ide-trace-surface.md) | Evolve the vanilla shell toward the debugger-style workbench (outline/thread/gate/code-pane, dockable panels) from the forge-trace brainstorm. Explicitly a mockup-iterate-refine loop, not a fixed deliverable. Now builds on the Electron shell — vision unchanged. | 43.3 | Design — iteration not started |
| [43.5 — Human-in-the-loop (suspend/resume)](phase-43.5-human-in-the-loop.md) | `kind: human` pipeline step + `Suspended` `StepEnvelope` outcome + resume-at-step-N — the mechanical prerequisite for 43.4's "Gate" concept and for approval-gated missions generally. Framework-agnostic (Core-level) — unaffected by the Electron pivot. | None (parallel-buildable with 43.1–43.3; blocks only 43.4's Gate feature) | Design |
| 43.6 — Cross-platform validation checkpoints | Periodic: confirm the Electron shell (and `forge webui` browser path) work on Windows/Linux. Not a spoke — a recurring checklist item against whichever spoke just shipped. | Each prior spoke | Ongoing |

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

- **Mission delivery to a local Docker brain.** The invariant is settled — no user-workspace mount —
  but the first delivery mechanism (read-only selected-mission mount, staged bundle, or a
  mission-containing image/artifact) is intentionally a decision gate in
  [43.2a](phase-43.2a-client-runtime-capability-boundary.md), before implementation begins.
- Whether intermediate mission role-switches (Architect proposes → Critic pushes back) surface
  inline in the vanilla shell, or stay hidden until 43.4's richer surface exists — decide in 43.3.
- Exact iteration cadence/exit criteria for 43.4 — "feels right" needs at least a rough rubric
  before that spoke starts, or it never converges.
- **Design gate scope (Cooper/Rams/Norman + progressive disclosure/honest affordances) still
  applies** — [desktop-interaction-principles.md](../design/desktop-interaction-principles.md)'s
  framework-agnostic principles carry over unchanged to the Electron shell; only its Avalonia-
  specific visual-identity retrofit note (formerly targeting shelved 43.2's Task 4) is now historical
  — see the [_completed doc](phase-43.2-avalonia-vanilla-shell_completed.md#task-4-design-visual-identity-skin)
  for that record.
- **Longer-term aspiration (not scoped to any spoke yet, 2026-07-26):** the user wants to eventually
  eliminate the manual copy-paste cycle currently used to hand work between Claude (architect/
  reviewer) and Codex (implementor) during this project's own build process — one surface instead.
  Forge Desktop's "missions attach instead of models" thesis is the natural candidate for that
  eventual tool (Forge Desktop itself, or a mission built on it), but this is explicitly a future
  idea, not a current task — revisit if/when Forge Desktop's multi-agent orchestration capabilities
  mature enough to make it concrete.
