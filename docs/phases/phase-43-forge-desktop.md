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

## Architecture (2026-08-01 — supersedes the Electron/Blazor-Server decision below)

**The canonical architecture is now [docs/design/forge-architecture.md](../design/forge-architecture.md)** —
read that doc rather than expecting the architecture re-explained here. Summary: Forge is a Mission
Runtime; the desktop is one replaceable client among several. Three layers — Mission Runtime
(Brain), Client Runtime (Hands, also the security enforcement point), Presentation (Face, no
reasoning/filesystem/terminal logic). Capability contracts and a Capability Registry replace a
fixed tool list; transport is an explicit, swappable contract (`IClientRuntimeChannel`).

**Desktop implementation: Blazor WebAssembly UI + Photino native packaging**, superseding the
Electron + Blazor Server shell below. Development and visual verification happen against a plain
browser tab (unchanged verification story); Photino only wraps the already-verified app for
shipping — see
[forge-architecture.md](../design/forge-architecture.md#native-host-ui-framework-and-the-verification-constraint).

**Prerequisite chain before desktop UI work resumes** (dependency order):
[43.8 — Capability Provider pattern](phase-43.8-capability-provider-pattern.md) →
[43.9 — Client Runtime authorization](phase-43.9-client-runtime-authorization.md) →
[43.10 — Transport contract](phase-43.10-transport-contract.md) →
[43.11 — Blazor WASM UI + Photino shell](phase-43.11-wasm-photino-shell.md). All four are
prerequisites, not optional polish — [43.3](phase-43.3-mission-attach-point.md) and beyond build on
this foundation, not the superseded Electron/Blazor-Server one.

**Superseded, not wasted — evidence and design knowledge carry forward:** the Electron/Blazor
Server track ([43.2](phase-43.2-electron-forge-desktop-shell.md),
[43.2a](phase-43.2a-client-runtime-capability-boundary.md),
[43.2b](phase-43.2b-oci-mission-delivery.md)) proved the tool-execution loop, the hands/brain
capability boundary, and OCI mission delivery all genuinely work — that proof carries forward into
43.8-43.11's design even though the specific framework doesn't. Task 3's visual-polish work (the
`forge.css` token application, tool-call indicator design, folder-open `+` menu) is real UX design
knowledge, not framework-specific code — [43.11](phase-43.11-wasm-photino-shell.md) rebuilds it
against the new rendering technology, not from scratch. The shelved Avalonia spike is
[phase-43.2-avalonia-vanilla-shell.md](phase-43.2-avalonia-vanilla-shell.md).

### Prerequisites checklist — do not start 43.3+ desktop UI work until all four are met

- [ ] **43.8** — `IFileProvider`/`ITerminalProvider` + Capability Registry built; Mission Runtime
      consumes registry-derived tool declarations, not the fixed `AgentToolDeclarations` constant.
- [ ] **43.9** — Every capability request passes through `ICapabilityDispatcher`; no provider is
      reachable without authorization first; audit records exist for every dispatch.
- [ ] **43.10** — `IClientRuntimeChannel` + `HttpClientRuntimeChannel` built; the WASM UI never
      references `HttpClient` directly; a capability request round-trips over the real loopback
      network boundary, not an in-process shortcut.
- [x] **43.11** — Photino maturity due diligence done; WASM UI verified against a plain browser tab
      (full loop: open folder, prompt, real tool call, styled response); the same app confirmed once
      more through the actual packaged Photino build on macOS. **Done 2026-08-08.**

