# Phase 46.1 — domain-ownership inventory evidence

> **Status:** self-adversarial second pass complete; proposed evidence awaiting independent supervising-Codex review. This source/documentation inspection
> was performed on 2026-09-07; it makes no implementation approval. Build, test, and default-path
> acceptance are **N/A** because no executable behavior changed.

## Scope and graph reconciliation

The solution contains the 28 production components named by the [Source Component Atlas](../../src/README.md), plus its two excluded diagnostic executables and test projects. The production-reference graph agrees with the documented direction: Core depends on Parser/Scout; ChatClients depends on Core; CLI composes Core/ChatClients/Scout/Serve/Docker; Runner composes Core/ChatClients/Serve and uses the existing CLI mission-source support; Worker composes Core/ChatClients and durable contracts only. Application Host is the intended composition root for Application, Transport, Client Runtime, and Presentation. No unlisted production executable or second store owner was found.

No source-adjacent README was changed: each describes the observed current component boundary,
including the current closed Worker catalog. The Phase 46 records, rather than a speculative README
rewrite, hold the proposed end state pending supervisory review.

## Adversarial second-pass result

The second pass re-traced the Core child-mission path, Worker/Host command paths, Application's
legacy adapter, Runner's output projection, and Phase 45's already-locked launch-snapshot design.
It **revises** the first pass: the nested-tool correction is not implementation-ready, durable
mission admission is already owned by the Phase 45 design rather than an open new architecture,
and `Answerer` is an implicit output convention rather than a separate execution loop.

| Composition and Repairability criterion | Result | Evidence / remaining gap |
|---|---|---|
| 1. Single owner | **FAIL** | Core/Janus jointly own nested execution and continuation today (F46.1-01); Worker owns persona mapping that should be a generic trace projection (F46.1-03). Phase 45 fixes launch-content ownership through Application → Host → Worker (D46.1-01). |
| 2. Simple composition | **FAIL** | `WorkerMissionResolver` plus two executors form a second declaration interpreter (F46.1-02). The retained queue/outbox/Host store split itself is narrow and passes. |
| 3. Failure locality | **FAIL** | Worker outbox/interruption and legacy tool-report failures are named, but nested tool continuation has no single Core contract identifying capability scope, resume target, visible failure, or recovery (Q46.1-02). |
| 4. Extension path | **FAIL** for durable runs; **PASS** for CLI/Runner | CLI/Runner parse, resolve, and execute arbitrary packaged declarations. Durable runs require named directories, resolver cases, executors, persona mapping, and image changes. |
| 5. Progressive disclosure and narrowness | **FAIL** | The existing Janus executor is readable but owns unrelated declaration topology, approval capture, provider continuation reconstruction, and progress adaptation. Its extraction is not a fix unless Core's narrow continuation boundary is first specified. |
| 6. No disguised complexity | **PASS, conditional** | The revised end state uses existing Core, Phase 45 launch snapshots, queue/outbox, and additive contracts; it rejects a plugin framework, runtime registry, dispatcher, wrapper stack, and settings mode. |
| 7. Evidence and implementation readiness | **FAIL** | Exact paths/tests exist, but Q46.1-02 is a Type-1 decision. No Step 2 implementation card is ready until the supervisor selects the generic nested delegation/resume rule. |

The remaining design gaps are Q46.1-02 (Type 1 nested tool delegation/continuation) and
Q46.1-03 (Type 2 hosted answer selection). No other durable admission/trust decision is open:
Phase 45 defines the required immutable snapshot chain and is a dependency, not permission to
invent a parallel catalog.

| Entry/process path | Confirmed call path and owner |
|---|---|
| CLI | `ForgeMission.Cli/Program.cs` → parse/lock/expert validation → `PipelineRunner` → `IExpertRunner` built by ChatClients → provider/search adapter → text/file result. It contains no Janus/Naive branch. |
| Hosted Runner | `Runner/Program.cs` → `RunnerMissionSource`/`RunnerRegistry` → `MissionRunHandler` → `PipelineRunner` → `/run` or `/run/stream`; API and ForgeUI are callers. OCI/built-in source selection is a host/deployment choice, not a Core execution branch. |
| Desktop Project | Desktop Supervisor → Application Host typed route → Application `MissionSubmissionService`/`ConversationHostClient` → Conversation Host durable command → Worker → progress queue → Host event/run projection → Application history/Presentation. Application owns manifest/journal; Host is the sole durable-store writer. |
| Legacy conversation | Application `ConversationService` → legacy protocol adapter or Conversation Host → `LegacyJanusToolDelivery` → Client Runtime dispatcher. This is a narrow compatibility route, not the Project Mission execution path. |

