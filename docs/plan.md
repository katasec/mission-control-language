# MCL — Implementation Plan

> **Active work only.** Completed and superseded work is in
> [plan_completed.md](plan_completed.md); deferred candidates and external conditions are in
> [backlog.md](backlog.md). Neither is part of the current plan.

## Now (2026-11-02)

| | |
|---|---|
| **NEXT STEP** | [Phase 77](phases/phase-77-anthropic-client.md): next task. |

## Active phases

| Phase | Status |
|---|---|
| [76 — Unified cloud mission execution](phases/phase-76-unified-cloud-run.md) | Intent re-decided 2026-10-31; durable `forge run` with Hands works; next steps not selected. |
| [77 — forge's own Anthropic client](phases/phase-77-anthropic-client.md) | Replaces tryAGI.Anthropic; removed from the CLI 2026-11-02; hosted rollout next. |

## Design docs

| Doc | Description |
|-----|-------------|
| [How conversations work](design/how-conversations-work.md) | Current conversation design reference. |
| [Backlog](backlog.md) | Deferred candidates, paused work, and external conditions. |
| [Completed / Resolved Archive](plan_completed.md) | Verified completed work and superseded plans. |
| [UI Design System](design/ui-design-system.md) | Forge UI tokens, themes, reusable primitives, and local-run gotchas. |
| [Architecture](design/architecture.md) | Components, boundaries, dependency flow. |
| [Security Architecture](design/security-architecture.md) | Mandatory design gate. |
| [Command-bus architecture](design/command-bus-architecture.md) | Governing target: Service Bus carries every command; queries direct. |
| [Engineering Philosophy](design/engineering-philosophy.md) | Mandatory design and implementation gate. |
| [Desktop Interaction Principles](design/desktop-interaction-principles.md) | Binding visual-reference acceptance for Desktop and ForgeUI changes. |
| [Default-Path Acceptance](design/default-path-acceptance.md) | Mandatory real-user configuration and end-to-end acceptance gate. |
| [Deploy Runbook](design/deploy.md) | Operational hosted-app deployment reference. |
