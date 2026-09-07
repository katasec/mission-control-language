# Phase 45.2 — Durable conversation turns

> **Status:** implementation-ready design; depends on [45.1](phase-45.1-version-evaluation-contracts.md).

## Task 2 — execute pinned versions as durable conversations and evaluations

### Why and component fit

This advances `Conversations.Contracts` and `ConversationHost`: one durable ordered owner for
commands, turn attempts, recovery and trace evidence. `ConversationWorker` executes/publishes
through its queue boundary without Project access. `Application/Missions` prepares/reconciles one
adapter call; `Application/Runs` exposes bounded projections. No Presentation, Host or Client
Runtime ownership moves.

### Affected components and files

| Component | Planned files |
|---|---|
| Conversations Contracts | `ConversationContracts.cs`, deterministic IDs and source-generated JSON context. |
| Conversation Host | `Grains/ConversationGrain.cs`, `Grains/MissionRunGrain.cs`, typed handlers/projections under `Api/`, persistence entities, Blob launch-artifact adapter, endpoint/recovery tests. |
| Conversation Worker | Queue command consumer, mission execution adapter, progress publisher and Worker tests. |
| Application | `Missions/MissionConversationService.cs`, `Runs/RunHistoryService.cs`, `Adapters/Conversations/ConversationHostClient.cs`, composition interfaces and tests. |
| Transport/Host | Additive transport records/routes, event relay mapping and contract tests. |

### Durable commands, projections, and state machine

Contracts append `MissionConversation` and `MissionEvaluation` purposes; existing enum ordinals and
historic reads stay unchanged. Records are additive:

```csharp
record StartMissionConversationRequest(Guid CommandId, Guid ProjectId,
    MissionVersionLaunch Launch, string Title);
record StartMissionConversationResponse(Guid ConversationId, Guid TurnId,
    Guid TurnAttemptId, long AcceptedSequence, ConversationTurnStatus Status);
record SubmitMissionTurnRequest(Guid ConversationId, Guid TurnId, Guid CommandId, string Text);
record RetryMissionTurnRequest(Guid ConversationId, Guid TurnId, Guid PriorAttemptId, Guid CommandId);
record CancelMissionTurnRequest(Guid ConversationId, Guid TurnId, Guid TurnAttemptId, Guid CommandId);
record GetMissionConversationRequest(Guid ConversationId);
record ReadMissionConversationsRequest(Guid ProjectId, MissionConversationCursor? Cursor);
record ReadMissionTurnTraceRequest(Guid ConversationId, Guid TurnId, Guid TurnAttemptId,
    long AfterSequence, long? ThroughSequence);
record StartMissionEvaluationRunRequest(Guid CommandId, Guid ProjectId, Guid EvaluationCaseId,
    MissionVersionLaunch Launch, EvaluationInput Input);
```

`MissionConversationSnapshot` projects ID, title, immutable launch, latest summary, active
turn/attempt and timestamp. `MissionTurnSnapshot` projects stable TurnId, message, ordered
attempts, status, answer/reason, compact evidence and TraceOrigin. `MissionEvaluationCompletion`
reports terminal answer/reason/origin to Application; `EvaluationService` alone persists/evaluates
the result.

A picker may create an empty conversation; submit then creates turn 1. Submit appends user message
before queuing one command and uses the pinned launch for every turn. One active attempt is allowed;
a new message is rejected while Queued, Running, CancelRequested or Cancelling. Presentation disables
its composer from this projection, never a local guess.

| Status | Meaning / permitted next action |
|---|---|
| Queued, Running | One active attempt; chronological progress; cancel permitted. |
| Succeeded | Answer/exact trace durable; next user message permitted. |
| Failed, Interrupted, Cancelled | Message, partial trace and terminal reason stay durable; retry same turn or send next turn permitted. |
| CancelRequested, Cancelling | Durable cancel accepted; composer stays disabled until observed terminal outcome. |

Cancellation is best effort through Worker-owned cancellation/process boundary. Request and observed
result are durable; no earlier event is removed or rollback implied. Host/Worker restart turns
uncertain provider work into Interrupted. Retry creates a new attempt—not resume. Evaluation uses
same state machine but cannot appear in operator conversation lists or receive follow-ups.

### Trace, artifact, failure, and security contracts

Host verifies launch hash, writes the bounded definition snapshot under its Blob ownership, persists
reference/hash, and puts verified content on the session-bound command. Worker cannot fetch a Project
file or Blob. Progress contains ConversationId, TurnId/TurnAttemptId or EvaluationCaseId, RunId and
stable EventId; Host assigns sequence. TraceOrigin is stored with terminal attempt/result and queried
by origin tuple—not reconstructed from generic run list.

| Boundary | Explicit behaviour / recovery | Required negative evidence |
|---|---|---|
| Duplicate/lost command response | Stable CommandId returns original acceptance; Application reconciles. | Drop response after acceptance; one conversation/attempt. |
| Worker failure/Host restart | Idempotent command/progress replay; uncertain call becomes Interrupted. | Restart mid-attempt; no silent second provider execution. |
| Cancel racing completion | Terminal completion wins; typed conflict/no-op exposes completion evidence. | Completion/cancel race. |
| Invalid launch snapshot | Hash/size validation precedes Blob/queue; reject without execution. | Bad hash, oversize, unknown version. |
| Foreign Project/conversation | Application validates session/Project binding; Host validates trusted Project identity. | Foreign query/refusal/no event leak. |

Conversation Host stays Tier 2 and sole store writer/sequence allocator. Worker keeps queue-only
access; no Project or Conversation-store permission. Application Host remains edge with no
data-plane credential. There is no new capability declaration/public ingress. JSON contexts are
source-generated and no reflection/suppression is introduced.

### Default path, verification, and done when

Use published zero-argument Desktop with absent overrides, normal cloud Mission route,
Supervisor-owned Kind bridge and disposable Project with approved version. Prove create/open
conversation, two turns, reopen, exact trace/back, controlled failure/retry and running cancel.
Record artifacts, IDs, sequences, defaults and outcomes; emulator/injected endpoint/synthetic event
tests are controlled evidence only.

- Contract/serialization: additive records/no Host-Worker leakage.
- Grain/persistence/queue: sequence order, idempotency, recovery, hash/origin query.
- Worker: one execution per attempt and cancellation containment.
- Surface-neutral Application transport: all commands/queries/refusals.
- Visual: N/A—existing renderer reuse.

**Done when:** later publication cannot alter a pinned launch; turns/attempts/failure/retry/cancel and
evaluation traces are durable/exact/recoverable; named controlled checks pass; default path evidence
passes; and Codex accepts completion.
