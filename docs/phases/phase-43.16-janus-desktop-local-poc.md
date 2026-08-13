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
| One-Silo Kind proof plus Azure data-plane provisioning/acceptance. | Multi-silo, HA, and cloud application deployment. |
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
8. **Kind is the primary product acceptance environment.** Desktop stays on the host; Kind runs
   durable services. A Bicep-provisioned non-production Azure data plane is a required second
   acceptance environment.

## Build sequence

### 1. Infrastructure foundation — first, in forge-infra

All infrastructure for this phase is authored, validated, and where it has no image dependency
deployed before application implementation begins. It lives in the sibling
'/Users/ameerdeen/progs/forge-infra' repository; mission-control-language contains application code
and acceptance evidence, not a second IaC implementation.

Create these ordered IaC units:

    dev/350-conversation-data/
      main.bicep, main.bicepparam
      storage.bicep, servicebus.bicep, identities.bicep
      scripts/write-kind-dev-credentials.sh
      kind/namespace.yaml, kind/conversation-host.yaml, kind/mission-worker.yaml
    dev/525-conversation-app/
      main.bicep, main.bicepparam

The 350 layer sits between 300-data and 400-appenv. It creates and deploys the cloud data plane:
the isolated Storage account/Table/Blob resources, Standard Service Bus namespace and
session-enabled duplicate-detection queue, Key Vault bootstrap of the two Kind-only credential
secrets, managed identities, and all least-privilege data-plane/AcrPull/Key-Vault role assignments.
The Worker receives Key Vault Secrets User at the individual `Mcl-ApiKey` and `Anthropic-ApiKey`
secret scopes only; it receives neither a vault-wide assignment nor billing/Rooms credentials. It
does not alter the existing Postgres layer.

The 525 layer declares the future cloud Conversation Host and worker Container Apps, their separate
identities, Key Vault references, ingress, scale rules, and non-secret Storage/Service-Bus endpoint
configuration. It is Bicep-validated and what-if reviewed now, before code is written. Its actual
Container Apps deployment waits only for the application images; cloud application hosting is not a
substitute for the local Kind product proof.

