# Concern 2 — Mission selection

> **2026-09-06 disposition:** resolved in the [finalized end state](end-state.md#disposition-of-all-fourteen-concerns); implementation follows [43.23](../../phases/phase-43.23-domain-ownership.md). The original inventory below is historical.

Recorded: 2026-09-05. Status: potential responsibility mismatch; review after Project Mission reconstruction reaches a verified baseline. Evidence was inspected on `codex/phase-43-22-reconstruction` through commit `5faf6f2`; recheck locations and behaviour before designing changes. These notes neither approve extraction nor supersede the active reconstruction plan.

## Current responsibilities

Expose the allowed Project missions; validate a selected mission; persist selection and enforce restrictions on changing it.

Current locations: `src/ForgeMission.ClientRuntime/Services/ProjectMissions.cs`, `ProjectStore.cs`, and `ProjectWorkbenchService.cs` in the same directory. The local catalog wrapper delegates to shared `ProjectMissionNames` contracts.

## Boundary concern

Bob executes authorized instructions. He should not own which mission a Project selects or the product rules governing that choice. Advertising available local capabilities is a different responsibility and remains relevant to Bob.

## Later discussion

Determine whether selection belongs wholly to Project management or warrants a distinct mission-catalog/application concern. Separate mission identity and availability from local execution capability. This note records a concern, not a decision to introduce another service. Default-path acceptance: N/A — documentation only.