## Direct mission trace

| Mission / route | Declaration and resolution | Execution and result | Judgment |
|---|---|---|---|
| Any CLI mission | `mission.mcl` and `mcl.lock` → `MclParser`, `ExpertResolver`, `PipelineRunner` in `Cli/Program.cs`. | The first declared mission and its `using` profiles drive generic step execution. | Retained generic path. |
| Any Runner mission | `RunnerMissionSource` resolves OCI/local/built-in bytes; `RunnerRegistry` parses, validates, resolves experts. | `MissionRunHandler` passes the declaration to `PipelineRunner`; indirect sweep found only built-in catalog labels, generic wire model-to-label lookup, and the `Answerer` output convention. | Generic execution; labels are host catalog policy, Q46.1-03 is a non-execution gap. |
| Durable Janus Project run | `Dockerfile.conversationworker` copies `missions/janus` and Worker startup requires `JanusMissionDirectory`. | `WorkerMissionResolver.Resolve("Janus")` invokes `JanusMissionExecutor`, which calls declared `Negotiate` and `Implement` separately. | F46.1-01/F46.1-02 confirmed. |
| Durable Naive Project run | Docker copies/loads `missions/naive` through a separate `NaiveMissionDirectory`. | `Resolve("Naive")` invokes `NaiveMissionExecutor` and its dedicated progress emission. | F46.1-02/F46.1-03 confirmed. |
| Legacy conversation | `ConversationApiEndpoints.HandleStartConversationAsync` allows only `SupportedMissionRef = "Janus"`. | Legacy Application adapter preserves historical prompt/tool delivery. | R46.1-01 retained compatibility boundary. |

## F46.1-01 — nested tool continuation is not generic

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Confirmed defect, P0, design-blocked — mission-specific run-loop workaround.** `PipelineRunner.cs` lines 235–259 retain only a child's text and discard `ToolCalls`; lines 183–202 pause only for a direct agent. `CreateChildOptions` (lines 372–389) deliberately withholds `Tools`, `StartAtAgent`, context objects, and pre-agent callback from children. |
| Current path | `MissionCommandProcessor.ProcessFreshStartAsync` → `JanusMissionExecutor.RunFullMissionAsync` → `RunAsync("Negotiate")`; on approval a second direct `RunAsync("Implement", Tools, AllowMultipleToolCalls:false)`. `RunContinuationAsync` reconstructs a provider conversation and calls `Implement` with `StartAtAgent:true`. This is evidence of three missing generic semantics, not merely a dropped result field. |
| Why/current owner | Core's README assigns provider-neutral pipeline semantics and result/trace contracts to Core. Worker's README assigns restartable mission reasoning/progress publication, not another declaration interpreter. Janus's executor owns declaration topology and continuation mechanics that belong at the generic Core seam. |
| State and failure | Worker correctly persists `WaitingForTool`/outstanding call before publish, retains an outbox, and reports interrupted rather than replaying an ambiguous provider call. Those effects remain Worker-owned. Core currently lacks the generic result/continuation information needed for that safety path. |
| Consumers/evidence | `PipelineRunOptions`, `MissionResult`, `PipelineTraceEvent`, `DirectExpertRunner`, Worker session state, `ConversationCommand`/`ConversationProgress`; tests: `ForgeMission.Tests/Runtime/PipelineTraceTests.cs`, `ConversationWorker.Tests/JanusMissionExecutorToolCallOptionsTests.cs`, and `MissionCommandProcessorTests.cs`. |
| End-state owner | **Core** owns an explicit generic nested tool-delegation result: the declared mission path/agent location, the permitted tool scope, and opaque continuation state sufficient to resume exactly that declaration. **Worker** remains the sole command recovery/outbox/progress owner. |
| Disposition / compatibility | **Replace with existing generic mechanism** after Q46.1-02. This is **Type 1** because a child may otherwise inherit a capability grant implicitly and continuation/result semantics cross Core into Worker. No `Janus` name, concrete executor, or host-side provider transcript reconstruction remains. |
| Containment/proof | Core must reject an unsupported nested delegation before provider work; Worker persists only a correctly identified request and retains its interrupted outcome on crash. Later proof: Core nested-tool, unsupported-child, and negative-resume tests; Worker redelivery/outbox tests; default Desktop acceptance after executable changes. |

