# Phase 43.16 Task 7 — Client Runtime and group-chat rendering

> **Status: Design complete; ready for Claude implementation (2026-08-14).** This task makes Janus selectable in Desktop, connects the existing local Client Runtime to the Task 6 message-first Conversation API, and projects the durable event stream as one group-chat transcript. It does not change ConversationHost, ConversationWorker, Service Bus, the Janus mission, `/v1/*`, or forge-infra.

## Outcome and scope

Selecting **Janus** creates a durable conversation through Client Runtime; it is not a different HTTP client in Presentation. The Client Runtime owns all calls to the Conversation API, retains the returned `ConversationId`, tails the durable stream, executes an expected Implementer tool request through the existing local capability authorization path, and posts its result. It relays complete typed `ConversationEvent` records through the existing loopback Client Runtime event stream. Presentation renders those records through a single projection whether they arrived from the initial durable replay or the live tail.

`ChatGPT` and `Websearch` retain their current `MissionRuntimeSession` / `CloudMissionRuntimeSession` paths and their current turn renderer. Janus is the only durable-conversation choice in this task. No provider/model picker, human approval control, conversation browser, artifact download, token streaming, conversation deletion, or general session persistence is introduced here.

## Locked ownership and path

```text
Presentation (WASM, no Host reference / raw HTTP)
  -> IClientRuntimeChannel (local HTTP + SSE adapter)
  -> Client Runtime (workspace, capability authorization, durable-session owner)
  -> ConversationHost HTTP/SSE adapter (fixed local-dev identity)
  -> ConversationGrain / Table / Blob / Service Bus
  -> ConversationWorker
```

`ConversationHostClient` is the only Client Runtime class that knows the Task 6 HTTP/SSE projection. It sends and receives the existing `ForgeMission.Conversations.Contracts` records using `ConversationContractsJsonContext`. `ConversationRuntimeSession` owns the client-side session state and tool hand-off policy; `ConversationTranscript` owns presentation projection. Neither reaches through the other’s boundary. No new public conversation “god command”, Host-local DTO, Orleans type, storage type, or provider SDK type is introduced.

## Client Runtime contract changes

Add the Contracts project reference to both `ForgeMission.ClientRuntime` and `ForgeMission.ClientRuntime.Transport`. The latter exposes the typed payload to Presentation; neither project may reference ConversationHost. Update the existing local transport records additively:

```csharp
enum SessionRuntimeKind { Mission, DurableConversation }

SessionSetupRequest(
    string WorkspaceRoot,
    string? Mission = null,
    SessionRuntimeKind Runtime = SessionRuntimeKind.Mission,
    string? ReplacesSessionId = null)

SessionSetupResponse(
    string SessionId,
    IReadOnlyList<string> AvailableCapabilities)

PromptResponse(
    string Content,
    bool IsError = false,
    Guid? ConversationId = null)

ClientRuntimeEventKind: existing values + ConversationEvent
ClientRuntimeEvent: existing fields + ConversationEvent? Conversation
```

The existing defaults keep all non-Janus callers source/behaviour compatible. `ConversationId` is returned by the durable prompt path and retained by the owning `ConversationRuntimeSession`; it is never fabricated by Presentation. The session setup response does not pretend that an empty new Janus session already has a conversation.

`AttachableMission` gains the runtime kind. The picker supplies `Mission` for ChatGPT/Websearch and `DurableConversation` plus wire mission `Janus` for Janus. On a durable prompt, the endpoint gets (or creates) the one `ConversationRuntimeSession` owned by that `ClientRuntimeSession`; normal mission paths remain their current direct construction. A session’s selected mission and runtime never change after setup. When replacing an existing picker session, `Home` sends its current ID as `ReplacesSessionId`; the store removes and disposes that old session before returning the new one. This cancels a durable tail before a user can switch missions, so an abandoned Janus session cannot later execute a local tool.