Only once all four are checked does [43.3](phase-43.3-mission-attach-point.md) (or any later desktop
UI spoke) have a foundation to build on.

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
- **UI framework: SUPERSEDED TWICE — Avalonia (2026-07-25) → Electron + Blazor Server (2026-07-27)
  → Blazor WebAssembly + Photino (2026-08-01).** Avalonia (MIT-licensed, Skia-rendered, one
  XAML/MVVM C# codebase for macOS + Windows) was chosen 2026-07-25 over true-native (two codebases)
  and browser/Electron (then judged weaker desktop feel, extra runtime). [Phase 43.2](phase-43.2-avalonia-vanilla-shell.md)'s
  Tasks 1–3 proved the shell genuinely worked, but Task 4 (visual identity) surfaced that verifying
  an Avalonia visual change needs a paid DevTools tier, per-machine license setup, and a multi-day
  environment saga — while the existing Blazor `ForgeUI` app has been fast to iterate on with
  zero-setup browser-based visual verification already available, which motivated the Electron +
  Blazor Server pivot. That combined-process shape (UI and tool execution sharing one Blazor Server
  process) was itself superseded 2026-08-01 once the broader Mission/Client/Presentation
  architecture ([forge-architecture.md](../design/forge-architecture.md)) made clear the UI should
  be sandboxed WASM with all real execution in a separate Client Runtime process — Photino replaces
  Electron as a thinner, `.NET`-native packaging layer around that same verify-in-a-browser
  development model. See [forge-architecture.md](../design/forge-architecture.md#why-not-maui-avalonia-or-tauri)
  for why Photino over MAUI/Avalonia/Tauri specifically.
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
| ~~43.2 — Avalonia vanilla shell~~ [(shelved)](phase-43.2-avalonia-vanilla-shell.md) | Superseded below. Tasks 1–3 (streaming, real tool execution, indicators) genuinely worked and are preserved as evidence in the [_completed doc](phase-43.2-avalonia-vanilla-shell_completed.md); Task 4 (visual identity) was abandoned mid-flight. `ForgeMission.Desktop` itself deleted 2026-08-01 now that 43.11 Batch A's WASM/Photino replacement is proven — git history is sufficient provenance. | 43.1, 43.7 | **Shelved 2026-07-27, code removed 2026-08-01** |
| [43.2 — Electron Forge Desktop shell](phase-43.2-electron-forge-desktop-shell.md) | The familiar coding-agent chat UI (Claude Code/Codex-shaped), Electron shell (+ browser tab for `forge webui`) over a Blazor Server Client Runtime, talking to a swappable Mission Runtime. Replaces the shelved Avalonia spoke above. | 43.1, 43.7 | **Tasks 1, 2a, 2b done (2b hardened via 43.2a/43.2b, below) — functional "Done when" met 2026-07-31. Next and only open task: Task 3 (tool-call indicators + `forge.css` visual polish) — today's shell is deliberately plain/unstyled HTML.** |
| [43.2a — Client Runtime capability boundary](phase-43.2a-client-runtime-capability-boundary.md) | Enforce the hands/brain boundary: Docker and hosted Mission Runtimes receive explicit mission inputs and protocol messages, never the opened local workspace; keep the existing Client Runtime loop target-invariant. | 43.2 Task 2b proof | **Boundary proven; its archive implementation is superseded by 43.2b, so no separate review remains open (re-verified as part of 43.2b's review instead)** |
| [43.2b — OCI mission delivery](phase-43.2b-oci-mission-delivery.md) | Move mission bootstrap into the Mission Runtime: the Client Runtime supplies a digest-pinned OCI reference; the local runner pulls, validates, caches, and loads it itself. | 43.2a | **DONE 2026-07-31** — public image, real Docker loop, and a live Electron UI proof (screenshot) all verified; architecture review completed, found one gap (a `MissionRef` that fails post-pull validation degraded silently instead of failing startup), fixed and architect-accepted on `codex/phase-43.2b-startup-hardening`. Superseded as the *active* mission-delivery mechanism by the new architecture, but the OCI delivery design itself is expected to carry forward into the Client Runtime's Mission Runtime connection under 43.11 — not thrown away. |
| **Superseded chain, prerequisite work below replaces it** — [43.2 Task 3 done 2026-08-01](phase-43.2-electron-forge-desktop-shell.md) (Electron/Blazor Server visual polish, real UX design knowledge that carries into 43.11) | — | — | Superseded by 43.8–43.11 |
| [43.8 — Capability Provider pattern](phase-43.8-capability-provider-pattern.md) | Migrate `IWorkspace`/`ToolExecutorRegistry` into `IFileProvider`/`ITerminalProvider` + a Capability Registry the Mission Runtime can query, replacing the fixed `AgentToolDeclarations` constant. | 43.1, 43.7 | Design |
| [43.9 — Client Runtime authorization](phase-43.9-client-runtime-authorization.md) | The security enforcement point: validate → authorize → dispatch → audit, sitting between the Mission Runtime's capability requests and 43.8's providers. Distinct from mission-level human-in-the-loop (43.5). | 43.8 | Design |
| [43.10 — Transport contract](phase-43.10-transport-contract.md) | `IClientRuntimeChannel` + `HttpClientRuntimeChannel` (HTTP + SSE/WebSockets) — the only path the sandboxed WASM UI has to reach the Client Runtime, since they're separate processes under the new architecture. | 43.9 | Design |
| [43.11 — Blazor WASM UI + Photino shell](phase-43.11-wasm-photino-shell.md) | Replaces the Electron/Blazor Server shell: WASM UI (verified against a plain browser, same loop as today) + Photino native packaging (thin wrapper only). Rebuilds Task 3's proven visual design against the new rendering technology. | 43.10 | **✅ DONE 2026-08-08** — Batch A + Batch B both complete, verified live |
| [43.3 — Mission-as-attach-point](phase-43.3-mission-attach-point.md) | Swap the model picker for a mission picker; wire `missions/sdlc-agent/` in as the flagship; decide intermediate-role-switch visibility. | 43.11 (WASM/Photino shell) | **In progress** — tasks 1–3 done, [PR #34](https://github.com/katasec/mission-control-language/pull/34) merged 2026-08-08 (corrected here 2026-08-10 — previously read "open for review," stale). **NEXT**: publish `sdlc-agent` to OCI + register it on the hosted catalog (task 4/5 — not verified as still-accurate in this pass, see spoke's open questions) |
| [43.4 — IDE trace surface (iterative)](phase-43.4-ide-trace-surface.md) | Evolve the vanilla shell toward the debugger-style workbench (outline/thread/gate/code-pane, dockable panels) from the forge-trace brainstorm. Explicitly a mockup-iterate-refine loop, not a fixed deliverable. Now builds on the Electron shell — vision unchanged. | 43.3 | Design — iteration not started |
| [43.5 — Human-in-the-loop (suspend/resume)](phase-43.5-human-in-the-loop.md) | `kind: human` pipeline step + `Suspended` `StepEnvelope` outcome + resume-at-step-N — the mechanical prerequisite for 43.4's "Gate" concept and for approval-gated missions generally. Framework-agnostic (Core-level) — unaffected by the Electron pivot. | None (parallel-buildable with 43.1–43.3; blocks only 43.4's Gate feature) | Design |
| 43.6 — Cross-platform validation checkpoints | Periodic: confirm the Electron shell (and `forge webui` browser path) work on Windows/Linux. Not a spoke — a recurring checklist item against whichever spoke just shipped. | Each prior spoke | Ongoing |
| [43.12 — AOT hygiene backlog](phase-43.12-aot-hygiene-backlog.md) | Cross-cutting engineering backlog raised during 43.11 Batch A's AOT validation: `ForgeMission.Docker` missing its `IsAotCompatible` marker (compiler-enforcement gap, not a live bug); the default `docker`-mode startup path never actually run under the published AOT binary; EF Core/Blazor Server AOT-quarantine noted as awareness-only. | None (backlog, not blocking) | Design — engineering backlog, not blocking |
| [43.13 — Mission Runtime resolution & orchestration layer](phase-43.13-mission-runtime-orchestration.md) | Moved "where does the Mission Runtime live, and start it if needed" out of `ClientRuntime` into a shared, surface-agnostic `ForgeMission.Orchestration` used by Desktop today, `forge webui`/future surfaces later. Locked decisions + GitHub Copilot prior-art research in the spoke; transport ([43.10](phase-43.10-transport-contract.md)) unaffected. | 43.1, 43.7 | **✅ DONE 2026-08-04** — all 8 tasks implemented + independently re-verified per task, all 3 termination paths live-verified against the published AOT binaries (real click on the close button, real `SIGTERM`, real Docker container torn down each time). Full suite: 356 passed, 0 failed. |
| [43.14 — Desktop cloud missions via API A](phase-43.14-desktop-cloud-missions.md) | Desktop reaches cloud missions through Forge's native API A (small additive tool-turn extension + reused `IEnrichmentCache` re-entrancy, no new session subsystem), not API B — API B stays reserved for external spec-bound clients (`claude`/`codex`). | 43.13 | **✅ DONE + LIVE 2026-08-08** — all 10 tasks shipped, live-verified with 4 named observations, see [_completed doc](phase-43.14-desktop-cloud-missions_completed.md) |
| [43.15 — Janus: minimal inter-agent mission](phase-43.15-janus-inter-agent-mission.md) | Minimal Claude-architect/OpenAI-implementer mission (`missions/janus/`) proving the primitives for the "eliminate manual Claude/Codex copy-paste" use case below — multi-provider `using`, a propose/approve/revise `loop`, `role: agent` gated on approval. Built instead of the fully-loaded `sdlc-agent` until 43.4/43.5 exist. | 43.1, 43.13 (Phase 25 `using`) | **Blocked** — mission built + validated, but `Approver` (Anthropic) crashes under the AOT binary on an upstream `anthropics/anthropic-sdk-csharp` bug (confirmed cross-platform, not a regression, not fixed in latest release — see spoke) |

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
- **Generative UI (CopilotKit/AG-UI) for tool-call rendering and HITL widgets** — captured as a
  brainstorm 2026-08-06, not yet a spoke:
  [generative-ui-copilotkit.md](../brainstorm/generative-ui-copilotkit.md). Confirms a real (if
  unproven-at-scale) path to embed CopilotKit's React generative-UI components inside the Blazor
  WASM shell via JS interop, backed by an AG-UI-shaped endpoint. Natural landing spots if pursued:
  [43.5](phase-43.5-human-in-the-loop.md) (approve/deny widgets) and
  [43.4](phase-43.4-ide-trace-surface.md) (general tool-call → component rendering). Not build-ready
  — no spike done yet.
- ~~**Longer-term aspiration (not scoped to any spoke yet, 2026-07-26)**~~ — **now being worked, see
  [43.15](phase-43.15-janus-inter-agent-mission.md) (2026-08-09).** The user wants to eventually
  eliminate the manual copy-paste cycle currently used to hand work between Claude (architect/
  reviewer) and Codex (implementer) during this project's own build process — one surface instead.
  Forge Desktop's "missions attach instead of models" thesis is the natural candidate; 43.15's
  `missions/janus/` is the first concrete build step, deliberately minimal rather than the full
  `sdlc-agent`, and is currently blocked on an upstream Anthropic SDK AOT bug — see that spoke.
