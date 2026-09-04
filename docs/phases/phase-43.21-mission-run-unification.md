# Phase 43.21 — Mission-run-first Project invocation

> **Status: design ready; implementation pending (2026-09-04).** This replaces the user-facing execution model of 43.20 Tasks 2 and 4. It does not invalidate the durable Conversation substrate or Project Explorer work.

## Outcome

A Project is durable local context. Every instruction a person submits invokes one selected, named **Mission Run** beneath that Project.

```text
Project → selected mission → Mission Run → experts → provider(s) → durable trace/outcome
```

The first product catalog is closed and contains exactly two built-in missions:

- `Janus` — the default; its multi-expert mission asset owns its composition.
- `Naive` — a one-expert mission asset; it deliberately performs direct, single-expert reasoning.

There is no `Default` choice, no model picker, no compact-picker description, and no exposed `MissionControl` mission. `Naive` is explicit, never an unnamed fallback. Repository demos and test fixtures are not product choices.

## Locked product contract

### One invocation shape

Selecting a mission is Project-scoped and persistent. Submitting the composer starts one child Mission Run of that selected mission. Presentation never branches execution:

| Selection | Child run | Meaning |
|---|---|---|
| `Janus` | `Janus` mission run | Execute Janus’s Proposer → Approver → Implementer workflow. |
| `Naive` | `Naive` mission run | Execute one zero-tool Controller expert, with no mission-level review path. |

This is a semantic child process, not a requirement to spawn an OS process per invocation. The durable Host/Worker queue remains the execution topology. Both choices must produce the same durable run identity and event stream.

The Project may retain a ConversationHost container only to order and replay its runs. That container executes no provider request and is never shown or named as a mission. A run is the sole user-visible unit that invokes reasoning.

### Contract and ownership

Presentation owns selection rendering, text entry, busy/error display, and navigation. It talks only to Client Runtime.

| Action | Surface-neutral Client Runtime contract | Owner and rule |
|---|---|---|
| Select | `SelectProjectMissionRequest { sessionId, mission }` → `{ selectedMission }` | Client Runtime allow-lists only `Janus` and `Naive`, atomically persists `ProjectManifest.SelectedMission`, and returns the canonical value. Presentation never supplies a path, digest, provider, or expert. |
| Invoke | `StartProjectMissionRunRequest { sessionId, commandId, input }` → `{ runId, acceptedSequence, status }` | Client Runtime derives the persisted mission, Project goal, allowed context/capabilities, and parent durable container. `commandId` is generated once at press and reused only for retry. |
| Execute | Host-internal `StartProjectMissionRun { containerId, commandId, mission, projectGoal, input, capabilities }` | ConversationHost creates/idempotently returns the child run and orders events. Worker resolves the named mission and executes it. Neither accepts an arbitrary provider/model. |

`input` is required bounded user text, distinct from the Project goal. Equal retries return the original run; changed mission or input under the same `commandId` is `Conflict`. Unknown/replaced sessions, unknown mission, blank/oversized input, terminal parent, and undeclared capability/context return typed errors and create no run.

The Worker owns the built-in catalog and resolves only `Janus` and `Naive`. `Naive` is the renamed checked-in zero-tool asset, `mission Naive(projectGoal, task) = { Controller }`; its executor rejects every tool request. Janus remains its existing checked-in mission and provider routing. No UI, Client Runtime, or Host component chooses an expert or provider.

### Migration and removal

`MissionControl` / `ProjectControl` is a legacy compatibility route, not a third mission. Neither new Project nor Presentation surface may use it.

1. Manifest v2 replaces `missionControlConversationId` with `projectMissionContainerId`; `selectedMission` remains and defaults to built-in `Janus`.
2. Reading v1 preserves its old control-conversation ID only as legacy durable history. Client Runtime creates one new Project Mission container deterministically for later runs; it neither replays old control messages as a current mission nor converts them into runs.
3. After Desktop and TUI use the new contracts, remove the ProjectControl endpoints, Client Runtime session, fixed `MissionControl` Worker resolver/executor, participant label, and user-facing strings in one deletion task. No dual user-facing path or compatibility picker is permitted.

