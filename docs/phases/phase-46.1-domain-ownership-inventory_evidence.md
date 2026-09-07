# Phase 46.1 — domain-ownership inventory evidence

> **Status:** proposed evidence for supervising-Codex review. This source/documentation inspection
> was performed on 2026-09-07; it makes no implementation approval. Build, test, and default-path
> acceptance are **N/A** because no executable behavior changed.

## Scope and graph reconciliation

The solution contains the 28 production components named by the [Source Component Atlas](../../src/README.md), plus its two excluded diagnostic executables and test projects. The production-reference graph agrees with the documented direction: Core depends on Parser/Scout; ChatClients depends on Core; CLI composes Core/ChatClients/Scout/Serve/Docker; Runner composes Core/ChatClients/Serve and uses the existing CLI mission-source support; Worker composes Core/ChatClients and durable contracts only. Application Host is the intended composition root for Application, Transport, Client Runtime, and Presentation. No unlisted production executable or second store owner was found.

No source-adjacent README was changed: each describes the observed current component boundary,
including the current closed Worker catalog. The Phase 46 records, rather than a speculative README
rewrite, hold the proposed end state pending supervisory review.

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
| Any Runner mission | `RunnerMissionSource` resolves OCI/local/built-in bytes; `RunnerRegistry` parses, validates, resolves experts. | `MissionRunHandler` passes the declaration to `PipelineRunner`; no Janus/Naive selection exists. | Retained generic path; F46.1-04 affects only response projection. |
| Durable Janus Project run | `Dockerfile.conversationworker` copies `missions/janus` and Worker startup requires `JanusMissionDirectory`. | `WorkerMissionResolver.Resolve("Janus")` invokes `JanusMissionExecutor`, which calls declared `Negotiate` and `Implement` separately. | F46.1-01/F46.1-02 confirmed. |
| Durable Naive Project run | Docker copies/loads `missions/naive` through a separate `NaiveMissionDirectory`. | `Resolve("Naive")` invokes `NaiveMissionExecutor` and its dedicated progress emission. | F46.1-02/F46.1-03 confirmed. |
| Legacy conversation | `ConversationApiEndpoints.HandleStartConversationAsync` allows only `SupportedMissionRef = "Janus"`. | Legacy Application adapter preserves historical prompt/tool delivery. | R46.1-01 retained compatibility boundary. |

## F46.1-01 — nested tool continuation is not generic

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Confirmed defect, P0 — mission-specific run-loop workaround.** `src/ForgeMission.Core/Runtime/PipelineRunner.cs` lines 235–259 recurse into a sub-mission then retain only `subResult.Text`; `ToolCalls` never reach the parent. Lines 183–202 return a tool pause only when the agent step is direct. |
| Current path | `MissionCommandProcessor.ProcessFreshStartAsync` → `JanusMissionExecutor.RunFullMissionAsync` → `RunAsync("Negotiate")`; on approval a second direct `RunAsync("Implement", Tools, AllowMultipleToolCalls:false)`. `RunContinuationAsync` rebuilds a provider conversation and calls `Implement` with `StartAtAgent:true`. |
| Why/current owner | Core's README assigns provider-neutral pipeline semantics and result/trace contracts to Core. Worker's README assigns restartable mission reasoning/progress publication, not another declaration interpreter. Janus's executor owns declaration topology and continuation mechanics that belong at the generic Core seam. |
| State and failure | Worker correctly persists `WaitingForTool`/outstanding call before publish, retains an outbox, and reports interrupted rather than replaying an ambiguous provider call. Those effects remain Worker-owned. Core currently lacks the generic result/continuation information needed for that safety path. |
| Consumers/evidence | `PipelineRunOptions`, `MissionResult`, `PipelineTraceEvent`, `DirectExpertRunner`, Worker session state, `ConversationCommand`/`ConversationProgress`; tests: `ForgeMission.Tests/Runtime/PipelineTraceTests.cs`, `ConversationWorker.Tests/JanusMissionExecutorToolCallOptionsTests.cs`, and `MissionCommandProcessorTests.cs`. |
| End-state owner | **Core** owns generic nested tool-pause propagation and an unambiguous continuation target. **Worker** remains the sole command recovery/outbox/progress owner. |
| Disposition / compatibility | **Replace with existing generic mechanism.** This is **Type 1**: continuation/result semantics cross Core into Worker and durable progress. The design must lock nested/parallel tool behavior, resume scope, trace ordering, and post-agent completion before implementation. |
| Containment/proof | Invalid nested continuations must fail visibly in Core; Worker must persist only a correctly identified request and retain its current interrupted outcome on crash. Later proof: Core nested-tool/negative-continuation tests, Worker redelivery/outbox tests, then default Desktop Project acceptance after behavior changes. |

