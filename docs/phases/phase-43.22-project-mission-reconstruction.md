# Phase 43.22 — Project Mission reconstruction

> **Codex-only reconstruction in progress, 2026-09-05.** State, Host history and runtime application are verified; workbench presentation is next.
> This phase uses the user-directed execution protocol below; it does not use a Claude relay or
> copy/paste handoff.

## Outcome and scope

Opening a Project shows Missions. A person selects Janus or Naive, submits an instruction,
sees one durable run, and opens its exact expert trace. Closing and reopening the Project
restores the same runs. Concurrent windows cannot corrupt the manifest or change the mission
of a request being retried. Project Explorer and Missions show the same run facts. Completion
in any view cannot focus an absent composer. The final product has one writable Project
Mission route and no Project Control writer.

This is a fresh implementation of feature coordination, selectively borrowing reviewed parts
of `4dbd8be9683fa01571f5402d8f3c2c31c3e60538`. It is **not** an instruction to merge that candidate,
preserve its coordinator, or replace the existing durable engine. The source comparison used
`50930df92ce3dc5df1be1f82286899aa673e846a`; the implementation branch starts from clean current
`main` containing this plan. Preserve intervening main fixes. Do not cherry-pick either entire
candidate increment. Source disposition is in the [audit record](phase-43.22-audit_completed.md)
and [file boundary](phase-43.22-file-boundary.md).

| Included | Explicitly outside this implementation |
|---|---|
| Janus/Naive selection; immutable submission; one active run per Project; reopenable paged run history and exact trace; local asset/context browser; Settings; legacy writer retirement | Local tool grants, autonomous filesystem implementation, stop/guidance, docking, registry browser/install, OCI lock migration, new provider/model chooser, hosted deployment, TUI renderer |
| Existing Host acceptance, event log, outbox, Worker execution and compatibility APIs | A new queue, run engine, datastore, process, generic workflow framework, or rewrite of Core/CLI |

**Product honesty:** these missions currently have zero local capabilities. They can produce
plans/code as text; they cannot claim to have created files or run tests on the user's machine.
Naive must answer the actual instruction, not inherit Mission Control's goal-refinement policy.

## Dependency-ordered implementation

Each row is one Codex-owned implementation task. Before a task begins, Codex rechecks its locked
contracts, file boundary, failure-boundary record, complexity risks and test matrix against current
main. After implementation, Codex reviews the resulting diff and evidence against the same `Done
when` conditions before starting the next task. All five tasks belong to one replacement feature
branch/PR; intermediate feature commits are not separately deployed. Task 5 also defines a narrowly
scoped prerequisite admission-fence PR for safe retirement. A task's component evidence is not
feature completion. No row is complete today.

| Order | Spoke | Deliverable | Status |
|---|---|---|---|
| 1 | [Project state](phase-43.22-project-state.md) | Serialized project transactions and bounded immutable submission journal | Verified — [evidence](phase-43.22-project-state_completed.md) |
| 2 | [Host history](phase-43.22-host-history.md) | Typed outcomes, bounded run index and exact event pages over existing data | Verified — [evidence](phase-43.22-host-history_completed.md) |
| 3 | [Runtime application](phase-43.22-runtime-application.md) | Surface-neutral submission/recovery/read actions; one subscription lifecycle | Verified — [evidence](phase-43.22-runtime-application_completed.md) |
| 4 | [Workbench presentation](phase-43.22-workbench-presentation.md) | Small composer and views; shared Runs list; reopenable trace; focused local Explorer | Depends on 1–3 |
| 5 | [Retirement and acceptance](phase-43.22-retirement-acceptance.md) | Remove legacy writers, correct Naive, verify whole product and publish evidence | Depends on 1–4 |

## Ownership and failure boundaries — locked

| Owner | Sole responsibility / seam | Must not own |
|---|---|---|
| `ProjectStore` + internal `ProjectManifestFile` | Validate Project data; perform one lease-protected manifest transaction | HTTP, run lifecycle, UI state |
| `ProjectMissionApplication` | Prepare/send/reconcile one immutable submission through `ConversationHostClient` | DOM, transcript rendering, Table access |
| `ProjectMissionReadSession` | Session-owned read/subscription lifetime, bounded read windows, invalidation | Submission identity generation, manifest mutation, local tool execution |
| `ProjectMissionToolRefusal` | Idempotently refuse unexpected tool requests | Capability registry/dispatcher or file execution |
| `ConversationGrain` | Existing durable acceptance and sequencing; serialize run-index reads | Presentation, local Project files |
| `ProjectRunIndex` + persistence adapter | Derive bounded queryable run summaries from canonical events/accepted commands | New authoritative run state or queue |
| `MissionComposer` | Draft text, keyboard interaction, focus while mounted | Durable retry state, mission execution |
| `Home` | Navigation and composition; forward UI intents through the channel | Buffering acceptance events, deriving statuses/counts, persistence/HTTP coordination |
| Existing transcript projection/view | Render the selected run's original expert evidence | Run submission or summary policy |

Use concrete internal classes except at existing transport/persistence boundaries. No interface
per helper. Functions disclose intent in their first 15–20 lines, normally stay within 20–40 lines,
and avoid nesting beyond two levels. Review any function with estimated cyclomatic complexity
above 10: split unrelated decisions, but do not disguise complexity by mechanically moving a
switch or inventing layers. A shallow protocol switch is not automatically a design defect.

