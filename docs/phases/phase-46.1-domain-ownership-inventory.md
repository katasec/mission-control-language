# Phase 46.1 — repository-wide ownership inventory and end state

> **Status:** inventory complete; awaiting adversarial review by a separate supervising Codex agent.
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
| F46.1-01 | Generic execution / Worker | **Confirmed defect — P0** | Nested `ToolCalls` are dropped by Core; Janus manually runs `Negotiate` then `Implement`. | Core owns generic nested pause/continuation semantics; Worker invokes a declaration once. | Approve a continuation/result contract before any Core edit. |
| F46.1-02 | Durable Worker catalog | **Confirmed defect — P0** | Resolver, startup config, image, and processor select `Janus`/`Naive` concrete executors. | Worker loads an authorized packaged declaration through one generic path. | Blocked by Q46.1-01; no registry/framework substitute. |
| F46.1-03 | Durable progress contract | **Confirmed defect — P0** | Janus mapper and contract participants encode particular experts/approval; Naive emits its own path. | Versioned generic pipeline-progress projection at Conversation contract/Worker boundary. | Lock wire/storage compatibility before implementation. |
| F46.1-04 | Runner result projection | **Confirmed defect — P1** | `BuildAgentText` privileges an expert literally named `Answerer`. | Runner returns declared result or an explicit generic result-selection contract. | Review API/ForgeUI `AgentText` compatibility. |
| F46.1-05 | Project manifest model | **Duplicate/dead code — P2, deferred** | Local/OCI mission-origin and snapshot fields have no production consumer; reads/writes only allow built-ins. | Retain only if the approved package model needs them; otherwise migrate/remove. | Depends on Q46.1-01 and Phase 45 authoring design. |
| R46.1-01 | Legacy conversation API | **Retained deliberate boundary** | Legacy `/conversations` accepts Janus and its compatibility adapter preserves that protocol. | Keep isolated; Project Mission is the candidate generic route. | Do not broaden this legacy wire. |
| R46.1-02 | CLI and Runner | **Retained / positive evidence** | Both parse, resolve, and execute declarations through `PipelineRunner` without Janus/Naive branches. | Preserve as the generic reference path. | Use its seams when designing F46.1-01/02. |
| Q46.1-01 | Durable Project admission | **Unresolved design question — Type 1** | Shared `ProjectMissionNames` two-name allow-list is enforced by Application, Host, and Worker. | One owner must authorize immutable packaged mission identity before Worker execution. | Decide trust proof, identity, zero-capability policy, and migration/rejection behavior. Blocks F46.1-02/03. |

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
| Runner.Contracts | Run/progress/artifact/usage wire. | Runner, API, ForgeUI. | F46.1-04 consumer contract |
| Runner | Stateless load/execute/artifact/cache host. | Core/ChatClients/Serve; API/ForgeUI callers. | F46.1-04, R46.1-02 |
| Application | Project, submission, session, conversation, history owners. | Host, Client Runtime, Conversation adapter. | Q46.1-01, F46.1-05 |
| Application.Transport | Typed action/event/JSON/channel vocabulary. | Presentation and Application Host. | None |
| Application.Host | Loopback HTTP/SSE composition. | Desktop starts it; Presentation calls it. | None |
| ClientRuntime | Scoped local capability policy/execution. | Application only. | None; Worker has no local-capability path. |
| Presentation | Rendering/navigation/view state. | Application Transport. | F46.1-03 consumer |
| Desktop | User-launched process supervision. | Starts Application Host/native Host. | None |
| Desktop.Contracts | Native-host and Supervisor pipe contract. | Desktop Host/Photino/Supervisor. | None |
| Desktop.Host | Disposable native-window process. | Desktop Contracts/Photino. | None |
| Desktop.Photino | Photino native-host implementation. | Desktop.Host. | None |
| Orchestration | Runtime endpoint/readiness/owned adapters. | Desktop/Application Host startup. | None; Vanilla is deployment default, not Core selection. |
| Conversations.Contracts | Versioned durable messages/projections. | Application Transport, Host, Worker, Presentation. | F46.1-03, Q46.1-01 |
| ConversationHost | Sole durable command/event/store owner. | Application adapter and Worker queue boundary. | R46.1-01, Q46.1-01 |
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
| 1 | Specify generic nested mission tool-pause/continuation semantics (F46.1-01). | Lock result propagation, continuation scope, trace ordering, and failure proof. |
| 2 | Resolve durable packaged-mission admission/trust and generic progress contract (Q46.1-01/F46.1-03). | Type-1 identity, authorization, zero-capability, wire/storage, rejection/recovery decision. |
| 3 | Replace Worker name resolver/concrete executors (F46.1-02). | Depends on 1–2; preserve Worker outbox and Host store ownership. |
| 4 | Remove Runner `Answerer` heuristic (F46.1-04). | Verify API/ForgeUI visible result compatibility. |
| 5 | Decide inactive Project mission origins (F46.1-05). | Depends on 2 and Phase 45 authoring design. |

### Step 1 done when

The inventory **disproves** the premise for the durable Worker: `Janus`/`Naive` select concrete
executors, and Janus manually splits its declared pipeline because nested tool calls do not
propagate through Core. It confirms the premise for CLI and hosted Runner execution. A separate
supervising Codex agent must adversarially review these proposed dispositions before any work is
approved.

The first smallest Phase 46.2 candidate is **F46.1-01 only**: a plan for generic nested
tool-pause/continuation propagation in Core. It is not approved and must not absorb Worker catalog
or durable-wire redesign.
