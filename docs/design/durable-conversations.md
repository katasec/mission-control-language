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

## North-star tiering gate

The durable-conversation design is governed by [Phase 42's north-star topology](../phases/phase-42-forge-cloud.md#3a-deployment-topology--the-north-star-locked-2026-07-18):
**CDN → tier 1 presentation → tier 2 application → tier 3 data, adjacent-only.** This is a
Type-1 decision. A temporary local proof may be simpler, but no cloud design is accepted if it
makes that separation materially harder to retrofit.

| Tier | Conversation responsibility | Must not hold |
|---|---|---|
| 1 — internet-facing | Authenticate/authorize and route browser, Desktop, and API requests to the conversation application service. | Azure Table/Blob credentials or data-plane RBAC. |
| 2 — internal applications | Conversation/Orleans service owns conversation state; mission worker executes commands and reports through an explicit internal service or durable-message contract. | Another bounded context's datastore credentials. |
| 3 — data | Conversation Azure Table/Blob state; the Service Bus command/progress transport used by tier-2 services. | Public ingress. |

**One datastore per bounded context is also Type-1.** The conversation store is owned by the
conversation application service. No other service queries or mutates it directly; data moves
between bounded contexts through a service contract or a durable message, never a cross-store query
or foreign key.

This makes the following current-infrastructure choices Type-2 and deliberately reversible before
cloud deployment: direct Worker Table/Blob grants, external ingress on a state-owning Conversation
Host, and its Worker-to-grain communication mechanism. The target cloud design must instead define
an edge-to-conversation-service route, keep the state-owning service internal, and remove the
Worker's direct conversation-store access unless it is itself part of that owning service.

**Cloud Worker-to-grain decision (2026-08-13).** A standalone Worker does not use an Orleans client
gateway discovered through Azure Table. That would grant it access to the Conversation service's
storage-clustering state and depend on a direct Container Apps workload path that is not the
documented service-to-service model (see [Container Apps inter-app
communication](https://learn.microsoft.com/azure/container-apps/connect-apps)). Instead it publishes
durable, idempotent trace/progress facts to a dedicated `conversation-progress` queue. The
state-owning Conversation service consumes that queue and is the only process that invokes
`ConversationGrain` and writes conversation Table/Blob state. The queues carry stable
event/command IDs and the conversation session ID; the service's grain remains the sole sequence
allocator.

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

## Reliable command and progress delivery

Service Bus carries work commands and completed trace/progress facts, never the transcript or UI
stream:

    Tier-1 edge -> Conversation service -> ConversationGrain checkpoint
        -> mission-command queue -> Worker
    Worker -> conversation-progress queue -> Conversation service -> ConversationGrain

Each command has a client-generated `command_id`. It becomes Service Bus `MessageId`, while
`SessionId` is the conversation ID for per-conversation order. Each Worker trace/progress fact has
a stable `event_id` as `MessageId` and the same conversation `SessionId`. Both queues use sessions
and duplicate detection where the namespace tier supports it; every consumer remains idempotent
because peek-lock delivery is at-least-once.

If a process dies after a send but before completion, recovery resends the same ID. The broker can
deduplicate it and the consuming service ignores an already-recorded transition. If it dies before
send, the durable pending command remains and a reminder/reconciler retries it. The Worker completes
its command only after the corresponding progress fact is broker-accepted; the Conversation service
completes progress only after its grain has durably accepted the idempotent event. Service Bus is
not the browser/Desktop event source: clients reconnect from the durable sequence.

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

### External collaboration projections

Forge may later project its durable conversation into an external collaboration system (for example,
a [Buzz](https://github.com/block/buzz) room) when that product integration has a concrete user
need. This is deliberately not an integration commitment or a Phase 43.16 deliverable.

The adapter is a distinct Tier-2 projection consumer: it reads the Conversation service through a
named internal contract, remembers its own delivery cursor, and uses Forge `EventId` for
idempotency while retaining Forge `Sequence` for source ordering. It owns any external credentials
and external projection state. It never reads conversation Table/Blob directly, never becomes a
second sequence allocator, and never turns transient token deltas/typing into durable transcript
facts. Start with one Forge identity and carry `Participant`, `RunId`, `Sequence`, and `EventId` as
metadata; mapping individual Janus participants to external identities is a separate product and
identity decision. Projection UIs may summarize routine successful tool bursts, but approval,
rejection, tool hand-off/result, error, and interrupted state remain individually visible.

## Local Kind topology

    Desktop / Client Runtime (host machine)
                  |
              port-forward
                  |
    kind namespace forge-durable
      - conversation-host: one API + Orleans Silo/gateway replica
      - mission-worker: one replica
      - forge-conversation-cloud: transient Kubernetes Secret
                  |
                  v
    Azure dev resource group
      - Azure Table Storage + Blob artifacts
      - Azure Service Bus mission-command and conversation-progress queues

One Silo is intentional: prove durable recovery before multi-silo placement, load shedding, or
scale-out. The next environment gate is two Silos only after restart/reconnect is green.

Kind runs the compute locally. Azure Table/Blob and Azure Service Bus are the real cloud services
from the first product proof; Azurite and the Service Bus emulator are not part of the Kind
topology. Automated tests may use emulators where appropriate, but a successful emulator run is
never the deployment proof.

## Azure development infrastructure

Azure infrastructure is a required deliverable of the durable-conversation work, not a later
configuration detail. The current dev resource group has no Storage account and no Service Bus
namespace, so neither dependency exists yet.

Infrastructure is implemented first, in the sibling 'forge-infra' repository:

    dev/350-conversation-data/
      main.bicep
      main.bicepparam
      storage.bicep
      servicebus.bicep
      identities.bicep
      kind/
    dev/525-conversation-app/
      main.bicep
      main.bicepparam

The 350 layer belongs after 300-data and before 400-appenv. **Its final state is deployed and
verified (2026-08-13):** the two-Job Kind verifier passed the service → command → Worker → progress
→ service round trip after the obsolete Worker Storage/command-Sender rights, combined SAS rule,
and legacy Key Vault secret were removed. The deployed layer implements this target contract:

- a Standard v2 Azure Storage account, with Tables for conversation events/indexes and Orleans
  checkpoint/clustering state, plus the 'forgeconversationartifacts' Blob container;
- a Standard Azure Service Bus namespace and session-enabled, duplicate-detection
  `mission-command` and `conversation-progress` queues;
- separate host and worker user-assigned managed identities and least-privilege role assignments.
  Both identities can pull their own images. The Conversation service can read/write Table/Blob,
  send `mission-command`, and receive `conversation-progress`. The Worker can receive
  `mission-command`, send `conversation-progress`, and read only the existing `Mcl-ApiKey` and
  `Anthropic-ApiKey` secrets through individual-secret Key Vault role assignments. The Worker has
  no Table/Blob role. Neither identity receives a vault-wide Key Vault role, billing, or Rooms
  database credentials.

Production Container Apps use managed identity and service endpoints. Local Kind cannot use an
Azure managed identity, so the refit writes **dev-only**, non-production, least-privilege connection
secrets to Key Vault through an idempotent deployment script, following the existing 300-data
precedent. The Conversation service receives its isolated Storage connection plus queue-scoped
`mission-command` Send and `conversation-progress` Listen credentials. The Worker receives only
queue-scoped `mission-command` Listen and `conversation-progress` Send credentials. No local Worker
credential authorises Storage access or Service Bus Manage.

The Bicep layer exports only non-secret endpoints/IDs. Its forge-infra Kind Make target uses the
developer's Azure CLI login to read those Key Vault secrets directly into the transient
'forge-conversation-cloud' Kubernetes Secret; no secret is committed, written to a parameter file,
or left on the host filesystem. The matching down target deletes that namespace Secret with the
Kind resources. Before the application images exist, that target applies a checked-in verification
Job which creates/reads/deletes a temporary Table entity, uploads/deletes a Blob probe, and sends
then session-receives a Service Bus probe message from Kind. Host/Worker manifest templates are
deliberately not applied until their task has defined the image, port, configuration, and health
contracts; they then replace the verification-only acceptance path with the full local service
proof.

The 525 layer declares the cloud Conversation service and Worker Container Apps, their identities,
ingress, scale rules, Key Vault references, and endpoint configuration. **Its final internal-service/
isolated-Worker refit was authored and what-if reviewed (2026-08-13) at local forge-infra commit
`ad7b77f`; it is not deployed.** Deployment remains blocked by pending application-image tags and
the later Tier-1 adapter integration review. The local Kind proof remains the first product
deployment.

The future Conversation service is an internal-only one-replica Container App; the Worker is a
one-replica Container App with no ingress because it consumes Service Bus rather than receiving
HTTP calls. The public routes are the existing Tier-1 adapters: ForgeUI for OIDC-authenticated
browser/Rooms users, and ForgeAPI for platform-key-authenticated Desktop and machine/API clients.
Both pass the authenticated tenant/user context through an internal Conversation-service contract;
neither identity holds conversation Table/Blob permission. Wiring either adapter to that contract
is a separate implementation and security-review task. Both Tier-2 apps use their separate 350
user-assigned identities and Azure SDK token credentials, never the Kind connection strings. The
code-facing configuration contract is:

    ConversationStorage__TableEndpoint
    ConversationStorage__BlobEndpoint
    ConversationServiceBus__FullyQualifiedNamespace
    ConversationServiceBus__QueueName
    AZURE_CLIENT_ID

`AZURE_CLIENT_ID` selects the appropriate user-assigned identity for `DefaultAzureCredential`.
The Worker alone also receives `MCL_API_KEY` and `ANTHROPIC_API_KEY` through its existing
individual-secret Key Vault references. The fully qualified Service Bus namespace is supplied as
`<namespace>.servicebus.windows.net`, never an HTTPS endpoint that application code must parse.
The 525 Bicep parameter file uses clearly pending ACR image tags so it can compile and what-if now;
its Make deployment target refuses to deploy until both point to published images.

The forge-infra Makefile gains '350-conversation-data-what-if' and
'350-conversation-data', the three '350-conversation-kind-*' targets, and
'525-conversation-app-what-if'/'525-conversation-app'. The required order is:

1. author/validate all 350 and 525 IaC in CI;
2. run 'make 350-conversation-data-what-if' from forge-infra and review it;
3. deploy 350 through its Make target and verify cloud resources/RBAC with Azure CLI;
4. run the Kind Make target to prove the local cluster reaches those cloud resources;
5. review the 525 what-if before code/image work; deploy it only when the images exist.

The Azure CLI is for observation, verification, and the Make targets' deployment path; it is never
used to hand-create a resource that Bicep must own.

## Deferred

- Human intervention/suspend/resume (43.5).
- General MCL checkpoint/resume for uncertain provider calls.
- Cloud catalog/OCI, multi-silo HA, Azure SignalR backplane, regional recovery/load shedding.
- Orleans durable-collection APIs until their exact supported public surface is selected.
- Migration of existing Rooms, membership, ledger, or other Postgres stores.
