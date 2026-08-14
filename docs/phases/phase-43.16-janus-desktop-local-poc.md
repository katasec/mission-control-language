# Phase 43.16 — Durable Janus conversation proof

> **Status: Implementation active (2026-08-14).** Durable persistence, Service Bus/Worker
> delivery, the conversation API/SSE, and the Client Runtime durable session are verified; the
> Kind/Azure product proof (Task 8) is next. This replaces the earlier in-memory Janus Desktop
> sketch. Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md);
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

#### Transport-neutral request/response messages

These Contracts records are the named public command/query messages; HTTP/SSE is only Task 6's
first projection of them. They contain neither `HttpContext`, status/header fields, route templates,
Orleans types, nor Storage types, so a future direct service, gRPC, or broker adapter can carry the
same semantic message. The Host's local development adapter uses its fixed development tenant/user
identity. A later ForgeUI/ForgeAPI adapter obtains identity at Tier 1 and passes authenticated
tenant/user context to the internal service; tenant/user IDs are therefore **not client-supplied
fields** in these v1 DTOs.

    StartConversationRequest(Guid CommandId, string MissionRef, string Goal,
                             ConversationCapabilityDeclaration[] Capabilities)
    StartConversationResponse(Guid ConversationId, Guid RunId, long AcceptedSequence,
                              ConversationRunStatus Status)

    SubmitConversationCommandRequest(Guid ConversationId, Guid CommandId, string Text)
    SubmitConversationCommandResponse(Guid ConversationId, Guid RunId, long AcceptedSequence,
                                      ConversationRunStatus Status)

    SubmitToolResultRequest(Guid ConversationId, Guid CommandId, Guid ToolRequestId, string Content,
                            bool IsError)
    SubmitToolResultResponse(Guid ConversationId, Guid RunId, long AcceptedSequence,
                             ConversationRunStatus Status)

    GetConversationRequest(Guid ConversationId)
    GetConversationResponse(ConversationSnapshot Snapshot)
    ReadConversationEventsRequest(Guid ConversationId, long After)

- `POST /conversations` accepts `StartConversationRequest`, deterministically derives its
  conversation and first-run IDs from the client `CommandId`, appends the `UserMessage` fact, and
  returns `201 Created` with
  `StartConversationResponse` and `Location: /conversations/{conversationId}`.
- `POST /conversations/{conversationId}/commands` accepts a follow-up `Text` for the conversation's
  pinned `MissionRef`, binds the route value to the message's `ConversationId`, creates a run, and returns `202 Accepted` with
  `Location: /conversations/{conversationId}`, `Retry-After: 1`, and
  `SubmitConversationCommandResponse`. The request cannot select a different mission or replace
  capabilities.
- `POST /conversations/{conversationId}/tool-results` binds the route value to
  `SubmitToolResultRequest.ConversationId` and returns
  `202 Accepted` with `Location: /conversations/{conversationId}`, `Retry-After: 1`, and
  `SubmitToolResultResponse`. The grain rejects an unknown or already-completed tool request without
  advancing the run.
- `GET /conversations/{conversationId}` maps to `GetConversationRequest` and returns `200 OK` with
  `GetConversationResponse`; an unknown conversation is `404`.
- `GET /conversations/{conversationId}/events?after={sequence}` maps to
  `ReadConversationEventsRequest` and projects its event sequence as `text/event-stream`. It
  emits each complete `ConversationEvent` as `event: conversation-event`, `id: {Sequence}`, and
  one source-generated JSON `data:` value. `after` is an optional non-negative `long`, default
  `0`; the server first replays events with `Sequence > after`, then follows live events. The
  client deduplicates by `EventId`, retains its highest rendered sequence, and reconnects with that
  value. SSE token deltas are outside this v1 durable contract.

Malformed adapter input is `400`; an accepted duplicate `CommandId` or already-recorded tool result
returns the original accepted response rather than producing another event. The endpoint adapter
owns HTTP status mapping; grains and queue consumers return typed results, never `HttpContext` or
`IResult`. The Task 6 source-generation tests extend Task 2's coverage to these additive message
types; this is a pre-publication correction, so no published wire compatibility is broken.

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

**Task 4 — done and verified 2026-08-13** (commit `951bf73`; suite unblocker `2a8aa60`). See its
existing persistence design and evidence below. Task 5 is also verified; next is Task 6,
conversation API and resumable SSE.

#### Files, packages, and composition root

