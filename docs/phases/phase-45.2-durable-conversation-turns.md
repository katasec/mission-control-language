# Phase 45.2 — Version-to-durable conversation integration

> **Status:** accepted, 2026-09-08. Implementation evidence: [completed 45.2](phase-45.2-durable-conversation-turns_completed.md).
> Depends on accepted
> [45.1](phase-45.1-version-evaluation-contracts.md) and accepted Phase 46.2 Tasks
> [A](phase-46.2-task-a-core-continuation.md), [B](phase-46.2-task-b-profile-hands.md), and
> [C](phase-46.2-task-c-generic-worker-execution.md). It replaces the stale pre-Phase-46
> durable-runtime design; it does not reopen the locked Phase 45 product decisions.

## Task 2 — admit authored versions to the generic durable path

### Why and component fit

This task connects the Project-owned schema-5 mission/version model to the accepted generic durable
runtime. It advances Application's local Project-facing use cases and typed conversation adapter:
Application resolves one immutable version value and coordinates one bounded request. It does not
make Application a durable-state owner, a Worker executor, or a local-capability authority.

The accepted Phase 46 path is retained unchanged:

| Owner | Existing responsibility that this task composes |
|---|---|
| Core | Generic root-scoped tool pause and opaque continuation. |
| Client Runtime (Bob) | Closed-profile local capability authorization, containment, execution, confirmation, cancellation, audit, and cleanup. |
| Conversation Host | Immutable-package admission, durable command/turn order, trace facts, attachment correlation, recovery, indexes, and sequence allocation. |
| Conversation Worker | One deployment-owned provider runner, generic immutable-package execution, and restartable progress publication. |
| Application / Projects | Schema-5 authored mission lifecycle, frozen package, evaluation cases/results, and the only Project-manifest transaction. |
| Application / Missions | Approved/Candidate resolution, durable request/reconciliation, and typed projection for later surfaces. |

No Janus/Naive resolver, executor, persona mapper, Worker catalog, provider/profile selection,
Project-reading Worker, Bob-to-Worker bridge, or second tool/continuation path may be introduced.
Those compiled Worker branches were removed by Phase 46.2 Task C.

### Pre-implementation contract closure — 2026-09-08

The accepted generic Host run remains the only execution rail. Its existing `RunId` carries the
generic execution attempt; for the new durable projections it is the `TurnAttemptId`. The following
additive Host-owned contract closes the missing integration shapes without changing a tier, store,
or authority boundary:

| Need | Locked contract and owner |
|---|---|
| Persistent operator conversation | Append `MissionConversation` to `ConversationPurpose`. A typed create command carries only `projectId`, idempotent `commandId`, and `DurableMissionLaunch`; Host validates/adopts the immutable launch, writes the empty conversation checkpoint, and owns a Project-keyed conversation index. The index is a Host-owned, rebuildable directory projection in the existing Conversation context/storage, never a Project store or a transcript copy: directory recovery rechecks each referenced conversation checkpoint and removes/retries incomplete entries. It returns the conversation ID and pinned launch/profile. No initial turn or Bob attachment is created. |
| Turn lifecycle | Typed submit carries `conversationId`, idempotent command ID, and message text. Host allocates a new `TurnId` and `TurnAttemptId`; typed retry carries the stored `TurnId` and a new idempotent command ID, and Host allocates the next attempt. Typed cancel names the stored conversation/turn/attempt and is idempotent. The Host derives the package/profile from its pinned launch; no caller can supply a version, package, profile, tool, or attachment. |
| Evaluation execution | Append `Evaluation` to `ConversationPurpose`. A typed Host-only evaluation create command carries the Project/version/case provenance, exact candidate revision/hash, immutable launch, and case input. Host retains the Candidate's declared profile as provenance but constructs a fixed `NoHands` execution launch from that same immutable package; it creates no Bob attachment and exposes no file or terminal tool declarations. Host creates a hidden one-shot conversation, owns its evaluation index and terminal projection, and never returns it from the operator-conversation list. It uses the same generic start/progress path, with no follow-up. Disposable workspace/terminal evaluation is a separately designed future capability. |
| Pending and reconciliation | `Application/Projects` atomically creates one `EvaluationResult` in `Pending` before Host admission; pending outcome, output, trace, and completion fields are null. On definitive Host rejection, or by an Application/Missions reconciliation query of the Host-owned evaluation projection, `MissionConversationService` asks the sole Project manifest writer to reconcile that same result identity to terminal `Passed`/`Failed`. An execution that began has an exact trace origin; a pre-admission validation rejection has no trace origin and its typed reason makes that absence explicit. An uncertain admission remains Pending and is reconciled; it is never fabricated as a pass. |
| Compatibility | Existing `MissionRun`, `ProjectMission`, Project-run index/routes, and legacy `/conversations` read shapes remain unchanged. The new purpose/projections are additive; existing checkpoints retain their ordinal/default meanings. |

