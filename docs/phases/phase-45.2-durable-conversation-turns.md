# Phase 45.2 — Durable conversation turns

> **Status:** corrective design in progress 2026-09-08; implementation is not assigned until the
> accepted 45.1 vocabulary and durable turn contract below are reconciled. Depends on accepted
> [45.1](phase-45.1-version-evaluation-contracts.md).

> **Correction authority:** [Phase 45.2 durable-turn contract reconciliation](phase-45.2-durable-turn-contract-reconciliation.md)
> supersedes the stale launch/profile, transport, default-path, and undefined-type material in this
> card. It is a required implementation boundary, not a decision left to an implementer.

## Task 2 — execute pinned versions as durable conversations and evaluations

### Why and component fit

This advances `Conversations.Contracts` and `ConversationHost`: one durable ordered owner for
commands, turn attempts, tool request/result facts, recovery and trace evidence.
`ConversationWorker` executes/publishes through its queue boundary without Project access or Bob.
`Application/Missions` prepares/reconciles one adapter call and binds a live conversation/session
attachment; Bob enforces/executes only through that attachment. `Application/Runs` exposes bounded
projections. No Presentation, Host or Client Runtime ownership moves.

### Affected components and files

| Component | Planned files |
|---|---|
| Conversations Contracts | `ConversationContracts.cs`, deterministic IDs and source-generated JSON context. |
| Conversation Host | `Grains/ConversationGrain.cs`, `Grains/MissionRunGrain.cs`, typed handlers/projections under `Api/`, persistence entities, Blob launch-artifact adapter, endpoint/recovery tests. |
| Conversation Worker | Queue command consumer, mission execution adapter, progress publisher and Worker tests. |
| Application / ClientRuntime | `Missions/MissionConversationService.cs`, Sessions attachment lifecycle, `Runs/RunHistoryService.cs`, `Adapters/Conversations/ConversationHostClient.cs`, profile-bound Bob session/containment adapter, composition interfaces and tests. |
| Transport/Host | Additive transport records/routes, event relay mapping and contract tests. |

### Durable commands, projections, and state machine

Contracts append `MissionConversation` and `MissionEvaluation` purposes; existing enum ordinals and
historic reads stay unchanged. Records are additive:

```csharp
record StartMissionConversationRequest(Guid CommandId, Guid ProjectId,
    MissionVersionLaunch Launch, MissionCapabilityProfile ApprovedProfile, string Title);
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
record AttachMissionHandsRequest(Guid ConversationId, Guid AttachmentId,
    Guid ApplicationSessionId, MissionCapabilityProfile ApprovedProfile);
record DetachMissionHandsRequest(Guid ConversationId, Guid AttachmentId);
record SubmitMissionToolResultRequest(Guid ConversationId, Guid TurnAttemptId, Guid CommandId,
    Guid ToolRequestId, MissionToolOutcome Outcome, string? Content, string? Reason);
```

`ApprovedProfile` must equal the immutable profile in `Launch`; Host rejects a mismatch before
creating durable state or queuing work. `AttachMissionHandsRequest` is an ephemeral liveness
registration, not a capability handle: Host retains only `(conversation, attachment, session,
profile)` to route an outstanding durable request back through Application. Application creates it
only after the approved version/profile is bound to a fresh Bob session, and removes it on session
disposal. Worker never sees an attachment ID, Bob, Project root, credential or local capability.

`MissionConversationSnapshot` projects ID, title, immutable launch, latest summary, active
turn/attempt and timestamp. `MissionTurnSnapshot` projects stable TurnId, message, ordered
attempts, status, answer/reason, compact evidence and TraceOrigin. `MissionEvaluationCompletion`
reports terminal answer/reason/origin to Application; `EvaluationService` alone persists/evaluates
the result.

A picker may create an empty conversation; submit then creates turn 1. Submit appends user message
before queuing one command and uses the pinned launch for every turn. One active attempt is allowed;
a new message is rejected while Queued, Running, CancelRequested or Cancelling. Presentation disables
its composer from this projection, never a local guess.

One active tool request is allowed per turn attempt. `MissionToolRequest` contains deterministic
`ToolRequestId`, root mission path, agent location, provider tool-call ID, tool name/arguments and
opaque Core continuation reference; no local path, credential or Bob reference is serialised.
Worker derives its ID from `(conversation_id, turn_attempt_id, tool_ordinal)`, persists the pause
and ordinal before publishing, and Host assigns canonical sequence on acceptance. A result carries
the same request ID and is accepted once; its ordered trace position is the attempt/ordinal plus
the Host sequence. Core alone resumes the opaque root continuation. A child mission has no direct
tool dispatcher or implicit inheritance; its allowed request pauses through the root profile scope.

| Status | Meaning / permitted next action |
|---|---|
| Queued, Running | One active attempt; chronological progress; cancel permitted. |
| AwaitingHands | A valid durable tool request has no live Application/Bob attachment. The request/sequence remain durable; no remote fallback, local bypass or synthetic success occurs. Reconnect may attach fresh hands under the same approved profile. |
| AwaitingToolResult, AwaitingToolConfirmation | Exactly one correlated request is with Bob or its fixed confirmation rule. A confirmation is durable status, not a profile or permission dial. |
| Succeeded | Answer/exact trace durable; next user message permitted. |
| Failed, Interrupted, Cancelled | Message, partial trace and terminal reason stay durable; retry same turn or send next turn permitted. |
| CancelRequested, Cancelling | Durable cancel accepted; composer stays disabled until observed terminal outcome. |

