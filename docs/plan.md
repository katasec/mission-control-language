# MCL — Implementation Plan

> **Active work only.** Completed and superseded work is in
> [plan_completed.md](plan_completed.md); deferred candidates and external conditions are in
> [backlog.md](backlog.md). Neither is part of the current plan.

## Now (2026-09-17)

| | |
|---|---|
| **NEXT STEP** | Make the GitHub Actions Desktop bundle the standard cross-platform build, download it unchanged for local Windows/macOS acceptance, and finish the MAUI Host spike. |

## Active phases

| Phase | Description | Status |
|-------|-------------|--------|
| [Phase 45 — Forge Desktop mission conversations](phases/phase-45-mission-conversations.md) | Evolve the existing Desktop Project workspace from one-shot Mission runs into version-pinned, durable Mission Conversations with in-Explorer authoring and evaluation. | Authoring, evaluation, publication, and pinned-conversation launch are accepted; eventual Mission Chat integration follows the separately scoped Phase 47 UI prototype. |
| [Phase 46 — domain-ownership remediation](phases/phase-46-domain-ownership-remediation.md) | Establish a repository-wide ownership inventory and remove confirmed duplicate, overlapping, and mission-specific runtime implementations through Codex-supervised work. | Core, hands, generic Worker, and hosted terminal-result remediation accepted; remaining default-path and compatibility cleanup depend on the Phase 45 integration chain. |
| [Phase 48 — MAUI Desktop Host spike](phases/phase-48-maui-desktop-host-spike.md) | Replace the Windows Photino native shell with a thin MAUI WebView host while preserving the existing loopback Application Host and Presentation UI. | MAUI implementation and a standard GitHub Actions desktop bundle are in progress; default-path acceptance follows downloaded artifacts. |

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
