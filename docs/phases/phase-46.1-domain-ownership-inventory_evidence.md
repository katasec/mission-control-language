# Phase 46.1 — domain-ownership inventory evidence

> **Status:** self-adversarial pass and operator-approved generic mission-to-hands design closure
> complete; proposed evidence awaiting independent supervising-Codex review. This source/documentation inspection
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
It **revised** the first pass: durable mission admission is already owned by the Phase 45 design
rather than an open new architecture, and `Answerer` is an implicit output convention rather than
a separate execution loop. The operator has now locked D46.1-02, so nested-tool continuation is
design-complete but still unimplemented.

| Composition and Repairability criterion | Result | Evidence / remaining gap |
|---|---|---|
| 1. Single owner | **PASS (design); FAIL (current source)** | D46.1-02 assigns Application the grant, Bob enforcement/execution, Host durable order, Worker restartable reasoning/progress, and Core pause/resume. Janus and persona ownership remain until F46.1-01–03 land. |
| 2. Simple composition | **PASS (design); FAIL (current source)** | The target is one typed path; no profile is a second runtime or Worker dispatcher. `WorkerMissionResolver` and concrete executors remain a second interpreter today. |
| 3. Failure locality | **PASS (design); FAIL (current source)** | D46.1-02 names `AwaitingHands`, correlation, typed denial/cancel/failure and recovery. Current Janus continuation has no generic contract. |
| 4. Extension path | **PASS (design); FAIL (current durable Worker)** | New versions select one fixed profile plus generic MCL/expert content. Current durable additions still need named directories, resolver cases, executors, mapping and image changes. |
| 5. Progressive disclosure and narrowness | **PASS (design); FAIL (current source)** | Core's opaque pause is distinct from the Host request/result and Bob policy seam. Janus still mixes topology, continuation and projection. |
| 6. No disguised complexity | **PASS** | Three fixed profiles, one live attachment and one outstanding request reject a registry, generic dispatcher, wrapper stack, permission dial and hidden Full Access mode. |
| 7. Evidence and implementation readiness | **PASS (design); FAIL (implementation)** | The locked contract names compatibility, negative cases and default-path proof. F46.1-01 is now plan-ready but unapproved; F46.1-01–03 are not implemented. |

The only remaining design question is Q46.1-03 (Type 2 hosted answer selection). No durable
admission/trust decision is open: D46.1-01 and D46.1-02 are locked Type-1 designs and Phase 45
implementation is their dependency, not permission to invent a parallel catalog or authority path.

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
| Classification and signal | **Confirmed defect, P0, design complete — mission-specific run-loop workaround.** `PipelineRunner.cs` lines 235–259 retain only a child's text and discard `ToolCalls`; lines 183–202 pause only for a direct agent. `CreateChildOptions` (lines 372–389) deliberately withholds `Tools`, `StartAtAgent`, context objects, and pre-agent callback from children. |
| Current path | `MissionCommandProcessor.ProcessFreshStartAsync` → `JanusMissionExecutor.RunFullMissionAsync` → `RunAsync("Negotiate")`; on approval a second direct `RunAsync("Implement", Tools, AllowMultipleToolCalls:false)`. `RunContinuationAsync` reconstructs a provider conversation and calls `Implement` with `StartAtAgent:true`. This is evidence of three missing generic semantics, not merely a dropped result field. |
| Why/current owner | Core's README assigns provider-neutral pipeline semantics and result/trace contracts to Core. Worker's README assigns restartable mission reasoning/progress publication, not another declaration interpreter. Janus's executor owns declaration topology and continuation mechanics that belong at the generic Core seam. |
| State and failure | Worker correctly persists `WaitingForTool`/outstanding call before publish, retains an outbox, and reports interrupted rather than replaying an ambiguous provider call. Those effects remain Worker-owned. Core currently lacks the generic result/continuation information needed for that safety path. |
| Consumers/evidence | `PipelineRunOptions`, `MissionResult`, `PipelineTraceEvent`, `DirectExpertRunner`, Worker session state, `ConversationCommand`/`ConversationProgress`; tests: `ForgeMission.Tests/Runtime/PipelineTraceTests.cs`, `ConversationWorker.Tests/JanusMissionExecutorToolCallOptionsTests.cs`, and `MissionCommandProcessorTests.cs`. |
| End-state owner | **Core** owns one root-scoped pause: mission-path/agent location, a root-tool-scope reference, provider tool-call identity and opaque continuation sufficient to resume exactly that root declaration. A child has no dispatcher, Bob reference or implicit `Tools` inheritance; its allowed request is routed through the root scope. **Worker** remains the sole command recovery/outbox/progress owner. |
| Disposition / compatibility | **Replace with existing generic mechanism** under D46.1-02. This is **Type 1** because the root scope crosses Core's result/continuation boundary, but it never grants a child a capability. No `Janus` name, concrete executor, host-side provider transcript, plugin, registry or Worker-side continuation remains. |
| Containment/proof | Core rejects a request outside the declared root scope before provider continuation. Worker persists exactly one opaque pause/request correlation before publish and reports interrupted rather than replays ambiguous provider work. Later proof: Core nested-child, unsupported-child, duplicate/negative-resume and ordered-trace tests; Worker redelivery/outbox tests; then default Desktop acceptance. |

