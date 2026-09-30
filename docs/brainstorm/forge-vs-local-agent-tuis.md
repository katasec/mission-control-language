# Brainstorm: what Orleans gives Forge that local agent TUIs would have to build

**Status: conceptual only, still being refined.** Written up from a design conversation on
2026-09-30. Don't build from this doc; promote chosen items into a phase first.

**The question.** Which user-facing advantages over local agent TUIs (Grok CLI, Codex CLI, Claude
Code) come *built in* with the Orleans stack Forge already uses
([durable-conversations.md](../design/durable-conversations.md)), and which would still have to be
built from scratch whatever stack we used? The first kind is Forge's real advantage: we get it
cheaply, and a competitor would need a distributed-systems project to match it.

## The core difference

Local agent TUIs run the agent loop inside the terminal process and save the session as a local
transcript, filed under the folder the tool was launched from. Forge runs the mission in a
`ConversationGrain` and a `MissionRunGrain` on the server, and the TUI is one client of that run.

## Leverage map: experiences to build on Orleans rather than from scratch

The working rule: before building a server-side feature, check whether one of these Orleans
features already does the hard part.

| Experience | Orleans feature to use | What we avoid building |
|---|---|---|
| "My recent sessions" across every project and machine (answers "which folder was I in?") | A **grain per user** (`UserSessionsGrain`, keyed by user) holding a summary of each session: title, project path, last update. Each `ConversationGrain` tells it when a session is created, renamed or updated. | A session catalog service and its query API. Simple title/summary matching over a few hundred entries can run inside the grain. Full-text search over content still needs a real index. |
| Detach and reattach to a live run | Virtual actors: the grain stays reachable by ID whether or not anyone is watching | A session registry and routing to whichever process owns the session |
| Live updates to every attached client | **Grain observers** (or Streams) for the live tail. Keep Table replay for catch-up. | Pub/sub fan-out and a connection registry |
| Who is attached or watching this run (presence) | In-memory state on the conversation grain | A presence service |
| Steer or cancel a running mission from any client | Grain method call. One call at a time per grain keeps the order safe. | Locks and cancellation plumbing across processes |
| Approvals and tool waits that last days, and scheduled or triggered missions | **Reminders** | A scheduler service and a durable job table |
| Live list of "what's running now across all my projects" | The user grain tracks the user's active runs | Polling or queries across the run store |
| A cap on concurrent runs or spend per user | A counter on the user grain, which handles one call at a time | A distributed rate limiter |
| Tool policy, audit and telemetry on every call | **Grain call filters** | Middleware copied into each service |
| Background work after a session goes idle (auto-title, summary, memory extraction) | Grain timers, or reminders if it must survive a restart | Background job infrastructure |

## 1. Built into Orleans

| Orleans primitive | Advantage for users | What you'd build without Orleans | Forge today |
|---|---|---|---|
| **Virtual actors**: a grain for each conversation or run is created when first called, found by its key, and unloaded when idle | Any client, on any machine, reaches the same live run by ID. There's no "which process owns this session?" | Session registry, routing to the owning process, sticky load balancing, and loading and evicting sessions in memory | Used: `ConversationGrain` and `MissionRunGrain` |
| **One call at a time per grain**: a grain handles one request, then the next | Several participants (Janus roles, teammates, several clients) act on one run without races | Distributed locks or a queue per session, plus careful locking code | Used: the grain is the only thing that assigns event sequence numbers |
| **Grain persistence** with ETag optimistic concurrency | A run's state machine survives a restart and is never overwritten by stale state | A state store, versioning, and conflict checks | Used: compact checkpoints in Azure Table |
| **Reminders**: durable timers that wake a grain even if it isn't loaded | Durable waits (approval, tool result, "retry at 3am") that last hours or days. Triggered and long-running missions need no open terminal. | A scheduler service, a persistent job table, and wake-up routing | Partly used: the reconciler retries pending commands. Scheduled missions aren't designed yet. |
| **Cluster membership and failover**: if a silo dies, its grains restart on another | A run keeps going after a server node dies | Health checks, leader election, and moving sessions to another node | Available, not realised: the first deployment has one silo, and multi-silo HA is deferred |
| **Grain observers and Orleans Streams**: push events to subscribers | Live updates to every attached client | A pub/sub layer and a connection registry | Not used: SSE replays from the Table event log instead. Streams are an option if we fan out to many clients. |
| **Grain call filters**: interceptors on every grain call | One place to enforce authorization, audit, and telemetry for every client | Middleware copied into each service | Not used yet: a candidate home for server-side tool policy |
| **JournaledGrain (event sourcing)** | Rebuild any run's state from its events | A hand-built event store and replay | Adopted by [Phase 54](../phases/phase-54-orleans-alignment.md) (2026-09-30): `JournaledGrain` + `CustomStorage` owns the single write; our Table adapter commits events and state in one entity-group transaction. |

