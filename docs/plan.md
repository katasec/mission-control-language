# MCL — Implementation Plan

> **Active work only.** Completed and superseded work is in
> [plan_completed.md](plan_completed.md); deferred candidates and external conditions are in
> [backlog.md](backlog.md). Neither is part of the current plan.

## Now (2026-10-10)

| | |
|---|---|
| **NEXT STEP** | Settle executable trust and the Host-owned content boundary for [unified cloud mission execution](phases/phase-76-unified-cloud-run.md), then lock the reviewed contracts and plan implementation. |

## Active phases

| Phase | Status |
|---|---|
| [70 — TUI text interaction](phases/phase-70-tui-text-interaction.md) | Rich selection implementation is reviewed; merge, installed-artifact verification and operator manual acceptance remain. |
| [71 — Chat project file paths](phases/phase-71-chat-project-file.md) | Operator accepted same-folder chats; Client/CLI merged and package published; formal default-path provenance pending. |
| [72 — Supervisor comparison](phases/phase-72-supervisor-comparison.md) | Independent native runs verified; operator output review and further comparisons remain open. |
| [76 — Unified cloud mission execution](phases/phase-76-unified-cloud-run.md) | OCI dependency published and accepted; cloud contracts technically reviewed, boundary decisions pending. |

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