`MissionLaunchProvenance(projectId, missionId, launch)` is Application-internal only. It is
constructed by rereading the manifest inside the existing Project transaction. Durable contracts
receive no Project path; Host persists only the `projectId` value, immutable launch, and the
evaluation metadata needed for its own index/projection. The Worker sees only the already accepted
generic command/package, and Bob remains attached only after the operator's exact-profile
acknowledgement.

### Admission and command contract

`DurableMissionLaunch` is the sole durable package/profile value. The legacy
`MissionVersionLaunch` compatibility record and the removed `MissionCapabilityProfile` do not
cross a durable contract. Application constructs a `MissionLaunchProvenance` only by rereading the
Project manifest under the existing Project transaction and resolving one of these allowed inputs:

```text
MissionLaunchProvenance
  projectId: Guid, missionId: Guid, launch: DurableMissionLaunch
```

| Operation | Allowed version | Required immutable facts | Rejection / no-write result |
|---|---|---|---|
| Create operator conversation | Current active **Approved** version | Project, mission, version, frozen `DurableMissionPackage`, package hash, and `MissionHandsProfile`. | Missing/inactive/superseded/tampered version is rejected before Host admission or Bob attachment. |
| Submit/retry/cancel a turn | Conversation's Host-stored launch only | Host-owned pinned package/profile and stable command/turn/attempt IDs. | A caller cannot substitute a version, profile, package, tool, or attachment. |
| Start evaluation | Exact current **Candidate** version and current case revision | Project, mission/version/case IDs, candidate revision, definition hash, frozen package/profile, and case input. | Stale candidate/case/hash returns `VersionChanged`; the Project manifest writer makes no change. |

Creating an operator conversation pins an empty transcript and creates no turn. Submission causes
Host allocation of `TurnId`, `TurnAttemptId`, and canonical sequence. Retry creates a new attempt;
it never replays a provider transcript or Core continuation. Cancel is an idempotent durable request
whose completion race returns the stored terminal evidence as a typed no-op. The Host, not
Application or Presentation, owns the Project-keyed conversation index and the query by
`(conversationId, turnId, turnAttemptId)`.

An evaluation is a hidden one-shot durable execution. It uses the same immutable package and
generic turn/progress path but is not listed as an operator conversation and accepts no follow-up.
Its terminal completion carries mission/version/case IDs, candidate revision, definition hash,
observed `Succeeded|Failed`, bounded answer/reason, and exact trace origin. Application alone calls
the Project-owned reconciliation method; stale completion is a typed no-write result. Provider
failure, interruption, and cancellation record an observed failure with its trace/reason, never a
stranded `Pending` result.

### Hands, recovery, and trace

The version's frozen `MissionHandsProfile` is the whole grant vocabulary. Application requires the
operator acknowledgement before it creates a fresh session-bound Bob attachment. Host persists only
the exact launch/attachment correlation and routes a requested tool through Application; Worker
never receives a Project root, attachment, Bob handle, credential, filesystem, terminal, network,
or capability authority.