`ClientRuntimeJsonContext` source-generates the changed request/response records and `SessionRuntimeKind`. Add a dedicated source-generated `ConversationRelayJsonContext` for the local `ClientRuntimeEvent` envelope plus its `ConversationEvent` payload, with the string-enum behaviour required by the Contracts wire. This keeps the existing `CapabilityOperation` wire representation unchanged while avoiding runtime `JsonSerializerOptions`/resolver chaining and reflection fallback. `ConversationContractsJsonContext` remains the source-generated metadata used by `ConversationHostClient` for every direct durable-conversation message/payload.

## ConversationHost adapter and durable-session behaviour

Create these focused Client Runtime classes under `Services/`:

| Class | Sole responsibility |
|---|---|
| `ConversationHostClient` | Task 6 HTTP/SSE projection: `StartConversationRequest`, `SubmitConversationCommandRequest`, `SubmitToolResultRequest`, and the `ConversationEvent` SSE reader. It owns route formatting, source-generated HTTP JSON, response-status failure, and complete SSE frame parsing. |
| `ConversationRuntimeSession` | One selected Janus conversation: start/follow-up choice, returned ID, highest delivered sequence, event-ID dedupe, fixed reconnect loop, and local tool hand-off. |

Register a named `conversation-host` `HttpClient`. `ConversationRuntime:BaseUrl` is required only when the durable Janus path is selected; a normal mission session must not require it. Desktop passes a configured non-empty value to its Client Runtime child as `ConversationRuntime__BaseUrl`, alongside the existing Mission Runtime settings. The Task 6 local adapter has a fixed `dev` tenant and no credential header, so this task passes no provider, storage, or Service Bus credential to Client Runtime.

For the first prompt, the session posts:

```csharp
new StartConversationRequest(
    Guid.NewGuid(), "Janus", prompt, ToCapabilityDeclarations(capabilities))
```

For subsequent prompts it posts `SubmitConversationCommandRequest` against the retained ID. The capability declarations are converted from the already-authoritative `CapabilityRegistry.ToolDeclarations` (`AIFunction.Name`, `Description`, and `JsonSchema`), so the Worker receives exactly the tools that this local workspace can dispatch. No workspace root or desktop path crosses the boundary.

After a successful start or follow-up acceptance, the session runs one background SSE tail. It opens `GET /conversations/{id}/events?after={lastSequence}`; each full `event: conversation-event` JSON record is source-generated into `ConversationEvent`, then published as one local `ClientRuntimeEvent`. It records the event ID and advances the high-water sequence before reconnecting. A duplicate event ID is ignored; a previously unseen event at or below the high-water sequence is a protocol error, surfaced as the existing local `Error` event rather than silently reordered. A normal SSE completion or transient HTTP failure retries with the same `lastSequence` after one fixed 250 ms delay. There is no retry-count/configuration knob. `ConversationRuntimeSession` is `IAsyncDisposable`: session replacement and Client Runtime shutdown both cancel and await its tail before allowing the session to disappear.

The Host’s Table transcript is the source of truth. Client Runtime’s in-memory sequence/event-ID set is only a live-tail cursor; it is not a transcript cache or second sequence allocator. A Client Runtime restart therefore restarts from the Host’s durable events, while a normal dropped Host SSE connection replays from the last delivered sequence.

### Expected tool hand-off

On a newly observed `ToolRequested` event, `ConversationRuntimeSession` must first verify all of:

1. `Participant == Implementer`;
2. `ToolRequest` is present and has a non-empty request ID/name; and
3. `ToolExecutorRegistry.CanExecute(name)` confirms the name is locally known.

Add that small `CanExecute` query to `ToolExecutorRegistry`; it exposes only its already-owned name set and adds no new execution path. The session converts the request’s `JsonElement` arguments with the same closed value mapping used by the existing mission sessions, constructs `FunctionCallContent`, and calls `ToolExecutorRegistry.ExecuteAsync` with the session’s existing `ICapabilityDispatcher`. That is the only execution path: local capability authorization, confirmation, workspace confinement, and audit stay intact. Unsupported/invalid requests become an error `ConversationToolResult`, never a fallback shell/filesystem operation.

