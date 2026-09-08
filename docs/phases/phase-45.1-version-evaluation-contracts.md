# Phase 45.1 — Version and evaluation contracts

> **Status:** implementation-ready, independently re-reviewed 2026-09-08. It depends on no
> unmerged Phase 45 code. Parent: [Phase 45](phase-45-mission-conversations.md).

## Task 1 — Project-owned authored versions and deterministic evaluation

### Ownership and scope

`Application/Projects` owns one atomic local manifest transaction for authored mission identity,
version lifecycle, immutable executable content, cases, and deterministic results.
`Application/Missions` later constructs a bounded immutable launch from an Approved version, but
owns neither Project mutation nor durable commands. Presentation, Application Host, Conversation
Host, Worker, and Bob gain no ownership here. This task adds Project records, lifecycle/evaluation
services, and surface-neutral Application interfaces only.

It adds no Presentation, HTTP route, Application.Transport request, remote conversation, evaluation
run, pending intent, Bob attachment, Worker message, or provider call. Those effects belong to 45.4
and 45.2. `StartEvaluation` and `CreateMissionConversation` are not exposed before 45.2; a premature
direct service call returns append-only `EvaluationUnavailable` and writes nothing.

The current generic Project submission route and its retained `SelectedMission`/
`ApprovedMissionLaunches` compatibility lane remain unchanged. Task 45.3 replaces that selection
only after its accepted durable/default-path proof. No OCI/catalog installation, profile knob,
provider selection, credential, network, arbitrary-path, or Full Access capability is introduced.

### Schema 5 migration and retained lane

Phase 46 already made the manifest schema 4 and added immutable `ApprovedMissionLaunches`. Schema
5 adds `MissionDefinitions` without guessing that a v4 launch is an authored version: it lacks a
mission identity/evaluation facts and could contain source the current parser rejects.

| Source schema | Open | First successful task-owned mutation | Meaning |
|---|---|---|---|
| 1–3 | Existing compatibility normalization. | Atomically write schema 5, normalized existing fields, empty `MissionDefinitions`; no remote work. | Historic Project/runs stay readable. |
| 4 | Read `ApprovedMissionLaunches` byte-for-value. | Atomically write schema 5, same launch array, empty `MissionDefinitions`; no remote work. | Current generic submission stays available. |
| 5 | Validate both lanes independently. | Normal atomic Project mutation. | Authored versions and the retained lane coexist without implicit conversion. |

Future schemas and malformed records fail without rewrite. A malformed retained launch fails only
the legacy-launch operation; a malformed authored definition fails only that authored operation.
The retained lane is a Type-2 compatibility exception removed only by a named historic-read migration
after 45.3 accepts the Approved-version route—not by this task.

### Exact persisted model

All records are Project-owned manifest JSON, source generated with camel-case string enums. Strings
are UTF-8 bounded before write; the existing 2 MiB aggregate manifest cap remains the outer limit.
Duplicate ID/name, unknown enum, blank required value, invalid hash, or size breach is a typed
no-write failure.

```text
ProjectMissionDefinition
  missionId: Guid, name: 1..120 chars, activeApprovedVersionId: Guid?,
  draft: MissionDraft?, versions: MissionVersion[]

MissionDraft
  draftId: Guid, definitionText: 1..262144 bytes, definitionHash: lower-case sha256,
  capabilityProfile: MissionHandsProfile, revision: positive int, updatedAtUtc

MissionVersion
  missionVersionId: Guid, versionNumber: positive int,
  state: Candidate|Evaluated|Approved|Superseded,
  definitionText: 1..262144 bytes, definitionHash: lower-case sha256,
  capabilityProfile: MissionHandsProfile, package: DurableMissionPackage,
  parentVersionId: Guid?, candidateRevision: positive int,
  createdAtUtc, evaluatedAtUtc?, approvedAtUtc?,
  evaluationCases: EvaluationCase[], evaluationResults: EvaluationResult[]

EvaluationCase
  evaluationCaseId: Guid, input: 1..32768 bytes,
  expectedSuccess: 0..4096 bytes, expectedFailure: 0..4096 bytes,
  expectedOutcome: Succeeded|Failed,
  requiredOutputFragments: 0..16 strings each 1..1024 bytes,
  forbiddenOutputFragments: 0..16 strings each 1..1024 bytes, revision: positive int

EvaluationResult
  evaluationResultId: Guid, evaluationCaseId: Guid, missionVersionId: Guid,
  candidateRevision: positive int, definitionHash: lower-case sha256,
  observedOutcome: Succeeded|Failed, observedOutputSummary: 0..4096 bytes,
  state: Pending|Passed|Failed, traceOrigin: EvaluationTraceOrigin?, completedAtUtc?

EvaluationTraceOrigin
  conversationId: Guid, turnId: Guid, turnAttemptId: Guid
```

There is exactly one mutable `MissionDraft` per mission. A Draft is not a version. Promotion validates
and freezes it as Candidate version 1 (or the next number), with a new version ID; later Candidate
edits retain ID/number and increment only `CandidateRevision`. `DefinitionAssetId` is deliberately
absent: current asset IDs are positional. Frozen text and package make later asset/editor changes
unable to alter a version. Mission names are unique case-insensitively within a Project; version
numbers are monotonic within a mission; case/result IDs are unique within their immediate owner.

