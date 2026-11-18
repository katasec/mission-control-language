# MCL — Implementation Plan

> **Active work only.** Deferred candidates and external conditions are in
> [backlog.md](backlog.md). Finished work is deleted; git history keeps it.

## Now (2026-11-19)

| | |
|---|---|
| **NEXT STEP** | [Phase 79](phases/phase-79-product-focus.md): the per-repo deletes are done in all repos; next is the hosted cleanup through forge-infra (see the spoke's Next line). |

## Active phases

| Phase | Status |
|---|---|
| [Phase 79 — Product focus](phases/phase-79-product-focus.md) | In progress: per-repo deletes done; hosted cleanup next. |

## Design docs

| Doc | Description |
|-----|-------------|
| [Product](design/product.md) | What Forge is, the loop, stage 1, and the test for every keep/delete decision. |
| [How conversations work](design/how-conversations-work.md) | Current conversation design reference. |
| [Backlog](backlog.md) | Deferred candidates, paused work, and external conditions. |
| [UI Design System](design/ui-design-system.md) | Forge UI tokens, themes, reusable primitives, and local-run gotchas. |
| [Architecture](design/architecture.md) | Components, boundaries, dependency flow. |
| [Security Architecture](design/security-architecture.md) | Mandatory design gate. |
| [Command-bus architecture](design/command-bus-architecture.md) | Governing target: Service Bus carries every command; queries direct. |
| [Engineering Philosophy](design/engineering-philosophy.md) | Mandatory design and implementation gate. |
| [Default-Path Acceptance](design/default-path-acceptance.md) | Mandatory real-user configuration and end-to-end acceptance gate. |
| [Deploy Runbook](design/deploy.md) | Operational hosted-app deployment reference. |
