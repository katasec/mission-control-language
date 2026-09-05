# Concern 3 — Mission submission and recovery

> **2026-09-06 disposition:** resolved in the [finalized end state](end-state.md#disposition-of-all-fourteen-concerns); implementation follows [43.23](../../phases/phase-43.23-domain-ownership.md). The original inventory below is historical.

Recorded: 2026-09-05. Status: potential responsibility mismatch; review after Project Mission reconstruction reaches a verified baseline. Evidence was inspected on `codex/phase-43-22-reconstruction` through commit `5faf6f2`; recheck locations and behaviour before designing changes. These notes neither approve extraction nor supersede the active reconstruction plan.

## Current responsibilities

Prepare immutable commands; check for active runs; create or resolve remote Project Mission containers; submit missions; reconcile receipts; record acceptance or rejection; retry uncertain submissions using the existing command identity.

Current locations: `src/ForgeMission.ClientRuntime/Services/ProjectMissionApplication.cs` and submission state/transition logic in `ProjectStore.cs` and `ProjectManifest.cs` in the same directory.

## Boundary concern

This coordinates a product workflow across local persistence and a remote service. It is not local capability execution. `ProjectMissionApplication` explicitly has no capability dependency.

## Later discussion

Name the application owner for submission and recovery, preserving immutable command identity and uncertain-outcome handling. The remote Conversation domain remains authoritative for durable run acceptance and execution state; extraction must not duplicate that authority. Bob's operation/result delivery is a separate failure boundary. No relocation or new process has been approved. Default-path acceptance: N/A — documentation only.