Add the following Host-only files; use one type per concern and keep endpoint code out of them:

    src/ForgeMission.ConversationHost/Persistence/
      IConversationEventStore.cs
      AzureTableConversationEventStore.cs
      IConversationArtifactStore.cs
      AzureBlobConversationArtifactStore.cs
      ConversationStorageOptions.cs
    src/ForgeMission.ConversationHost/Grains/
      ConversationAddress.cs
      ConversationCheckpoint.cs
      MissionRunCheckpoint.cs
      ConversationGrain.cs / MissionRunGrain.cs
      IConversationGrain.cs / IMissionRunGrain.cs
      ConversationGrainResults.cs
    src/ForgeMission.ConversationHost.Tests/
      AzuriteFixture.cs
      ConversationPersistenceTests.cs
      ConversationGrainTests.cs

Add only these production packages to `ForgeMission.ConversationHost.csproj`, all pinned to
`10.0.0`: `Microsoft.Orleans.Server`, `Microsoft.Orleans.Clustering.AzureStorage`, and
`Microsoft.Orleans.Persistence.AzureStorage`; add `Azure.Data.Tables` `12.11.0`,
`Azure.Storage.Blobs` `12.29.1`, and `Azure.Identity` `1.21.0`. Add
`Testcontainers.Azurite` `4.13.0` to the test project only. Do not add any Azure or Orleans
dependency to Contracts, Client Runtime, CLI, or Presentation. Extend the existing source-level
boundary test to prove that only ConversationHost and its test project may name Orleans/Azure
packages, and only Host may reference its persistence or grain namespaces.

`ConversationStorageOptions` has exactly these required settings:

    ConnectionString                 # Kind/Azurite only
    TableEndpoint                    # production managed-identity path
    BlobEndpoint                     # production managed-identity path
    EventTableName = forgeconversationevents
    ArtifactContainerName = forgeconversationartifacts
    OrleansClusterId = forge-conversation-dev
    OrleansServiceId = forge-conversation

The composition root selects exactly one credential path: a non-empty `ConnectionString`, otherwise
both endpoints with `DefaultAzureCredential`. Fail startup if the selected path is incomplete;
never silently fall back from endpoints to a connection string. Kind receives only its existing
Host-only development Storage secret; production uses the Host managed identity. Register one
`TableServiceClient`, one `BlobServiceClient`, `IConversationEventStore`, and
`IConversationArtifactStore` as singletons.

Call `UseOrleans` from `Program.cs`, configure Azure Storage clustering, and configure these named
Table grain stores against the same selected credential/client:

    conversation-checkpoint  -> ConversationCheckpoint
    mission-run-checkpoint   -> MissionRunCheckpoint

Their physical Azure Table names are `OrleansConversationCheckpoints` and
`OrleansMissionRunCheckpoints`, respectively. These alphanumeric names are intentionally distinct
from both the event table and the logical provider names, which Azure Table cannot use because they
contain hyphens.

Use `IPersistentState<T>` with `[PersistentState]`, rather than `Grain<TState>`. State loads before
`OnActivateAsync`; every changed transition explicitly awaits `WriteStateAsync`. The one-Silo Kind
configuration uses the fixed development cluster/service IDs above. No local membership, in-memory
grain state, generic default store, dashboard, client gateway, reminder, stream, or multi-silo
setting belongs in this task. Orleans creates its own clustering/checkpoint tables under the Host's
existing Table role; the Azurite fixture creates only the application event table/container.

#### Identity, Table/Blob layout, and persistence seams

The future authenticated adapter supplies a server-trusted internal value:

    ConversationAddress(string TenantId, Guid ConversationId)

It has no public HTTP/Contracts representation. Its canonical grain/Table partition key is
`v1|{TenantId}|{ConversationId:N}`; `TenantId` must be non-empty and cannot contain `|`. Task 6's
development adapter uses the fixed tenant `dev`; it never takes a tenant from a route or request.
The canonical address string is the `IConversationGrain` key. An address determines transcript
scope; a run ID never makes a separate transcript partition.

`IConversationEventStore` is the only application-level reader/writer of
`forgeconversationevents`:

    Task<ConversationEvent?> FindByEventIdAsync(ConversationAddress address, Guid eventId, CancellationToken ct)
    Task<ConversationEvent> AppendAsync(ConversationAddress address, ConversationEvent @event, CancellationToken ct)
    IAsyncEnumerable<ConversationEvent> ReadAfterAsync(ConversationAddress address, long sequence, CancellationToken ct)
    Task<ConversationEvent?> ReadLatestForRunAsync(ConversationAddress address, Guid runId, CancellationToken ct)

