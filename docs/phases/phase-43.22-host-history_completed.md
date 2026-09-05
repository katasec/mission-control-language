# Phase 43.22 task 2 — Host history completion record

Verified 2026-09-05. The active contracts and design remain in
[Host outcomes and bounded history](phase-43.22-host-history.md).

## Delivered boundary

The existing Conversation Host owns Project Mission admission and reads. It now derives a single
container identity from the Project ID, accepts only Janus or Naive with zero capabilities, and
returns typed Project API errors. The existing grain, event log, pending-transition repair,
outbox and Worker dispatch remain canonical.

`ProjectRunIndex` is a Host-owned, rebuildable Azure Table projection in the existing event-table
partition. It advances one contiguous 25-event batch per request and produces bounded pages and
exact 200-event trace scans. It stores no command queue, execution state or second authoritative
run model. `ConversationHostClient` is the sole Client Runtime decoder for typed Project outcomes.

## Failure and complexity review

| Failure | Owner and observed result | Recovery / negative evidence |
|---|---|---|
| Invalid identity, cursor, range or input | API/grain return `invalidRequest`; an unknown mission returns `unknownMission`. | No event append occurs. |
| Wrong or absent container | API returns `wrongPurpose` or `notFound`. | A foreign conversation is never repurposed. |
| Duplicate or competing command | Grain returns `commandConflict` or `runAlreadyActive`. | Deterministic Project-derived create identity prevents a second container. |
| Index cannot form a valid sequence | `ProjectRunIndex` returns `historyInvalid`; an incomplete index returns `historySynchronizing`. | It preserves canonical events and never invents summary data. |
| Storage/transport failure | Project route adapter returns typed `serviceUnavailable`. | Injected Azure storage failure has a 503 response with the stable code. |

The parent review rejected the first version because create identity still trusted a caller-supplied
command and the grain did not independently validate the mission. The accepted revision enforces
both boundaries. The new index, persistence adapter, grain reads and API adapters were reviewed
against the complexity gate; bounded fold, persistence and transport mapping remain separate
cohesive functions. No exception was recorded.

## Verification

| Check | Observation |
|---|---|
| Host Azurite suite | `dotnet test src/ForgeMission.ConversationHost.Tests/ForgeMission.ConversationHost.Tests.csproj --no-restore` — **178 passed, 0 failed**. Includes Project create/start, exact events and receipt, 33-event bounded resumption, deterministic create, direct grain validation and typed storage failure. |
| Client decoder and state tests | `dotnet test src/ForgeMission.Tests/ForgeMission.Tests.csproj --no-restore --filter "FullyQualifiedName~ConversationHostClientProjectTests|FullyQualifiedName~ProjectStoreTests"` — **62 passed, 0 failed**. |
| Diff hygiene | `git diff --check` passed. |

Default-path Desktop acceptance remains aggregated in task 5 because this task has no user-facing
Desktop route by itself.
