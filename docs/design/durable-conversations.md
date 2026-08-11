# Durable conversations — Orleans, Azure Table, Blob, and Service Bus

> **Status: approved architectural direction, 2026-08-12.** The first implementation is
> [Phase 43.16 — Durable Janus conversation proof](../phases/phase-43.16-janus-desktop-local-poc.md).
> Forge Desktop and Forge Rooms are different projections of one durable conversation, not separate
> stores or runtimes.

## Decision

A Forge conversation is a durable, ordered record of people, mission participants, lifecycle
events, and local-capability hand-offs. It has one canonical event sequence:

    Forge Desktop / Forge Rooms
                |
         Conversation API
                |
        ConversationGrain
          /             \
 Azure Table events   MissionRunGrain
       + Blob                |
                    Service Bus command queue
                            |
                      mission worker

Desktop remains the holder of local authority. Only Client Runtime can authorize and execute file,
terminal, browser, Git, or Docker capabilities. A mission requests a capability; it never gets
desktop access. Rooms renders the same durable events but does not gain those capabilities.

## Boundary with existing decisions

This introduces Orleans for the managed/durable conversation tier. It does not alter
[Phase 31](../phases/phase-31-forge-runtime-platform.md)'s decision against Orleans for generic
self-hosted batch and scheduled pipelines. Kubernetes remains their substrate; Orleans is the
stateful managed-conversation backend Phase 31 left open.

It also does not migrate the existing Rooms relational model or billing ledger:

- Rooms identity, membership, invitations, and the ledger remain Postgres-backed: they need
  relational constraints, access checks, and aggregation.
- 'platform_keys' remains the first existing store eligible for the planned Table migration.
- New durable mission conversations are a new bounded context and start Table-native. They have no
  prior Postgres schema to migrate.

## Durable model

| Grain | Key | Durable responsibility | Not responsible for |
|---|---|---|---|
| ConversationGrain | tenant + conversation ID | Sequence allocation; event repair; accepted command IDs; active tool wait; conversation status. | Running provider calls, local tools, membership. |
| MissionRunGrain | tenant + run ID | Run state machine and terminal state. | A grain per Proposer, Approver, or Implementer. |
| UserMemoryGrain (later) | tenant + user ID | Explicit curated preferences/memory. | The full transcript. |

One conversation owns mutation order. Janus's Proposer, Approver, and Implementer are event
participants in one run, not actor identities; per-expert grains would create chatty distributed
calls without a useful ownership boundary.

### Event contract

The additive, versioned Forge contract is shared by Conversation API, Client Runtime, Desktop, and
future Rooms. It is distinct from the compatibility-bound '/v1/*' endpoints.

    ConversationEvent v1
      conversation_id, run_id?, sequence, event_id, version
      kind: user_message | participant_started | participant_message | approval |
            tool_requested | tool_result | run_status | artifact | error
      participant: User | Proposer | Approver | Implementer | Forge
      attempt?, text?, reason?, tool?, artifact_ref?, occurred_at_utc

The log contains semantic facts and completed participant messages. Text-token deltas are
best-effort live updates only, coalesced for the active participant message. Completion writes one
durable 'participant_message' event, so a restart can lose only an unfinished visual draft, never
a completed expert response or approval decision.

### Storage and compaction

Named Azure Table grain state stores compact checkpoints only: IDs, last sequence, accepted command
IDs, active run/step, expected tool request IDs, and terminal status. It is not a growing chat blob:
the Orleans Azure Table provider has a 1 MB row limit.

| Store | Partition key | Row key | Purpose |
|---|---|---|---|
| forgeconversationevents | tenant + conversation ID | zero-padded sequence | Ordered event replay. |
| forgeconversationindex | tenant + user ID | reverse-time + conversation ID | Conversation list projection. |
| forgeconversationartifacts Blob container | tenant/conversation/run path | artifact ID | Large raw output/tool output/files. |

