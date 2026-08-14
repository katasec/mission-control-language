# Phase 43.16 Task 6 — Conversation API and resumable SSE

> **Status: Implementation accepted; phase verification pending (2026-08-14).** The corrected
> Task 6 implementation satisfies its design and has a clean build, 120/120 ConversationHost tests,
> and three consecutive 9/9 real-Kestrel API/SSE runs. The full solution suite is not currently
> green because the unmodified live `ClaudeCode_MultiToolTask_ThroughForgeServe_AgenticMission`
> integration test reproducibly expects one tool call but observes two; this must be resolved or
> explicitly classified before Task 6 can be marked verified. Tasks 4 and 5 are accepted and
> verified through `951bf73` and `1da4dc7`; status was recorded at `aea36e6`. This task exposes
> their durable Conversation service through an additive Forge-native message contract, with
> HTTP/SSE as its first projection. It does not change the Worker, Service Bus delivery, Desktop UI,
> `/v1/*`, mission definitions, or forge-infra.

## Outcome and scope

`ForgeMission.ConversationHost` becomes the local development Conversation API: it hosts the
existing Orleans Silo and progress consumers, maps five durable-conversation routes plus a simple
health response, and configures the existing source-generated Contracts JSON context for minimal
API binding/serialization. The Worker remains a separately deployed command consumer/executor.

