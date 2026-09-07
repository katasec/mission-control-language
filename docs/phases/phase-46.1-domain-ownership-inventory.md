# Phase 46.1 — repository-wide ownership inventory and end state

> **Status:** inventory, self-adversarial pass, and operator-approved generic mission-to-hands
> design closure complete; awaiting independent supervising-Codex review.
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
| F46.1-01 | Generic execution / Worker | **Confirmed defect — P0, design complete** | Nested `ToolCalls` are dropped; child options deliberately withhold tools and continuation state; Janus hand-runs `Negotiate` then `Implement`. | Core owns a generic root-scoped tool pause and opaque continuation; a child request never inherits Bob or a tool dispatcher. Worker invokes a declaration once. | Ready for a separately supervised Core-only plan; D46.1-02 fixes the capability/continuation contract. |
| F46.1-02 | Durable Worker catalog | **Confirmed defect — P0, dependency-blocked** | Resolver, startup config, image, and processor select `Janus`/`Naive` concrete executors. | Application/Host use Phase 45's immutable verified launch/profile snapshot; Worker executes supplied declaration and generic tool facts through one path. | Depends on F46.1-01 and Phase 45's profile, durable-tool, and bounded-Bob implementation; no registry/framework substitute. |
| F46.1-03 | Durable progress contract | **Confirmed defect — P0, dependency-blocked** | Janus mapper and contract participants encode particular experts/approval; Naive emits its own path. | Additive generic trace/tool facts carry mission path, expert, attempt, correlation, ordered status and result; historic Janus events remain readable. | Depends on D46.1-02's durable request/result contract and the F46.1-02 migration. |
| F46.1-05 | Project manifest model | **Duplicate/dead code — P2, deferred** | Local/OCI mission-origin and snapshot fields have no production consumer; Phase 45 explicitly excludes OCI/catalog installation. | Remove through a compatible Application/Projects migration after Phase 45's version model lands, unless a later approved OCI design needs it. | Do not retain speculative origin fields by default. |
| R46.1-01 | Legacy conversation API | **Retained deliberate boundary, with limit** | Legacy `/conversations` accepts Janus and its adapter validates Janus tool delivery through Bob. | Retain its external protocol/failure containment only; its downstream Worker branch remains F46.1-02. | Do not broaden the wire; review retirement only with an explicit client migration decision. |
| R46.1-02 | CLI and Runner | **Retained / positive evidence** | Both parse, resolve, and execute declarations through `PipelineRunner` without Janus/Naive branches. | Preserve as the generic reference path. | Use its seams when designing F46.1-01/02. |
| D46.1-01 | Durable Project admission | **Retained deliberate boundary / locked Type-1 dependency** | Current shared two-name allow-list is the present boundary; Phase 45's `MissionVersionLaunch` is the already-designed replacement chain. | Application owns version content; Host verifies/stores bounded content; Worker receives verified value only, with no path, credential, provider selection, or capability. | Phase 45 implementation is a prerequisite, not a new Phase 46 architecture decision. |
| D46.1-02 | Generic mission-to-hands | **Locked Type-1 design / implementation pending** | Core intentionally isolates child tools/continuations; current durable tool handling is Janus-specific. | Version declares one immutable profile; Application grants one live conversation attachment to Bob; Host owns ordered request/result facts; Worker carries generic facts; Core resumes only the root scope. | Implement F46.1-01 first, then Phase 45's profile and durable-tool path; no runtime escalation, implicit inheritance, or Worker-side transcript. |
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
| Application | Project, submission, session, conversation, history owners. | Host, Client Runtime, Conversation adapter. | D46.1-01, D46.1-02, F46.1-05 |
| Application.Transport | Typed action/event/JSON/channel vocabulary. | Presentation and Application Host. | D46.1-02 |
| Application.Host | Loopback HTTP/SSE composition. | Desktop starts it; Presentation calls it. | None |
| ClientRuntime | Scoped local capability policy/execution. | Application only. | D46.1-02; Worker has no local-capability path. |
| Presentation | Rendering/navigation/view state. | Application Transport. | D46.1-02, F46.1-03 consumer |
| Desktop | User-launched process supervision. | Starts Application Host/native Host. | None |
| Desktop.Contracts | Native-host and Supervisor pipe contract. | Desktop Host/Photino/Supervisor. | None |
| Desktop.Host | Disposable native-window process. | Desktop Contracts/Photino. | None |
| Desktop.Photino | Photino native-host implementation. | Desktop.Host. | None |
| Orchestration | Runtime endpoint/readiness/owned adapters. | Desktop/Application Host startup. | None; Vanilla is deployment default, not Core selection. |
| Conversations.Contracts | Versioned durable messages/projections. | Application Transport, Host, Worker, Presentation. | F46.1-03, D46.1-01, D46.1-02 |
| ConversationHost | Sole durable command/event/store owner. | Application adapter and Worker queue boundary. | R46.1-01, D46.1-01, D46.1-02 |
| ConversationWorker | Restartable reasoning/progress publisher. | Conversation queues; Core/ChatClients. | F46.1-01, F46.1-02, F46.1-03, D46.1-02 |
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
| 1 | Plan Core's generic root-scoped pause/continuation (F46.1-01). | D46.1-02 is locked; the independently supervised plan must not grant a child Bob authority or absorb any durable/profile work. |
| 2 | Implement Phase 45's immutable launch/profile and durable tool path (D46.1-01/D46.1-02). | Project version → Application attachment → bounded Bob; Host verifies/orders; Worker receives verified value and generic facts only. |
| 3 | Replace Worker name resolver/concrete executors and persona mapper (F46.1-02/03). | Depends on 1–2; retain Worker outbox and Host store ownership; use additive generic trace/tool facts. |
| 4 | Resolve hosted answer selection (Q46.1-03). | Type-2 content/response decision; do not delete the current convention without a visible-result contract. |
| 5 | Remove inactive Project mission origins (F46.1-05). | After Phase 45 migration unless a separately approved OCI design needs them. |

