# Phase 45.1 — Version and evaluation contracts

> **Status:** implementation-ready design; depends on no Phase 45 code. It changes no behaviour
> until implemented. Parent: [Phase 45](phase-45-mission-conversations.md).

## Task 1 — make Project-owned versions and evaluation explicit

### Why and component fit

This advances `Application/Projects`’ purpose: one transaction-protected authority for Project
identity, manifest mutation, and bounded content. `Application/Missions` advances its purpose by
preparing immutable intent and reconciling acceptance without owning durable runs. Application
Transport makes the same action callable from Desktop and a future TUI; Presentation gains no
lifecycle authority.

### Affected components and files

| Component | Planned files |
|---|---|
| Application/Projects | `ProjectManifest.cs`, `ProjectManifestFile.cs`, `ProjectManifestJsonContext.cs`, `ProjectService.cs`, new `MissionVersions/MissionVersionService.cs`, `MissionVersions/EvaluationService.cs`, and tests. |
| Application/Missions | `MissionCatalog.cs`, new `MissionConversationService.cs`, composition interfaces in `ApplicationComposition.cs`, and focused tests. |
| Application Transport/Host | `ApplicationContracts.cs`, JSON context/channel bindings, `ApplicationEndpoints.cs`, contract/route tests. |
| Conversations Contracts | `ConversationContracts.cs`, JSON context and round-trip/boundary tests; no Host/Worker behaviour yet. |
| Documentation | This completion record and source-adjacent READMEs if a public owner vocabulary changes. |

### Data, persistence, and migration contract

`forge.project.json` migrates atomically from schema 3 to schema 4 on first successful create/open
write. Schema-3 Project Mission history remains readable and immutable. Migration creates no remote
conversation and does not reinterpret a historic `ProjectMissionContainerId`.

```text
ProjectMissionDefinition
  MissionId, Name, ActiveApprovedVersionId?, Draft?, Versions[]

MissionVersion
  MissionVersionId, VersionNumber, State, DefinitionHash, DefinitionAssetId,
  ParentVersionId?, CandidateRevision, CreatedAtUtc, EvaluatedAtUtc?, ApprovedAtUtc?

EvaluationCase
  EvaluationCaseId, MissionVersionId, Input,
  ExpectedSuccess, ExpectedFailure, ExpectedOutcome,
  RequiredOutputFragments[], ForbiddenOutputFragments[], Revision

EvaluationResult
  EvaluationResultId, EvaluationCaseId, MissionVersionId, CandidateRevision,
  DefinitionHash, ObservedOutcome, ObservedOutputSummary, State,
  TraceOrigin, CompletedAtUtc
```

`ExpectedSuccess` and `ExpectedFailure` are author-visible criteria. `ExpectedOutcome` is
exactly `Succeeded` or `Failed`; result `Passed` requires that outcome plus every required
fragment and no forbidden fragment in the terminal answer/reason. `Failed` is a terminal observed
result that misses criteria; `Pending` is not publishable. `EvaluationService` owns this
deterministic comparison—never the UI or an LLM. Exact evidence remains the trace.

Draft content is mutable and unselectable. Promoting creates Candidate N+1. Saving Candidate content
increments `CandidateRevision`, recomputes hash, clears results and returns it to Candidate. Only
a Candidate whose current results cover every case and all pass becomes Evaluated. Publish is one
Project transaction: recheck state/revision/results; set current Approved; set prior Approved
Superseded; retain every existing conversation launch. Failed recheck returns typed error/no write.

### Typed shared actions

Added additively to `ApplicationContracts`; Host binds one concrete route per request and invokes a
matching typed Application interface. `ProjectOperationErrorCode` appends `MissionNotFound`,
`VersionNotFound`, `VersionNotApproved`, `VersionChanged`, `EvaluationRequired`,
`EvaluationCaseInvalid`, `EvaluationUnavailable`, and `PublishConflict`.

```csharp
record ListMissionVersionsRequest(string SessionId);
record MissionVersionView(Guid MissionId, Guid MissionVersionId, int VersionNumber,
    MissionVersionState State, string DefinitionHash, int CandidateRevision, bool Selectable);
record CreateMissionDraftRequest(string SessionId, string Name, Guid? DerivedFromVersionId);
record SaveMissionDraftRequest(string SessionId, Guid MissionId, string DefinitionText, int Revision);
record PromoteMissionCandidateRequest(string SessionId, Guid MissionId, int DraftRevision);
record GetMissionAuthoringRequest(string SessionId, Guid MissionId, Guid? MissionVersionId);
record SaveEvaluationCaseRequest(string SessionId, EvaluationCaseDraft Case, int Revision);
record DeleteEvaluationCaseRequest(string SessionId, Guid EvaluationCaseId, int Revision);
record StartEvaluationRequest(string SessionId, Guid MissionVersionId, Guid EvaluationCaseId,
    Guid CommandId, int CandidateRevision, string DefinitionHash);
record GetEvaluationResultsRequest(string SessionId, Guid MissionVersionId, int CandidateRevision);
record PublishMissionVersionRequest(string SessionId, Guid MissionVersionId,
    int CandidateRevision, string DefinitionHash);
```

`StartEvaluationRequest` creates immutable local intent here; 45.2 supplies durable execution.
`PublishMissionVersionRequest` contains no caller-provided state/result. Internal
`MissionVersionLaunch` is defined now with ID/version/hash/artifact/text; only Application computes
it, so a caller cannot substitute a hash or artifact.

### Validation and failure boundaries

| Boundary | Containment and visible result | Recovery/evidence |
|---|---|---|
| Malformed MCL draft | Version service parses before Candidate/evaluation/publish; returns source-span diagnostic and makes no version/result mutation. | Edit/save valid revision; parser/transport negative tests. |
| Stale save/evaluate/publish | Revision/hash comparison happens inside manifest transaction; returns `VersionChanged`/conflict and never overwrites newer content. | Refresh/retry deliberately; concurrent-write test. |
| Evaluation launch uncertainty | Project records Pending intent with one CommandId; 45.2 reconciles it, not a second run. | Retry same request; lost-response controlled test. |
| Manifest write failure | Existing `ManifestWriteFailed`; no claim of Candidate/Evaluated/Approved success. | Repair/retry through owner; atomic-write failure test. |

No public ingress, credential, Tier-3 grant, direct Host/Worker Project read, or Client Runtime
capability is added. Project is sole author-data owner. JSON remains source-generated; parser use
adds no reflection.

### Default path, verification, and done when

The implementation default path is published zero-argument Desktop, no positional URL,
`MissionRuntime:*`/`FORGE_API_ENDPOINT`/`ConversationRuntime:*` overrides, normal cloud Mission
route, Supervisor-owned Kind bridge, and disposable Project. Through ordinary UI, create/save valid
MCL, create candidate/cases, and reopen it. Fixture endpoints/test Projects are controlled evidence.

- Unit/contract: schema-3 migration, enum/JSON compatibility, parser diagnostics, lifecycle table,
  result invalidation, deterministic criteria and stale paths.
- Integration: Host routes call typed owners; a surface-neutral channel performs each action with no
  Presentation types.
- Visual: N/A—existing renderer reuse only.
- Default: record artifact, defaults, dependency provenance, Project, action and reopened durable
  manifest observation.

**Done when:** schema 4 and source-generated contracts enforce all listed invariants; schema-3
history remains readable; focused checks pass; normal Desktop path reopens candidate/cases; Codex
accepts Claude’s evidence summary.
