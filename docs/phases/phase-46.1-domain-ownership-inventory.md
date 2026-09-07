# Phase 46.1 — repository-wide ownership inventory and end state

> **Status:** inventory and self-adversarial pass complete; awaiting independent supervising-Codex review.
> Parent: [Phase 46 — domain-ownership remediation](phase-46-domain-ownership-remediation.md).
> This is documentation-only work: build, test, and default-path acceptance are **N/A** until a
> proposed remediation changes executable behavior.

## Why this spoke exists

The existing component atlas and Phase 43.23 ownership record describe intended boundaries, but
they do not prove that the current source contains one execution path per generic language
behavior. This spoke produces the evidence needed to distinguish a legitimate, narrow adapter
from duplicate orchestration or mission-specific runtime behavior.

## Inventory method — locked

Read each production component's nearest README before assessing code. Start at outward entry
points and trace inward: CLI/Desktop/host/worker entry point → typed contract → domain owner →
adapter → generic Core execution/provider/tool boundary → state/progress/result. Then trace every
supported mission in the reverse direction from its declaration and experts to that entry path.

For every candidate, record only observable facts with exact source pointers. Similar names,
classes, and test fixtures are leads, not findings. A finding is confirmed only when code shows two
production paths own the same behavior, an owner reaches through another owner's seam, a concrete
mission changes the selected core execution model, or a component owns work outside its documented
reason for existing.

## Required finding record

| Field | Required content |
|---|---|
| Evidence | Exact source paths, entry/call path, and affected tests/contracts. |
| Why it exists | The user-facing or architectural responsibility, quoted or linked from the nearest component README; a proposed owner must advance that stated purpose. |
| Current behavior | The state, decision, side effect, lifecycle, or failure behavior currently owned or duplicated. |
| Boundary | What belongs inside the component and what belongs to an adjacent owner; include every bypass of that intended seam. |
| Consumers and dependencies | Direct callers, downstream consumers, upstream inputs, dependency direction, and any layer reaching around the intended owner. |
| Ownership judgment | Why the current location violates—or satisfies—the component's stated purpose and the Phase 46 generic-runtime outcome. |
| End-state owner | One authoritative component/adapter and its bounded responsibility. |
| Overlap/slop signal | Parallel implementation, responsibility-free wrapper, mission-specific generic-runtime branch, duplicate DTO/contract/state, competing orchestration path, unused abstraction, or a temporary/PoC path that became product behavior. |
| Disposition | Retain, merge, move, replace with an existing generic mechanism, delete, or defer. “Refactor” alone is not a disposition. |
| Contract and migration | Public/wire/storage/default-path effect, dependency order, compatibility requirement, and any Type-1/Type-2 decision. |
| Priority | Foundational generic runtime ownership, then domain/data ownership, then duplicate/dead wrappers and helpers; state any dependency that changes this order. |
| Failure and proof | Containment owner, caller-visible result, recovery owner, focused negative evidence, and normal-path evidence if executable behavior changes. |

The ledger must separately classify: confirmed defect; retained deliberate boundary; duplicate or
dead code; and unresolved design question. An unresolved question blocks implementation rather
than becoming an implementer assumption. The completed ledger is a component-by-component
ownership map and prioritized remediation plan, not a list of suspicious names.

## Mandatory review lenses

| Lens | Passing condition |
|---|---|
| Generic MCL execution | Mission names and concrete mission classes do not choose a core runner/run loop. New mission behavior is expressed by declared MCL, experts, generic step kinds, or a justified external-protocol adapter. |
| Domain/data ownership | One bounded-context owner mutates each fact/store; callers use typed owner contracts and never cross a datastore or internal seam. |
| Runtime/process ownership | Supervisor, host, application, client capability runtime, durable coordination, and mission reasoning each own their documented lifecycle and no second loop. |
| Transport/presentation | Hosts bind concrete typed actions; Presentation renders and gathers input without owning product rules or hidden state. |
| External effects and credentials | One narrow adapter owns each provider/network/filesystem/process boundary; credentials and policy stay with the least-privileged owner. |
| Code shape | The main flow is outline-first; side effects and failures are named; no forwarding wrapper, settings knob, generic framework, or abstraction exists without a present boundary. |

