# Phase 43.16 — Durable Janus conversation proof

> **Status: Design review (2026-08-12).** This replaces the earlier in-memory Janus Desktop sketch
> before implementation starts. Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md);
> builds on [43.11](phase-43.11-wasm-photino-shell.md), the completed
> [43.15 Janus mission](phase-43.15-janus-inter-agent-mission.md), and the shared
> [durable-conversations design](../design/durable-conversations.md).

## Proof

In Forge Desktop, select **Janus**, submit a local task, and watch the real
Proposer/Approver/Implementer exchange as a group conversation. Stop the conversation service or
disconnect Desktop, restart/reconnect, and recover the completed trail in order.

The user selects a mission, not a provider/model. Janus's current OpenAI/Anthropic profiles remain
the mission 'forge.toml' concern. The session is observational: human approve/deny/edit/suspend
controls remain 43.5. Tool requests are machine-to-machine hand-offs to Client Runtime's existing
authorization path, not a human-in-the-loop shortcut.

| Included | Deferred |
|---|---|
| Durable Janus transcript/run state/reconnect. | Cloud catalog, OCI publishing, provider picker. |
| Orleans ConversationGrain and MissionRunGrain. | Existing Rooms/membership/ledger migration from Postgres. |
| Table events, Blob artifacts, Service Bus reliable command delivery. | General resume after an uncertain in-flight provider call. |
| One-Silo Kind proof with Azurite and Service Bus emulator. | Multi-silo, HA, and cloud deployment. |
| Desktop group-chat projection; future Rooms reuse. | 43.4 workbench and 43.5 human controls. |

## Locked decisions

1. **New bounded context.** Durable conversations start Table-native; no existing Postgres table
   migrates in this phase.
2. **Checkpoint plus event log.** Small Azure Table grain state records operational checkpoint;
   append-only ConversationEvent records are the canonical transcript.
3. **Reliable means at-least-once.** Each command has one stable ID, becomes Service Bus MessageId,
   and is idempotent in grain and worker. The bus is never transcript storage.
4. **One conversation owns order.** ConversationGrain allocates sequence and appends events. Janus
   experts are participants, never grains.
5. **Desktop owns local tools.** Host publishes expected tool request; Client Runtime
   authorizes/executes and posts the result.
6. **Recovery is explicit.** Completed steps and waits replay. An uncertain in-flight provider call
   becomes 'interrupted', never silently duplicated.
7. **Forge-native API is additive.** Conversation HTTP/SSE is separate from unchanged '/v1/*' doors.
8. **Kind is the acceptance environment.** Desktop stays on the host; Kind runs durable services.

## Build sequence

### 1. Contracts and project boundaries

Create:

    src/ForgeMission.Conversations.Contracts/
      ConversationContracts.cs
      ConversationContractsJsonContext.cs
    src/ForgeMission.ConversationHost/
    src/ForgeMission.ConversationHost.Tests/

Add these to 'src/ForgeMission.slnx'. Contracts are AOT-safe and may be referenced by Client
Runtime/Presentation. ConversationHost is a normal server/Silo and may reference Orleans,
Azure Table/Blob, Service Bus, Core, and ChatClients; those dependencies must not leak into the
AOT Client Runtime or CLI.

Define source-generated v1 DTOs:

    ConversationEvent, ConversationSnapshot, ConversationCommand
    StartConversationRequest/Response
    SubmitConversationCommandRequest/Response
    SubmitToolResultRequest/Response
    ConversationParticipant, ConversationEventKind, ConversationRunStatus

A command contains command ID, conversation/run IDs, mission reference, goal/continuation data, and
capability declarations. It never contains credentials or the local workspace path.

**Done when:** serialization tests round-trip every event kind through generated contexts and
architecture tests prove Client Runtime/CLI do not reference Host/Orleans/Azure SDK assemblies.

### 2. Durable-ready MCL trace facts

Replace the narrow synchronous PipelineRunOptions step callbacks with awaited structured callbacks.
Add AOT-safe PipelineStepStarted, PipelineStepDelta, PipelineStepCompleted, and tool-request trace
records under 'ForgeMission.Core.Runtime'.

Each record carries mission name/path, expert/participant, kind, attempt, and completed
StepEnvelope where relevant. Propagate callbacks through nested mission invocation. This is
essential: PipelineRunner currently reconstructs options for Janus's Negotiate and Implement
sub-missions, which otherwise hides the Proposer/Approver/Implementer trace. Parallel steps retain
their own lifecycle facts; conversation sequence allocation serialises their rendered order.

Keep MissionResult and existing CLI/runner progress contracts compatible. The new trace surface is
additive.

**Done when:** a deterministic Janus test sees Proposer complete, Approver with verdict, a rejected
retry attempt, and Implementer only after approval, including across the nested MCL missions.

### 3. Table/Blob persistence and Orleans ownership

Implement inside ConversationHost:

    IConversationEventStore / AzureTableConversationEventStore
    IConversationArtifactStore / AzureBlobConversationArtifactStore
    ConversationGrain / MissionRunGrain

Configure named Azure Table grain storage for ConversationCheckpoint and MissionRunCheckpoint and
Azure Storage clustering for Kind. Event append is idempotent by event ID, uses grain-assigned
sequence, and exposes ReadAfterAsync(conversationId, sequence). Content beyond Table bounds goes
to Blob before a reference event is appended.