### Adversarial second-pass result

| Criterion | Result | Evidence / gap |
|---|---|---|
| Single owner | **PASS (locked design); FAIL (current source)** | Application grants the profile-bound attachment; Bob enforces/executes; Host orders durable facts; Worker reasons/publishes; Core pauses/resumes. Janus and persona ownership remain until F46.1-01–03 land. |
| Simple composition | **PASS (locked design); FAIL (current source)** | The one typed path composes Transport, Application, Bob, Host, Worker, and Core. The current named resolver/executors remain a second interpreter. |
| Failure locality | **PASS (locked design); FAIL (current source)** | D46.1-02 names correlation, `AwaitingHands`, typed denial/cancel/failure, and recovery owners. Current Janus continuation has no such generic contract. |
| Extension path | **PASS (locked design); FAIL (current durable Worker)** | A new version declares one fixed profile and generic MCL/expert content; no catalog image branch or executor. Current durable additions still require branches. |
| Progressive disclosure and narrowness | **PASS (locked design); FAIL (current source)** | Each boundary has one concern; Core receives an opaque pause, not a transcript. The Janus executor still combines topology, continuation, and projection. |
| No disguised complexity | **PASS** | Three fixed profiles, one attachment, one outstanding request, and existing queue/outbox boundaries replace no behavior with a registry, dispatcher, wrapper stack, dial, or hidden Full Access mode. |
| Evidence and implementation readiness | **PASS (design); FAIL (implementation)** | The contract, negative cases, and default-path observation are named. F46.1-01 is now plan-ready but remains unapproved; the code gaps F46.1-01–03 remain. |

The [evidence record](phase-46.1-domain-ownership-inventory_evidence.md#d461-02--locked-generic-mission-to-hands-contract)
holds D46.1-02's exact protocol, failure and proof records. Q46.1-03 remains the only independent
Type-2 design question; the remaining gaps in F46.1-01–03 are implementation, not architecture.

### Step 1 done when

The inventory **disproves** the premise for the durable Worker: `Janus`/`Naive` select concrete
executors, and Janus manually splits its declared pipeline because Core deliberately isolates child
tools/continuations and drops child pauses. D46.1-02 now locks the generic root-scoped replacement.
It confirms generic execution for CLI and hosted Runner, subject to the non-blocking hosted-output
convention in Q46.1-03. A separate supervising Codex agent must adversarially review this design
closure before any work is approved.

The first implementation-ready **candidate** is Core-only F46.1-01: generic root-scoped
pause/result/opaque-continuation semantics. It is not approved and must not absorb profile
admission, Bob enforcement, Host/Worker durable facts, catalog migration, or legacy routing.