## Inventory sequence

| Order | Slice | Required outcome |
|---|---|---|
| 1 | Component atlas, solution graph, entry points, build/publish scripts | Reconcile actual project/dependency/process graph with the documented owners; identify undocumented/excluded production paths. |
| 2 | MCL parser, resolver, `ForgeMission.Core`, CLI, provider and tool boundaries | Map the one intended generic mission execution path and identify any concrete-mission selection or duplicate executor. |
| 3 | Missions, experts, Janus, Naive, hosted Runner/Worker, and progress contracts | Trace each mission declaration through execution and determine whether every special path is data, a protocol adapter, or an improper runner. |
| 4 | Application, Client Runtime, Application Host, Desktop, Orchestration, Presentation | Check use-case, transport, capability, process, and UI ownership against Phase 43.23's end state. |
| 5 | Conversation Host/Worker, API, Billing, Rooms, ForgeUI, stores, queues, and deployment interfaces | Check bounded-context ownership, tier direction, credential flow, retry/recovery, and cross-context access. |
| 6 | Tests, diagnostics, legacy compatibility paths, and source-adjacent documentation | Classify evidence-only code, dead/overlapping paths, stale ownership documentation, and every proposed remediation's safe order. |

## Deliverables and completion

Maintain the active findings as compact tables below, with deeper per-finding evidence in linked
records when necessary. Do not convert this into a chronological work log. Once an investigation
is resolved, move its narrative evidence to the matching `_completed` record while retaining its
current disposition here for Step 2. The summary map must show each meaningful component's stated
purpose, owner, bounded responsibilities, direct consumers/dependencies, and links to its finding
records, including components with no defect.

### Findings ledger

| ID | Slice | Status | Current owner/path | Proposed end state | Next decision |
|---|---|---|---|---|---|
| F46.1-01 | Generic execution / Worker | **Confirmed defect — P0, design-blocked** | Nested `ToolCalls` are dropped; child options deliberately withhold tools and continuation state; Janus hand-runs `Negotiate` then `Implement`. | Core owns an explicit generic nested tool delegation/pause/continuation semantic; Worker invokes a declaration once. | Q46.1-02 must lock delegated capability scope and resumable state before any Core edit. |
| F46.1-02 | Durable Worker catalog | **Confirmed defect — P0, dependency-blocked** | Resolver, startup config, image, and processor select `Janus`/`Naive` concrete executors. | Application/Host use Phase 45's immutable verified launch snapshot; Worker executes its supplied declaration through one generic path. | Depends on existing Phase 45 launch-snapshot contract and F46.1-01; no registry/framework substitute. |
| F46.1-03 | Durable progress contract | **Confirmed defect — P0, dependency-blocked** | Janus mapper and contract participants encode particular experts/approval; Naive emits its own path. | Additive generic trace facts carry declared mission path/expert/attempt/tool/status data; historic Janus events remain readable. | Lock the additive wire/projection contract with Phase 45 before implementation. |
| F46.1-05 | Project manifest model | **Duplicate/dead code — P2, deferred** | Local/OCI mission-origin and snapshot fields have no production consumer; Phase 45 explicitly excludes OCI/catalog installation. | Remove through a compatible Application/Projects migration after Phase 45's version model lands, unless a later approved OCI design needs it. | Do not retain speculative origin fields by default. |
| R46.1-01 | Legacy conversation API | **Retained deliberate boundary, with limit** | Legacy `/conversations` accepts Janus and its adapter validates Janus tool delivery through Bob. | Retain its external protocol/failure containment only; its downstream Worker branch remains F46.1-02. | Do not broaden the wire; review retirement only with an explicit client migration decision. |
| R46.1-02 | CLI and Runner | **Retained / positive evidence** | Both parse, resolve, and execute declarations through `PipelineRunner` without Janus/Naive branches. | Preserve as the generic reference path. | Use its seams when designing F46.1-01/02. |
| D46.1-01 | Durable Project admission | **Retained deliberate boundary / locked Type-1 dependency** | Current shared two-name allow-list is the present boundary; Phase 45's `MissionVersionLaunch` is the already-designed replacement chain. | Application owns version content; Host verifies/stores bounded content; Worker receives verified value only, with no path, credential, provider selection, or capability. | Phase 45 implementation is a prerequisite, not a new Phase 46 architecture decision. |
| Q46.1-02 | Nested tool delegation | **Unresolved design question — Type 1** | Core intentionally does not inherit `Tools`, `StartAtAgent`, or continuation callbacks into a declared child mission. | One Core-owned continuation model must make permitted nested tool delegation explicit and preserve least privilege. | Supervisor must choose the supported delegation/resume rule; blocks F46.1-01. |
| Q46.1-03 | Hosted answer selection | **Unresolved design question — Type 2** | Runner's `Answerer` convention displays the pre-verifier answer; MCL has no declared step-output selector. | Either document the convention as built-in content policy or design an explicit generic result-selection semantic. | Does not block generic execution or F46.1-01–03; do not delete the behavior blindly. |

