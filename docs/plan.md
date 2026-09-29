# MCL — Implementation Plan

> **Active work only.** Completed and superseded work is in
> [plan_completed.md](plan_completed.md); deferred candidates and external conditions are in
> [backlog.md](backlog.md). Neither is part of the current plan.

## Now (2026-09-29)

| | |
|---|---|
| **NEXT STEP** | [Phase 53 — forge-client and the `forge` CLI](phases/phase-53-forge-client.md): choose the next `forge chat` step with Ameer (chat list and mission switching, token streaming, or Markdown). |

## Active phases

| Phase | Description | Status |
|-------|-------------|--------|
| [Phase 53 — forge-client and the `forge` CLI](phases/phase-53-forge-client.md) | Extract the Desktop's client core and Bob into a shared `forge-client` repo; then the `forge chat` TUI for cloud conversations in the terminal. | 🔵 forge-client, conversation memory, the `forge chat` TUI and naked Claude default done; next step to choose. |
| [Phase 46 — domain-ownership remediation](phases/phase-46-domain-ownership-remediation.md) | Establish a repository-wide ownership inventory and remove confirmed duplicate, overlapping, and mission-specific runtime implementations through Codex-supervised work. | ⏸ **On hold — review after the reorg before starting.** |

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