## Architecture and engineering review

Read [Forge Architecture](../design/forge-architecture.md), [Security Architecture](../design/security-architecture.md),
[Engineering Philosophy](../design/engineering-philosophy.md), [Code Style](../design/code-style.md),
[Desktop Interaction Principles](../design/desktop-interaction-principles.md), and
[UI Design System](../design/ui-design-system.md) with the relevant spoke.

| Gate | Design result and implementation proof |
|---|---|
| Context/data ownership | PASS: Client Runtime owns local Project manifest; ConversationHost alone owns its existing conversation Table/Blob data. The run index is a rebuildable projection in that same table/context. Worker reports through the existing progress contract. |
| Public entry/auth | PASS: no new internet entry. Existing local Client Runtime session boundary and loopback Host route remain; caller supplies session ID, never arbitrary home/container/tenant. Host uses its existing development conversation address/identity policy. This is a local Desktop feature, not approval for public exposure without an authenticated tier-1 adapter. |
| Credentials/tiers | PASS: Presentation has no provider/storage credentials; Client Runtime receives no Host data-plane access; Host retains conversation storage/transport scope; Worker retains provider and existing transport scope. No RBAC, secret, ingress or cross-store change. |
| Consequential decisions | PASS: manifest version, journal transitions, immutable retries, history consistency, old-writer retirement and zero authority are defined in the spokes. These ownership/contracts are Type 1; no decision is delegated to implementation. |
| Structural containment | PASS: stable OS lease, atomic publication, Host deduplication, bounded journal, bounded history reads and no capability dependencies. Cross-process contention and injected failures prove these, not warnings. |
| Abstractions/knobs | PASS: new owners correspond to actual failure seams. Fixed limits are documented; no configurable mission engine or alternate backend. Derived indexing solves bounded historical queries without changing the append/outbox algorithm. |
| Main flow/cognitive load | PASS: transport delegates to named operations; pure projection rules are separate from I/O; component review rejects moved monoliths and unused abstractions. |
| Desktop behaviour/owner | PASS: restored runs and stable navigation belong to Client Runtime and Presentation respectively. No native callback, Supervisor, process or credential ownership changes. |
| Adapter observation | PASS for design: candidate completion scheduled `Home` focus while Trace lacked an input; the fix is mounted-composer ownership. No Photino workaround is proposed. Actual browser/native acceptance remains required. |
| Replacement boundary | PASS: Host remains replaceable; browser and native clients consume identical contracts. Existing Supervisor lifecycle tests remain required. |
| Surface parity | PASS: every action is a named Client Runtime request/response in task 3. Contract tests invoke it without Blazor/Desktop types; TUI rendering is outside scope. |
| Visual | Design bound by task 4 references/spec; implementation PASS requires browser evidence and packaged parity. No earlier candidate visual claim closes this task. |

## Delivery boundary

This documentation change has **Default-path acceptance: N/A — documentation/reference assets only**.
Feature implementation must pass [task 5's acceptance matrix](phase-43.22-retirement-acceptance.md).
The existing clean-main-only Kind provenance rule requires the narrowly scoped post-merge
default-path sequencing exception defined there. Merged is not release-ready or complete.

### Codex-only execution protocol

This is a user-directed exception limited to Phase 43.22. Codex performs the implementation and
the documented self-review directly in this task, with no Claude assignment, no relay prompt and
no separate copy/paste planning turn. The repository-wide Claude/Codex workflow remains unchanged
for other phases.

At each task boundary, Codex records the concrete files, changed contracts, relevant failure
boundaries, complexity review and verification results in the task's completion companion before
advancing. A discovered conflict with the locked design returns to this design for correction; it
is not permission to improvise. After each task, retain one status/evidence pointer here and move
verified narrative to a `_completed` companion; keep dependent contracts in the active spokes.

**Feature done when:** all five task conditions pass, obsolete writers are absent, full required
checks pass, the same release revision passes the normal packaged route, agent visual review passes,
and the operator independently accepts the visible result. Report actual deleted/replaced/added
production lines separately; there is no deletion quota and no claim that retaining 99% is success.

## Design review closure (2026-09-05)

| Pressure test | Locked resolution |
|---|---|
| Selection changes while response is lost | Disk journal keeps original selection/input; Retry supplies no mutable payload. |
| Two windows or late responses | Whole-file transaction plus previous-command comparison; terminal journal views cannot regress to Prepared. |
| Large history or reopening | Host-owned bounded derived index, fixed-size event ranges and Runtime page windows; no full-history UI buffers or manifest status writer. |
| Index behind requested run | Synchronizing is distinct from caught-up NotFound; partial load is never called empty history. |
| Existing code/contract accuracy | Preserved Project create route is singular `project-mission`; start response includes ContainerId. Existing channel stays generic SendAsync/Subscribe. |
| Core/CLI dependency expansion | Excluded OCI lock migration; Explorer reads local declared assets and raw lock text only. |
| Legacy commands in flight | Permanent admission-fence prerequisite before consumer removal; per-command durable output plus actual latest Worker session state, not an invented receipt ledger. |
| Focus blast radius | Only mounted composer may request focus; no terminal-event handler owns it. |

This is design review, not evidence that implementation passes. Codex records and reviews each
task's completion evidence directly under this phase's user-directed execution protocol.