## F46.1-02 — durable Worker has a closed named runner

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Confirmed defect, P0 — mission-name branch and concrete executors.** `Messaging/WorkerMissionResolver.cs` maps only `Janus`/`Naive`; `Messaging/MissionCommandProcessor.cs` branches to `JanusMissionExecutor`/`NaiveMissionExecutor`; `Program.cs` requires two named directories; `Dockerfile.conversationworker` copies two assets and supplies two environment variables. |
| Boundary/dependencies | Worker validly owns queue consumption, Worker session/retry state, provider invocation, and progress send. It does not own a product catalog or compiled execution path per declaration. `ProjectMissionNames` is checked in Application, Conversation Host, and Worker, so a third Project mission is rejected before generic execution. |
| Consumers/evidence | Application `MissionCatalog`/`ProjectService`, `ConversationApiEndpoints`, `ConversationGrain`, queue command, and Worker resolver. Relevant tests: `NaiveMissionRunTests`, `MissionCommandProcessorTests`, `ConversationApiTests`, `ProjectRunHistoryTests`, and Application Project/MissionSubmission tests. |
| End-state owner | **Phase 45's locked chain** is the authoritative admission design: Application/Projects owns immutable `MissionVersionLaunch` content and its one profile; Application binds the approved conversation/session to Bob; Host verifies hash/size, stores bounded content and orders durable tool facts; Worker receives verified value content and generic tool facts only. Worker never receives a Project path, credential, Bob handle, provider selection, local root or arbitrary directory. |
| Disposition / compatibility | **Replace with existing generic mechanism, P0** after F46.1-01 and Phase 45's profile/attachment/durable-tool implementation. Do not add a plugin system, registry, command-supplied directory or hidden elevated fallback. The established snapshot-and-hands topology is **Type 1** and defines identity, trust, versioning, authority, rejection and recovery ownership. |
| Containment/proof | Invalid hash/size/version is rejected by Host before Blob/queue/provider work. A missing live attachment becomes `AwaitingHands`; Bob returns out-of-profile/denied/cancelled/failed outcomes as typed facts. Preserve Worker crash/outbox behavior. Later proof needs invalid-launch, profile-tamper, root-escape/network/credential denial, redelivery, historic Janus/Naive reads, new-version and default-path evidence. |