## UI contract

Retain the Task 3 rail but rename `Mission Control` to **Missions**. Its entries are **Project Explorer**, **Missions**, and **Settings**.

The Missions page has one compact picker associated with the mission-input composer:

```text
Mission: Janus ▼
  Janus
  Naive
```

Janus is visibly selected on a new Project. The popup contains only those two names: no descriptions, model names, expert names, or “Default” row. The accessible label is `Mission`. The primary action is **Run**; it reports `Starting Janus…` or `Starting Naive…`, then renders only that run’s durable activity. It never calls a generic chat/control endpoint or labels a response “Forge”.

Required states: first open with Janus selected; picker open; Naive selected; invalid input; accepted/busy; Janus participant activity; Naive output; typed start failure; selection persistence after reopen; and v1 migration notice/history link when history exists. Add responsive SVG references before implementation. The Task 3 Explorer/Settings references remain binding for their owned slices. Use Workbench tokens and repeat four-corner, continuous-resize, long-text, 125/150/200% zoom, both-mode, and packaged parity checks.

## Architecture and engineering gates

| Gate | Result |
|---|---|
| Product behaviour | PASS — selecting and invoking a named mission creates one observable child run. |
| Ownership | PASS — Presentation renders; Client Runtime persists selection/derives local facts; Host owns run/events; Worker executes; mission assets own expert/provider composition. |
| Replacement boundary | PASS — no Desktop Host ownership, provider credential, or direct datastore access changes. TUI uses the same contracts. |
| Security architecture | PASS — no new public ingress, identity, datastore ownership, or provider-secret route. Selection is allow-listed below Presentation; paths stay below Client Runtime. |
| Engineering philosophy | PASS — one invocation contract replaces adjacent user paths; selection has one persistent owner; legacy compatibility has one removal task. |
| Default path | Applies — zero-argument published Desktop, normal local ConversationHost/Worker, and a disposable new Project visibly start default Janus and produce durable activity. |

## Tasks

### Task 1 — Universal durable Project Mission Run

Add manifest v2 migration, Project Mission-container creation, selection persistence, and the two contracts above. Generalize Host/Worker dispatch so Janus and Naive both create ordinary runs with durable `run_id` events. Retain the legacy route only as unreachable compatibility until Task 3.

**Done when:** contract/migration/idempotency tests prove either allow-listed selection creates exactly one identically-shaped child run; invalid selection/input and changed retries produce typed failures with no run; Worker rejects every mission outside the closed catalog; and the full suite passes.

### Task 2 — Mission-first Desktop and TUI surface

Replace Project Control with Missions and its exact two-name picker. Use only Task 1’s Client Runtime contracts. Rename the rail entry, remove `MissionControl` wording, and show live durable activity for the selected run.

**Done when:** browser and packaged Desktop show Janus visibly preselected, expose only Janus and Naive, persist deliberate selection, and submit both through the same named run action; TUI-equivalent contract tests pass and no Presentation code references provider, model, expert, or ProjectControl endpoint.

### Task 3 — Legacy route removal

Remove the obsolete ProjectControl public endpoints, Client Runtime session, fixed `MissionControl` resolver/executor, participant type, and user-facing strings. Retain only Task 1’s read-only legacy-history migration path.

**Done when:** source and contract tests prove no user-invocable `MissionControl` or ProjectControl execution path remains; a migrated Project starts Janus/Naive runs and retains prior history without treating it as a current mission; full suite and default-path packaged run pass.

## Completion condition

The correction is complete when a new or migrated Project makes the selected mission obvious, starts a durable child Mission Run for every submitted instruction, and shows Janus’s multi-expert exchange or Naive’s one-expert result without a direct-model or hidden-control path.