Detailed evidence and the proposed, unapproved dispositions are in the
[Phase 46.1 evidence record](phase-46.1-domain-ownership-inventory_evidence.md).

### Component ownership map

| Component | Why it exists / owner | Consumers and dependencies | Findings |
|---|---|---|---|
| Parser | MCL source spans/syntax. | Core. | R46.1-02 |
| Core | Provider-neutral resolution, execution, trace and primitive contracts. | CLI, Runner, Worker, Application/Client Runtime. | F46.1-01 |
| ChatClients | Provider-SDK adapter. | CLI, Runner, Worker. | R46.1-02 |
| Scout | Web-search contract/adapter. | Core; composed by CLI/Runner. | R46.1-02 |
| CLI | Native-AOT command composition. | Core, ChatClients, Scout, Serve, Docker. | R46.1-02 |
| Serve | Shared OpenAI/Anthropic wire mapping. | CLI and Runner. | None |
| Docker | Narrow Docker operations. | CLI and Orchestration. | None |
| Runner.Contracts | Run/progress/artifact/usage wire. | Runner, API, ForgeUI. | Q46.1-03 consumer contract |
| Runner | Stateless load/execute/artifact/cache host. | Core/ChatClients/Serve; API/ForgeUI callers. | Q46.1-03, R46.1-02 |
| Application | Project, submission, session, conversation, history owners. | Host, Client Runtime, Conversation adapter. | D46.1-01, F46.1-05 |
| Application.Transport | Typed action/event/JSON/channel vocabulary. | Presentation and Application Host. | None |
| Application.Host | Loopback HTTP/SSE composition. | Desktop starts it; Presentation calls it. | None |
| ClientRuntime | Scoped local capability policy/execution. | Application only. | None; Worker has no local-capability path. |
| Presentation | Rendering/navigation/view state. | Application Transport. | F46.1-03 consumer |
| Desktop | User-launched process supervision. | Starts Application Host/native Host. | None |
| Desktop.Contracts | Native-host and Supervisor pipe contract. | Desktop Host/Photino/Supervisor. | None |
| Desktop.Host | Disposable native-window process. | Desktop Contracts/Photino. | None |
| Desktop.Photino | Photino native-host implementation. | Desktop.Host. | None |
| Orchestration | Runtime endpoint/readiness/owned adapters. | Desktop/Application Host startup. | None; Vanilla is deployment default, not Core selection. |
| Conversations.Contracts | Versioned durable messages/projections. | Application Transport, Host, Worker, Presentation. | F46.1-03, D46.1-01 |
| ConversationHost | Sole durable command/event/store owner. | Application adapter and Worker queue boundary. | R46.1-01, D46.1-01 |
| ConversationWorker | Restartable reasoning/progress publisher. | Conversation queues; Core/ChatClients. | F46.1-01, F46.1-02, F46.1-03 |
| ConversationPresentation | Presentation-only activity rendering. | Presentation. | F46.1-03 consumer |
| API | Platform-key ingress and settlement composition. | Billing and Runner. | None; static catalog is edge policy. |
| Billing | Accounts, keys, pricing, ledgers. | API/ForgeUI. | None |
| Rooms | Collaboration facts/invariants. | Rooms.Data and ForgeUI. | None |
| Rooms.Data | Rooms EF persistence/schema owner. | ForgeUI. | None |
| ForgeUI | Authenticated Rooms/Runner surface. | Rooms, Rooms.Data, Billing, Runner. | None; agent directory is UI policy. |
| TransportProbe / ProjectServiceProbe | Diagnostic executables owned by Transport/Application. | Diagnostic-only. | Retained evidence, no second owner. |