`AppendAsync` rejects a wrong conversation ID, `Version != 1`, non-positive sequence, or an
oversized inline payload. It first resolves `eventId`; if found, it returns the stored event only
when every semantic field (including sequence) is equal, otherwise it throws a clear invariant
violation. A conflicting append that has no matching event ID is a sequence/row-key collision and
also fails loudly; it must never be treated as a duplicate. It does not generate IDs, sequences,
timestamps, or Table keys — those are grain-owned.

The event table uses one transaction and partition for each append:

| Row type | Row key | Stored fields | Purpose |
|---|---|---|---|
| Event | `0-{sequence:D19}` | `EventJson`, `RunId`, `OccurredAtUtc` | Canonical ordered transcript. |
| Idempotency | `1-{eventId:N}` | `Sequence`, `EventJson` | Event-ID lookup/equality check. |

Both rows use the `ConversationAddress` partition key. `ReadAfterAsync` queries only the `0-` key
range in ascending order through `ConversationContractsJsonContext`, returning only
`Sequence > after`; idempotency rows are never transcript data. `ReadLatestForRunAsync` enumerates
the same ordered conversation range and retains the last matching `RunStatus` for the requested
run; the initial proof deliberately trades this rare activation repair for no second run index. No
initial retention deletes either row type. This provides durable event/command ID idempotency
without putting an unbounded history in grain state.

The Table entity maximum is 1 MiB, but a string property is much smaller. Fix the conservative
inline limit at **48 KiB of UTF-8 `EventJson`**. Large content is stored through:

    Task<ConversationArtifactReference> PutAsync(
      ConversationAddress address, Guid? runId, Guid artifactId,
      string contentType, string? fileName, Stream content, CancellationToken ct)
    Task<Stream> OpenReadAsync(ConversationAddress address,
      Guid? runId, ConversationArtifactReference artifact, CancellationToken ct)

`AzureBlobConversationArtifactStore` writes create-only to
`{escaped-tenant}/{conversationId:N}/{run-or-conversation}/{artifactId:N}` in private container
`forgeconversationartifacts`. `artifactId` is supplied by the grain/caller and remains stable on
retry; an existing blob is accepted as that artifact and never overwritten. The caller writes the
Blob before appending its `Artifact` reference event. The store derives the complete Blob path from
the address/reference only; it never accepts a Blob path or SAS URI from an event/request. No Blob
delete or retention policy is introduced.

#### Grain ownership and recovery protocol

The only grain identities are `ConversationGrain` per `ConversationAddress` and `MissionRunGrain`
per `{TenantId}|{RunId:N}`. Proposer, Approver, Implementer, a tool request, and a transcript event
are values in state — never grains. Their interfaces are internal to ConversationHost; later API
adapters/queue consumers use `IGrainFactory`, not direct storage or another service.

**Orleans serializer boundary.** Contracts intentionally remain free of Orleans attributes and
packages, and `JsonElement` must not rely on an unverified fallback serializer. Consequently no
Contracts record is a grain-interface parameter/return type or a field in Orleans persistent state.
Host-local grain DTOs are `[GenerateSerializer]` types with explicit `[Id]` fields and carry only
primitives/enums plus source-generated JSON strings:

    ConversationCommandInput(string CommandJson)
    ConversationProgressInput(string ProgressJson)
    ConversationSnapshotResult(string SnapshotJson)
    ConversationEventBatch(string[] EventJson)
    MissionRunEventInput(Guid EventId, Guid RunId,
                         ConversationEventKind Kind, ConversationRunStatus? RunStatus)
    MissionRunInterruption(Guid RunId, Guid EventId, DateTimeOffset OccurredAtUtc)

The caller serializes Contracts values with `ConversationContractsJsonContext`; the grain
deserializes at its boundary, validates it, and converts its response back before returning. The
pending record persists `PlannedEventJson` and optional `AcceptedCommandJson`, rather than an
object graph containing `JsonElement`. Add `[GenerateSerializer]`/`[Id]` to every Host-local grain
state, enum-bearing input/result, and pending-transition type. Do not add a serializer package,
codec, reflection fallback, or any Orleans annotation to Contracts.

`ConversationCheckpoint` contains exactly:

    string TenantId; Guid ConversationId; string MissionRef; Guid? ActiveRunId;
    long LastSequence; ConversationRunStatus Status; Guid? ExpectedToolRequestId;
    PendingConversationTransition? PendingTransition; DateTimeOffset UpdatedAtUtc

