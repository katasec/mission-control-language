# Phase 46.1 — repository-wide ownership inventory and end state

> **Status:** active design/investigation. Parent: [Phase 46 — domain-ownership remediation](phase-46-domain-ownership-remediation.md).
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
| — | — | No inventory evidence recorded yet. | — | — | Begin at the component/dependency graph, then trace generic execution before judging Janus or Naive. |

### Component ownership map

| Component | Why it exists / owner | Consumers and dependencies | Findings |
|---|---|---|---|
| — | To be established from the Source Component Atlas and nearest README during Slice 1. | To be established from the solution/dependency and call-path trace. | No inventory evidence recorded yet. |

### Step 1 done when

The ledger covers every required slice; each confirmed finding has the complete required record;
and a Codex supervisor has adversarially reviewed the proposed owners, deletions, compatibility,
failure boundaries, and remediation order. The result explicitly proves or disproves the premise
that Janus/Naive select mission-specific core execution paths. It then names the first smallest
remediation task for Phase 46.2, if any.
