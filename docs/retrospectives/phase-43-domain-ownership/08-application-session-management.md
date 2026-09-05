# Concern 8 — Application session management

> **2026-09-06 disposition:** resolved in the [finalized end state](end-state.md#disposition-of-all-fourteen-concerns); implementation follows [43.23](../../phases/phase-43.23-domain-ownership.md). The original inventory below is historical.

Recorded: 2026-09-05. Status: potential responsibility mismatch; review after Project Mission reconstruction reaches a verified baseline. Evidence was inspected on `codex/phase-43-22-reconstruction` through commit `5faf6f2`; recheck locations and behaviour before designing changes. These notes neither approve extraction nor supersede the active reconstruction plan.

## Current responsibilities

Require Project creation/opening before establishing an initial session; bind workspace, selected mission, runtime kind, and conversation/read lifetimes to that session; replace and dispose those lifetimes on a mission switch.

Current location: `src/ForgeMission.ClientRuntime/Transport/ClientRuntimeSessionStore.cs`, including `ClientRuntimeSession` and the Project-only initial-session path.

## Boundary concern

Bob needs an authorized execution context, including workspace scope, permissions, pending confirmations, and cancellation. He should not require knowledge of a Forge Project or selected mission to possess that context. Application sessions currently bundle both responsibilities.

## Later discussion

Separate application-session ownership from local execution-context ownership. Preserve the structural guarantee that startup grants no ambient workspace authority and that abandoned sessions cannot execute later tools. A Project-independent Bob must still require explicit authorized scope. This is not permission to expose arbitrary workspace access. Default-path acceptance: N/A — documentation only.