`PendingConversationTransition` holds the source-generated JSON for one fully planned
`ConversationEvent`, plus optional source-generated JSON for its accepted `ConversationCommand`
and `DispatchState` (`NotDispatched` only in this task). It is a recovery record, not history. The
accepted command's deterministic `UserMessage` uses `CommandId` as its `EventId`; duplicate command
acceptance therefore resolves through the durable event-ID row. Before Task 5 exists, acceptance
stops after the event/checkpoint is durable — it never enqueues.

`IConversationGrain` exposes only these Task-4 operations; result/acceptance records are internal
Host DTOs, not public wire contracts:

    Task<ConversationCommandAcceptance> AcceptCommandAsync(ConversationCommandInput command)
    Task<ConversationProgressAcceptance> RecordProgressAsync(ConversationProgressInput progress)
    Task RecordRunInterruptionAsync(MissionRunInterruption interruption)
    Task<ConversationSnapshotResult> GetSnapshotAsync()
    Task<ConversationEventBatch> ReadAfterAsync(long sequence)

`AcceptCommandAsync` validates conversation identity, pins `MissionRef` on first acceptance,
rejects a different mission or an active run, and creates a new run after a terminal prior run. It
plans a deterministic `UserMessage` event followed by a deterministic `RunStatus(Queued)` event.
Each is one independent pending transition; the returned acceptance names the second event's
sequence. `RecordProgressAsync` validates
conversation/run identity, converts Worker progress into the closed event contract, and never
accepts a Worker sequence. Its acceptance distinguishes an already-recorded equal event from a new
one, and carries a rejected result for an invalid tool result. Both use this fixed protocol:

1. Repair any prior pending transition before accepting new work.
2. Allocate `LastSequence + 1`, fully plan the event with stable ID/timestamp, and persist it in
   `PendingTransition`.
3. Await idempotent `AppendAsync`, which appends both rows or returns the equal prior event.
4. Advance `LastSequence`, clear `PendingTransition`, update snapshot fields, and persist state.

There is deliberately no claimed atomic transaction between Orleans state and the application event
table. A crash after step 2 or 3 is repaired by retrying the same planned ID/sequence; a new request
never overtakes it. `OnActivateAsync` repairs its own pending transition first. If it finds a Table
append after an older checkpoint, it advances from the returned event. A Table sequence beyond the
checkpoint without the matching planned event fails activation loudly as corruption — it must not
guess a missing transition.

On accepting `ToolRequested`, set `ExpectedToolRequestId` and `WaitingForTool`; only its matching
tool result clears it. Unknown, mismatched, or already-completed tool results return a rejected
internal result and do not append, advance, or enqueue. Task 5 later derives safe continuation from
this checkpoint; Task 4 only preserves it.

`MissionRunCheckpoint` contains only `TenantId`, `RunId`, `ConversationId`, `Status`,
`ExecutionBoundary` (`NotStarted`, `ExecutingProvider`, `WaitingForTool`, `Terminal`), stable
`InterruptionEventId`/`InterruptionOccurredAtUtc` (both null until needed), and `UpdatedAtUtc`.
`IMissionRunGrain` exposes `ApplyDurableEventAsync(MissionRunEventInput)` and
`Task<ConversationRunStatus> GetStatusAsync()`; it never returns its complete checkpoint. The
following fixed mapping prevents a completed safe boundary from looking like an uncertain provider
call after restart:

| Durable event | Status / execution boundary |
|---|---|
| `RunStatus(Queued)` or `RunStatus(Running)` | stated status / `NotStarted` — dispatch is not a provider call. |
| `ParticipantStarted` | `Running` / `ExecutingProvider`. |
| `ParticipantMessage`, `Approval`, `Artifact`, `ToolResult`, `Error` | preserve non-terminal status / `NotStarted` — the reported fact is durable before a later call may begin. |
| `ToolRequested` or `RunStatus(WaitingForTool)` | `WaitingForTool` / `WaitingForTool`. |
| terminal `RunStatus` (`Completed`, `Rejected`, `Interrupted`, `Failed`) | stated status / `Terminal`. |
| `UserMessage` | no run-state change; `AcceptCommandAsync` already initialized the run. |

ConversationGrain invokes `ApplyDurableEventAsync` only after the corresponding event appended,
except for its dedicated interruption-report operation below.

