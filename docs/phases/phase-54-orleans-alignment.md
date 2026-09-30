# Phase 54 — Orleans alignment (forge-conversations Host)

> **Status: design (2026-09-30). Not build-ready** — tasks need their own design before handoff.
> Origin: the [53.8 incident](phase-53.8-token-streaming.md#incident-2026-09-30--host-deadlock-after-a-mid-turn-restart-blocks-task-8)
> and a review of the Host against Orleans guidance, cross-checked online the same day.

**Goal:** make the conversation Host use Orleans the way Orleans is designed to be used, so that
future work layers on its capabilities instead of working around them. Principles 1, 2 and 7
(one owner, no duplicate paths, least code) apply directly.

## Review against Orleans guidance (2026-09-30)

Each claim below is cited from the official docs or the dotnet/orleans repo.

| # | Practice | Host today | Verdict | Source |
|---|---|---|---|---|
| 1 | Grain calls never form a cycle (non-reentrant grains deadlock) | Fixed in forge-conversations#12: `MissionRunGrain` no longer calls back | Confirmed; fix is canonical, no reentrancy needed | [request scheduling](https://learn.microsoft.com/en-us/dotnet/orleans/grains/request-scheduling), [best practices](https://learn.microsoft.com/en-us/dotnet/orleans/resources/best-practices) |
| 2 | Activation: an exception in `OnActivateAsync` fails activation | `ConversationGrain.OnActivateAsync` replays the pending transition, reads the Table tail, calls the run grain, and throws on log corruption | Corrected: docs don't forbid calls there, but failures make the grain unusable. Keep activation to local repair; the corruption throw (fail-closed) must be an explicit decision | [grains](https://learn.microsoft.com/en-us/dotnet/orleans/grains/), [persistence](https://learn.microsoft.com/en-us/dotnet/orleans/grains/grain-persistence/) |
| 3 | Don't split state that changes together ("chatty grains may be better combined") | `MissionRunGrain` only mirrors facts `ConversationGrain` recorded; nothing in production reads it | Confirmed: two owners for one fact | [best practices](https://learn.microsoft.com/en-us/dotnet/orleans/resources/best-practices) |
| 4 | Atomic state change | Events in our Table log, checkpoint in Orleans grain storage, reconciled by a hand-rolled `PendingTransition` | Corrected: a custom event log is fine; the defect is two stores with no shared commit. Table entity-group transactions are atomic within one partition (≤100 ops, 4 MiB). `JournaledGrain` (CustomStorage) is optional; `Microsoft.Orleans.Journaling` is alpha | [log-consistency providers](https://learn.microsoft.com/en-us/dotnet/orleans/grains/event-sourcing/log-consistency-providers), [entity group transactions](https://learn.microsoft.com/en-us/rest/api/storageservices/performing-entity-group-transactions) |
| 5 | Push to clients: per-host observers with resubscribe; broadcast channels fan out to grains, not clients | In-process `ConversationEventHub` feeds SSE; correct only with one silo | Confirmed: works now; silently drops events with 2+ silos (inference, untested) | [observers](https://learn.microsoft.com/en-us/dotnet/orleans/grains/observers), [GPS/SignalR sample](https://learn.microsoft.com/en-us/samples/dotnet/samples/orleans-gps-device-tracker-sample/), [broadcast channel](https://learn.microsoft.com/en-us/dotnet/orleans/streaming/broadcast-channel) |
| 6 | `[ReadOnly]` for reads; keep frequent ephemeral calls off the non-reentrant queue | No `[ReadOnly]`; each 53.8 delta is a full grain turn | Confirmed | [request scheduling](https://learn.microsoft.com/en-us/dotnet/orleans/grains/request-scheduling), [one-way](https://learn.microsoft.com/en-us/dotnet/orleans/grains/oneway) |
| 7 | Reminders for durable retry; at-most-once messaging → app dedupe | Outbox reminder (30 s due, 1 min period); `command_id` dedupe; Service Bus consumers | Confirmed. Keep Service Bus (no Orleans provider for it) | [timers and reminders](https://learn.microsoft.com/en-us/dotnet/orleans/grains/timers-and-reminders), [streaming](https://learn.microsoft.com/en-us/dotnet/orleans/streaming/) |
| 8 | Call timeouts, deactivation, rollover | 30 s default timeout broke the deadlock; revision rollover briefly runs two silos (~30 s observed) | Set `[ResponseTimeout]` on long methods; never persist in `OnDeactivateAsync`; the rollover window can drop live events via the in-process hub | [grains](https://learn.microsoft.com/en-us/dotnet/orleans/grains/), [ACA deployment](https://learn.microsoft.com/en-us/dotnet/orleans/deployment/deploy-to-azure-container-apps) |

## Candidate tasks (priority order; each needs its own design)

| # | Task | Effect |
|---|---|---|
| 1 | Fold or delete `MissionRunGrain`; run state lives in `ConversationGrain` | Removes the only cross-grain edge and the run-grain call in activation |
| 2 | Checkpoint row in the event partition, committed with the events in one entity-group transaction; remove `PendingTransition` | Removes the most complex code in the Host |
| 3 | Deltas off the non-reentrant queue (publish without a grain turn, or `[AlwaysInterleave]` with no state change) plus delta sequence dedupe | Streaming no longer queues behind commands |
| 4 | `[ReadOnly]` on read methods (after confirming they don't mutate) | Reads stop waiting on each other |
| 5 | Activation limited to local repair; explicit decision on the fail-closed corruption check | Predictable activation |
| 6 | Before any second silo: SSE hosts subscribe as grain observers with resubscribe and log catch-up | Correct fan-out on multiple silos and during rollover |
| 7 | `[ResponseTimeout]` on long methods | Explicit timeouts |

Not changing: the Table event log itself, Service Bus ingress/progress, reminders, `command_id` dedupe.