Cancellation is best effort through Worker-owned cancellation/process boundary. Request and observed
result are durable; no earlier event is removed or rollback implied. Host/Worker restart turns
uncertain provider work into Interrupted. Retry creates a new attempt—not resume. Evaluation uses
same state machine but cannot appear in operator conversation lists or receive follow-ups.

`MissionToolOutcome` is append-only and explicit: `Succeeded`, `DeniedOutOfProfile`,
`DeniedByPolicy`, `DeniedByOperator`, `Cancelled`, and `Failed`. `ConfirmationRequired` is a
nonterminal generic status recorded by Host; an Application.Transport confirmation decision is
routed to the same Bob attachment, which alone then executes or returns a terminal outcome.
Ordinary operations inside `ProjectWorkspaceAndTerminal` do not acquire a command-text
"dangerousness" prompt; they proceed under Bob's structurally bounded profile. A separate user
turn cancellation terminalizes the attempt and does not resume its pending Core pause.

### Trace, artifact, failure, and security contracts

Host verifies launch hash/profile, writes the bounded definition snapshot under its Blob ownership,
persists reference/hash/profile and puts verified content/profile-limited generic tool declarations
on the session-bound command. Worker cannot fetch a Project file or Blob and has no filesystem,
terminal, network, credential or Bob access. Progress contains ConversationId, TurnId/TurnAttemptId
or EvaluationCaseId, RunId and stable EventId; Host assigns sequence. TraceOrigin is stored with
terminal attempt/result and queried by origin tuple—not reconstructed from generic run list.

Application is the authority boundary: it verifies the durable pinned profile before creating a
fresh hands attachment for the live session, binds that session to Bob, and translates only Bob's
typed status/result through Application.Transport. Bob's `ProjectWorkspace` root containment
applies to every Project profile; its terminal profile must use structural filesystem, environment,
process and network containment. A current working directory or command filter is not sufficient.
`NoHands` provides no Bob-dispatchable mission tools. Out-of-profile paths, host authority,
credentials and network access are typed denials, never confirmation prompts.

| Boundary | Explicit behaviour / recovery | Required negative evidence |
|---|---|---|
| Duplicate/lost command response | Stable CommandId returns original acceptance; Application reconciles. | Drop response after acceptance; one conversation/attempt. |
| Worker failure/Host restart | Idempotent command/progress replay; uncertain call becomes Interrupted. | Restart mid-attempt; no silent second provider execution. |
| Cancel racing completion | Terminal completion wins; typed conflict/no-op exposes completion evidence. | Completion/cancel race. |
| Invalid launch snapshot | Hash/size validation precedes Blob/queue; reject without execution. | Bad hash, oversize, unknown version. |
| Profile/approval/attachment mismatch | Application and Host require exact launch/profile equality; no mismatch creates a Bob attachment or durable tool grant. | Tampered profile, wrong session/conversation and reconnect-to-different-version refusal. |
| Missing hands | Host records `AwaitingHands` after the ordered request and does not queue a remote/local fallback. | Detach before request; reconnect attaches fresh bounded Bob and resumes the same request once. |
| Tool correlation or replay | Host accepts one result only when conversation, attempt and request ID match its outstanding fact; Worker persists pause/outbox before send. | Wrong/duplicate/late result has typed conflict/no-op and no second continuation/effect. |
| Out-of-profile / containment violation | Bob returns typed denial; no Presentation approval prompt appears. | `NoHands`, root escape, terminal-on-workspace-only, network and credential denial observations. |
| Foreign Project/conversation | Application validates session/Project binding; Host validates trusted Project identity. | Foreign query/refusal/no event leak. |

Conversation Host stays Tier 2 and sole store writer/sequence allocator. Worker keeps queue-only
access; no Project or Conversation-store permission, and no local hands authority. Application Host
remains edge with no data-plane credential. The profile is a versioned declaration rather than a
new public capability ingress. JSON contexts are source-generated and no reflection/suppression is
introduced.

### Default path, verification, and done when

Use published zero-argument Desktop with absent overrides, normal cloud Mission route,
Supervisor-owned Kind bridge and disposable Project with approved version. Prove create/open
conversation, visible exact-profile approval, an in-root bounded operation, two turns, reconnect
through `AwaitingHands`, an explicit denial, exact trace/back, controlled failure/retry and running
cancel. Record artifacts, IDs, profile, request correlations, sequences, defaults and outcomes;
emulator/injected endpoint/synthetic event tests are controlled evidence only.

- Contract/serialization: additive records/no Host-Worker or Worker-Bob leakage; historic Janus
  reads remain valid.
- Core/Worker/Host: nested root pause, sequence order, one-outstanding-request, correlation,
  idempotency, restart/recovery, hash/profile/origin query and no replay.
- Application/Bob: exact-profile acceptance, fresh attachment/reconnect, `NoHands`, root escape,
  network/credential denial and structural terminal containment.
- Surface-neutral Application transport: all create/attach/status/confirmation/result actions,
  queries and refusals.
- Visual: N/A—existing renderer reuse.

**Done when:** later publication cannot alter a pinned launch/profile; turns/attempts/tool
request-result/failure/retry/cancel and evaluation traces are durable/exact/recoverable; Bob is the
only local authority; named controlled checks pass; default-path evidence passes; and Codex accepts
completion.