On activation, `ExecutingProvider` first checks `ReadLatestForRunAsync`: a terminal transcript fact
wins and is adopted. Otherwise MissionRunGrain generates/stores its stable interruption ID/time,
sets its own state to `Interrupted`/`Terminal`, and persists that state **before** calling
`ConversationGrain.RecordRunInterruptionAsync`. The latter appends the matching deterministic
`RunStatus(Interrupted)` through the normal pending-transition protocol but deliberately does not
call MissionRunGrain back; its run checkpoint is already durable. Re-activation retries the same
stored interruption ID until the fact is present. This avoids a grain-call cycle during activation.
It never invokes a provider or re-sends work. `WaitingForTool` remains waiting. Activation may only
use Host-local storage and the paired conversation/run grain — never a Worker, queue, provider,
local capability, or another bounded context.

This preserves the Type-1 ownership boundary: ConversationHost alone has Table/Blob access and
allocates transcript order. The future Worker has neither Storage access nor an Orleans client or
gateway path.

#### Architecture-security and engineering gates

| Gate question | Locked Task-4 answer |
|---|---|
| Bounded context and owner | Durable conversations are a new bounded context. Internal Tier-2 ConversationHost owns its event Table, Blob artifacts, and Orleans checkpoints. |
| Public entry point | None is introduced here. The already-locked future Tier-1 ForgeUI (OIDC) / ForgeAPI (platform key) adapters route authenticated context internally; neither receives Table/Blob credentials. |
| Tier-2/3 contracts | ConversationHost talks directly only to its Tier-3 Storage and its in-process Silo. Task 5's Worker reports through Service Bus and never receives Storage/Orleans access. |
| Credentials and enforcement | Kind uses the existing Host-only dev connection string; deployed Host uses its user-assigned managed identity. Azure 350 RBAC grants Table/Blob only to that Host identity. |
| Type classification | Context ownership, edge separation, and Worker exclusion are Type 1. One-Silo Kind, Table row layout, 48 KiB threshold, and Azurite test transport are Type 2; all stay behind the two store interfaces and can change without changing the event contract. |

There is one owner per external dependency: the event store owns Table access, the artifact store
owns Blob access, and grains own transition ordering/recovery. There are no retry or storage-mode
knobs: a malformed configuration or a non-repairable state discrepancy fails loudly, while the one
specified pending-transition path is structurally idempotent. Fresh-Host replay and Blob
dereference are the required observations, so the design does not confuse a written checkpoint with
a durable conversation.

#### Tests and verification

`AzuriteFixture` starts one throwaway Azurite container with `Testcontainers.Azurite`, exposes its
connection string to a fresh in-process Host/Silo, and creates the event table/container. Each test
uses unique tenant/conversation/run IDs and starts a second Host/Silo against the same endpoint to
prove reactivation/replay rather than reading memory. Docker absence fails explicitly as an
integration-environment error; these required tests never silently skip.

Add focused tests proving:

1. A command creates checkpoint and ordered deterministic events; a fresh Host reactivates it and
   `ReadAfterAsync(0)` returns the same sequences/event IDs in order.
2. Repeating a command ID returns its prior acceptance without a new event/sequence; repeating a
   Worker event ID does the same, while the same ID with a changed payload fails loudly.
3. A crash-shaped persisted `PendingTransition` is repaired once on activation; its successor has
   no sequence gap or duplicate.
4. `ExecutingProvider` reactivates as one durable `Interrupted` status with no provider/queue call;
   `WaitingForTool` remains waiting.
5. Content over 48 KiB uploads before its `Artifact` reference is appended, and `OpenReadAsync`
   returns the original bytes using only address/reference data.
6. A tool-shaped command/progress payload containing `JsonElement` crosses an actual grain call and
   survives checkpoint reactivation through its Host-local JSON DTO, with no Orleans serializer
   fallback or Contracts annotation.
7. Boundary tests prove Contracts, Client Runtime, CLI, and Presentation neither name nor reference
   Host/Orleans/Azure; no Worker project exists or changes in this task.

Run `dotnet build src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx`. The named
completion observation is fresh-Host Azurite transcript replay plus Blob dereference, not an
in-memory grain test.

**Done when:** the named Table/Blob stores and two named Orleans checkpoint stores are configured
only in ConversationHost; Azurite integration tests create/re-activate grains, replay an ordered
transcript from a fresh Host, reject duplicate/mismatched event and command IDs, repair one pending
transition, surface a stale executing run as `Interrupted`, and dereference a Blob-backed artifact;
boundary tests, solution build, and complete suite pass. No Task-5 queue/Worker, Task-6 endpoint,
Task-7 Client Runtime/Presentation, mission, or forge-infra change is included.

