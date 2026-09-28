# MCL — Backlog

> Deferred work, design candidates, and external conditions. These items are **not active work**;
> move one back to [plan.md](plan.md) only when it is deliberately selected.

## Product and platform candidates

| Item | Status / pointer |
|------|------------------|
| [Phase 45.5 — Live existing-mission chat](phases/phase-45.5-live-existing-mission-chat.md) | Deferred by operator direction on 2026-09-25. The repository extraction closed 2026-09-26 ([Phase 50](phases/phase-50-repository-extraction.md)); if reselected, resume in `forge-desktop`, not here. |
| Explorer OCI dependency/portable-lock migration | Separate from the current [Phase 43.23 ownership end state](phases/phase-43.23-domain-ownership_completed.md#task-4--ownership-acceptance-and-documentation-2026-09-06). The linked [43.22 reconstruction](phases/phase-43.22-project-mission-reconstruction.md) is historical reference only; its earlier Core/CLI/lock candidate is not an implicit prerequisite of the mission picker or run history. |
| [Phase 50 row 7 — tidy this repo](phases/phase-50-repository-extraction.md) | Deferred 2026-09-26: place `clients/`, `editors/`, `html/`; further AGENTS.md/README cleanup. |
| [Phase 51 — downloaded-artifact checksum acceptance](phases/phase-51-desktop-publish-script.md) | Deferred by operator direction on 2026-09-28. The canonical macOS/Windows workflow-artifact downloads, SHA-256 sidecar verification, and target-platform launches remain required if Phase 51 is reselected. |
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
| Cloud as the default local target (replacing Kind) | **Selected 2026-09-28** as [Phase 52.1](phases/phase-52.1-cloud-conversations.md). |
| `forge dev start` / `stop` | Deferred from [Phase 52](phases/phase-52-desktop-simplification.md), 2026-09-28. A `forge-mcl` CLI command in the `ark dev start` pattern (`~/progs/ark/docker-helper/docker-helper.go`): plain Docker runs locally built Conversation Host and Worker images against the same real-Azure Storage and Service Bus connection strings Kind uses today; fails fast on missing prerequisites. Replaces Kind for local work. Serves runtime development only: the Desktop uses cloud ForgeAPI until one of the two follow-ups below exists. Until then local work is manual: Kind up, `kubectl port-forward` (namespace `forge-durable`), then set both `MissionRuntime:BaseUrl` and `ConversationRuntime:BaseUrl`. |
| Unauthenticated local runtime mode | Deferred from [Phase 52.1](phases/phase-52.1-cloud-conversations.md), 2026-09-28. A locally launched Host bound to loopback serves the Desktop without a platform key or ownership checks (single local user). Structural guard: exists only in the local launch configuration; a cloud deployment cannot enable it; the Host never accepts a client-supplied `MemberId`. Requires the Host to expose the same message-based routes ForgeAPI exposes. |
| Move `forge login` key issuance to ForgeAPI | Deferred from [Phase 52.1](phases/phase-52.1-cloud-conversations.md), 2026-09-28. Moves platform key issuance and `GrantStartingCreditAsync` out of ForgeUI so ForgeUI is fully read-only for billing and loses `authbilling_db` write access. Removes 52.1's transitional exception. |
| Move Rooms and `forge exec` to the command bus | From [command-bus architecture](design/command-bus-architecture.md), 2026-09-29. ForgeUI and ForgeAPI publish run commands instead of calling forge-runner's HTTP `/run`; then delete `/run` so the runner has one entry point. |
| Extract Rooms as a tier-2 service | From [command-bus architecture](design/command-bus-architecture.md), 2026-09-29. Moves the Rooms context and `rooms_db` out of the ForgeUI edge. |
| API B continuation constraint | Recorded 2026-09-29. Phase 52.1 removes transcript-replay continuation from forge-runner. The spec-bound Claude Code wire ([Phase 42.6 task 5b](phases/phase-42.6-hosted-endpoint-ttfa.md#tasks--status)) can only replay transcripts; resuming it needs its own design against the saved-continuation runner. |
| Desktop Supervisor orphans children on SIGTERM | Found 2026-09-29 during [Phase 52.1 Task 1](phases/phase-52.1-cloud-conversations_completed.md#task-1--forge-runner-absorbs-the-worker): `pkill` of `ForgeMission.Desktop` left `ForgeMission.Desktop.Host` and `ForgeMission.Application.Host` running. Superseded if [Phase 52.3 Pipeless Boot](phases/phase-52.3-pipeless-boot.md) lands first; its parent-death rule must cover SIGTERM. |
| forge-runner image CI identity | Found 2026-09-29: `forge-runner-image.yml` fails at Azure login because the forge-runner repo has no `AZURE_*`/`ACR_*` Actions variables and forge-infra's CI identity likely lacks OIDC federation for this repo (a Phase 50 extraction gap). Runner images are pushed from a workstation until fixed (GitHub repo settings + `150-ci`). |
| Price for `grok-4.5` | Found 2026-09-29 ([52.1 Task 2 record](phases/phase-52.1-cloud-conversations_completed.md#task-2--stateless-one-shot-durable-only-tool-loops)): billing logged no pricing rate for `grok-4.5` and charged the fallback rate on a WebSearch run. Add the rate to the pricing table owned by Billing. |
| ForgeAPI missing `libgssapi_krb5.so.2` | Found 2026-09-29: ForgeAPI logs `Cannot load library libgssapi_krb5.so.2` at startup but serves normally. Likely the Npgsql/Kerberos probe on a slim base image; confirm and either install the library or disable the probe. Moot for the billing DB once Billing is extracted (52.1 Task 3). |
| Billing-only Postgres role | Found 2026-09-29 (52.1 Task 3): Billing connects with the Postgres admin login, which can also reach `rooms_db`. Give Billing a role limited to `authbilling_db`. |
| Local ForgeAPI | Deferred, 2026-09-28. Would let the Desktop use local runtimes with authentication; needs a local platform DB. Not planned. |
| Split Bob (ClientRuntime) out of `forge-desktop` | Deferred from [Phase 52](phases/phase-52-desktop-simplification.md), 2026-09-28. Own repo/package so a TUI can reuse it; keep the capability-dispatch and confirmation seam. |
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
| [Phase 43 — remaining Desktop follow-ups](phases/phase-43-forge-desktop.md#current-routing) | Phase 43's selected work is verified complete (2026-09-07) and its row now sits in [plan_completed.md](plan_completed.md). Its hub's routing table remains the single list of what is still deferred under that number — mission catalog/OCI, rich workbench, human gates. Reselect one explicitly before implementation. |

## External conditions and future design candidates

| Item | Status / pointer |
|------|------------------|
| Grok web-search integration tests | Blocked by exhausted xAI account credit/spend limit, not a code defect. See [Phase 41](phases/phase-41-live-retrieval-scout.md). |
| Multi-agent debate, language-governance process, mechanical guardrails | Deferred design candidates; select and design explicitly before implementation. |
