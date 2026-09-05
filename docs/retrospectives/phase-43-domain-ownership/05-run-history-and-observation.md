# Concern 5 — Run history and observation

Recorded: 2026-09-05. Status: potential responsibility mismatch; review after Project Mission reconstruction reaches a verified baseline. Evidence was inspected on `codex/phase-43-22-reconstruction` through commit `5faf6f2`; recheck locations and behaviour before designing changes. These notes neither approve extraction nor supersede the active reconstruction plan.

## Current responsibilities

Retrieve run lists, run details, and events; paginate history; maintain bounded read state; follow replayable progress streams; reconnect and invalidate or refresh client views.

Current locations: `src/ForgeMission.ClientRuntime/Services/ProjectMissionReadSession.cs`, `ProjectRunReadState.cs`, and `ConversationTailReader.cs`.

## Boundary concern

Observing a Project's run history is separate from performing local operations. Bob does need execution status and reliable receipt/delivery of his own work, but that does not make him the owner of all conversation history and progress.

## Later discussion

Name the client read/subscription owner while retaining remote authority for durable history. `ConversationTailReader` serves both observation and tool-delivery paths: separate their ownership carefully rather than removing required delivery guarantees. Preserve cancellation, replay cursors, and bounded reads. No independent service is implied. Default-path acceptance: N/A — documentation only.