### 5. Service Bus delivery and mission worker

**Task 5 — done and verified 2026-08-14** (commit `1da4dc7`). See [Service Bus delivery and Janus Worker](phase-43.16-task-5-service-bus-worker.md): full build has 0 warnings/errors and full suite has 615 passed, 11 known provider-dependent skips, 0 failed. Real Service Bus/Kind proof remains Task 8.

### 6. Conversation API and resumable SSE

**Task 6 — done and verified 2026-08-14.** See [Conversation API and resumable SSE](phase-43.16-task-6-conversation-api-sse.md)
and [its completed evidence](phase-43.16-task-6-conversation-api-sse_completed.md). It maps the
five additive Forge-native routes, preserves pinned capability declarations for follow-up runs, and
closes the Table-replay/live-notifier race without making a healthy SSE connection a correctness
condition. The local adapter uses the fixed `dev` tenant only; a future Tier-1 ForgeAPI/ForgeUI
adapter supplies authenticated identity.

**Done when:** the Task 6 document's named HTTP/SSE tests pass (including disconnect/reconnect
replay), `/v1/*` contract tests remain unchanged, and the full solution build/test passes.

### 7. Client Runtime and group-chat rendering

**Task 7 — done and verified 2026-08-14** (commit `18b9eba`). See
[Client Runtime and group-chat rendering](phase-43.16-task-7-client-runtime-group-chat.md) and
[its completed evidence](phase-43.16-task-7-client-runtime-group-chat_completed.md). Client
Runtime owns the durable Janus session end to end (start/follow-up, tail reconnect, expected-tool
hand-off with a stable result command ID); `ConversationSessionSlot` serializes prompt admission,
lazy session creation, and replacement disposal as one operation, closing a real race found during
review where a prompt could otherwise start an orphaned durable session after a mission switch.
Presentation projects the event stream through a pure `ConversationTranscript` model.

**Done when:** [Task 7's named condition](phase-43.16-task-7-client-runtime-group-chat.md#done-when)
is verified — met, per the completed evidence above.

### 8. Product proof and evidence

**Prerequisite — [Task 8a: Kind runtime build-out](phase-43.16-task-8a-kind-runtime-buildout.md)
— done and verified 2026-08-14.** Built the real ConversationHost/Worker container images and
rolled them out into Kind with immutable, commit-SHA-derived provenance; discovered as a blocking
gap during Task 8 planning (the Kind manifests were still `image: TBD` placeholders). Kept
explicitly separate from the evidence-only run below. Both real Deployments are live in
`forge-durable` now.

**Status: active and unverified (2026-08-14).** The first live-proof attempt validly proved
observations #1–#3 below (real, unscripted revision-then-approval cycle) against real OpenAI/
Anthropic providers, then hit a genuine product defect that failed the run before observations
#4–#6 could be attempted: the Implementer's provider call emitted two tool calls in one turn and
tripped `JanusPipelineProgressMapper`'s deliberate "exactly one tool call per request" guard. See
[Task 8b: Janus one-tool-per-turn contract](phase-43.16-task-8b-janus-one-tool-per-turn.md) for the
full finding (conversation ID, event sequence, root cause) and the corrective fix — a second
prerequisite, **done and verified 2026-08-14**: code merged
([#43](https://github.com/katasec/mission-control-language/pull/43)) and deployed into
`forge-durable` (`make 350-conversation-kind-up` against merged main
`01047eab2a086587743a04163041802f295878b4`, both Deployments rolled out).

The rerun that followed Task 8b's fix reproduced observations #1-#4 for real (including sequential
tool hand-offs completing a genuine multi-file plan) and then surfaced a second, distinct blocker
during observation #6: a raw, non-JSON `kind-verifier-*` probe message — orphaned on the shared
`conversation-progress` queue by an earlier, self-healing-looking verifier retry — got picked up by
the live Host and, with `MaxConcurrentSessions=1`, starved conversation
`173da2e0-248e-5637-ac1b-4c8fea4ad05a`'s own healthy session indefinitely. See
[Task 8c: poison-progress containment](phase-43.16-task-8c-poison-progress-containment.md) for the
full diagnosis and corrective fix — a **third prerequisite, implementation in progress**, not yet
merged.

**Task 8's live proof itself remains not reauthorized** — it requires an explicit separate go-ahead and a full rerun
using the original "Implement a rate limiter." goal before this section can be marked done.

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