## F46.1-02 — durable Worker has a closed named runner

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Confirmed defect, P0 — mission-name branch and concrete executors.** `Messaging/WorkerMissionResolver.cs` maps only `Janus`/`Naive`; `Messaging/MissionCommandProcessor.cs` branches to `JanusMissionExecutor`/`NaiveMissionExecutor`; `Program.cs` requires two named directories; `Dockerfile.conversationworker` copies two assets and supplies two environment variables. |
| Boundary/dependencies | Worker validly owns queue consumption, Worker session/retry state, provider invocation, and progress send. It does not own a product catalog or compiled execution path per declaration. `ProjectMissionNames` is checked in Application, Conversation Host, and Worker, so a third Project mission is rejected before generic execution. |
| Consumers/evidence | Application `MissionCatalog`/`ProjectService`, `ConversationApiEndpoints`, `ConversationGrain`, queue command, and Worker resolver. Relevant tests: `NaiveMissionRunTests`, `MissionCommandProcessorTests`, `ConversationApiTests`, `ProjectRunHistoryTests`, and Application Project/MissionSubmission tests. |
| End-state owner | One trusted packaged-mission admission boundary, specified by Q46.1-01, supplies immutable declaration identity/content and zero-capability policy. Worker uses one generic load/execute/progress bridge; Core executes the language. |
| Disposition / compatibility | **Replace with existing generic mechanism, P0** after Q46.1-01. Do not add a plugin system, registry, or command-supplied directory. This is **Type 1**: code/provider execution across durable queue/store requires identity, trust proof, versioning, rejection, and existing-command migration rules. |
| Containment/proof | Untrusted/load-invalid input must fail before provider use; admission failure must return a durable failure/rejection; Worker crash/outbox behavior stays unchanged. Later proof needs tampered/unsupported identity, redelivery, Janus/Naive compatibility, a new packaged declarative mission, and default-path evidence. |

## F46.1-03 — progress wire encodes Janus and Naive personas

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Confirmed defect, P0 — mission-specific projection.** `Janus/JanusPipelineProgressMapper.cs` maps `Proposer`/`Approver`/`Implementer`, turns judge status into `Approval`, and rejects unknown experts. `NaiveMissionExecutor` bypasses it; `MissionCommandProcessor` emits `ConversationParticipant.Naive`. `Conversations.Contracts/ConversationContracts.cs` persists Janus/Naive participant vocabulary and approval semantics. |
| Boundary/current owner | Worker has generic pipeline trace data yet owns persona vocabulary and an approval product rule. Conversation Host correctly owns durable sequence/store; Presentation correctly renders. The invalid middle layer is a mission-specific Worker-to-contract mapping, not an external protocol adapter. |
| Consumers/evidence | `PipelineTraceEvent`, `MappedProgressFact`, `ConversationProgress/Event/Participant/Approval`, checkpoint/event JSON, Presentation transcript. Tests: `JanusPipelineProgressMapperTests`, `NaiveMissionRunTests`, `ConversationContractsRoundTripTests`, `ConversationProgressHandlerTests`, and Presentation tests. |
| End-state owner | **Conversations.Contracts plus Worker** own a versioned generic execution-progress projection; **Presentation** owns labels/rendering. Mission metadata, if needed, arrives as authorized data, not a C# switch. |
| Disposition / compatibility | **Move/replace, P0. Type 1:** event enums/shapes are persisted and transit HTTP/SSE/Service Bus. Existing histories must remain readable through additive versioning/migration and generic client rendering. |
| Containment/proof | Unknown mapping must fail before durable corruption; a progress-send failure remains in the existing Worker outbox. Later proof: backward contract reads, sequencing/replay, unknown metadata rejection, generic rendering, and default Project run. |