## 2. Not Orleans: built by Forge whatever the stack

Being honest here keeps the pitch credible.

| Advantage | Why Orleans doesn't provide it |
|---|---|
| Full-text search across all projects | Orleans looks up a grain by key and has no query API or secondary indexes. A user grain covers "list and filter my sessions" (see the leverage map). Searching inside conversation content needs its own index. |
| History follows you across machines and clients | Comes from storing conversations on the server, keyed by user. Any server database would do this. Orleans only makes the *live* run reachable. |
| User-level memory (`UserMemoryGrain`, later) | The grain is a convenient owner, but the value is the curated memory, which is Forge's design. |
| Usage and cost for each user | Ledger and billing live in forge-platform, not in grains. |
| Commands applied once (`command_id`) | Our code plus Service Bus duplicate detection. Orleans calls deliver at most once and don't deduplicate. |
| Detach, reattach, catch up | Replay comes from our Table event log and SSE. Orleans keeps the run *alive* while nobody is watching; we built the catch-up. |
| Run in the cloud, tools on your own machines | Forge's client-runtime design. Orleans only holds the durable wait. |
| Exactly-once provider calls | Nobody provides this. If a silo crashes mid-call, that LLM call may run again. |
| Parallel experts | Deliberately *not* one grain per expert. Parallelism comes from the runner. |

## Takeaways

- **The advantage Orleans makes cheap:** a live, stateful run with one owner that lives
  outside any terminal. It's reachable from anywhere, can wait durably for days, and handles
  several participants one at a time. Local TUIs would need a session service, locks,
  a scheduler and failover to get there.
- **Easy to copy, not an Orleans advantage:** search and history across projects and
  machines. They're still worth building, because they fix a pain users have today (see below).
- **Available but not yet realised:** failover (one silo today), push through Streams or
  observers, and policy through call filters. Don't claim these until they're turned on.
- **First TUI demos that rely on Orleans:** (a) start a long mission, quit, reopen, and it streams
  from where it left off; (b) start in the TUI and approve from another client.

## Appendix: the search pain that started this

It's hard to find a past session when you can't remember which project folder you were in.
Checked on 2026-09-30:

| Tool | What was checked | Search across projects? |
|---|---|---|
| Grok CLI | Sessions are stored under `~/.grok/sessions/<encoded-cwd>/`. `grok sessions search "session"` found a match when run from the PowerShell repo and returned 0 when run from `/tmp`. | No for local search. It also adds "remote results", which weren't tested because the login had expired. |
| Claude Code | Sessions are stored under `~/.claude/projects/<encoded-cwd>/`. The binary contains a "show all projects" string, probably a toggle in the `/resume` picker. | Probably yes, from inside `/resume`. The toggle wasn't clicked. |
| Codex CLI | Not installed on this machine. `codex resume --all` was recalled from memory, not checked here. | Probably yes, via a flag. |

Workaround today: `grep -ril "<term>" ~/.grok/sessions ~/.claude/projects`