The authoritative public contract already lives in
[43.16 Task 2](phase-43.16-janus-desktop-local-poc.md#transport-neutral-requestresponse-messages)
and [Durable conversations](../design/durable-conversations.md#transport-neutral-conversation-messages).
Its named command/query messages and `ConversationEvent` stream are the API; the following are only
the first HTTP/SSE projection:

    POST /conversations
    POST /conversations/{conversationId}/commands
    POST /conversations/{conversationId}/tool-results
    GET  /conversations/{conversationId}
    GET  /conversations/{conversationId}/events?after={sequence}

No route is added under `/v1`. There is no token-delta event, pagination API, CORS policy,
authentication implementation, Room projection, or human approval control in this task.

## Message contract before transport

`StartConversationRequest`, `SubmitConversationCommandRequest`,
`SubmitToolResultRequest`, `GetConversationRequest`, `GetConversationResponse`, and
`ReadConversationEventsRequest` belong to `ForgeMission.Conversations.Contracts` and are the
transport-neutral public API. `ConversationId` is part of every existing-conversation command/query
message, even where the HTTP adapter also obtains it from a route. Thus a future direct service,
gRPC, or broker adapter can invoke the same operation without reconstructing meaning from a URL.

The three mutations intentionally remain distinct typed messages: start selects the initial mission
and capabilities; a follow-up submits user text against pinned conversation state; and a tool result
proves an outstanding authorized hand-off. They have distinct validation, authorization, and
idempotency rules. Do not replace them with a public `ConversationCommand { kind: ... }` envelope.
Each named message may instead grow additively when its own semantic operation evolves.

HTTP status codes, `Location`/`Retry-After`, fixed development identity, minimal-API binding, and
SSE's `event:`/`id:`/`data:` framing are adapter concerns. The endpoint maps between those concerns
and the Contracts messages; no Contracts type, grain method, persistence interface, or Worker
message may expose an HTTP type. SSE projects the `ReadConversationEventsRequest` result; the
durable `ConversationEvent` and its sequence—not a live HTTP response—are the reconnect contract.

### Required correction to the initial implementation

The initial Claude implementation correctly delivered durable replay, idempotency/recovery, and
the HTTP/SSE adapter against the earlier contract revision, but it is **not accepted** yet. Before
resubmitting it must make these bounded changes and add/adjust the named tests:

1. Add `ConversationId` to `SubmitConversationCommandRequest` and
   `SubmitToolResultRequest`; add `GetConversationRequest`, `GetConversationResponse`, and
   `ReadConversationEventsRequest` to Contracts and `ConversationContractsJsonContext`. The
   adapter must construct/bind these messages, reject a route/body conversation-ID disagreement as
   `400`, and return `GetConversationResponse`, not a bare snapshot. The grain-local Orleans DTOs
   stay Host-only.
2. For accepted follow-up and tool-result HTTP projections, set the documented
   `Location: /conversations/{conversationId}` and `Retry-After: 1` headers. Test both headers;
   `Results.Accepted(null, ...)` alone does not meet this contract.
3. Implement the existing bounded-input decision: source-generate and measure the start
   `ConversationCommand` UTF-8 JSON before durable work (maximum 32 KiB), and the derived
   tool-result `ConversationProgress` JSON before calling `RecordProgressAsync` (the existing
   48 KiB inline-event threshold). Add `Invalid` to `ConversationCommandOutcome`, return its
   reason without state advance, map it to `400`, and test both limits. Table limits remain the
   integrity backstop, not public validation.
4. Extend source-generation and real-Kestrel tests to prove the same Contracts message can be
   used without HTTP meaning, while HTTP only maps path/query/header concerns at its edge.

5. A transport-neutral query handler returns the public `GetConversationResponse` (or its typed
   missing/invalid outcome), never a bare `ConversationSnapshot` that HTTP happens to wrap. Likewise
   `ReadConversationEventsRequest` validates its own `ConversationId`/`After` and yields its
   ordered `ConversationEvent` sequence to the adapter; it must not collapse into an HTTP-only
   boolean existence helper. `ConversationSseWriter` remains the HTTP adapter's specialised
   replay/subscribe/catch-up/drain implementation and may re-read the durable store after the
   message handler's bounded read, because that second read is what closes the live-subscription
   race. This costs one extra Table read per SSE connection, not per event, and does not change the
   public contract. Its reversal path is to pass the handler's first batch to the writer while
   preserving the fixed subscribe-before-catch-up order; do that only if measurement makes this
   connection-open cost material. Add direct-handler tests for both query responses as well as the
   existing mutation-message test.

These are corrections to the already locked design, not new scope. Re-run the full solution suite
afterward and provide the exact result in the next completion summary.

### Review evidence (2026-08-14)

Codex accepted the corrected implementation after direct diff review: public messages carry their
own existing-conversation identity; named message handlers have no HTTP types; HTTP/SSE maps them
without becoming their semantic definition; accepted mutations set the documented headers; and
source-generated bounded validation returns typed `Invalid` before state advance. The source tree
has no Task 6 edits under `ForgeMission.Api` or `ForgeMission.Tests`.

`dotnet build src/ForgeMission.slnx --no-restore` succeeded with 0 warnings and 0 errors.
`ForgeMission.ConversationHost.Tests` passed 120/120, and the nine `ConversationApiTests` passed
three consecutive independent runs. A full `dotnet test src/ForgeMission.slnx --no-restore` did
not pass: the existing live `ClaudeCode_MultiToolTask_ThroughForgeServe_AgenticMission` test failed
twice (including a focused rerun) at `ClaudeCodeTests.cs:153`, asserting one tool call but observing
two. It is outside Task 6's diff, but it is now a documented verification gate rather than hidden
behind a Task 6 completion claim.

## Locked API behaviour

The local adapter derives `ConversationAddress("dev", conversationId)` itself. It never accepts a
tenant or user field, workspace root, provider credential, or arbitrary local filesystem path from
HTTP. A later Tier-1 ForgeAPI/ForgeUI adapter will authenticate and authorize, then pass its
server-trusted tenant/user context to the internal Conversation service; it must not gain
Table/Blob permission.

| Route | Adapter action and result |
|---|---|
| `POST /conversations` | Require non-empty `commandId`, `missionRef`, and `goal`, a non-null capability array, and `missionRef == "Janus"` for this Janus-only proof. Derive both conversation and initial-run IDs from the client command ID, call the grain's start acceptance, and return `201 Created`, `Location: /conversations/{conversationId}`, and `StartConversationResponse`. An exact retry therefore reaches the same grain and returns the original acceptance; unequal reuse reaches its typed conflict result. |
| `POST /conversations/{id}/commands` | Bind a valid non-empty GUID route ID to `SubmitConversationCommandRequest.ConversationId`; require non-empty `commandId` and `text`. The grain—not the adapter—uses its pinned mission and capabilities to create a new run. Return `202 Accepted`, `Location: /conversations/{conversationId}`, `Retry-After: 1`, and `SubmitConversationCommandResponse`. |
| `POST /conversations/{id}/tool-results` | Bind the valid route ID to `SubmitToolResultRequest.ConversationId`; require valid request GUIDs and non-null `content`. The grain validates the outstanding request and turns the result into one durable `ToolResult` plus its deterministic continuation command. Return `202 Accepted`, `Location: /conversations/{conversationId}`, `Retry-After: 1`, and `SubmitToolResultResponse`. |
| `GET /conversations/{id}` | Bind the route ID to `GetConversationRequest` and project `GetConversationResponse`; an uninitialized/no-mission grain is `404`, not an empty snapshot. |
| `GET /conversations/{id}/events?after=` | Bind route/query values to `ReadConversationEventsRequest`, validate `after` as a non-negative `long` (default `0`), reject unknown conversations before starting the response, then produce the SSE projection below. |

Add `ConversationDeterministicIds.Conversation(Guid commandId)` using the fixed v5 name
`conversation:{commandId:N}`, and `InitialRun(Guid commandId)` using
`initial-run:{commandId:N}`. This is a narrow Contracts change (plus its source-generation tests),
not a new HTTP DTO or cross-project dependency. The server does not mint random identities for a
start request: without these deterministic address and run IDs, a client that lost its `201`
response could not use its stable `CommandId` to retry the same logical conversation.

Malformed request JSON, empty required strings/IDs, an invalid route GUID, a negative/non-numeric
`after`, or any mission other than `Janus` is `400 Bad Request`. A missing conversation is `404`.
An active run, an unknown/already-completed/mismatched tool request, or a reused command/event ID
whose semantic content differs is `409 Conflict`; none appends an event or dispatches a command.
The endpoint layer maps these known typed/rejection outcomes and narrowly classified invariant
exceptions to HTTP. It does not pass `HttpContext`, `IResult`, or HTTP exceptions into a grain.
Unexpected storage, Orleans, or Service Bus failures remain failures (the normal server error path)
rather than becoming an invented durable Error event.

An accepted exact duplicate returns the original acceptance response, even after the run reaches a
terminal status:

- A duplicate start/follow-up command has its original `UserMessage` sequence `n`, so its accepted
  response is deterministically `{ acceptedSequence: n + 1, status: queued }`: the paired queued
  fact is always the next transition.
- A duplicate tool result has its original `ToolResult` event sequence `n`, so its response is
  deterministically `{ acceptedSequence: n, status: waitingForTool }`. The Worker subsequently
  publishes the state-changing `RunStatus`; accepting the tool result itself does not pretend that
  happened synchronously.

This fixes the current start-command duplicate path, which incorrectly reads the *latest* run
status and can therefore return a later terminal status rather than the original acceptance.

## Grain changes — preserve ownership and pinned inputs

Only `ConversationGrain` allocates sequence numbers, appends events, and commands the Worker.
The HTTP adapter calls it through `IGrainFactory`; it never reads or writes `IConversationEventStore`
directly except through the SSE replay service described below.

Add the following Host-local `[GenerateSerializer]` input records in
`Grains/ConversationGrainResults.cs`, and add matching methods to `IConversationGrain`:

    ConversationFollowupCommandInput(Guid CommandId, string Text)
    ConversationToolResultInput(Guid CommandId, Guid ToolRequestId, string Content, bool IsError)

Their primitive fields deliberately keep Contracts records/`JsonElement` out of the Orleans
interface. Add `ConversationCommandOutcome` (`Accepted`, `Conflict`) and
`ConversationCommandOutcomeResult(ConversationCommandOutcome Outcome,
ConversationCommandAcceptance? Acceptance, string? ConflictReason)`. All three accept methods
return this Host-local result: an expected active-run, tool-request, or unequal-ID conflict is
explicitly `Conflict`; only an accepted command carries `Acceptance`. The endpoint maps that result
to `202`/`409`; it must never classify every `InvalidOperationException` as a client conflict.

Make duplicate equality equally explicit at the persistence seam. Replace
`FindByEventIdAsync`'s event-only return with the Host-local
`StoredConversationEvent(ConversationEvent Event, string? AcceptedCommandJson)`. The Azure Table
store loads the idempotency companion row together with the event row; test fakes do the same.
The grain compares the reconstructed start command's source-generated JSON with
`AcceptedCommandJson`, plus the event's ID/conversation/run/kind/participant/text, and returns the
typed `Conflict` on any mismatch. Follow-up and tool-result paths use the same returned event for
their narrower explicit equality checks. `AppendAsync` retains its own equality guard as a storage
integrity backstop, but normal client-conflict control flow does not call it and does not catch its
exceptions.

Extend `ConversationCheckpoint` with `PinnedCapabilitiesJson` (source-generated
`ConversationCapabilityDeclaration[]` JSON) and `PendingRunStart`. The latter is a
`[GenerateSerializer]` Host-local recovery record containing the complete accepted start-command
JSON, one preallocated queued-event ID, and its preallocated occurrence timestamp. On the first
accepted start, persist the validated capability declarations, pinned mission, and
`PendingRunStart` in one checkpoint write **before** the first event transition. `PendingRunStart`
is the sole retained start-command copy during this window: do **not** also set
`ActiveStartCommandJson`, because two full command copies could exceed the Azure Table-backed
Orleans-state cell limit.

After its ordinary pending-transition repair, activation detects `PendingRunStart`: it resolves the
command-ID event, appends the `UserMessage` if absent, then resolves the stable queued-event ID. If
that event is absent, it plans/appends/dispatches the queued transition through the existing durable
pending-transition/outbox protocol. Once that queued event is present, set
`ActiveStartCommandJson = PendingRunStart.StartCommandJson` and clear `PendingRunStart` in **one**
checkpoint write. The queued transition's completed pending-transition protocol already proves its
dispatch was broker-accepted, so this recovery step does not resend. This closes the literal crash
gap between the two start facts and keeps the paired `n + 1` duplicate acceptance true. A start
retry first repairs `PendingRunStart`, then uses the normal exact-duplicate path.

The capabilities remain after a run becomes terminal; `ActiveStartCommandJson` keeps its current,
narrower lifetime for a currently active tool continuation. A follow-up command reconstructs its
`ConversationCommand` inside the grain from `MissionRef`, `PinnedCapabilitiesJson`, its new run
ID, and supplied text. It must not let an adapter select a mission or replace capabilities. For an
exact follow-up duplicate, equivalence is the stored user-message event's conversation/run/kind/text
plus the grain-pinned mission/capabilities; `associatedCommandJson` remains null because the client
cannot supply any other semantic command field.

`AcceptToolResultAsync` similarly constructs the `ConversationProgress` inside the grain using its
active run and the caller's stable `CommandId` as `EventId`. Before it rejects a non-active request,
it first resolves the durable event-ID row: an exactly equal previously accepted tool result returns
its fixed acceptance; unequal reuse is conflict. Its normal success path uses the existing
`RecordProgressAsync`/deterministic-continuation semantics. Reuse the existing event-store semantic
comparison; do not create a second in-memory idempotency map.

The matching `ToolResult` participant is `Implementer`: it completes the Implementer's declared
tool hand-off, which lets Task 7 render one coherent Implementer tool row. `Forge` remains reserved
for infrastructure/lifecycle facts such as `RunStatus` and dead-letter errors.

### Orleans state and payload bounds

`ConversationGrain` is deliberately a small, sequential ownership boundary, not a place for
provider execution, blocking I/O, or a growing transcript. The event log remains Table-owned; grain
state contains only the operational checkpoint and at most **one** full accepted start-command JSON
copy. Before any checkpoint write, the grain validates the source-generated UTF-8
`ConversationCommand` JSON is at most **32 KiB**. This fixed bound covers the goal plus all
capability declarations/schema, leaves margin below the Azure Table provider's 64 KiB cell limit,
and is enforced for both first and follow-up starts. An over-limit client request returns an explicit
typed `Invalid` acceptance result mapped to `400`; it is never allowed to become a storage failure.

Likewise, before calling `RecordProgressAsync` for a Client Runtime tool result, validate the
source-generated resulting `ConversationProgress` JSON against the existing 48 KiB inline-event
limit. An over-limit content payload returns the same typed `Invalid`/`400` result rather than
relying on the store's invariant exception. Add `Invalid` to `ConversationCommandOutcome`; its
reason is suitable for the adapter's `400` response. The store retains its existing limits and
throws as the non-client-reachable integrity backstop.

## Durable replay and one-replica live notifier

Define the narrow `IConversationEventNotifier` beside the grain contract, then create
`Api/ConversationEventHub.cs` as its one in-process singleton implementation and register both.
This keeps the grain independent of the HTTP adapter. It is **not** transcript storage and it is not an Orleans Stream. The grain
publishes only after `AppendAsync` has succeeded and `AdvanceAsync` has durably written the
checkpoint; notifier failure or an absent subscriber must not change the grain transition, its
outbox, or a Service Bus acknowledgement.

Each subscription is scoped to one `ConversationAddress` and has a fixed bounded channel of 64
events. Publishing must not await a slow HTTP client. If a channel cannot accept an event, mark that
subscription stale, complete it, and remove it; the SSE endpoint ends normally and the client
reconnects from its last rendered sequence. Do not use an unbounded per-client queue, a global
transcript cache, a retry loop, or a configuration knob. A client can see a duplicated live
notification during grain recovery; its event ID/sequence makes that harmless.

Create `Api/ConversationSseWriter.cs` (or an equivalently focused helper) to own response framing
and the replay/live handoff. For every emitted event it writes and flushes exactly:

    event: conversation-event
    id: {Sequence}
    data: {one ConversationContractsJsonContext JSON value}

with the terminating blank line. Set `Content-Type: text/event-stream`, `Cache-Control: no-cache`,
and `X-Accel-Buffering: no`. Do not emit a synthetic completion event or token deltas; cancellation,
a stale subscription, or a broken response simply ends this HTTP response.

The writer carries a local `cursor`, initially `after`, and uses this fixed ordering:

1. read and emit durable Table events with `Sequence > cursor`, advancing `cursor` after each;
2. subscribe to the address's hub **before** the final catch-up;
3. read and emit durable events after the new cursor again, skipping `Sequence <= cursor`;
4. drain the subscription, emitting only `Sequence > cursor` and advancing it.

The first Table replay supplies history; subscribe-before-second-read closes the append race; the
second durable read closes the subscribe race; the cursor removes the harmless duplicate delivered
by both sources. Table remains the recovery source, so a Host restart, notifier loss, full client
channel, and ordinary network disconnect are all reconnect conditions rather than data loss. One
Silo/one Host replica is an explicit Task 6 limitation; a future multi-replica deployment replaces
only the live notifier/backplane, not the replay contract.

`Program.cs` stays the composition root: configure minimal-API JSON by inserting
`ConversationContractsJsonContext.Default` into `ConfigureHttpJsonOptions`, register the event hub,
call `app.MapConversationApi()`, and map `GET /health` to a fixed `200` process-health response.
Keep storage/client construction, Orleans, Service Bus consumers, and route implementation out of
each other's files. The `AzuriteFixture` must call the same API mapping, register the same hub, and
listen on an allocated loopback Kestrel port so tests use a real `HttpClient`/SSE response rather
than invoking endpoint delegates.

## Files and tests

| File | Change |
|---|---|
| `src/ForgeMission.ConversationHost/Program.cs` | Register source-generated HTTP JSON and the singleton event hub; map API and health routes. |
| `src/ForgeMission.Conversations.Contracts/ConversationDeterministicIds.cs`, contracts, JSON context, and their tests | Add deterministic conversation and initial-run IDs plus the transport-neutral existing-conversation command/query messages; extend source-generation round trips, including stable UUID-v5 test vectors and distinct-name assertions. |
| `src/ForgeMission.ConversationHost/Api/ConversationApiEndpoints.cs` | Focused minimal-route adapter: validation, fixed `dev` address, grain calls, and explicit HTTP mapping. |
| `src/ForgeMission.ConversationHost/Grains/IConversationEventNotifier.cs` | Grain-owned narrow post-durability notification contract; no HTTP or storage dependency. |
| `src/ForgeMission.ConversationHost/Api/ConversationEventHub.cs` | Narrow non-durable notifier/subscription implementation with bounded stale-client containment. |
| `src/ForgeMission.ConversationHost/Api/ConversationSseWriter.cs` | SSE framing plus the replay/subscribe/catch-up/drain algorithm. |
| `src/ForgeMission.ConversationHost/Persistence/IConversationEventStore.cs`, `AzureTableConversationEventStore.cs`, and test fakes | Return the Host-local stored event plus accepted-command JSON to make duplicate equality explicit; retain `AppendAsync` as an integrity backstop. |
| `src/ForgeMission.ConversationHost/Grains/IConversationGrain.cs`, `ConversationGrainResults.cs`, `ConversationCheckpoint.cs`, `ConversationGrain.cs` | Host-local typed acceptance/conflict/invalid results, one-copy start-pair recovery, pinned capabilities, exact-duplicate acceptance, fixed payload bounds, and post-durable live publish. |
| `src/ForgeMission.ConversationHost.Tests/AzuriteFixture.cs` | Register/map the real API and expose its loopback base URI; do not add an ASP.NET test-server package. |
| `src/ForgeMission.ConversationHost.Tests/ConversationApiTests.cs` | New real-Kestrel HTTP/SSE integration coverage below. |
| `src/ForgeMission.ConversationHost.Tests/ConversationGrainTests.cs` | Extend persistence/idempotency coverage for a crash after the `UserMessage` but before its queued pair, terminal duplicate starts, pinned-capability follow-ups, and duplicate tool-result acceptance if that is clearer than asserting it only over HTTP. |

`ConversationApiTests` must prove, using source-generated contract request/response JSON and an
`HttpCompletionOption.ResponseHeadersRead` SSE client:

1. start Janus returns `201`/Location and its two initial durable events; repeating the exact same
   request reaches the same deterministic conversation/run and returns the original acceptance
   without additional events; snapshot and health return their documented responses;
2. a terminal first run accepts a follow-up without client-supplied mission/capabilities, and the
   fake dispatcher observes the original pinned capability declarations;
3. tool-result acceptance validates the expected request, creates only one continuation, and exact
   duplicate retries return the original acceptance without another event/dispatch; mismatches are
   `409` with no state advance, while an over-limit payload is `400` with no state advance;
4. each HTTP route maps its path/query values into the same transport-neutral Contracts message
   that a non-HTTP adapter would invoke; malformed/unknown/active-run inputs return the documented
   `400`/`404`/`409` mapping; and
5. the SSE client reads a known sequence, cancels/disconnects, misses later events, reconnects with
   its last sequence, and receives exactly the later durable events in sequence order. Include the
   subscribe/catch-up overlap (an append published to the hub while replay is in progress) and
   assert it is neither omitted nor emitted twice.

Run:

    dotnet build src/ForgeMission.slnx
    dotnet test src/ForgeMission.slnx

Existing `/v1/*` contract tests must remain unchanged; this task neither edits nor re-baselines
them.

## Azure cloud-pattern review (2026-08-14)

This design intentionally composes a small set of applicable patterns rather than adopting the
catalog as a feature list:

| Pattern | Task 6 decision |
|---|---|
| Asynchronous Request-Reply | The service validates and durably accepts a command before returning. `POST /conversations/{id}/commands` and `/tool-results` return `202`, the durable `GET /conversations/{id}` status resource in `Location`, and fixed `Retry-After: 1`; SSE is the preferred push path, while GET is the reconnect/poll fallback. `201` remains correct for creating the conversation resource. There is no `303` because completion is represented by the same durable conversation resource, not a distinct result resource. |
| Event Sourcing + CQRS | Ordered Table events are the system of record; the Orleans checkpoint is a bounded operational snapshot, never a second transcript. SSE/Desktop/Rooms are read projections. A conversation index or other materialized view remains later work, only when a query cannot be served by the stream/snapshot. |
| Claim Check | Existing Blob artifacts are the claim-check path: large payload is written before its reference event. Task 6 rejects oversized tool-result content rather than adding an unscoped client Blob-upload flow or putting large payloads on Service Bus. |
| Queue-Based Load Leveling + Sequential Convoy | Service Bus separates accepted work from Worker execution; `ConversationId` is the session/category key, preserving FIFO within a conversation while independent conversations can scale in parallel. Existing producer/consumer envelope validation and DLQs protect this key and unblock poison sessions. |
| Health Endpoint Monitoring | `/health` is deliberately a liveness-only process endpoint for this task, with no fake dependency probe. A later deployment task must define separate readiness observations for storage, Service Bus, and Orleans when a platform probe actually consumes them. |

Circuit Breaker, throttling, competing-consumer tuning, a global pub/sub projection, saga/compensation,
and caching are deferred. They do not solve a present Task 6 risk, and several would weaken the
fixed-order/durable-fact contract or introduce provider-spend policy before it is designed.

## Earlier Forge message-API reconciliation (2026-08-14)

Reviewed the prior `/v1` tool-turn, Desktop cloud-mission, Rooms, and Client Runtime transport
designs before building this API. The shared rules are deliberate; the transport models remain
separate:

| Earlier design | Reuse / boundary for durable conversations |
|---|---|
| [42.3 tool-capable responder](phase-42.3-tool-capable-enriching-responder.md) | Reuse the strict tool-result correlation rule and the finding that a provider-facing tool result has its own role/shape. Do **not** reuse its full-client-history replay, prefix-hash enrichment cache, or deferred response replay: `/v1/messages` is a compatibility wire whose client owns the loop; `ConversationCommand`/`ConversationEvent` are a server-owned durable protocol. |
| [43.14 Desktop cloud missions](phase-43.14-desktop-cloud-missions.md) | Preserve the distinction it locked: an idempotency token is not a continuation/re-entrancy key. Here `CommandId` identifies one accepted command, `EventId` one durable fact, and `ToolRequestId` one authorized hand-off; none substitutes for another. |
| [38.1 Rooms foundation](phase-38.1-room-foundation.md) | Reuse append-before-broadcast and recovered-history-before-live delivery. Do **not** reuse Rooms Postgres messages or SignalR as a second transcript; Rooms becomes a projection of the Conversation service's Table event log. |
| [43.10 Client Runtime transport](phase-43.10-transport-contract.md) | Reuse HTTP POST plus SSE as transport shapes, but not its in-memory/lossy event hub. Presentation reaches the Host only through the Client Runtime in Task 7; the Conversation API's sequence cursor/Table replay is what makes a dropped SSE recoverable. |

The regression boundary is explicit: Task 6 neither changes `/v1/*` contracts nor moves Desktop's
local-tool authorization out of Client Runtime. Its tests retain the existing `/v1/*` suite unchanged
and prove that a `tool_requested` durable fact still requires the matching `ToolRequestId` result.

## Orleans best-practices review (2026-08-14)

Reviewed against Microsoft's Orleans guidance before Task 6 implementation. The design is aligned:

- one small, independent, non-reentrant `ConversationGrain` owns one ordered conversation; Janus
  participants are events, not chatty grains, and one conversation's required sequence is a
  deliberate local coordinator rather than a cross-conversation bottleneck;
- provider calls and long-running execution remain in the Worker, not a grain; grain operations use
  awaited storage, grain, and dispatcher calls only—no blocking waits, locks, or reentrancy;
- `IPersistentState<T>` is the compact operational checkpoint, while the transcript stays in Table;
  the one-copy start recovery and fixed 32 KiB command bound now enforce that distinction against
  the Azure Table state-cell limit; and
- Orleans request delivery is at-most-once, so the stable client `CommandId`, deterministic initial
  address/run IDs, idempotency rows, pending transition/start recovery, and Service Bus outbox are
  the end-to-end retry contract rather than an assumption that a failed call is rerun.

No stateless worker, grain reentrancy, timers for state batching, or multi-silo test cluster is
introduced: none solves a present Task 6 need. One-Silo/one-host live notifications remain a
declared proof limitation; Table replay is the durable reconnection path and later HA replaces only
the notifier backplane.

### OrleansContrib pattern review (2026-08-14)

The community Observer pattern confirms that subscriptions are transient and must be explicitly
managed. It is therefore deliberately **not** the conversation delivery guarantee: our bounded
in-process notifier has the same best-effort role, while the durable Table sequence remains the
only reconnect source. The Hub pattern is also deferred: a global aggregation hub adds a singleton
pressure point and can reorder buffered events, neither of which is acceptable for this one
conversation's canonical sequence. A future multi-silo notifier backplane may use a hub-like
implementation internally, but it must preserve the current SSE cursor/replay contract.

Cadence, Dispatcher, Reduce, and Smart Cache do not solve a present Task 6 requirement. The
durable outbox reminder is a recovery mechanism, not cadence/batching; it must send the individual
stable command ID promptly. Service Bus consumers already resolve each message directly to its
conversation grain, which preserves per-conversation ordering; introducing an Orleans dispatcher
would add a hop and latency without reducing a demonstrated boundary cost. Aggregation and cached
reads are premature while this proof has one Silo and Table remains the authoritative transcript.

## Architecture-security and engineering gates

| Gate | Locked answer |
|---|---|
| Bounded context / owner | Durable Conversation is the bounded context. ConversationHost/`ConversationGrain` is its sole sequence allocator and Table/Blob owner; the Worker has no Store/Orleans access. |
| Public entry / tier | This is a local Kind/port-forward development adapter with fixed `dev` identity. The cloud Host remains internal-only; later Tier-1 ForgeAPI/ForgeUI authenticates, authorizes, and routes without data-plane credentials. |
| Tier-2 / Tier-3 contract | Host-to-Worker commands and Worker-to-Host progress remain the existing Service Bus contracts. Table/Blob and Service Bus remain Tier 3 with no public ingress. SSE reads through the owning Host, never directly from Table. |
| Credentials | No new credentials, grants, packages, or ingress are introduced. Host retains its existing least-privilege data/queue directions; Worker retains no Storage/Orleans credential. |
| Type | Conversation ownership, public-edge target, and no cross-store access remain Type 1 locked decisions. The bounded one-replica notifier and SSE mechanics are Type 2 behind `IConversationEventNotifier`; replacement condition is multi-replica/HA work requiring a shared backplane while preserving Table replay. |
| Ownership / failure boundary | Routes adapt HTTP only; grain owns mutation/idempotency/outbox; event store owns durable replay; notifier owns best-effort live delivery; SSE writer owns framing/reconnect handoff. A full/broken live channel ends, never blocks or mutates a run. |
| Knobs / abstractions | One real seam (`IConversationEventNotifier`) exists to enforce the non-durable live boundary. Capacity 64, a 32 KiB command bound, and the existing 48 KiB event bound are fixed for this proof; no heartbeat, retry, cache, or configurable transport abstraction is added. |
| Verification | Named real-Kestrel HTTP/SSE tests prove the critical disconnect/replay and replay/live-overlap observations; full build/test proves the additive change preserves existing contracts. |

**Done when:** the named tests above pass, including the disconnect/reconnect and overlap cases;
`dotnet build src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx` pass; and existing
`/v1/*` contract tests are unchanged. No Task 7/8, Worker, mission-definition, or forge-infra
change is included.