## F46.1-04 — hosted result projection recognizes `Answerer`

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Confirmed defect, P1 — concrete expert-name heuristic.** `Runner/MissionRunHandler.cs` lines 234–247 returns the last trace with `ExpertName == "Answerer"` for verified output, otherwise `MissionResult.Text`. Execution remains generic but the visible outcome is inferred from an undeclared name. |
| Owner/dependencies | Runner owns `RunResponse` projection; Core owns actual declared result. API `MissionExecutionService` and ForgeUI `RoomAgentInvoker` consume `AgentText`. `Runner.Tests/MissionRunHandlerTests.cs` asserts the current special output. |
| End state/disposition | **Runner** returns `MissionResult.Text`, unless a generic declared result-selection semantic is first designed in Core. **Replace with existing generic result, P1.** |
| Compatibility/proof | Public Runner/API/UI response behavior changes, no store ownership. Later proof covers no/multiple `Answerer`, actual built-in visible results, and normal hosted path. |

## F46.1-05 — inactive Project mission origins

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Duplicate/dead code, P2 deferred.** `Application/Projects/ProjectManifest.cs` defines `Local`/`Oci` origins and digest/snapshot reference fields, but all production selections/readers (`ProjectService.cs`, `ProjectMissionHistoryReader.cs`, `MissionCatalog.cs`) accept only `BuiltIn` plus `ProjectMissionNames`. No production code constructs/consumes local or OCI selection. |
| End state/disposition | Do not create a second source of truth. **Defer:** retain only if Q46.1-01 and Phase 45 require it; otherwise Application/Projects removes it by explicit compatible migration. |
| Compatibility/proof | Manifest schema v3 is a storage commitment. Later work needs historic-manifest reads, migration evidence, and a normal Project observation. |

## Retained boundary and unresolved decision

### R46.1-01 — legacy Janus compatibility

`ConversationApiEndpoints.HandleStartConversationAsync` deliberately accepts Janus only on the legacy `/conversations` route. `Application/Adapters/Janus/LegacyJanusToolDelivery.cs` preserves its protocol hand-off while Client Runtime retains local policy/execution. This is a narrow compatibility seam with named failure behavior, not the generic Project Mission admission route. Retain it; do not broaden it in this remediation.

### Q46.1-01 — who admits a durable packaged mission?

The current `ProjectMissionNames` allow-list prevents caller-selected provider/model/expert/path input and keeps Project runs at zero capabilities. Replacing it with unchecked discovery would violate the security architecture: a Tier-2 Worker would accept executable/provider instructions without a named authorization decision.

Before implementation, a supervising design must decide:

1. Which owner authorizes immutable package identity and what exact identity/version/digest crosses Application → Conversation Host → Worker?
2. How does Worker obtain verified bytes without arbitrary command-supplied paths, and which identity can use provider credentials?
3. Is zero local capability fixed for every Project package; if not, what least-privileged approval/policy owner enforces it?
4. How do persisted Janus/Naive commands/events, old images, rejection/retry, and interrupted runs remain readable and recoverable?

This is Type 1 and blocks F46.1-02/F46.1-03. It cannot be delegated to an implementer or replaced by a settings knob, plugin framework, registry, or second Worker.

## First smallest Step 2 candidate — not approved

Prepare a plan for **F46.1-01 only**: generic nested tool-pause/continuation propagation in `ForgeMission.Core`. It advances Core's stated provider-neutral MCL execution responsibility and does not add durable state, admission, provider selection, or catalog policy. The plan must first specify compatible `MissionResult`/trace semantics and prove nested agent pause/resume without a Janus branch. It must not edit code until a supervising Codex agent explicitly approves it.