The actual solution graph matches the atlas's 28 production components and two excluded diagnostic
probes. The evidence record contains exact references, entry/process paths, test anchors, and the
only relevant execution divergence.

### Proposed remediation order — not approved work

| Order | Candidate disposition | Dependency / gate |
|---|---|---|
| 1 | Supervising design decision for nested tool delegation (Q46.1-02). | Lock tool grant inheritance, opaque continuation state, resume target, trace ordering, and failure proof; no code task yet. |
| 2 | Implement Phase 45's immutable launch-snapshot chain (D46.1-01). | Existing Type-1 design: Project content → Host verification/Blob → verified Worker value, with no capability/path/credential grant. |
| 3 | Replace Worker name resolver/concrete executors and persona mapper (F46.1-01–03). | Depends on 1–2; retain Worker outbox and Host store ownership; use additive generic trace facts. |
| 4 | Resolve hosted answer selection (Q46.1-03). | Type-2 content/response decision; do not delete the current convention without a visible-result contract. |
| 5 | Remove inactive Project mission origins (F46.1-05). | After Phase 45 migration unless a separately approved OCI design needs them. |

### Adversarial second-pass result

| Criterion | Result | Evidence / gap |
|---|---|---|
| Single owner | **FAIL** | F46.1-01 and F46.1-03 give Core/Janus and Worker/persona mapping competing ownership; D46.1-01 supplies the Phase 45 launch-content owner chain. |
| Simple composition | **FAIL** | F46.1-02's resolver/executors are a second declaration interpreter; the existing Worker outbox and Host store boundary remain narrow. |
| Failure locality | **FAIL** | Worker recovery and legacy tool-report failure are explicit, but Q46.1-02 lacks one Core-owned capability scope/resume/failure contract. |
| Extension path | **FAIL** for durable Worker; **PASS** for CLI/Runner | Durable additions require code/image branches; CLI/Runner execute declarations generically. |
| Progressive disclosure and narrowness | **FAIL** | The readable Janus executor still combines topology, approval capture, continuation reconstruction, and progress adaptation. |
| No disguised complexity | **PASS, conditional** | The proposed path reuses Core, Phase 45 snapshots, queue/outbox and additive contracts; it rejects a registry, dispatcher, framework, wrapper stack, and settings mode. |
| Evidence and implementation readiness | **FAIL** | Q46.1-02 is Type 1; no implementation card is ready. |

Detailed second-pass evidence, including F46.1-04's reclassification as Q46.1-03 and the resolved former
durable-admission question, is in the [evidence record](phase-46.1-domain-ownership-inventory_evidence.md#adversarial-second-pass-result).
Remaining gaps: Q46.1-02 (Type 1 nested tool delegation/continuation) and Q46.1-03 (Type 2 hosted
answer selection).

### Step 1 done when

The inventory **disproves** the premise for the durable Worker: `Janus`/`Naive` select concrete
executors, and Janus manually splits its declared pipeline because Core deliberately isolates child
tools/continuations and drops child tool pauses. It confirms generic execution for CLI and hosted
Runner, subject to the non-blocking hosted-output convention in Q46.1-03. A separate supervising
Codex agent must adversarially review these proposed dispositions before any work is approved.

There is **no implementation-ready Phase 46.2 task yet**. The former F46.1-01 candidate is too
small only superficially: Q46.1-02 must first decide generic nested tool delegation and resume
semantics. After that decision, the smallest implementation card is Core-only F46.1-01; it must
not absorb Worker catalog, durable-wire, or Phase 45 launch-snapshot work.
