# Concern 13 — Generic infrastructure versus Janus-specific behaviour

> **2026-09-06 disposition:** resolved in the [finalized end state](end-state.md#disposition-of-all-fourteen-concerns); implementation follows [43.23](../../phases/phase-43.23-domain-ownership.md). The original inventory below is historical.

Recorded: 2026-09-05. Status: potential mission-specific coupling; review after Project Mission reconstruction reaches a verified baseline. Evidence inspected through reconstruction commit `5faf6f2`. This is not a mandate to build a generic framework.

## Observed responsibilities

Shared conversation contracts name roles such as Proposer and Implementer. A Worker mapper translates Janus expert trace facts into conversation events. The client durable tool path checks the participant is Implementer before dispatching. Worker mission resolution selects explicitly packaged missions.

Current locations: `src/ForgeMission.Conversations.Contracts/ConversationContracts.cs`, `src/ForgeMission.ConversationWorker/Janus/JanusPipelineProgressMapper.cs`, `src/ForgeMission.ConversationWorker/Messaging/WorkerMissionResolver.cs`, and `src/ForgeMission.ClientRuntime/Services/ConversationRuntimeSession.cs`.

## Boundary concern

A particular mission's expert names, negotiation structure, and tool-use rules can become assumptions in infrastructure presented as reusable. Bob should ultimately enforce explicit execution authority rather than require knowledge of a particular mission's cast of characters.

These choices originated in a bounded Janus proof. A fixed mission adapter or packaged-mission switch can be appropriate; its presence alone does not prove misplaced responsibility.

## Later discussion

Identify which rules are intentionally Janus-specific and which contracts promise mission independence. Check whether a different mission can use the durable path and local tools without changing generic infrastructure. Preserve existing authorization and delivery guarantees; do not simply delete participant checks. Generalize only at a demonstrated boundary. Default-path acceptance: N/A — documentation only.
