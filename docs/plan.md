# MCL — Implementation Plan

> **Active work only.** Completed and superseded work is in
> [plan_completed.md](plan_completed.md); deferred candidates and external conditions are in
> [backlog.md](backlog.md). Neither is part of the current plan.

## Now (2026-11-14)

| | |
|---|---|
| **NEXT STEP** | [Phase 79](phases/phase-79-product-focus.md): incremental deletes, one repo at a time; repos 1–7 done (~38.6k lines, ~37%, deleted; forge-infra is now Terraform with gitops); mission-control-language (repo 8, decision D11) is next. |

## Active phases

| Phase | Status |
|---|---|
| [Phase 79 — Product focus](phases/phase-79-product-focus.md) | In progress: step 4, incremental deletes. Repos 1–7 done; mission-control-language (repo 8) next. |

## Design docs

| Doc | Description |
|-----|-------------|
| [Product](design/product.md) | What Forge is, the loop, stage 1, and the test for every keep/delete decision. |
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