The session posts exactly one logical result through `SubmitToolResultRequest`. Its `CommandId` is the new public deterministic helper `ConversationDeterministicIds.ClientToolResult(Guid toolRequestId)`, using the locked name `client-tool-result:{toolRequestId:N}`. This is a narrow Contracts addition with a known-vector/stability test; it allows an HTTP retry or Client Runtime reconnect to receive Task 6’s original acceptance rather than append another durable result/continuation. Within one Client Runtime process, cache the completed `ToolExecutionResult` by request ID until its matching durable `ToolResult` appears, so a replay retries the post but never re-executes the same local request.

Local tool execution itself cannot be made exactly-once across a Client Runtime process crash: the local machine has no transaction that atomically performs a filesystem/shell side effect and writes a durable Host result. This task does not hide that limitation. The durable boundary is idempotent and the in-process replay guard prevents ordinary SSE duplicates; a restart while a tool is executing is an explicitly deferred local-execution recovery problem, distinct from the Host/Worker provider interruption rule.

## Presentation projection and rendering

Create a small, pure `ConversationTranscript` projection/model in `ForgeMission.ClientRuntime.Presentation`, plus a focused Razor renderer component if that makes the markup smaller than `Home.razor`. It takes `ConversationEvent` values, deduplicates strictly by `EventId`, tracks the highest sequence, and produces one ordered presentation model. `Home` applies every `ClientRuntimeEventKind.ConversationEvent` to that one projection. It does not parse SSE, retain its own second event-ID policy, call `HttpClient`, or name ConversationHost.

The projection/rendering rules are fixed:

| Durable event | Rendering |
|---|---|
| `UserMessage` | Existing user-style bubble. |
| `ParticipantStarted` | Transient “{participant} is thinking…” indicator keyed by participant + attempt; cleared by that participant’s completed message/error. |
| `ParticipantMessage` | Named Proposer/Approver/Implementer bubble; contiguous same participant + attempt entries are one visual attempt group. |
| `Approval(Approved)` | Explicit approved state. |
| `Approval(RevisionRequested)` | Explicit revision-requested state and feedback. |
| `RunStatus(Rejected)`, or `RunStatus(Failed)` after the current run’s most recent approval is `RevisionRequested` | “Not approved” with that final feedback, matching Phase 43.15’s presentation-only decision. Other failed/interrupted statuses remain their own visible operational state. |
| `ToolRequested` / matching `ToolResult` | One Implementer tool row keyed by `ToolRequestId`; it changes from requested/running to completed/error when the result arrives. No extra transient Client Runtime tool row is emitted. |
| `Error`, other `RunStatus`, `Artifact` | Individually visible compact status/error/artifact rows; artifact stays a reference, never a direct Blob call. |

The normal `ChatTurn` renderer remains intact and is selected only for `SessionRuntimeKind.Mission`. For Janus, a prompt is rendered only after its durable `UserMessage` arrives; `PromptResponse` is an acceptance, not a synthetic chat answer. Presentation’s local SSE connection can reconnect as it does today; the durable session’s Host tail provides replay from its cursor, and the projection makes a live/replayed duplicate harmless.

## Security architecture gate

| Gate question | Locked answer |
|---|---|
| Bounded context and data owner | ConversationHost (Tier 2) alone owns the conversation Table/Blob context; Client Runtime owns only local workspace authority. |
| Public entry point | None is added. Presentation reaches its loopback Client Runtime only; Task 6’s Host adapter is the local proof endpoint. A future Tier-1 ForgeAPI/ForgeUI adapter owns authenticated public ingress. |
| Tier-2 contracts | Client Runtime uses the named Contracts messages through `ConversationHostClient`; Host/Worker continue using their existing Service Bus contract. |
| Tier-3 access | Client Runtime and Presentation receive no Table, Blob, Orleans, or Service Bus credential/RBAC. Host remains the only Table/Blob owner; Worker remains queue-only. |
| Secrets | Client Runtime receives only the configured local Host base URL; provider keys stay with Worker, and data-plane credentials stay with Host/Worker as already designed. |
| Type and reversal | The local direct Client Runtime → Host adapter is a Type-2 local-proof exception, scoped to Task 6’s fixed `dev` identity and Kind port-forward. It is removed/replaced when the later Tier-1 adapter authenticates and routes the same Contracts messages; no Contracts or projection rewrite is required. |
| Enforcement/proof | Project-reference boundary test proves Presentation/Client Runtime do not reference Host/Orleans/Azure; runtime tests prove tool execution goes through `ICapabilityDispatcher`; Task 8 supplies the Kind port-forward proof. |

