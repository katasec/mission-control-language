# Phase 43 — Forge Desktop completed record

> **Historical reference only.** The active routing hub is
> [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). This file preserves the resolved context
> that used to make that hub too large to scan. Do not treat historical framework plans or their
> then-open questions as active work.

## Completed foundation

- **Tool ownership and workspace boundary:** [43.1](phase-43.1-tool-execution-engine.md) and
  [43.7](phase-43.7-workspace-provider.md) proved Forge executes its own local tools through the
  Client Runtime boundary.
- **Desktop shell foundation:** [43.8](phase-43.8-capability-provider-pattern.md),
  [43.9](phase-43.9-client-runtime-authorization.md),
  [43.10](phase-43.10-transport-contract.md), and
  [43.11](phase-43.11-wasm-photino-shell.md) established capability/authorization/transport
  boundaries and the Photino + Blazor WASM shell. 43.11 was live-verified 2026-08-08.
- **Runtime supervision:** [43.13](phase-43.13-mission-runtime-orchestration.md) moved local
  Mission Runtime resolution out of Client Runtime, with normal-close, SIGTERM, and Docker cleanup
  checks verified 2026-08-04.
- **Cloud and Janus proof:** [43.14](phase-43.14-desktop-cloud-missions.md) is done/live;
  [43.15](phase-43.15-janus-inter-agent-mission.md) proved the mission; and
  [43.16](phase-43.16-janus-desktop-local-poc.md) completed the visible durable Janus Desktop
  proof on 2026-08-16.

## Superseded desktop-shell tracks

- The Avalonia spike is shelved; its evidence is retained in
  [43.2 Avalonia completed record](phase-43.2-avalonia-vanilla-shell_completed.md).
- The Electron + Blazor Server track is superseded by the WASM/Photino split; its history is in
  [43.2 Electron completed record](phase-43.2-electron-forge-desktop-shell_completed.md).
- The canonical decision is not either historical framework choice: it is the three-layer,
  replaceable-client architecture in [Forge Architecture](../design/forge-architecture.md).

## Historical task map

The detailed design, build narrative, verification, and retained open work remain with their
individual spokes. This list is intentionally a reference map rather than an active task table:

| Area | Record |
|---|---|
| Capability/provider/transport foundation | [43.8](phase-43.8-capability-provider-pattern.md), [43.9](phase-43.9-client-runtime-authorization.md), [43.10](phase-43.10-transport-contract.md) |
| Native packaging and lifecycle proof | [43.11](phase-43.11-wasm-photino-shell.md) |
| Mission Runtime resolution and process supervision | [43.13](phase-43.13-mission-runtime-orchestration.md) |
| Cloud mission access | [43.14](phase-43.14-desktop-cloud-missions.md) and its [_completed record](phase-43.14-desktop-cloud-missions_completed.md) |
| Janus mission and durable group chat | [43.15](phase-43.15-janus-inter-agent-mission.md), [43.16](phase-43.16-janus-desktop-local-poc.md), and their task records |

## Previous hub context deliberately removed from active routing

The old parent hub repeated framework-pivot history, the prerequisite checklist, task-by-task
completion claims, and dated open questions. The controlling design is now in
[Forge Architecture](../design/forge-architecture.md), and each task's current truth is in its
own spoke. Keeping that material in the active parent forced a fresh agent to read unrelated
history before it could act; it is retained here only as a guide to the primary records.
