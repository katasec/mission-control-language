# Concern 11 — Durable coordination versus mission reasoning

> **2026-09-06 disposition:** resolved in the [finalized end state](end-state.md#disposition-of-all-fourteen-concerns); implementation follows [43.23](../../phases/phase-43.23-domain-ownership.md). The original inventory below is historical.

Recorded: 2026-09-05. Status: ownership review candidate; revisit after Project Mission reconstruction reaches a verified baseline. Evidence inspected through reconstruction commit `5faf6f2`. No new service or migration is approved.

## Responsibilities and present owners

Accept and deduplicate commands, serialize state transitions, persist ordered events and run checkpoints, dispatch work, handle progress delivery, and recover failed attempts. These responsibilities are separate from interpreting a mission, invoking experts/models, and deciding subsequent reasoning steps.

Current locations: `src/ForgeMission.ConversationHost/Grains/`, `Persistence/`, and `Messaging/`; `src/ForgeMission.ConversationWorker/Messaging/`; mission executors under `src/ForgeMission.ConversationWorker/Janus/`; shared execution machinery in Core.

## Boundary concern

The brain metaphor can hide the distinction between mission reasoning and durable workflow coordination. Host and Worker already provide meaningful separation; their existence is not evidence that everything is misplaced. Review whether authoritative run state, worker execution state, and mission-specific decisions each have one owner.

Historical evidence: `951bf73` introduced Table/Blob persistence and Orleans ownership; `3308e3a` introduced Service Bus delivery and Janus execution during Phase 43.16.

## Later discussion

Map command acceptance, retries, checkpoints, delivery, execution, and recovery to their owners. Distinguish authoritative state from projections and transient worker state. Avoid moving remote authority into the client submission journal; [concern 3](03-mission-submission-and-recovery.md) covers that client boundary. Default-path acceptance: N/A — documentation only.