## Engineering-philosophy gate

`ConversationHostClient` is the sole external-host seam; `ConversationRuntimeSession` owns consequential session/retry/tool behaviour; and `ConversationTranscript` is a pure projection. The only new configuration value is the present operational requirement, `ConversationRuntime:BaseUrl`; reconnection uses a fixed delay, not a speculative policy surface. The durable Host event and stable result-command ID structurally contain replay risk instead of a UI convention. The named verification is a scripted Host SSE drop/reconnect plus a rendered rehydrated tool result with no duplicate row.

## Files and verification

Expected implementation changes are limited to:

- `src/ForgeMission.Conversations.Contracts/ConversationDeterministicIds.cs` and its tests — deterministic client tool-result command ID.
- `src/ForgeMission.Core/Tools/ToolExecutorRegistry.cs` — the narrow `CanExecute` query used to reject unknown durable tool names before dispatch.
- `src/ForgeMission.ClientRuntime.Transport/*` — additive runtime/session/event records, route, the two source-generated JSON contexts, and Contracts reference.
- `src/ForgeMission.ClientRuntime/Program.cs`, `Transport/ClientRuntimeEndpoints.cs`, `Transport/ClientRuntimeSessionStore.cs`, and new focused services — durable-session selection/replacement, adapter, tail, and tool hand-off.
- `src/ForgeMission.Desktop/Program.cs` — pass only `ConversationRuntime__BaseUrl` when configured.
- `src/ForgeMission.ClientRuntime.Presentation/AttachableMissions.cs`, `Pages/Home.razor`, and a focused projection/renderer — Janus picker and durable renderer.
- Client Runtime, Contracts, Presentation/UI, and architecture boundary tests. Add a component-test dependency only if needed to assert rendered markup; do not test markup through raw HTTP.

Required tests:

1. `ConversationRuntimeSession` sends the start/follow-up Contracts messages with capability declarations, retains the returned ID, parses a real-shaped multi-frame SSE stream, and relays typed events.
2. A completed Host stream reconnects with the last delivered sequence; duplicate replay neither republishes the event nor executes its tool again; replacing the session cancels its tail before a subsequent durable request can be dispatched.
3. An Implementer `ToolRequested` reaches the existing registry/dispatcher, posts the stable `ClientToolResult` command ID, and uses an error result for malformed/unsupported tools.
4. The transcript component renders approved, revision-requested, not-approved, and a rehydrated completed tool result; applying its event twice does not create a second bubble/tool row.
5. Existing mission-session/transport tests prove ChatGPT/Websearch stay on their old path.
6. Boundary tests prove Presentation has no raw HTTP or ConversationHost reference and Client Runtime/Contracts have no ConversationHost/Orleans/Azure reference.

Run `dotnet build src/ForgeMission.slnx --no-restore` and `dotnet test src/ForgeMission.slnx --no-restore`. The Task 8 Kind proof remains a separate product gate; this task does not claim the Azure/Kind deployment is verified.

## Done when

Janus is selectable in Desktop; Client Runtime, not Presentation, starts/continues the typed durable conversation and retains its returned ID; complete ConversationHost events travel through the existing local SSE channel and reconnect from the last durable sequence; an expected Implementer request executes only through the existing capability path and posts an idempotent result; and one transcript renderer shows named bubbles, attempts, approval/revision/not-approved, typing, and an exactly-once rehydrated tool row. ChatGPT/Websearch and `/v1/*` remain unchanged. The named tests, architecture boundary checks, full solution build, and full suite pass.