### Immutable package and shared profile

`ForgeMission.Conversations.Contracts.MissionHandsProfile` is the single profile enum for manifest,
Application services, and future transport, using existing `noHands`, `projectWorkspace`, and
`projectWorkspaceAndTerminal` wire values. The Application-internal `MissionCapabilityProfile` is
removed. An author chooses one known profile only when creating or replacing a Draft; Candidate save
and later lifecycle preserve it. A caller never supplies a profile, package, hash, capability, Bob
handle, or durable state with a launch/attachment.

`MissionVersionService` is the package builder at the Project boundary. On Candidate promotion and
valid Candidate save, it reads only manifest-listed local MCL, lock, and expert content through the
existing bounded Project-content owner; parses/resolves that exact content; constructs a
Contracts-owned `DurableMissionPackage`; and validates its canonical hash through Core in the same
Project transaction. `MissionVersion.Package` freezes MCL source, root identity/input, lock
provenance, and resolved expert markdown. A parse, lock, size, or hash failure yields a typed
diagnostic and writes neither version nor package.

`MissionVersionLaunch` remains Application-internal compatibility provenance. Contracts never
depends on Application; `DurableMissionLaunch` is its only durable launch type. In 45.2 Application
constructs that existing contracts type from an Approved frozen package; Host revalidates before
Blob/queue work, and Worker receives only package/profile—not Project paths, mutable assets,
credentials, provider choice, or Bob.

### Lifecycle and failure boundary

Creating a mission creates a Draft. Promoting freezes it to Candidate. Saving a Candidate requires
its exact revision, recomputes text/package hashes, increments revision, clears results, and keeps
it Candidate. `EvaluationService` alone records results: `Passed` requires observed outcome equals
`ExpectedOutcome`, every required fragment occurs, and no forbidden fragment occurs; otherwise it
is `Failed`. Candidate becomes Evaluated only when every current case has a matching revision/hash
result and all pass. Publish is one Project transaction: recheck, mark Approved, supersede prior
active Approved, and leave existing conversations/legacy launches unchanged.

| Failure | Owner/result | Recovery/proof |
|---|---|---|
| Invalid MCL, changed lock, package size/hash | MissionVersionService returns typed diagnostic; no mutation or remote work. | Correct/retry; parser/lock/hash tests. |
| Stale draft/candidate/case/publish | ProjectService lease transaction returns `VersionChanged`/`PublishConflict`; no overwrite. | Refresh/retry; concurrent-write test. |
| Invalid/unknown profile or substitution | Project service rejects it; no caller-selected launch/attachment profile. | Reopen version; tamper test. |
| Execution requested before 45.2 | Service returns `EvaluationUnavailable`; no Pending result/command/attachment. | Retry after 45.2; no-write test. |
| Criteria mismatch | EvaluationService records Failed and blocks publish. | Edit/evaluate again; deterministic test. |

### Interfaces, gates, and evidence

`ApplicationComposition` exposes named internal owner interfaces only: list/create/get Draft, save
Draft, promote Candidate, get Version state, save/delete case, list results, record a later 45.2
completion, and publish. It adds neither generic dispatch nor routes. Phase 45.3 owns
Approved-version transport/read actions; 45.4 owns authoring/evaluation/publish transport, JSON DTOs,
and UI. This prevents a half-public protocol before the surface and durable executor exist.

Security Architecture is **PASS**: ProjectService remains sole local-store owner; no new public
ingress, Tier-3 access, credential, cross-context query, Host/Worker Project read, or Bob authority
exists. This is Type-2 schema/contract evolution with a retained-lane removal condition. Engineering
Philosophy is **PASS**: one manifest writer/evaluator/profile vocabulary, frozen values not mutable
references, and named unavailability rather than speculative pending state.

Presentation-surface parity is **PASS by construction**: later surfaces use these named Application
interfaces. Visual acceptance is **N/A**: no markup, CSS, navigation, or interaction changes. No
authoring UI exists yet, so this task cannot claim the default Desktop author/save/evaluate/reopen
journey. Phase 45.4 owns and must pass that zero-argument Desktop proof with absent overrides,
normal dependency route, disposable Project, reopened evidence, and visual acceptance. Controlled
Project/service checks are non-acceptance evidence for that UI journey.

Required evidence: schema 1–4 reads; v4→5 preservation; no remote migration work; future-schema
non-rewrite; retained legacy submission; frozen package against later asset edits; tampered profile
refusal; parser/lock/size/hash diagnostics; stale no-write; deterministic criteria/result
invalidation; publish race; unavailable execution/no-write; source-generated manifest compatibility;
focused Application tests; full solution build/test; and Native-AOT `make install`.

**Done when:** schema 5/source-generated records enforce these invariants; schema 1–4 and current
generic submission remain readable; Candidate freezes executable content; focused/full/AOT checks
pass; an independent reviewer accepts evidence; and Phase 45.4's UI/default-path obligation remains
active rather than claimed here.