## F46.1-02 — durable Worker has a closed named runner

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Confirmed defect, P0 — mission-name branch and concrete executors.** `Messaging/WorkerMissionResolver.cs` maps only `Janus`/`Naive`; `Messaging/MissionCommandProcessor.cs` branches to `JanusMissionExecutor`/`NaiveMissionExecutor`; `Program.cs` requires two named directories; `Dockerfile.conversationworker` copies two assets and supplies two environment variables. |
| Boundary/dependencies | Worker validly owns queue consumption, Worker session/retry state, provider invocation, and progress send. It does not own a product catalog or compiled execution path per declaration. `ProjectMissionNames` is checked in Application, Conversation Host, and Worker, so a third Project mission is rejected before generic execution. |
| Consumers/evidence | Application `MissionCatalog`/`ProjectService`, `ConversationApiEndpoints`, `ConversationGrain`, queue command, and Worker resolver. Relevant tests: `NaiveMissionRunTests`, `MissionCommandProcessorTests`, `ConversationApiTests`, `ProjectRunHistoryTests`, and Application Project/MissionSubmission tests. |
| End-state owner | **Phase 45's locked chain** is the authoritative admission design: Application/Projects owns immutable `MissionVersionLaunch` content; Conversation Host verifies hash/size and owns its bounded Blob copy; Worker receives verified value content, never a Project path, credential, provider selection, capability, or arbitrary directory. Worker uses one generic load/execute/progress bridge; Core executes the language. |
| Disposition / compatibility | **Replace with existing generic mechanism, P0** after Phase 45's launch-snapshot implementation and Q46.1-02. Do not add a plugin system, registry, or command-supplied directory. The established snapshot topology is **Type 1** and defines identity, trust, versioning, rejection, and recovery ownership. |
| Containment/proof | Invalid hash/size/version is rejected by Host before Blob/queue/provider work; Worker cannot fetch Project data and a Project command contains zero capability declarations. Preserve Worker crash/outbox behavior. Later proof needs Phase 45's invalid-launch tests, redelivery, Janus/Naive migration reads, a new approved declaration, and default-path evidence. |

## F46.1-03 — progress wire encodes Janus and Naive personas

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Confirmed defect, P0 — mission-specific projection.** `Janus/JanusPipelineProgressMapper.cs` maps `Proposer`/`Approver`/`Implementer`, turns judge status into `Approval`, and rejects unknown experts. `NaiveMissionExecutor` bypasses it; `MissionCommandProcessor` emits `ConversationParticipant.Naive`. `Conversations.Contracts/ConversationContracts.cs` persists Janus/Naive participant vocabulary and approval semantics. |
| Boundary/current owner | Worker has generic pipeline trace data yet owns persona vocabulary and an approval product rule. Conversation Host correctly owns durable sequence/store; Presentation correctly renders. The invalid middle layer is a mission-specific Worker-to-contract mapping, not an external protocol adapter. |
| Consumers/evidence | `PipelineTraceEvent`, `MappedProgressFact`, `ConversationProgress/Event/Participant/Approval`, checkpoint/event JSON, Presentation transcript. Tests: `JanusPipelineProgressMapperTests`, `NaiveMissionRunTests`, `ConversationContractsRoundTripTests`, `ConversationProgressHandlerTests`, and Presentation tests. |
| End-state owner | **Conversations.Contracts plus Worker** own additive generic trace facts carrying declared mission path, expert identifier/kind, attempt, text/reason, tool request, and terminal status. **Presentation** renders labels as data. Judge completion is an ordinary completed trace fact; historic Janus `Approval`/participant events remain readable rather than being reinterpreted. |
| Disposition / compatibility | **Move/replace, P0. Type 1:** event enums/shapes are persisted and transit HTTP/SSE/Service Bus. Add the generic projection for new launch-snapshot runs; retain historic readers/events until an explicit migration/retention decision. |
| Containment/proof | Host validates generic progress shape before append; unknown/malformed trace input becomes a terminal failure without corrupting durable state; progress-send failure remains in the Worker outbox. Later proof: old-event reads, generic sequencing/replay, malformed trace rejection, generic rendering, and default Project run. |

