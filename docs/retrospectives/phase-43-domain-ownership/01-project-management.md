# Concern 1 — Project management

> **2026-09-06 disposition:** resolved in the [finalized end state](end-state.md#disposition-of-all-fourteen-concerns); implementation follows [43.23](../../phases/phase-43.23-domain-ownership.md). The original inventory below is historical.

Recorded: 2026-09-05. Status: potential responsibility mismatch; review after Project Mission reconstruction reaches a verified baseline. Evidence was inspected on `codex/phase-43-22-reconstruction` through commit `5faf6f2`; recheck locations and behaviour before designing changes. These notes neither approve extraction nor supersede the active reconstruction plan.

## Current responsibilities

Draft Project titles and home locations; create and open Projects; validate goals, identities, and manifest schemas; maintain Project metadata and serialize manifest updates.

Current locations: `src/ForgeMission.ClientRuntime/Services/ProjectStore.cs`, `ProjectManifest.cs`, `ProjectManifestFile.cs`, and `ProjectManifestJsonContext.cs` in the same directory.

## Boundary concern

The Client Runtime, anthropomorphized as Bob, receives instructions, checks authorization, executes local operations, and reports results. Understanding what constitutes a Forge Project and enforcing its invariants is a separate domain. Local storage does not establish domain ownership.

Bob may perform authorized filesystem operations. Project semantics, schema validation, and transaction rules need a named owner outside his capability-execution responsibility.

## Later discussion

Identify the Project domain owner and its persistence boundary, including how Project opening establishes an authorized workspace. This note records a concern, not an approved extraction design. Default-path acceptance: N/A — documentation only.