## F46.1-03 — progress wire encodes Janus and Naive personas

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Confirmed defect, P0 — mission-specific projection.** `Janus/JanusPipelineProgressMapper.cs` maps `Proposer`/`Approver`/`Implementer`, turns judge status into `Approval`, and rejects unknown experts. `NaiveMissionExecutor` bypasses it; `MissionCommandProcessor` emits `ConversationParticipant.Naive`. `Conversations.Contracts/ConversationContracts.cs` persists Janus/Naive participant vocabulary and approval semantics. |
| Boundary/current owner | Worker has generic pipeline trace data yet owns persona vocabulary and an approval product rule. Conversation Host correctly owns durable sequence/store; Presentation correctly renders. The invalid middle layer is a mission-specific Worker-to-contract mapping, not an external protocol adapter. |
| Consumers/evidence | `PipelineTraceEvent`, `MappedProgressFact`, `ConversationProgress/Event/Participant/Approval`, checkpoint/event JSON, Presentation transcript. Tests: `JanusPipelineProgressMapperTests`, `NaiveMissionRunTests`, `ConversationContractsRoundTripTests`, `ConversationProgressHandlerTests`, and Presentation tests. |
| End-state owner | **Conversations.Contracts plus Worker** own additive generic trace/tool facts: mission path, expert identifier/kind, attempt, tool ordinal/correlation, request/result/status and text/reason. Host owns canonical sequence and durable request/result state; **Presentation** renders labels, profile, status and confirmation as data. Judge completion is an ordinary completed trace fact; historic Janus `Approval`/participant events remain readable rather than being reinterpreted. |
| Disposition / compatibility | **Move/replace, P0. Type 1:** event enums/shapes are persisted and transit HTTP/SSE/Service Bus. Add generic facts for new launch-snapshot runs; preserve historic event readers. `AwaitingHands`, confirmation and typed terminal outcomes are additive, not a new Worker transcript. |
| Containment/proof | Host validates shape, correlation, one-outstanding-request and trace ordering before append; unknown/malformed/late input is rejected without corruption. Progress-send failure remains in Worker outbox. Later proof: old-event reads, generic sequencing/replay, duplicate/wrong-correlation rejection, no-hands recovery, generic rendering and default Project run. |

## Q46.1-03 — terminal declared result is the hosted answer

| Field | Evidence, owner, and proposed disposition |
|---|---|
| Classification and signal | **Resolved Type-2 projection correction (2026-09-08).** The former Runner convention selected the last trace entry named `Answerer` for a verified run. That made a specific expert name load-bearing even though MCL has no intermediate-result selector. |
| Owner/dependencies | Runner owns `RunResponse.AgentText`; Core owns the terminal `MissionResult`; API `MissionExecutionService` and ForgeUI `RoomAgentInvoker` forward the projection. Rooms does not choose a trace step. |
| Supervisor decision | The terminal declared MCL result is always the operator-facing answer. Mission declarations express verification, debate, synthesis, and retry topology so their terminal step emits the intended answer. `Answerer` is ordinary author vocabulary, not runtime behavior. |
| Compatibility/proof | Task D removes the name lookup while retaining failure and tool-pause behavior. The regression proves a prior step literally named `Answerer` cannot override a distinct terminal verifier result; the hallucination guard's terminal verifier emits its verified original answer. No MCL grammar, TOML, wire-schema, catalog, or Rooms selector changes. |

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

### D46.1-02 — locked generic mission-to-hands contract

This is the operator-approved Type-1 replacement for Q46.1-02. It is a target design, not a
description of current source behavior. It closes the architecture question; F46.1-01–03 remain
implementation gaps and need separate supervising-Codex plan approval.

#### Fixed profile, grant, and enforcement boundaries

`MissionCapabilityProfile` is a versioned declaration with exactly three initial values:
`NoHands`, `ProjectWorkspace`, and `ProjectWorkspaceAndTerminal`. A mission version carries one
value; changing it creates a new version. The profile is a request for a bounded capability set,
not authority and not a user-adjustable bundle of tools.