## Q46.1-03 — hosted result selection is an implicit convention

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Unresolved design question, Type 2 — implicit content convention, not a second executor.** `Runner/MissionRunHandler.cs` lines 234–247 displays the last `Answerer` trace text for a verified run. Built-in verified assets intentionally use `Answerer → Verifier`, so displaying raw `MissionResult.Text` would display verifier text instead of the user answer. MCL `output(Mission)` selects a mission/file target, not an intermediate step output. |
| Owner/dependencies | Runner owns `RunResponse.AgentText`; Core owns the pipeline result; API `MissionExecutionService` and ForgeUI `RoomAgentInvoker` consume the projection. `Runner.Tests/MissionRunHandlerTests.cs` proves generic non-`Answerer` agent flows already use the terminal result. |
| Alternatives / supervisor decision | Choose one: **(a)** retain and document `Answerer` as a built-in catalog/content convention (smallest, no language change); or **(b)** add an explicit generic result-selection semantic to MCL and migrate built-ins (more expressive but a language/API design change). Do not remove the convention or add a name-based runner registry. |
| Compatibility/proof | This does not block generic execution or F46.1-01–03. Any future change affects public Runner/API/UI response selection and needs built-in visible-result, absent/multiple selector, and hosted default-path evidence. |

## F46.1-05 — inactive Project mission origins

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Duplicate/dead code, P2 deferred.** `Application/Projects/ProjectManifest.cs` defines `Local`/`Oci` origins and digest/snapshot reference fields, but all production selections/readers (`ProjectService.cs`, `ProjectMissionHistoryReader.cs`, `MissionCatalog.cs`) accept only `BuiltIn` plus `ProjectMissionNames`. No production code constructs/consumes local or OCI selection; Phase 45 explicitly excludes OCI/catalog installation. |
| End state/disposition | Do not create a second source of truth. **Defer:** after Phase 45's version migration, Application/Projects removes the unused origins unless a separate OCI/catalog design explicitly needs them. |
| Compatibility/proof | Manifest schema v3 is a storage commitment. Later work needs historic-manifest reads, migration evidence, and a normal Project observation. |

## Retained boundary and unresolved decision

### R46.1-01 — legacy Janus compatibility

`ConversationApiEndpoints.HandleStartConversationAsync` deliberately accepts Janus only on the legacy `/conversations` route. `ConversationService` creates this scope only for a durable legacy session; `LegacyJanusToolDelivery` validates `Implementer` tool events, delegates every capability decision to Bob, and reports an HTTP failure visibly without claiming durable acknowledgement. This is a narrow compatibility seam with named failure behavior, not the generic Project Mission admission route.

The second pass finds one limit: the route's `MissionRef` still reaches the Worker and therefore its downstream compiled Janus selection is F46.1-02, not an adapter exemption. Retain the transport/client compatibility contract only; do not broaden it, and do not retire it without an explicit caller-migration/retention decision.

### D46.1-01 — durable package admission is already a Phase 45 dependency

The first pass incorrectly left durable package trust as a new open Phase 46 decision. Phase 45's
locked architecture already assigns it: Application/Projects creates immutable content and a
`MissionVersionLaunch`; Conversation Host validates hash/size, owns the bounded Blob artifact and
trusted Project identity; Worker receives verified value content only. Commands carry no Project
path, credential, provider selection, local capability, or arbitrary directory. That is the
required Type-1 owner chain for F46.1-02/03, and is a Phase 45 implementation dependency rather
than a second catalog design.

### Q46.1-02 — nested tool delegation and continuation

Core currently treats a child mission as isolated: it does not inherit tool declarations,
continuation state, or pre-agent enrichment. That is a valid least-privilege default, so simply
propagating `Tools` would be a security regression. The supervising Codex agent must choose and
lock one generic rule before F46.1-01 is implementable:

1. Permit an explicitly declared child-agent path to receive the root's already-authorized tool
   scope, and return opaque continuation data naming that exact path; or
2. Keep child isolation and declare nested tool use unsupported, requiring mission shapes that do
   not place agents below a mission boundary.

The decision must define parallel-child behavior, provider conversation state, result/trace order,
unsupported-path failure, Worker persistence, and default-path proof. It is **Type 1** because it
controls capability delegation across a durable execution boundary. A plugin, registry, option, or
Worker-side transcript workaround is not an acceptable third alternative.

## First smallest Step 2 candidate — not approved

There is no implementation-ready task. The former F46.1-01 candidate is correctly Core-owned but
was incorrectly treated as ready: Q46.1-02 must first select the nested delegation/resume rule.
After that decision, the smallest card is Core-only F46.1-01—generic result/trace/continuation
propagation with no Worker catalog, durable-wire, launch-snapshot, provider-policy, or legacy-route
change. It must not edit code until a supervising Codex agent explicitly approves it.