| Boundary event | Owner and durable outcome | Recovery / negative proof |
|---|---|---|
| No live attachment | Host records `AwaitingHands` after the ordered request; no fallback or synthetic success. | A fresh same-profile attachment may redeliver the same request once. |
| Wrong, duplicate, late, or cross-conversation result | Host accepts no fact, continuation, or effect. | Typed conflict/no-op; exact correlation tests. |
| Out-of-profile or containment escape | Bob returns a typed denial. | No confirmation or escalation; `NoHands`, root/symlink, terminal, network, and credential negatives. |
| Provider uncertainty or restart | Worker/Host record `Interrupted`; the uncertain call is not replayed. | Retry starts a new attempt. |
| Cancel racing completion | Host's terminal completion wins. | Caller receives the stored terminal evidence; no event is removed. |

`TraceOrigin` remains an exact Host-owned origin tuple. It is queried by the stored conversation,
turn, and attempt IDs, never reconstructed from a generic run list. Existing sequence/order,
outbox, package-validation, and opaque-continuation proofs from Phase 46 remain the authoritative
generic-runtime evidence; this task proves their correct use from schema-5 Project facts.

### Scope, security, and implementation boundary

The expected implementation is limited to `Application/Projects` and `Application/Missions` version
resolution/evaluation reconciliation, the named `ConversationHostClient` adapter and composition
interfaces, plus only the additive Contracts/Host fields or projections necessary to carry the
schema-5 provenance and evaluation completion. Presentation/transport routes are 45.3 and 45.4.
The Worker, Core pause protocol, generic executor, deployment provider binding, and Bob policy are
accepted prerequisites—not work to duplicate or refactor here.

Security Architecture is **PASS**: Application remains the only Project-file/manifest owner;
Conversation Host remains the sole Conversation Table/Blob writer and sequence allocator; Worker
keeps queue/provider scope only; Application Host has no data-plane credential; Bob alone holds
local capability authority. This is additive Type-2 integration over the already-locked Type-1
boundaries. There is no new public ingress, datastore owner, credential grant, or cross-store query.

Engineering Philosophy is **PASS**: one Project resolver, one Host index/order owner, one generic
Worker path, and one Bob authority boundary. Immutable values replace mutable references; all
admission, stale-result, retry, cancellation, and missing-hands outcomes are typed. No registry,
dispatcher, wrapper stack, profile knob, fallback, or speculative compatibility lane is allowed.

### Verification and allocation of acceptance

Controlled integration evidence must cover:

- Approved/Candidate resolution, profile/package/hash tamper refusal, and Project/conversation
  isolation;
- idempotent create/submit/retry/cancel, exact turn/attempt order, and trace-origin query;
- evaluation completion recording, stale candidate/case/hash no-write, and terminal failure
  reconciliation;
- exact attachment/profile acknowledgement, `AwaitingHands` reconnect, wrong/duplicate/late result
  refusal, Bob containment, cancellation race, and interruption;
- historic schema-1–4 manifests, `SelectedMission`, `ApprovedMissionLaunches`, and legacy
  `/conversations` reads remaining compatible; and
- focused tests, full solution build/test, and Native AOT publish.

Visual acceptance is **N/A**: this task changes no markup, CSS, navigation, or Desktop lifecycle.
The zero-argument Desktop default-path journey is deliberately **deferred**, not passed: 45.3 owns
the operator conversation surface and 45.4 owns authoring/evaluation/publish. Their evidence must
use the published `dist/forge-desktop/ForgeMission.Desktop` with absent overrides, normal cloud
Mission route, Supervisor-owned Kind bridge, clean-main Host/Worker provenance, and a disposable
Project. Controlled integration tests here cannot close that user-facing acceptance.

**Done when:** an Approved schema-5 version creates a pinned generic durable conversation; an exact
Candidate/case creates and reconciles a hidden evaluation; the accepted Phase 46 generic path is
used without a mission-specific executor or new authority; failures preserve the named durable
facts and typed recovery; compatibility reads remain valid; focused/full/AOT checks pass; and an
independent reviewer accepts the evidence. Phase 45.3/45.4's visual and zero-argument Desktop
obligations remain active rather than claimed here.

### Completion

Accepted — see [completed 45.2](phase-45.2-durable-conversation-turns_completed.md) for independent review and named verification. Desktop visual/default-path acceptance remains deferred to 45.3/45.4 exactly as allocated.
