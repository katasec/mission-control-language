# MCL — Implementation Plan

> **Active work only.** Completed and superseded work is in
> [plan_completed.md](plan_completed.md); deferred candidates and external conditions are in
> [backlog.md](backlog.md). Neither is part of the current plan.

## Now (2026-10-01)

| | |
|---|---|
| **NEXT STEP** | Under selection — Ameer will choose the next phase. |

## Active phases

| Phase | Description | Status |
|-------|-------------|--------|
| [Phase 53 — forge-client and the `forge` CLI](phases/phase-53-forge-client.md) | Extract the Desktop's client core and Bob into a shared `forge-client` repo; then the `forge chat` TUI for cloud conversations in the terminal. | 🔵 All spokes done; next step under selection. |

## Design docs

| Doc | Description |
|-----|-------------|
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