The event log is canonical; the index can be rebuilt. The entity holds bounded render/filter fields
and a payload/reference, never credentials or raw binary. Large content goes to Blob before its
referencing event is appended.

Azure Table transactions span one partition only, so this design makes no false atomic
Table-plus-Service-Bus promise. Recovery is idempotent: checkpointed command and event IDs are
stable, activation repairs a missing deterministic event before retry, and the worker rejects an
already-applied command ID.

Retention/compaction is deferred. A later compactor must write a verified summary/artifact event
before deletion; no initial implementation deletes transcript data.

## Reliable command delivery

Service Bus carries work commands, not the transcript or UI stream:

    HTTP submit/tool result -> ConversationGrain checkpoint -> mission-command queue -> worker

Each command has a client-generated 'command_id'. It becomes Service Bus 'MessageId', while
'SessionId' is the conversation ID for per-conversation order. Queue duplicate detection is enabled
where the namespace tier supports it; the worker remains idempotent because peek-lock delivery is
at-least-once.

If a process dies after a send but before completion, recovery resends the same command ID. The
broker can deduplicate it and the worker ignores an already-recorded transition. If it dies before
send, the durable pending command remains and a reminder/reconciler retries it. Service Bus is not
inserted between normal Orleans grain calls and is not the browser/Desktop event source: clients
reconnect from the durable sequence.

## Mission execution and failure semantics

Pipeline execution exposes awaited, nested-mission-safe trace callbacks: mission path, participant,
attempt, lifecycle, completed StepEnvelope, and tool-request identity. Callback completion is
awaited before advancing so a completed Janus step is durable before its successor starts.

A tool hand-off works as follows:

1. MissionRunGrain appends 'tool_requested' and enters 'waiting_for_tool'.
2. Client Runtime receives it, authorizes/executes via its existing dispatcher.
3. Client Runtime posts the stable request ID and result.
4. The grain validates it, appends 'tool_result' once, and queues the safe continuation.

An opaque provider request cannot be made exactly-once across a Silo crash. The proof is explicit:
a tool wait is resumable; a completed step is never silently rerun; a run found executing without a
completed safe boundary becomes visible 'interrupted'. General checkpoint/resume of an uncertain
provider call is deferred rather than pretending duplicate calls cannot happen.

## Reconnect and projections

The Conversation API provides:

    POST /conversations
    POST /conversations/{conversationId}/commands
    POST /conversations/{conversationId}/tool-results
    GET  /conversations/{conversationId}/events?after={sequence}
    GET  /conversations/{conversationId}

The SSE route first replays Azure Table events after the requested sequence and then emits live
updates. Clients retain their last rendered sequence and deduplicate by event ID. An SSE drop is
normal, not data loss. Desktop renders group chat; Rooms and the later 43.4 workbench are further
projections of the same events, not new trace databases.

## Local Kind topology

    Desktop / Client Runtime (host machine)
                  |
              port-forward
                  |
    kind namespace forge-durable
      - conversation-host: one API + Orleans Silo/gateway replica
      - mission-worker: one replica
      - Azurite: Table + Blob, persistent local volume
      - Service Bus emulator + its SQL Server dependency

One Silo is intentional: prove durable recovery before multi-silo placement, load shedding, or
scale-out. The next environment gate is two Silos only after restart/reconnect is green.

On this Apple-silicon machine the Service Bus emulator image is arm64, but its documented SQL
Server dependency is amd64-only. Local scripts must preflight Docker emulation and report a clear
remediation before applying manifests. Azurite Table support is developer-emulator evidence only;
the final storage-provider acceptance run uses a real non-production Azure Storage account.

## Deferred

- Human intervention/suspend/resume (43.5).
- General MCL checkpoint/resume for uncertain provider calls.
- Cloud catalog/OCI, multi-silo HA, Azure SignalR backplane, regional recovery/load shedding.
- Orleans durable-collection APIs until their exact supported public surface is selected.
- Migration of existing Rooms, membership, ledger, or other Postgres stores.
