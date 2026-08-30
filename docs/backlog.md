# MCL — Backlog

> Deferred work, design candidates, and external conditions. These items are **not active work**;
> move one back to [plan.md](plan.md) only when it is deliberately selected.

## Product and platform candidates

| Item | Status / pointer |
|------|------------------|
| [Phase 22 / 22b — Non-LLM and ONNX experts](phases/phase-22-non-llm-experts.md) | Partial; resume only if an embedded-model use case requires it. |
| [Phase 26 — Tooling foundation](phases/phase-26-tooling-foundation.md) | Tree-sitter/LSP deferred until external demand. |
| [Phase 27 — Project assistant missions](phases/phase-27-project-assistant.md) | Design candidate. |
| [Phase 29 — UC reference missions](phases/phase-29-uc-reference-missions.md) | Deferred reference missions. |
| [Phase 30 — Concept missions](phases/phase-30-concept-missions.md) | Research/demo candidate. |
| [Phase 31 — Runtime platform](phases/phase-31-forge-runtime-platform.md) | Design candidate. |
| [Phase 37 — Evaluation harness](phases/phase-37-eval-harness.md) | Design candidate; evaluate when evidence becomes the bottleneck. |
| [Phase 39 — Metered runtime and marketplace](phases/phase-39-metered-runtime-marketplace.md) | Paused in favour of Desktop; the edge-rate-limit spend hole remains an accepted F&F-scale constraint. |
| [Phase 41 — Live retrieval](phases/phase-41-live-retrieval-scout.md) | Live work exists; roll the search-front template only when selected. |
| [Phase 42 — Forge Cloud](phases/phase-42-forge-cloud.md) | Local leg done; hosted work remains deferred. |
| [Phase 42.6 task 5b](phases/phase-42.6-hosted-endpoint-ttfa.md#tasks--status) | Hosted `forge claude @websearch` chat-wire adapter remains on hold. |
| Durable run control and enforcement | **Next after the Phase 43.4 UI exercise.** Add user-triggered `StopMission`, run-ID cancellation-source ownership, `Stopping`/`Stopped by user` durable outcomes, terminal process-tree cancellation, and auditable best-effort cancellation results. Recovery must create a new named run and retain stopped/failed/interrupted history; later checkpoint resume is explicit only from a verified safe boundary. The current workbench mock is design only; see [Phase 43.4](phases/phase-43.4-ide-trace-surface.md). |

## UI plans that are no longer implementation work

| Item | Disposition / replacement |
|------|---------------------------|
| [Phase 34 — Forge UI](phases/phase-34-forge-ui.md) | Reference rationale only. Its old standalone Next.js proposal is not planned. Hosted UI outcomes are in [Phase 38](phases/phase-38-forge-rooms.md)/[Phase 40](phases/phase-40-forge-ui-shell.md); Desktop outcomes are in [Phase 43.16](phases/phase-43.16-janus-desktop-local-poc.md) and the future [Phase 43.4](phases/phase-43.4-ide-trace-surface.md). |
| [Phase 35 — Forge UI (Blazor Server)](phases/phase-35-forge-ui-blazor.md) | **Superseded — do not implement as a new phase.** The Desktop replacement is [Phase 43.11](phases/phase-43.11-wasm-photino-shell.md) plus [Phase 43.16](phases/phase-43.16-janus-desktop-local-poc.md); future Desktop trace/workbench work is [Phase 43.4](phases/phase-43.4-ide-trace-surface.md). Existing hosted ForgeUI/Rooms evolution is [Phase 38](phases/phase-38-forge-rooms.md)/[Phase 40](phases/phase-40-forge-ui-shell.md). |

## Deferred responsive Desktop follow-up

| Item | Status / pointer |
|------|------------------|
| [Phase 43.17 Tasks 4–5 — bounded event delivery and progressive rendering](phases/phase-43.17-responsive-desktop.md#task-4--bounded-frame-friendly-event-delivery) | Deferred by operator direction on 2026-08-17 after Task 3. Reselect explicitly before implementation. |

## External conditions and future design candidates

| Item | Status / pointer |
|------|------------------|
| Grok web-search integration tests | Blocked by exhausted xAI account credit/spend limit, not a code defect. See [Phase 41](phases/phase-41-live-retrieval-scout.md). |
| Multi-agent debate, language-governance process, mechanical guardrails | Deferred design candidates; select and design explicitly before implementation. |