| Concern | Sole owner and exact rule |
|---|---|
| Version declaration | Application/Projects persists the one profile with the immutable mission version and includes it in `MissionVersionLaunch`. |
| Launch approval / grant | Application displays that exact profile when a new Mission Conversation is created. Only an acceptance of the pinned version/profile creates the durable conversation record and a live Application conversation/session attachment. The profile itself never grants a tool. |
| Live attachment | Application binds `(conversation_id, mission_version_id, approved_profile, application_session_id)` to one fresh, ephemeral Bob session on create/reconnect. The attachment expires with that local session; it cannot be reused by another conversation or version. |
| Policy and execution | Bob (`ClientRuntime`) is the only capability policy, structural-containment, confirmation, audit, execution, cancellation and cleanup owner. Application never evaluates a tool and Host/Worker never receives Bob, a root path, credential or capability handle. |
| Durable facts | Conversation Host owns accepted request/result identity, canonical sequence, `AwaitingHands`/tool state and replay. Worker publishes/consumes generic facts only; it owns restartable provider reasoning and its outbox, not a durable transcript or policy. |
| Continuation | Core owns the generic root-scoped pause/result/opaque continuation. The continuation carries no Bob authority. A child mission never receives a dispatcher or implicit tool inheritance; an allowed child request pauses through the root's declared scope. |
| Presentation | Desktop/TUI renders the profile, tool status and any confirmation through Application.Transport. It never grants, narrows, expands or executes authority. |

The profile meanings are fixed for this design:

| Profile | Bob-dispatchable scope | Structural denial |
|---|---|---|
| `NoHands` | No mission tools are declared or dispatchable. | Any tool request is a typed out-of-profile denial, never a prompt or fallback. |
| `ProjectWorkspace` | Bounded Project-workspace file capabilities only. | Any resolved path outside the validated Project root, symlink escape, credential access, network use, terminal request or other host authority is denied. |
| `ProjectWorkspaceAndTerminal` | The same bounded file capabilities plus terminal execution whose filesystem view, working directory, environment and process boundary are structurally constrained to that Project workspace. | Leaving the Project boundary, network access, credentials, arbitrary host access or another capability is denied. Command-text "dangerousness" heuristics are not an authorization mechanism. |

The existing `WorkspaceTerminalProvider` documents an unrestricted shell/environment boundary, so
it cannot itself satisfy the terminal profile. The later Bob task must supply structural process
containment at the ClientRuntime seam; changing a working directory, filtering command text, or
asking the Worker to decide is not sufficient.

#### One end-to-end durable path

```text
Desktop/TUI
  -> Application.Transport typed create/reconnect, status, and confirmation actions
  -> Application verifies pinned version/profile and attaches the live session to Bob
  -> Bob evaluates fixed profile + its per-operation policy, then audits/executes/cancels
  -> Application submits a typed correlated tool outcome
  -> Conversation Host appends/assigns sequence and queues one generic result command
  -> Conversation Worker persists/publishes generic progress and invokes Core once
  -> Core resumes the opaque root continuation and emits the next generic result or pause
```

At start, Application derives the root's generic tool declarations solely from the approved
profile. Worker sees their names/schema and the verified launch value only; declarations are not a
Bob connection or a local-capability grant. A child agent may emit a tool call only through Core's
root scope. Core returns `ToolPause(root_mission_path, agent_location, tool_call_id,
continuation_token)`; the Worker persists the opaque pause, never reconstructs a provider
conversation or interprets its token.

For every attempt there is one outstanding request. Worker deterministically derives
`ToolRequestId` from `(conversation_id, turn_attempt_id, tool_ordinal)`, persists the exact pause
and ordinal before publishing, and emits a generic request fact. Host assigns its canonical event
sequence on acceptance. The correlation record is therefore `(conversation_id, turn_attempt_id,
tool_ordinal, tool_request_id, host_sequence)`; a result must name the exact request ID and is
accepted once. Parallel children are traceable through their root mission path but cannot create a
second active request; Core queues/resumes one root pause at a time in trace order.

#### States, outcomes, and recovery

