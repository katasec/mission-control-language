# Phase 52.2 — Trim Orchestration

> **Status: design (2026-09-28).** Starts after [52.1](phase-52.1-cloud-conversations.md).
> Hub: [Phase 52](phase-52-desktop-simplification.md).

**Goal:** delete the `forge-desktop` Orchestration code the cloud default no longer needs, so
[52.3](phase-52.3-pipeless-boot.md) absorbs only resolution and health checks.

## Scope

| Delete | Reason |
|---|---|
| `LocalKindConversationRuntimeTunnel` and the tunnel branch in `ConversationRuntimeBootstrap` | Kind is no longer the default; local dependencies move to the deferred `forge dev start`. |
| `LocalDockerMissionRuntimeLauncher`, `IMissionRuntimeLauncher`, `ProviderEnvironmentFile`, and `MissionRuntime:Mode=docker` | Starting containers belongs to a CLI dev command, not the Desktop. |
| `BuiltinMissionReferences` | Only if unreferenced; confirm before deleting. |

**Keeps:** Mission and Conversation URL resolution (default + override) and the health probe.

## Gates

| Gate | Result |
|---|---|
| Architecture / security | N/A — removes local-only code; no entry point, store, or credential changes. |
| Engineering philosophy | PASS — removes knobs (`Mode=docker`) and a process the Desktop should not own. |
| Default path | Unchanged from 52.1. Remove the Kind rows from [Default-Path Acceptance](../design/default-path-acceptance.md). |

**Done when:** the listed code and its tests are gone; `dotnet build` and `dotnet test` pass; the
52.1 default-path action passes on the published bundle.
