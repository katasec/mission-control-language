# MCL — Implementation Plan

> **Completed-work archive: [plan_completed.md](plan_completed.md).** Every phase verified `Done`
> (and resolved open issues / decided "under discussion" topics) lives there, not here — this file
> only shows what's still active. Skip the archive entirely unless you specifically need historical
> context (why a past decision was made, when something shipped); it is never required to answer
> "what's next."

## Now (2026-08-09)

| | |
|---|---|
| **NEXT STEP** | **[Phase 43 — Forge Desktop](phases/phase-43-forge-desktop.md)** (top priority) — missions attach instead of models. Full status: [hub](phases/phase-43-forge-desktop.md#dependency-ordered-task-list). **Active: [43.15 — Janus](phases/phase-43.15-janus-inter-agent-mission.md)** — AOT fix designed + spiked, task assignment ready for Codex, not yet implemented; see the spoke's own NEXT STEP, not repeated here. [PR #34](https://github.com/katasec/mission-control-language/pull/34) (43.3) awaits review. Phase 39/42.6 paused in favor of this. |
| **Paused — [42.6 task 5b](phases/phase-42.6-hosted-endpoint-ttfa.md#tasks--status)** | `forge claude @websearch` hosted chat-wire adapter. User manual: ON HOLD, after 42.6. |
| **Spend hole (Ph. 39)** | Known + accepted at F&F scale. Edge rate limit **DEFERRED 2026-07-18 — no longer a launch gate** (no CDN/WAF tier exists, so it was never config-only); freeze-at-zero remains the live control ([42.6 task 6](phases/phase-42.6-hosted-endpoint-ttfa.md#tasks--status)). |
| **Standing check** | Bottleneck = **evidence + clients**, not design — [Phase 37 eval harness](phases/phase-37-eval-harness.md) stays the highest-value alternative. |

This file is a lookup table only — status + pointers. All depth (design, decisions, evidence, tasks)
lives in the phase hubs/spokes under [phases/](phases/).

## Phases

| Phase | Description | Status |
|-------|-------------|--------|
| [Phase 22 — Non-LLM Expert Kinds](phases/phase-22-non-llm-experts.md) | `kind` field in expert frontmatter (`llm` default, `onnx`, `http`). Static runner dispatch, no reflection. Context bag gains typed numeric values. Motivated by log anomaly detection (UC-3). | Partial — ONNX support tracked separately as [Phase 22b](phases/phase-22b-onnx.md) below |
| [Phase 26 — Tooling Foundation](phases/phase-26-tooling-foundation.md) | Source positions on AST nodes, TextMate grammar (syntax highlighting), Tree-sitter grammar (incremental parsing), LSP server (completion, hover, go-to-definition). After grammar stabilises in Phase 25. | Partial — Tree-sitter/LSP deferred until external demand, see hub |
| [Phase 22b — ONNX Expert Kind](phases/phase-22b-onnx.md) | Complete Phase 22 by adding `kind: onnx` for in-process ML model inference. `OnnxExpertRunner` loads an ONNX model, reads named float features from the context bag, runs inference, writes score back. Typed numeric values in context bag. Required for UC-1 (embedded vision models), UC-2 (embedded scoring models), UC-3 (log anomaly AnomalyDetector). | Partial — see hub for spoke-level breakdown |
| [Phase 29 — UC Reference Missions](phases/phase-29-uc-reference-missions.md) | Three demo missions proving MCL against real customer use cases. UC-1: Image Analysis Pipeline (`parallel {}` fan-out to vision experts + synthesiser). UC-2: Trading Signal Aggregator (3 parallel market context experts + signal synthesiser). UC-3: Log Anomaly Detection (LogParser → AnomalyDetector `kind:onnx` → RootCauseAnalyst → IncidentReporter). UC-1 and UC-2 can start before ONNX; UC-3 blocked on Phase 22b. Hub + 3 spokes. | Todo |
| [Phase 30 — Concept Missions (Research Paper Demos)](phases/phase-30-concept-missions.md) | Nine standalone missions proving MCL against foundational LLM and neurosymbolic reasoning papers — Self-Refine, Multi-Agent Debate, Constitutional AI, Mixture of Agents, Hybrid LLM+ML, LLM-as-Judge, Hallucination Reduction, Verifiable Reasoning, Compositionality. MCL positioned as a neurosymbolic orchestration language; accessibility (non-technical domain experts) is the unifying theme. Most are LLM/rule-only; one depends on Phase 22b (ONNX), see hub. | Brainstorm |
| [Phase 27 — Project Assistant Missions](phases/phase-27-project-assistant.md) | Three-layer mission composition: `project-assistant` (generic hub/spoke ops), `software-project-assistant` (extends with architect + developer modes), `product-owner-assistant` (extends with PO-specific experts). Served behind `forge serve` and pointed at by Claude Code — MCL intercepts every request and routes it through the right expert chain. Self-hosting demonstration. UC-4 — deliberately last: customer use cases (UC-1/2/3) validate the language first. | Design |
| [Phase 31 — Forge Generate & Capability Packaging](phases/phase-31-forge-runtime-platform.md) | `forge generate` produces standard K8s manifests (CronJob, Deployment) from forge.toml declarations. `forge dev start/stop` spins up a local kind cluster for testing. `forge publish` packages missions as OCI artifacts. Orleans deferred indefinitely — K8s is the runtime substrate for current use cases. Hub + 7 spokes. | Design |
| [Phase 34 — Forge UI](phases/phase-34-forge-ui.md) | Dedicated UI for MCL missions. Two audiences: non-technical users (verified/unverified trust signal, "how do I know?" disclosure) and developers (full pipeline trace, per-step pass/fail, loop convergence). Existing AI surfaces (Claude Desktop, Copilot, Codex) dissolve MCL's structured reasoning by design — this tension is structural and not fixable. A dedicated UI is the only way the core value proposition (visible trust, not just accuracy) is surfaced to users. Hub + 7 spokes. | Design |
| [Phase 35 — Forge UI (Blazor Server)](phases/phase-35-forge-ui-blazor.md) | Blazor Server implementation of Phase 34. Shares types directly with `ForgeMission.Core` (`StepEnvelope`, `MissionResult`, `MissionStatus`) — no serialisation boundary. `MissionService` calls `PipelineRunner` directly, no `forge serve` in the path. Thin client (~250KB SignalR stub), real-time trace streaming via `StateHasChanged()`. View models: `ChatMessage`, `PipelineTraceEvent`, `TrustSignal`. Hub + 7 spokes. | Design |
| [Phase 39 — Metered Container Runtime & Mission Marketplace](phases/phase-39-metered-runtime-marketplace.md) | Turn ForgeUI into a metered, containerised, multi-tenant runtime — one uniform execution path, one cost-meter, tiered pricing over the same ledger. Load-bearing decisions + per-sub-phase status: [hub lookup table](phases/phase-39-metered-runtime-marketplace.md#status--handoff-updated-2026-07-09). | **Paused 2026-07-25** in favor of [Phase 43](phases/phase-43-forge-desktop.md) — not abandoned, resume once 43.1–43.3 prove out. |
| [Phase 41 — Live Retrieval (Scout)](phases/phase-41-live-retrieval-scout.md) | Generic live-internet retrieval (`ForgeMission.Scout`, `IWebSearch` swap point, `kind: search`); `@grok` search-fronted in Rooms + streaming progress. Decisions + status in the hub. | **Live; next = Task 7 (roll search-front template), branch unmerged — see hub** |
| [Phase 42 — Forge Cloud](phases/phase-42-forge-cloud.md) | Expose MCL missions over the wires coding agents already speak (`/v1/messages` · `/v1/responses` · MCP), local + hosted from one container. Full status per sub-spoke: [hub §6](phases/phase-42-forge-cloud.md#6-spokes-dependency-ordered). | Local leg done; hosted leg in build — see hub |
| [Phase 43 — Forge Desktop (coding-agent client)](phases/phase-43-forge-desktop.md) | Coding-agent desktop client where **missions attach instead of models**. Canonical architecture: [forge-architecture.md](design/forge-architecture.md). Full status per sub-spoke: [hub's task table](phases/phase-43-forge-desktop.md#dependency-ordered-task-list). | Design — top priority; active thread is 43.15/Janus, see "Now" above |
| [Phase 37 — Evaluation Harness (`eval`/`dataset`/`report`)](phases/phase-37-eval-harness.md) | Turn "expert composition elevates quality" from a *vibe* into a *number*. Run one or more missions over a `dataset` of inputs, score each output with a `role: judge` (pairwise A/B with position-bias swap, or pointwise against a rubric), and aggregate into a comparison report. Reuses what already ships: judges (Phase 25a), mission composition (Phase 25), parallel fan-out (Phase 21), and the two existing A/B demo pairs (`elevator-pitch` ↔ `elevator-pitch-refined`, `loop-demo-naive` ↔ `loop-demo`). Only the outer loop + aggregation is new. Core fork: in-language `eval` construct (on-thesis, recommended) vs `forge eval` CLI (bootstrap, ships first). Closes the founding hypothesis loop by writing a real result into `docs/findings.md`. Hub + 7 spokes. | Design |

Phases verified fully `Done` (Phases 1–25a and others) moved to
[plan_completed.md](plan_completed.md#completed-phases) 2026-08-09 — open that file only when you
need historical context; it isn't needed to answer "what's next."

## Open issues

Resolved issues (#1–#6) moved to [plan_completed.md](plan_completed.md#resolved-open-issues) —
full narrative + evidence preserved there. Summary: OaiServer spec compliance (done, Cli bump
still pending), Python client example (done), provider-key IaC drift (done 2026-07-09),
`ExecExpertRunnerTests` Windows hygiene (done 2026-07-26), WDAC DLL-load gotcha on this box
(root-caused, closed, no fix planned), migration job DB-wipe risk (defused + structurally fixed
2026-07-19). No open issues currently outstanding.

## Under discussion

Resolved: ~~Mission Composition~~, ~~Skills and Tools~~, ~~Parallel steps runtime model~~, and
~~Context bag typing~~ — all Done/decided, moved to
[plan_completed.md](plan_completed.md#resolved-under-discussion-items).

| Topic | Description |
|-------|-------------|
| Multi-Agent Debate (`debate {}` block) | Round orchestration, per-round context summarisation, cross-agent output wiring. Deferred from Phase 25; needs a dedicated phase. Research-backed default: rounds: 3, warn beyond 5. |
| Language governance process | Java uses JSRs, C# uses Language Design Meeting notes, Go uses a formal proposal process (golang/proposal). Key design decisions are currently recorded in ad-hoc markdown files. A standardised proposal format — problem, prior art, alternatives considered, decision, rationale — would make decisions traceable and give future contributors clear reasoning rather than just outcomes. Decide format, location (`docs/proposals/`?), and whether past decisions (Phase 25 pre-flight) are backfilled. |

## Design docs

| Doc | Description |
|-----|-------------|
| [Completed / Resolved Archive](plan_completed.md) | Resolved open issues + closed "under discussion" items, moved out of the active tables above per the hub/spoke completed-work rule. |
| [UI Design System](design/ui-design-system.md) | How ForgeUI is themed: tokenized `forge.css` (CSS custom properties), automatic dark mode, reusable primitives, auth IA, and the local-run gotchas (OIDC-needs-HTTPS, `MCL_API_KEY`, Blazor prerender nav). Read before touching `src/ForgeUI`. |
| [Deploy Runbook](design/deploy.md) | **Operational how-to for shipping the hosted app.** The release loop (build image via `gh workflow run *-image.yml` → **roll the Container App separately** → verify live), topology (two images → ACR → `ca-forge-ui-dev` + `ca-forge-runner-dev`, `forge.katasec.com`), which-image-for-which-change, and gotchas (build≠deploy, amd64-only, custom-domain cert params, provider keys on the runner). Sits atop [38.7](phases/phase-38.7-hosting-deployment.md) (the infra *why*). |
| [Language Design](design/language.md) | Grammar, syntax decisions, primitives, capitalisation rationale |
| [Standard Library](design/stdlib.md) | Definition of what qualifies as a stdlib expert — four gates, current members, worked examples |
| [Architecture](design/architecture.md) | Components, boundaries, dependency flow |
| [Interaction Modes & Classifier-Router Pattern](design/interaction-modes.md) | Human-AI collaboration modes, classifier as HAProxy, SDLC meta-mission, `when {}` conditional step primitive |
| [SDLC Meta-Mission](design/sdlc-meta-mission.md) | Planned reference example — mission composition + debate{} + routing in one file; feature gap analysis and build order |
| [Research Foundations](design/research.md) | Academic literature mapped to MCL design decisions — Self-Refine, Reflexion, Multi-Agent Debate, Constitutional AI, MoE routing, MoA |
| [Observability (OTel)](design/observability.md) | Traces/metrics/logs via OpenTelemetry. Three lanes kept distinct (debug/ops vs live UX progress vs durable reporting); one instrumentation point in `PipelineRunner`; instrument-in-Core (BCL `ActivitySource`/`Meter`, AOT-safe) / export-in-host (OTel SDK, non-AOT); versioned telemetry contract; `ILogger<T>` + `[LoggerMessage]` + structured templates, no Serilog. Applies to the engine + Phase 38. |
| [MAF Research](design/maf.md) | Microsoft Agent Framework 1.0 spike findings |
| [Methodology](design/methodology.md) | The broader engineering approach MCL fits into |
| [Why MCL exists](why.md) | Origin, core thesis, methodology, thinking models |