| Boundary / condition | Durable state and visible result | Recovery owner |
|---|---|---|
| Valid tool request with a live attachment | Host records ordered `ToolRequested` and `AwaitingToolResult`; Application delivers it to Bob. | Bob completes one typed result; Host queues exactly one Core continuation. |
| Valid tool request without a live attachment | Host records ordered `ToolRequested` then `AwaitingHands`; it does not run remotely, fall back, or synthesize success. | On reconnect Application creates a fresh attachment under the already-approved profile and Host redelivers the outstanding request; cancellation may terminate the attempt. |
| Out of profile / root escape / network / credential / unknown capability | Bob returns `DeniedOutOfProfile` with the request correlation; Host stores it and Core receives it as an error tool result. No approval prompt appears. | Bob/Application owns the denial; Core decides the mission's normal error continuation. |
| Bob policy denies, operation fails, or its fixed capability contract needs confirmation | Bob emits respectively `DeniedByPolicy`, `Failed`, or `ConfirmationRequired`, all correlated. Confirmation is a fixed Bob capability rule, not a profile dial or command-string heuristic. | Application.Transport renders the status. A confirmation decision returns through Application to the same Bob attachment; only Bob then executes or returns `DeniedByOperator`. |
| Cancellation | Before an effect completes, Bob returns `Cancelled`; a user cancelling the entire attempt makes Host terminalize it and never resume the pause. | Bob owns operation cancellation; Host owns durable terminal state; Worker does not replay an ambiguous effect. |
| Wrong, duplicate, unknown, or late result | Host rejects it with a typed conflict/no-op and appends no second result; the original request remains authoritative. | Caller refreshes/reconciles; Worker/Host idempotency preserves one continuation. |
| Worker/Host restart | Worker reloads its persisted opaque pause/outbox; uncertain in-provider execution becomes `Interrupted`, never a replay. Host replays only accepted facts in canonical sequence. | Worker owns provider recovery/outbox; Host owns durable replay/order. |

`ConfirmationRequired` is a durable generic tool-status fact so a reconnect can show the same
request. It is not a new participant, Janus approval, or a Presentation-owned decision. The UI has
no profile checkboxes, narrowing control or escalation path. A broader power requires a future
named profile and design; there is no `FullAccess`, arbitrary path, credential, publishing/push or
network profile in this contract.

#### Compatibility, verification, and implementation order

Historic Janus participants, approval events, tool results and legacy transport remain readable.
The legacy Janus route gains no authority; its downstream specialized Worker path is removed only
by F46.1-02/03's generic migration. New facts and status/outcome enums append additively with
source-generated JSON. There is no new public ingress, direct cross-store access, data-plane
credential at Application Host, or Worker-to-Bob route.

| Proof | Required observation after implementation |
|---|---|
| Core contract | A nested child request produces one root pause with no inherited dispatcher; unsupported/duplicate/late continuations fail explicitly and preserve trace order. |
| Host/Worker durability | Wrong correlation is rejected; one request/result is sequenced once; Worker redelivery resends its outbox; restart during provider work is `Interrupted`; no attachment is `AwaitingHands`. |
| Application/Bob containment | A profile/hash mismatch creates neither conversation grant nor Bob attachment. `NoHands`, out-of-profile, root escape, network and credential requests are typed denials. Terminal execution proves structural workspace/process containment, not command filtering. |
| Presentation parity | Desktop and TUI invoke the same typed create/attach/status/confirmation actions and render the same profile/outcome; neither owns policy or dispatch. |
| Default path | The published zero-argument Desktop, absent overrides, normal cloud/Kind route and a disposable Project create an approved-version conversation; observe profile approval, bounded in-root operation, reconnect `AwaitingHands` recovery, denial and durable ordered trace/result. Controlled fakes remain non-acceptance evidence. |

Implementation order is: (1) F46.1-01 Core root pause/continuation without increasing authority;
(2) Phase 45.1 immutable profile/launch and Application grant model; (3) ClientRuntime's bounded
workspace/terminal enforcement; (4) Phase 45.2 Host/Worker generic durable protocol; (5)
F46.1-02/03 Worker catalog/projection migration; then Presentation evidence in 45.3. Each step is
separately planned and approved; this record authorizes no code.

## First smallest Step 2 candidate — not approved

The first implementation-ready **candidate** is F46.1-01 only: Core's generic root-scoped
pause/result/opaque-continuation semantics. It contains no profile persistence, Application grant,
Bob policy, Host/Worker durable protocol, catalog migration or legacy-route change, and must not
increase authority. It still needs a separate supervising Codex agent to approve its task card
before any code is edited.
