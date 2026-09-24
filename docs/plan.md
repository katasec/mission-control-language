# MCL — Implementation Plan

> **Active work only.** Completed and superseded work is in
> [plan_completed.md](plan_completed.md); deferred candidates and external conditions are in
> [backlog.md](backlog.md). Neither is part of the current plan.

## Now (2026-09-21)

| | |
|---|---|
| **NEXT STEP** | Extract the durable Conversations bounded owner into `forge-conversations`; preserve the packaged consumer and default Desktop/Kind paths. |

## Active phases

| Phase | Description | Status |
|-------|-------------|--------|
| [Repository extraction — Conversations](phases/repository-extraction-conversations.md) | Move the durable Conversations bounded owner into `forge-conversations` and cut consumers over to published packages. | Active: destination bootstrap and owner move. |
| [Phase 45 — Forge Desktop mission conversations](phases/phase-45-mission-conversations.md) | Evolve the existing Desktop Project workspace from one-shot Mission runs into version-pinned, durable Mission Conversations with in-Explorer authoring and evaluation. | Next: live existing-mission chat in the fixed Phase 47 screen. Authoring is accepted work, not the next task. |
| [Phase 46 — domain-ownership remediation](phases/phase-46-domain-ownership-remediation.md) | Establish a repository-wide ownership inventory and remove confirmed duplicate, overlapping, and mission-specific runtime implementations through Codex-supervised work. | Core, hands, generic Worker, and hosted terminal-result remediation accepted; remaining default-path and compatibility cleanup depend on the Phase 45 integration chain. |

## Design docs

| Doc | Description |
|-----|-------------|
| [Backlog](backlog.md) | Deferred candidates, paused work, and external conditions. |
| [Completed / Resolved Archive](plan_completed.md) | Verified completed work and superseded plans. |
| [UI Design System](design/ui-design-system.md) | Forge UI tokens, themes, reusable primitives, and local-run gotchas. |
| [Architecture](design/architecture.md) | Components, boundaries, dependency flow. |
| [Security Architecture](design/security-architecture.md) | Mandatory design gate. |
| [Engineering Philosophy](design/engineering-philosophy.md) | Mandatory design and implementation gate. |
| [Desktop Interaction Principles](design/desktop-interaction-principles.md) | Binding visual-reference acceptance for Desktop and ForgeUI changes. |
| [Default-Path Acceptance](design/default-path-acceptance.md) | Mandatory real-user configuration and end-to-end acceptance gate. |
| [Deploy Runbook](design/deploy.md) | Operational hosted-app deployment reference. |
