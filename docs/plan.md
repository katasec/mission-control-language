# MCL — Implementation Plan

> **Active work only.** Completed and superseded work is in
> [plan_completed.md](plan_completed.md); deferred candidates and external conditions are in
> [backlog.md](backlog.md). Neither is part of the current plan.

## Now (2026-09-05)

| | |
|---|---|
| **NEXT STEP** | **Phase 43 — Forge Desktop:** Codex begins task 1 of the Project Mission reconstruction from its active spoke. |

## Active phases

| Phase | Description | Status |
|-------|-------------|--------|
| [Phase 43 — Forge Desktop (coding-agent client)](phases/phase-43-forge-desktop.md) | Coding-agent desktop client where **missions attach instead of models**. Canonical architecture: [forge-architecture.md](design/forge-architecture.md). | Reconstruction design ready; implementation pending |
| [Phase 44 — Local development bootstrap integrity](phases/phase-44-local-development-bootstrap.md) | Fresh local ForgeUI startup creates the existing Rooms and Auth & Billing databases without manual PostgreSQL setup. | Design ready; implementation pending |

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
