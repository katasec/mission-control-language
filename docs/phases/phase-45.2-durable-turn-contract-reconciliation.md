# Phase 45.2 — durable-turn contract reconciliation

> **Status:** draft for independent review 2026-09-08. This is an implementation-contract
> alignment to the locked [Phase 45](phase-45-mission-conversations.md) design, not a new design
> phase or an operator-decision request. Parent: [Phase 45.2](phase-45.2-durable-conversation-turns.md).

## Locked ownership and scope correction

This correction applies the accepted schema-5 version model and Phase 46 generic hands path. It
does not change the locked ownership: Projects resolves immutable values; Application grants a live
attachment; Host orders/stores facts; Worker executes only verified package values; Bob alone
executes local hands. It adds no provider/profile choice, Project-reading Worker, public ingress,
credential, catalog, or legacy compatibility expansion.

45.2 is internal Application/Contracts/Host/Worker work. Operator conversation transport belongs to
45.3; author/evaluation transport and UI belongs to 45.4. Visual evidence is N/A here. The normal
zero-argument Desktop journey is explicitly deferred to 45.3/45.4, which must use the published
artifact, normal cloud route, Supervisor Kind bridge, clean-main Host/Worker provenance, and a
disposable Project. Controlled integration is not default-path acceptance.

## Immutable provenance and purposes

Contracts retains `DurableMissionLaunch` as the only package/profile value. It appends:

```text
MissionLaunchProvenance(projectId, missionId, launch: DurableMissionLaunch)
```

Application creates it only by rereading the schema-5 mission, checking the requested version is
the active Approved version (or the exact current Candidate for an evaluation), and verifying its
definition hash, `MissionHandsProfile`, and frozen `DurableMissionPackage`. `MissionVersionLaunch`
remains an Application-internal schema-4 compatibility record and never crosses the durable wire.
No request accepts `ApprovedProfile`: the immutable launch already carries it.

`ConversationPurpose` appends `MissionConversation` and `MissionEvaluation` without changing
historic values. A MissionConversation is an empty pinned operator transcript on creation. A
MissionEvaluation is a hidden one-shot execution/trace holder: never listed as an operator
conversation and never accepts a follow-up.

## Turns, evaluations, and hands

Start creates no turn:

```text
StartMissionConversation(commandId, provenance, title) -> (conversationId, acceptedSequence, status)
SubmitMissionTurn(conversationId, commandId, text) -> (turnId, turnAttemptId, acceptedSequence, status)
RetryMissionTurn(conversationId, turnId, priorAttemptId, commandId) -> new attempt
CancelMissionTurn(conversationId, turnId, turnAttemptId, commandId, reason) -> accepted or terminal no-op
StartMissionEvaluation(commandId, provenance, evaluationCaseId, candidateRevision, definitionHash, input)
```

Host allocates IDs and all sequence/order. One attempt is active per conversation. Its explicit
state is `Queued`, `Running`, `AwaitingHands`, `AwaitingToolResult`,
`AwaitingToolConfirmation`, `CancelRequested`, `Cancelling`, `Succeeded`, `Failed`,
`Interrupted`, or `Cancelled`. New turn submission is rejected through every nonterminal state.
Retry is a new provider execution, never transcript/Core continuation replay. Each turn uses only
the pinned package plus its own input; durable records retain semantic facts/audit trace, never a
prior raw provider/expert transcript.

Existing exact hands records remain unchanged: attachment accepts the exact stored launch, and a
tool result includes conversation, attachment, attempt, command and request IDs. The tool-side
cancel is distinct from a user turn cancel. An evaluation with a hands-bearing profile receives a
fresh exact-profile Application/Bob attachment for its hidden evaluation context; it never inherits
authority from a Project, version, or prior conversation. `NoHands` has no Bob-dispatchable tool.

Evaluation input is copied from the current Project case; callers cannot supply pass policy,
profile, package, or criteria. Completion contains mission/version/case IDs, candidate revision,
definition hash, exact trace origin, observed `Succeeded|Failed`, and bounded answer/reason.
Application calls `EvaluationService.RecordCompletionAsync`; a stale revision/hash is a typed
`VersionChanged` no-write result. Cancellation, interruption, or provider failure records observed
failure with trace/reason; it cannot leave a stranded Pending result.

## Host artifacts, index, cancellation, and compatibility

Host validates package hash/size, serializes the verified bounded launch artifact under its Blob
ownership, stores only its reference plus immutable provenance in checkpoint/index, and supplies
the verified value package to Worker commands. Worker has no Blob, Conversation store, Project,
credential, path, provider-selection, or Bob access. Host owns a Project-keyed MissionConversation
index and trace queries by `(conversationId, turnId, turnAttemptId)`; Application never maintains a
competing conversation list. Logged-in Forge-user/tenant association remains explicitly deferred
and is neither introduced nor inferred by this index.

Turn cancellation is additive and idempotent. Host durably records `CancelRequested`, Worker makes
best-effort cancellation at its provider-call boundary, and the terminal completion wins any race.
The caller receives the existing terminal evidence as a typed no-op; no event is removed. A restart
with ambiguous provider completion becomes `Interrupted`; retry starts a new attempt.

All records/enums/checkpoints are additive and source generated. Historic Janus/ProjectMission
reads, schema 1–4 manifests, `SelectedMission`, `ApprovedMissionLaunches`, and legacy
`/conversations` remain unchanged until the later post-45.3 removal task. This is Type-2 contract
evolution with no new Type-1 conflict.

## Required proof

Focused evidence covers approved/candidate resolver tamper rejection; Project/index isolation;
idempotent create/submit/retry/cancel; exact turn/attempt order and trace range; package artifact
recovery; fresh turn execution/no transcript replay; exact profile attachment, missing-hands
recovery, duplicate/late result refusal, Bob containment; evaluation completion stale/no-write;
provider interruption and cancellation race; historic reads. Completion requires build, full test,
Native AOT, Worker image, Host/Worker deployment evidence, then the later 45.3 default Desktop
proof. Security and Engineering Philosophy are PASS: named single owners, immutable values, no
fallback/registry/knob, explicit recovery, and a structural Host/Worker/Bob boundary.