ConversationGrain persists accepted command state before dispatch and repairs its deterministic
event on activation. MissionRunGrain records terminal status, never creates per-expert grains, and
marks a stale executing checkpoint as interrupted rather than replaying an uncertain provider call.

**Done when:** Azurite integration tests create/re-activate a grain, replay an ordered transcript
from a fresh host, reject duplicate event/command IDs, and dereference a Blob-backed artifact.

### 4. Service Bus delivery and mission worker

Add:

    IConversationCommandQueue / AzureServiceBusConversationCommandQueue
    ConversationCommandDispatcher / ConversationCommandWorker

Configure a session-enabled 'mission-command' queue with duplicate detection. Set MessageId to
command ID and SessionId to conversation ID. The worker uses peek-lock, records its
command/run-state transition before completing the message, retries pending work with the identical
ID, and turns dead-letter failure into a visible error/run-status event.

The worker loads Janus from its existing mission and forge configuration, invokes traced
PipelineRunner, and appends each completed trace event through ConversationGrain. At a tool call it
appends tool_requested, waits for the matching tool result, then enqueues only the safe
continuation. It never accesses a local filesystem or terminal.

**Done when:** queue integration tests prove duplicate delivery creates one durable transition,
commands for separate conversations remain isolated, and an unexpected tool-result ID does not
advance the run.

### 5. Conversation API and resumable SSE

ConversationHost Program hosts the Silo, worker, API, health endpoints, and source-generated JSON.
Map the five routes in [durable-conversations.md](../design/durable-conversations.md#reconnect-and-projections).

SSE first reads durable events after the client's supplied sequence, then follows live appends.
Correctness is replay, not a permanently healthy connection. A one-replica in-process live notifier
is allowed only because event history always comes from Table.

The local HTTP adapter supplies fixed development tenant/user identity only; contract/grain keys
retain tenant/user ownership for a later Forge identity adapter.

**Done when:** an HTTP test submits Janus, disconnects after a known sequence, reconnects, and
gets exactly later events in order; existing '/v1/*' contract tests remain unchanged.

### 6. Client Runtime and group-chat rendering

Add ConversationRuntimeSession beside MissionRuntimeSession and CloudMissionRuntimeSession. Extend
session setup/prompt contracts only to select durable conversation runtime and preserve the returned
conversation ID. Presentation never calls ConversationHost directly.

Client Runtime starts/submits Janus, subscribes/reconnects using last sequence, relays typed
conversation events through its existing local SSE endpoint, executes 'tool_requested' through
ToolExecutorRegistry and ICapabilityDispatcher, then posts the matching result.

Update AttachableMissions with **Janus** on this durable local path. ChatGPT/Websearch remain on
their existing paths until separately migrated. In Pages/Home.razor, add named participant bubbles,
attempt grouping, approval/revision/not-approved states, transient typing, and Implementer tool
rows. Rehydrated and live events use one renderer; normal missions retain their current renderer.

**Done when:** UI tests render approval, revision, not-approved, and rehydrated tool result without
duplicates; boundary tests still prohibit Presentation's direct Host dependency.

### 7. Local Kind environment

Add:

    deploy/durable-kind/
      namespace.yaml, azurite.yaml, servicebus-emulator.yaml
      servicebus-config.json, conversation-host.yaml, mission-worker.yaml
    scripts/durable-dev-up.ps1
    scripts/durable-dev-down.ps1
    scripts/durable-dev-status.ps1

Scripts preflight Docker, Kind/Kubectl, and the Service Bus emulator SQL Server emulation
prerequisite before changing the cluster. They create/use one named cluster, build/load Host/Worker
images, apply manifests, wait for health, print the Desktop port-forward endpoint, and clean up
only named resources. The first manifest has one Host/Silo and one worker.

**Done when:** durable-dev-up reports all dependencies healthy; durable-dev-down removes only named
development resources; unavailable amd64 SQL emulation fails clearly before deployment.

### 8. Product proof and evidence

Run real Janus with configured OpenAI and Anthropic providers through Desktop and Kind. Record named
observations for:

1. picker to Janus task submission;
2. ordered Proposer/Approver/Implementer group conversation;
3. rejection/revision with no Implementer before approval;
4. authorized local Implementer tool call and durable result;
5. service/Desktop disconnect and replay from missed sequence;
6. visible interrupted status rather than a silent duplicate after intentional in-flight stop;
7. browser and packaged Photino rendering of recovered conversation;
8. successful solution build/test.

Azurite-only success is local-dev evidence. A non-production Azure Storage acceptance run is required
before any production-ready claim.

## Done when

- Janus is selectable in Desktop and its real multi-provider exchange is one ordered group chat.
- Completed messages, approval/revision, tool hand-offs/results, and terminal state survive Host
  restart and Desktop reconnect.
- Service Bus commands are delivered at least once and observed once at the durable run-state
  boundary.
- Desktop remains sole local-tool executor; '/v1/*' clients work unchanged.
- The Kind proof and all named evidence above are recorded.

## Hand-off gate

This is ready for a Claude implementation assignment only after the operator reviews and approves
it. The assignment follows [claude-codex-workflow.md](../design/claude-codex-workflow.md): one task
at a time, named files/tests, and Codex review against this Done-when condition.