**525 cloud topology requires a further design gate before implementation is accepted.** An
externally-ingressed Conversation Host that owns Table/Blob state would collapse the Phase 42
internet-facing and application/data-owning tiers. Likewise, a separate Worker with direct
Table/Blob access breaks conversation-store ownership. The current un-deployed 525 Bicep is useful
scaffolding only; do not deploy or accept it until the next design assigns an explicit tier-1 edge
route, an internal-only state-owning Conversation service, and a Worker-to-service progress path
that does not grant the Worker direct store access. See [durable-conversations.md](../design/durable-conversations.md#north-star-tiering-gate).

The forge-infra Makefile gains:

    make 350-conversation-data-what-if
    make 350-conversation-data
    make 350-conversation-kind-up
    make 350-conversation-kind-down
    make 350-conversation-kind-status
    make 525-conversation-app-what-if
    make 525-conversation-app

'350-conversation-kind-up' creates/reuses the 'forge-durable' cluster with
'kind create cluster --name forge-durable', reads the Bicep-created dev-only cloud credentials
from Key Vault through the operator's Azure CLI login into a transient namespace Secret, and runs a
checked-in verification Job that creates/reads/deletes a temporary Azure Table entity, uploads and
deletes a Blob probe, and sends then session-receives a Service Bus probe message. This is the 350
acceptance proof before Host/Worker images exist. The Host and Worker
manifest templates are checked in now but are not applied until their application tasks define their
image, port, environment, and health contracts; their later version of the target builds/loads the
local images, waits for health, and prints the Desktop endpoint. Down removes only that named Kind
cluster/namespace; it never deletes Bicep-owned Azure resources. The local Kind compute therefore
uses the real Azure Table/Blob and Service Bus resources from day one.

Before deploying any Azure layer, run and review its Make what-if target. Deploy only through its
Make target. Verify each deployment with Azure CLI observations: Storage account/Table/Blob,
Service Bus namespace/queue/session/duplicate settings, Key Vault secret presence without printing
values, and role assignments. The current dev resource group has neither a Storage account nor a
Service Bus namespace, so this is an upfront dependency, not optional polish.

**Done when:** 350 is deployed and verified; Kind can reach the real Azure data plane; 525 compiles
and has a reviewed what-if; all IaC is committed/pushed in forge-infra before the first application
task starts.

**Current status (2026-08-13):** The final 350 data-plane refit is deployed and verified live on
`forge-infra` branch `codex/350-conversation-data` through local commit `f9cf457`: its two-Job
Kind verifier passed the service → command → Worker → progress → service round trip after the
obsolete Worker Storage/command-Sender rights, combined SAS rule, and legacy Key Vault secret were
removed. The Tier-1 decision is locked: ForgeUI is the OIDC browser/Rooms edge and ForgeAPI is the
platform-key Desktop/machine edge; both route internally to the Conversation service. The 525
internal-service/isolated-Worker refit is accepted at local forge-infra commit `ad7b77f`: its
Bicep and parameters compile, its what-if creates only the two undeployed Container Apps, and the
pending-image guard blocks a real deployment. It must not be deployed until application images and
the Tier-1 integration review are ready.

**Task 2 is complete and verified**, on this repo's `codex/conversation-contracts` branch:
`ForgeMission.Conversations.Contracts` (every v1 record/enum, no project or package reference),
`ForgeMission.ConversationHost` (a `CreateSlimBuilder`/`Build`/`Run` shell referencing only
Contracts), and `ForgeMission.ConversationHost.Tests` are added to `ForgeMission.slnx`. 55 tests
pass: every `ConversationEventKind` representative, every request/response/command/progress/tool/
capability/snapshot type, and every enum value round-trip through
`ConversationContractsJsonContext`; a dedicated test proves null optional `ConversationEvent`
payload fields are omitted; source-level boundary tests prove Contracts has no project/package
reference and that Contracts, Client Runtime, and CLI name neither `ConversationHost` nor Orleans/
Azure SDK packages. `dotnet build src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx`
both pass clean. **Next: Task 3, durable-ready MCL trace facts.**

### 2. Contracts and project boundaries

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

This task defines data and project seams only: **no endpoint, grain, queue, Storage, or MCL
execution implementation belongs here.** All types below are `public sealed record` DTOs with
camel-case source-generated JSON. Identifiers are `Guid`; Service Bus converts `ConversationId` to
its `N` string representation only at the queue adapter boundary. Timestamps are
`DateTimeOffset`. Collections use arrays, never an unbounded object bag.

#### v1 semantic event and snapshot

    ConversationEvent(
      Guid EventId, int Version, Guid ConversationId, Guid? RunId, long Sequence,
      ConversationEventKind Kind, ConversationParticipant Participant, int? Attempt,
      string? Text, string? Reason, ConversationApproval? Approval,
      ConversationToolRequest? ToolRequest, ConversationToolResult? ToolResult,
      ConversationArtifactReference? Artifact, ConversationRunStatus? RunStatus,
      DateTimeOffset OccurredAtUtc)

    ConversationSnapshot(
      Guid ConversationId, string MissionRef, Guid? ActiveRunId, long LastSequence,
      ConversationRunStatus Status, Guid? ExpectedToolRequestId, DateTimeOffset UpdatedAtUtc)

`ConversationEvent.Version` is `1` for this implementation. `Sequence` is assigned only by
`ConversationGrain`; a Worker never supplies it. `RunId` is null only for a future
conversation-level fact, not for Janus v1 run events. `Text`, `Reason`, `Approval`,
`ToolRequest`, `ToolResult`, `Artifact`, and `RunStatus` are nullable because each event kind has
one relevant semantic payload; no generic JSON payload is allowed.

    ConversationParticipant: User | Proposer | Approver | Implementer | Forge
    ConversationEventKind: UserMessage | ParticipantStarted | ParticipantMessage | Approval |
                           ToolRequested | ToolResult | RunStatus | Artifact | Error
    ConversationRunStatus: Queued | Running | WaitingForTool | Completed | Rejected |
                           Interrupted | Failed
    ConversationApprovalOutcome: Approved | RevisionRequested | NotApproved

    ConversationApproval(ConversationApprovalOutcome Outcome, string? Feedback)
    ConversationToolRequest(Guid RequestId, string ToolName, JsonElement Arguments)
    ConversationToolResult(Guid RequestId, string Content, bool IsError)
    ConversationArtifactReference(string ArtifactId, string ContentType, string? FileName)

For `UserMessage`, `ParticipantMessage`, and `Error`, use `Text`/`Reason`; `Approval` uses
`ConversationApproval`; tool kinds use their matching tool record; `RunStatus` uses `RunStatus`;
and `Artifact` uses `Artifact`. `ParticipantStarted` has no additional payload. Tool arguments may
contain a mission-relative path but never a desktop workspace root or other local-machine path.

#### Commands and Worker progress

The command/progress queue bodies live in Contracts because the Host and Worker are separate
processes. They are not HTTP DTOs:

    ConversationCommand(
      Guid CommandId, Guid ConversationId, Guid RunId, ConversationCommandKind Kind,
      string MissionRef, string Goal, ConversationCapabilityDeclaration[] Capabilities,
      ConversationToolResult? ToolResult)

    ConversationCommandKind: StartMission | ContinueAfterTool
    ConversationCapabilityDeclaration(string Name, string Description, JsonElement InputSchema)

    ConversationProgress(
      Guid EventId, Guid ConversationId, Guid RunId, ConversationEventKind Kind,
      ConversationParticipant Participant, int? Attempt, string? Text, string? Reason,
      ConversationApproval? Approval, ConversationToolRequest? ToolRequest,
      ConversationToolResult? ToolResult, ConversationArtifactReference? Artifact,
      ConversationRunStatus? RunStatus, DateTimeOffset OccurredAtUtc)

`CommandId` is generated by the submitting client and is the command queue's `MessageId`.
`ConversationProgress.EventId` is generated once by the Worker and is the progress queue's
`MessageId`. Both queues use the `ConversationId` as `SessionId`. The command includes mission,
goal/continuation, and capability declarations so the Worker needs no conversation-store read. It
contains neither credentials nor local workspace paths. `ConversationProgress` deliberately has no
sequence: the Conversation service converts it to the canonical `ConversationEvent` through the
grain.

#### HTTP request/response contract

The Host's local development adapter uses its fixed development tenant/user identity. A later
ForgeUI/ForgeAPI adapter obtains identity at Tier 1 and passes authenticated tenant/user context to
the internal service; tenant/user IDs are therefore **not client-supplied fields** in these v1 DTOs.

    StartConversationRequest(Guid CommandId, string MissionRef, string Goal,
                             ConversationCapabilityDeclaration[] Capabilities)
    StartConversationResponse(Guid ConversationId, Guid RunId, long AcceptedSequence,
                              ConversationRunStatus Status)

    SubmitConversationCommandRequest(Guid CommandId, string Text)
    SubmitConversationCommandResponse(Guid ConversationId, Guid RunId, long AcceptedSequence,
                                      ConversationRunStatus Status)

    SubmitToolResultRequest(Guid CommandId, Guid ToolRequestId, string Content, bool IsError)
    SubmitToolResultResponse(Guid ConversationId, Guid RunId, long AcceptedSequence,
                             ConversationRunStatus Status)

- `POST /conversations` accepts `StartConversationRequest`, creates a conversation and its first
  run, appends the `UserMessage` fact, and returns `201 Created` with
  `StartConversationResponse` and `Location: /conversations/{conversationId}`.
- `POST /conversations/{conversationId}/commands` accepts a follow-up `Text` for the conversation's
  pinned `MissionRef`, creates a run, and returns `202 Accepted` with
  `SubmitConversationCommandResponse`. The request cannot select a different mission or replace
  capabilities.
- `POST /conversations/{conversationId}/tool-results` accepts `SubmitToolResultRequest` and returns
  `202 Accepted` with `SubmitToolResultResponse`. The grain rejects an unknown or already-completed
  tool request without advancing the run.
- `GET /conversations/{conversationId}` returns `200 OK` with `ConversationSnapshot`; an unknown
  conversation is `404`.
- `GET /conversations/{conversationId}/events?after={sequence}` returns `text/event-stream`. It
  emits each complete `ConversationEvent` as `event: conversation-event`, `id: {Sequence}`, and
  one source-generated JSON `data:` value. `after` is an optional non-negative `long`, default
  `0`; the server first replays events with `Sequence > after`, then follows live events. The
  client deduplicates by `EventId`, retains its highest rendered sequence, and reconnects with that
  value. SSE token deltas are outside this v1 durable contract.

Malformed route/request data is `400`; an accepted duplicate `CommandId` or already-recorded tool
result returns the original accepted response rather than producing another event. The endpoint
adapter owns HTTP status mapping; grains and queue consumers return typed results, never
`HttpContext` or `IResult`.

#### Project and JSON boundary

`ForgeMission.Conversations.Contracts` has no package or project references. Its
`ConversationContractsJsonContext` must source-generate metadata for every record, enum, array,
and `JsonElement` above, with `PropertyNamingPolicy = CamelCase` and
`DefaultIgnoreCondition = WhenWritingNull`. `ConversationHost` references Contracts; Contracts
never references Host, Core, Client Runtime, Presentation, Orleans, Azure SDKs, or provider SDKs.
`ForgeMission.ClientRuntime` may reference Contracts in Task 7, but neither Client Runtime nor CLI
references Host.

Project scaffolding is deliberately minimal:

- `ForgeMission.Conversations.Contracts.csproj` uses `Microsoft.NET.Sdk`, `net10.0`, implicit
  usings, nullable enabled, and `IsAotCompatible=true`; it has no `ItemGroup` dependency entries.
- `ForgeMission.ConversationHost.csproj` uses `Microsoft.NET.Sdk.Web`, `net10.0`, nullable
  enabled, and initially references only Contracts. Its `Program.cs` is the smallest
  `WebApplication.CreateSlimBuilder`/`Build`/`Run` shell with **no mapped endpoint or hosted
  service**; Task 4 introduces Orleans and Task 6 introduces routes.
- `ForgeMission.ConversationHost.Tests.csproj` uses `Microsoft.NET.Sdk`, `net10.0`,
  `IsPackable=false`, xUnit `2.9.3`, `Microsoft.NET.Test.Sdk` `17.14.1`, and
  `xunit.runner.visualstudio` `3.1.4`; it references Contracts and Host. Do not add Azure,
  Orleans, provider, or test-server packages in Task 2.

Add all three projects to `ForgeMission.slnx`. The architecture test reads the relevant csproj
files relative to the solution root and asserts: Contracts has neither `ProjectReference` nor
`PackageReference`; Client Runtime and CLI name neither ConversationHost nor Orleans/Azure SDK
packages; and Host is not referenced by Contracts, Client Runtime, or CLI. This is intentionally a
cheap source-level boundary test: later dependency changes fail it at review time, before they can
be hidden behind a transitive runtime load.

**Done when:** serialization tests round-trip one representative of every event kind plus every
request/response, command, progress, tool, capability, and snapshot type through the generated
context; a contract test proves null optional event fields are omitted; architecture tests prove
Contracts has no dependencies and Client Runtime/CLI do not reference Host/Orleans/Azure SDK
assemblies. `dotnet build src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx` pass.

### 3. Durable-ready MCL trace facts

Replace the narrow synchronous step lifecycle callbacks on `PipelineRunOptions`
(`OnStepStart`/`OnStepComplete`) with one awaited, structured Core trace seam. This task creates
execution facts only; it neither persists them nor references Conversation contracts, Orleans,
Azure, Service Bus, HTTP, Client Runtime, or Presentation. Task 5 maps the completed/tool facts to
the already-defined Worker progress contract; Task 4 is the only owner of durable transcript order.

#### Trace surface

Add `PipelineTraceEvent.cs` under `ForgeMission.Core.Runtime` with the following public, sealed
record hierarchy. `MissionPath` is an immutable-in-practice `IReadOnlyList<string>` created by the
runner as a fresh array; it contains mission names only, from root to the mission currently
executing. `MissionName` is its final element. `ExpertName` remains the MCL expert name (and is the
future Janus participant mapping input); Core does not depend on `ConversationParticipant`.

    abstract PipelineTraceEvent(
      string MissionName, IReadOnlyList<string> MissionPath,
      string ExpertName, string ExpertKind, int Attempt)

    PipelineStepStarted(...)
    PipelineStepDelta(..., string Text)
    PipelineStepCompleted(..., StepEnvelope Envelope)
    PipelineToolRequested(..., IReadOnlyList<PipelineToolCall> Calls)
    PipelineToolCall(string CallId, string Name, JsonElement Arguments)

`PipelineStepDelta.Text` is a non-empty raw streaming chunk. It is transient: Task 3 does not
claim it is resumable or turn it into a `ConversationEvent`. `PipelineStepCompleted` is emitted for
both pass and fail envelopes, after the output/history context update and before the runner decides
whether the envelope fails the mission. `PipelineToolRequested` is emitted after that step's
completed event, once for the non-empty set of client tool calls that makes `PipelineRunner` return
early. It carries no provider SDK object: convert each `FunctionCallContent` to the closed
`PipelineToolCall` shape, including a cloned `JsonElement` of its arguments. Use a small
`Utf8JsonWriter`-based conversion (the existing Runner mapper is a useful shape), not reflection
serialization; an unsupported argument CLR type fails with a clear exception.

Add the following optional final fields to `PipelineRunOptions`; retain its existing mission,
context, writer, tool, and continuation fields:

    IReadOnlyList<string>? MissionPath = null
    Func<PipelineTraceEvent, CancellationToken, Task>? OnTrace = null

Remove `OnStepStart` and `OnStepComplete`. `OnTrace` is always awaited with the run cancellation
token. A throwing trace sink therefore fails the run and lets its caller apply normal retry/nack
policy; facts must not silently disappear. `OnSearchProgress` remains unchanged in this task: it is
the existing synchronous callback imposed by Scout's `IProgress` backend, not a pipeline lifecycle
fact, and preserves the current runner streaming contract. No caller is required to supply
`OnTrace`, so a normal CLI/API run has unchanged trace overhead and behavior.

#### Runner behavior and nested paths

At the start of `RunAsync`, establish the effective path as `options.MissionPath ??
new[] { options.MissionName }`. Before invoking a real expert, await `PipelineStepStarted`; its
attempt is the current invocation's loop attempt. When the existing writer-driven streaming branch
runs, await a `PipelineStepDelta` for every non-empty yielded chunk in the same order as the writes.
Do **not** force the streaming path merely because `OnTrace` exists: several non-LLM runners expose
a text-only streaming adapter that cannot preserve a failing `StepEnvelope`. The non-streaming path
therefore emits started/completed (and possible tool-request) facts but no deltas, preserving all
existing pass/fail behavior.

`PipelineStepCompleted` must be awaited before the next step begins. The completed and tool-request
facts are emitted in that order for one step. Do not emit synthetic lifecycle facts for the
sub-mission invocation itself: the actual experts inside it are the visible conversation trail.

Replace both ad-hoc child `new PipelineRunOptions(...)` calls in `ExecuteStepAsync` and
`ExecuteParallelStepAsync` with one small `CreateChildOptions` helper. It passes the current
`StepWriter`, `ContentWriter`, `OnSearchProgress`, and `OnTrace`; gives the child its explicit
binding vars; and appends its declared mission name to the parent path. It deliberately does **not**
inherit `ContextObjects`, `Tools`, `StartAtAgent`, or `OnPreAgentComplete`, preserving today's
isolated sub-mission/tool semantics. This is the essential Janus fix: Proposer/Approver have
`[Janus, Negotiate]`, and Implementer has `[Janus, Implement]`.

Parallel steps may call the sink concurrently and retain their own facts/path/attempt. Task 3 does
not impose a global sequence or sort them; `ConversationGrain` serializes accepted Worker progress
in Task 4/5. For sequential steps, awaiting the sink provides their observable order.

#### Existing Runner and CLI compatibility

Keep `MissionResult`, `/run`, `/run/stream`, `RunResponse.Trace`, `RunTraceStep`, `RunProgress`,
and CLI `--steps` wire/console shapes unchanged. Update `MissionRunHandler` to consume `OnTrace`:
map `PipelineStepStarted` to the existing transient `RunProgress(expertName, expertKind)` and map
each `PipelineStepCompleted` to the existing `RunTraceStep` using that event's envelope and attempt.
Ignore deltas/tool facts there for now; they are for the durable Worker path. Protect its buffered
trace list if parallel callbacks can enter it concurrently. Keep its Scout progress callback
unchanged. This improves internal trace coverage to nested missions without changing a public
Runner DTO.

Update the existing Scout lifecycle test to collect `PipelineStepStarted` through `OnTrace` rather
than the removed callback. Add focused Core tests:

1. A deterministic Janus-shaped mission (`Janus -> Negotiate loop(2) -> Proposer -> Approver`, then
   `Implement -> Implementer`) uses a stub where Approver fails once then approves. Its trace proves
   Proposer and Approver completed at `[Janus, Negotiate]`, the first Approver completed with the
   failed verdict at attempt 1, the second negotiation attempt occurs, and Implementer at
   `[Janus, Implement]` starts only after the approving Approver completion.
2. A gated `OnTrace` test proves a step does not invoke its expert until its awaited started sink is
   released, and a two-step mission proves completed is observed before the next started event.
3. A tool-capable stub proves `PipelineToolRequested` follows its completed fact and exposes only
   the closed call ID/name/arguments shape.

**Done when:** the Core and Runner compile with no remaining `OnStepStart`/`OnStepComplete`
references; the deterministic nested Janus trace and awaited-order/tool tests pass; the existing
Scout lifecycle test passes through `OnTrace`; and `dotnet build src/ForgeMission.slnx` plus
`dotnet test src/ForgeMission.slnx` pass. No Conversation Host, Worker, Client Runtime,
Presentation, mission definition, or forge-infra file changes in this task.

**Task 3 is complete and verified**, on this repo's `codex/durable-pipeline-trace` branch:
`PipelineTraceEvent.cs` adds the closed `PipelineStepStarted`/`PipelineStepDelta`/
`PipelineStepCompleted`/`PipelineToolRequested`/`PipelineToolCall` hierarchy;
`PipelineRunOptions.OnStepStart`/`OnStepComplete` are gone, replaced by the awaited
`MissionPath`/`OnTrace` fields; `PipelineRunner`'s new `CreateChildOptions` helper is the single
place a nested sub-mission's options are built, appending its mission name to the parent path
without inheriting `ContextObjects`/`Tools`/`StartAtAgent`/`OnPreAgentComplete`; the
writer-driven streaming condition is untouched, so `OnTrace` alone never forces streaming;
`MissionRunHandler` maps `PipelineStepStarted`/`PipelineStepCompleted` to the existing
`RunProgress`/`RunTraceStep` shapes under a lock, ignoring deltas/tool facts for now. `dotnet
build src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx` both pass clean: 401 tests
(390 passed + 11 pre-existing xAI-credit-exhaustion skips, issue #7 — unrelated), including the
updated Scout lifecycle test and three new `PipelineTraceTests` covering the nested Janus-shaped
trace, the awaited gate/ordering proof, and the closed tool-request shape. **Next: Task 4,
Table/Blob persistence and Orleans ownership.**

### 4. Table/Blob persistence and Orleans ownership

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

### 5. Service Bus delivery and mission worker

Add:

    IConversationCommandQueue / AzureServiceBusConversationCommandQueue
    IConversationProgressQueue / AzureServiceBusConversationProgressQueue
    ConversationCommandDispatcher / ConversationCommandWorker / ConversationProgressConsumer

Configure session-enabled `mission-command` and `conversation-progress` queues with duplicate
detection. A command uses its command ID as `MessageId` and its conversation ID as `SessionId`; a
Worker trace/progress fact uses its stable event ID as `MessageId` and the same conversation
`SessionId`. The Worker uses peek-lock, publishes the progress fact before completing its command,
retries pending work with identical IDs, and turns dead-letter failure into a visible error/run-status
event. The Conversation service consumes progress and invokes `ConversationGrain`; it alone assigns
sequence and appends the event to the conversation store.

The worker loads Janus from its existing mission and forge configuration, invokes traced
PipelineRunner, and publishes each completed trace event through `conversation-progress`. At a tool
call it publishes `tool_requested`, waits for the matching tool result, then enqueues only the safe
continuation. It never accesses a local filesystem, terminal, Orleans client gateway, or conversation
Table/Blob store.

**Done when:** queue integration tests prove duplicate command and progress delivery creates one
durable transition, commands for separate conversations remain isolated, an unexpected tool-result
ID does not advance the run, and the Worker has no reference to Orleans or the conversation
Table/Blob stores.

### 6. Conversation API and resumable SSE

ConversationHost Program hosts the Silo, progress consumer, API, health endpoints, and
source-generated JSON. The separately deployed Worker hosts command dispatch/execution. Map the
five routes in [durable-conversations.md](../design/durable-conversations.md#reconnect-and-projections).

SSE first reads durable events after the client's supplied sequence, then follows live appends.
Correctness is replay, not a permanently healthy connection. A one-replica in-process live notifier
is allowed only because event history always comes from Table.

The local HTTP adapter supplies fixed development tenant/user identity only; contract/grain keys
retain tenant/user ownership for a later Forge identity adapter.

**Done when:** an HTTP test submits Janus, disconnects after a known sequence, reconnects, and
gets exactly later events in order; existing '/v1/*' contract tests remain unchanged.

### 7. Client Runtime and group-chat rendering

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

The Kind proof uses the Bicep-provisioned Azure data plane, not local emulators. Automated
emulator tests remain supplementary; the real Azure run is required before any production-ready
claim.

## Done when

- Janus is selectable in Desktop and its real multi-provider exchange is one ordered group chat.
- Completed messages, approval/revision, tool hand-offs/results, and terminal state survive Host
  restart and Desktop reconnect.
- Service Bus commands are delivered at least once and observed once at the durable run-state
  boundary.
- Desktop remains sole local-tool executor; '/v1/*' clients work unchanged.
- The Kind proof, Bicep deployment, Azure CLI verification, and all named evidence above are
  recorded.

## Hand-off gate

This is ready for a Claude implementation assignment only after the operator reviews and approves
it. The assignment follows [claude-codex-workflow.md](../design/claude-codex-workflow.md): one task
at a time, named files/tests, and Codex review against this Done-when condition.
