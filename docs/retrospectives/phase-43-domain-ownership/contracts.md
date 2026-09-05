# Ownership contracts

Companion to [end state](end-state.md). The source baseline is `ef2e636cd37db6702364745c337d3156674541c6`. Existing request/response definitions below are normative by reference, not placeholders for new types. Move them without changing serialization, validation, limits or error behavior.

## Shared application actions

The complete existing wire vocabulary is [ClientRuntimeContracts.cs](../../../src/ForgeMission.ClientRuntime.Transport/ClientRuntimeContracts.cs), its JSON registration is [ClientRuntimeJsonContext.cs](../../../src/ForgeMission.ClientRuntime.Transport/ClientRuntimeJsonContext.cs), and route/error behavior is [ClientRuntimeEndpoints.cs](../../../src/ForgeMission.ClientRuntime/Transport/ClientRuntimeEndpoints.cs). Event serialization separately uses [ConversationRelayJsonContext.cs](../../../src/ForgeMission.ClientRuntime.Transport/ConversationRelayJsonContext.cs), including the embedded conversation string-enum options; do not merge those settings with numeric application DTO enums. These become Application.Transport / Application.Host sources during implementation. References in this historical baseline table are updated to their destination paths in the same implementation commit that moves them.

| Existing request → response | Owner / local call | Preserved semantics |
|---|---|---|
| `ProjectDraftRequest` → `ProjectDraftResponse` | `ProjectService.Draft(goal, titleOverride, homeOverride)` | Pure proposal; no I/O, reservation, session or authority. |
| `ProjectCreateRequest`, `ProjectOpenRequest` → `ProjectOperationResponse` | ProjectService's existing Create/Open, then ApplicationSessionService attaches the validated Project | Created/Opened has Session only; GoalRequired has Proposal only and creates nothing; Failed has Error only. Keep current expected-error mapping. |
| `SessionSetupRequest` → `SessionSetupResponse` | `ApplicationSessionService.ReplaceAsync` | Requires existing outgoing session and same Project root; misuse remains HTTP 400. It cannot establish initial authority. |
| `SelectProjectMissionRequest` → `SelectProjectMissionResponse` | `ProjectService.SelectMissionAsync(home, mission, ct)` | Existing Project transaction/catalog validation; no content service or UI decision. |
| `StartProjectMissionRunRequest`, `RetryProjectMissionSubmissionRequest` → `ProjectSubmissionResponse` | `MissionSubmissionService.StartAsync(session, request, ct)` / `RetryAsync(session, request, ct)` | Resolve home from live session; immutable command/journal protocol below. |
| `GetProjectMissionStateRequest` → `GetProjectMissionStateResponse` | Application session's Project Mission read scope composes History state and starts Observation | Subscribe-before-read readiness preserved; response combines current manifest/journal and Host page. No hidden submission retry or capability execution. |
| `GetProjectRunsRequest` → `GetProjectRunsResponse` | `RunHistoryService.GetRunsAsync(cursor, ct)` in that read scope | Existing typed cursor and bounded Host page; validate container Project identity/purpose. |
| `GetProjectRunRequest` → `GetProjectRunResponse` | `RunHistoryService.GetRunAsync(runId, ct)` | Exact run detail, existing ownership checks/errors. |
| `GetProjectRunEventsRequest` → `GetProjectRunEventsResponse` | `RunHistoryService.GetEventsAsync(runId, after, through, ct)` | Exact bounded sequence range; no unrestricted event download. |
| `GetProjectWorkbenchRequest`, `OpenProjectDocumentRequest` → corresponding existing responses | `ProjectContentService.GetProjection(home)` / `OpenDocument(home, entryId)` | Entry identity resolved against a fresh manifest; 1 MiB, symlink/path, UTF-8, binary and hash checks retained. |
| `PromptRequest` → `PromptResponse` | ConversationService for DurableConversation; per-prompt legacy protocol client for Mission | Preserve current start/follow-up behavior, response meaning and error transport; Project Missions continue to use run/retry, not this route. |
| `CapabilityDispatchRequest` → `CapabilityDispatchResponse` | Application maps existing wire request to Core request; Bob dispatches | Session lookup is not approval. Existing capability policy, confirmation, audit and provider containment still apply. |
| `ConfirmationResponseRequest` → `ConfirmationResponse` | `Application/Interaction/PendingConfirmationHandler.Resolve` for that session | Only matching pending confirmation settles; stale/unknown returns Accepted=false. It cannot force-allow policy denial. |
| Existing event subscription | ApplicationEventHub in Host; Application owners publish existing event DTOs through an injected callback | Rename CLR event names only. Preserve kinds/fields, sequence behavior and existing bounded invalidation. SSE disconnect does not cancel a remote run. |

`IApplicationChannel` is the renamed existing channel: `Task<TResponse> SendAsync<TRequest,TResponse>(TRequest request, CancellationToken ct)` and `IAsyncEnumerable<ApplicationEvent> Subscribe(CancellationToken ct)`. Existing DTOs referring to `ProjectRunPage`, `ProjectRunDetail`, `ProjectRunEventPage` and `ConversationEvent` retain those exact `Conversations.Contracts` types. No second domain enum or untyped response envelope is introduced. Source-generated JSON registrations move with their records; numeric enums retain their ordinal values.

Expected domain outcomes remain typed. Preserve existing HTTP 400/404 misuse/not-found behavior and unexpected transport failures rather than converting every exception to a generic success response. Application owns domain-error mapping and session lookup; endpoints bind requests, delegate and encode responses.

### Public library entry point

Host must not require public exposure of ProjectRecord, ApplicationSession or the internal service graph. `ApplicationApi` is a thin, closed request dispatcher in Application, not another domain owner. It uses explicit request-type cases for the table above (no reflection or dynamic discovery), resolves the internal session, and delegates. The route adapter therefore does not construct or manipulate application state. Its complete public surface is:

```csharp
public sealed class ApplicationApi : IAsyncDisposable
{
    public static ApplicationApi Create(
        IHttpClientFactory clients,
        string? missionRuntimeMode,
        CapabilityAuthorizationPolicy policy,
        Action<ApplicationEvent> publish,
        CancellationToken applicationStopping);

    public Task<TResponse> InvokeAsync<TRequest, TResponse>(
        TRequest request, CancellationToken ct);

    public ValueTask DisposeAsync();
}

public sealed class ApplicationSessionNotFoundException : Exception;
public sealed class SessionReplacementRejectedException(string message) : Exception(message);
public sealed class ApplicationRequestRejectedException(string message) : Exception(message);
```

`IHttpClientFactory` is the existing System.Net.Http abstraction; policy is the existing Core type and ApplicationEvent is the renamed transport event record. Create composes the concrete internal owners, with the existing default Project root and named `mission-runtime`/`conversation-host` clients; it performs no Project I/O or execution-session creation. Host supplies those clients with already-resolved endpoints/credentials, passes the existing MissionRuntime:Mode value and registers one API instance for process lifetime. Preserve `UsesCloudMissionRuntime`: null or case-insensitive cloud selects the cloud adapter; other values select the existing compatibility adapter. Null mission keeps each adapter's existing default. The test harness can supply controlled clients, explicitly non-acceptance. Domain classes and their Project/session types remain internal.

Missing sessions raise ApplicationSessionNotFoundException, mapped by Host to the existing 404, except confirmation lookup, which preserves Accepted=false. Invalid replacement raises the existing exception made public, mapped to 400. An unsupported capability operation raises ApplicationRequestRejectedException with the existing `Unsupported capability request.` message, also mapped to 400. Expected Project failures use their existing response payloads; legacy prompt HttpRequestException/InvalidOperationException handling still emits Error and returns PromptResponse(IsError=true). Unsupported request/response generic pairs are a programming error (`ArgumentException`), not an extensibility path. API disposal closes and joins all owned session disposal before completing. It contains request routing and composition only; journal, history, policy, protocol and lifecycle algorithms remain with the owners named above. Host's event hub receives the injected callback and owns SSE clients, not domain decisions.

## Local execution boundary

The following is the complete new public local boundary. Types other than `ClientExecutionSession` already exist in `ForgeMission.Core.Tools`; their definitions remain unchanged. Names below are C# signatures, with implementation omitted deliberately.

```csharp
// namespace ForgeMission.ClientRuntime
public sealed class ClientExecutionSession : ICapabilityDispatcher, IAsyncDisposable
{
    public static ClientExecutionSession Create(
        string root,
        CapabilityAuthorizationPolicy policy,
        ICapabilityConfirmationHandler confirmation,
        CancellationToken lifetime);

    public IReadOnlyList<string> AvailableCapabilities { get; }
    public IReadOnlyList<Microsoft.Extensions.AI.AITool> ToolDeclarations { get; }

    public Task<ToolExecutionResult> DispatchAsync(
        string capabilityName, object request, CancellationToken ct);

    public ValueTask DisposeAsync();
}
```

It replaces `WorkspaceState`, with private `LocalDiskWorkspace`, registry, dispatcher, audit and lifetime tracking. Existing `ICapabilityDispatcher` uses `object request`; only the current typed Core request variants are supported, with existing invalid-request results. This extraction does not redesign that shared interface. `ToolDeclarations` preserves the registry's existing **declaration-only** AITool values for compatibility adapters; Bob never invokes them as a reasoning loop. No provider registry, provider object, manifest, conversation ID, mission name, network URL, credential or event DTO escapes through this boundary. Policy is explicit, with no implicit fallback in Create; Host resolves the existing configured/default policy before construction.

Application alone creates this scope after Project validation and retains it in its private `ApplicationSession`. Its string session ID remains the existing opaque local API ID. Project Guid, container Guid, run Guid and command Guid remain distinct fields/types in their existing records; no session ID is reused as a remote command ID. Bob has no need for any of those domain IDs. No process boot or content/history read creates a new execution session by itself.

Dispatch admits work only while the scope is open, links request cancellation with scope lifetime, and tracks every admitted operation to settlement. Dispose closes admission atomically, cancels the linked lifetime, awaits admitted operations, then releases resources. Concurrent/repeated Dispose calls join the same completion. A dispatcher reference retained before close must still reject a later dispatch with an explicit error result and no provider call. Cancellation is an observed stop request, not rollback or proof that an external side effect never happened. Preserve existing provider result/error semantics and record that uncertainty honestly.

`PendingConfirmationHandler` remains in Application/Interaction, implements the existing Core confirmation interface, and publishes the existing confirmation event through a callback. Bob waits through that interface; it does not know transport or user-interface types. Closing the execution scope cancels pending confirmation waits so disposal cannot hang awaiting a UI response.

## Application attachment lifecycle

`ApplicationSessionService` replaces ClientRuntimeSessionStore. Its internal `ApplicationSession` contains immutable Id, validated Project home, selected legacy mission/runtime, one ClientExecutionSession, the confirmation handler, the existing serialized conversation slot, and the Project Mission read/observation slot. It may retain a Project summary for display, but mutations and launches re-read the Project owner; cached summaries never authorize a submission. Use these existing slot algorithms rather than introducing a generic session framework.

For replacement, one session-owned close operation wins against concurrent replacement/admission. Remove it from lookup, close new work admission, cancel the attachment lifetime, drain its conversation delivery and observation, then dispose Bob and publish the replacement. No replacement may change home. A failed replacement does not revive the disposed old attachment or undo a durable Project/Host write; report failure, and the user reopens the Project. All disposal callers join the same completion. Hold no Project file lease while awaiting network or teardown.

If create succeeds durably but attachment setup fails, the Project remains created. Propagate the unexpected attachment/transport failure; do not delete the manifest or repeat Create automatically. Recovery is Open of that same returned/known home, not a second Project creation. This preserves the current separation of durable creation from ephemeral session setup.

The Project Mission read scope is a small lifetime/composition object, not a new domain service: it owns one RunHistoryService and one RunObservationService and delegates typed reads. It creates no Bob reference. `GetStateAsync` establishes observation before fetching the first page, retaining current initial-subscription failure/retry behavior. Other reads retain current lazy/readiness behavior; do not add unrelated remote calls.

Closing a Project attachment, changing its legacy session or losing a UI subscription stops its local work; it does **not** submit remote run cancellation. Supervisor shutdown owns process cleanup. Mission stop/resume remains outside this refactor.

## Submission and durable ownership

Preserve [ProjectManifest.cs](../../../src/ForgeMission.ClientRuntime/Services/ProjectManifest.cs), schema version 3, v1/v2 reads and legacy read-only fields. ProjectService is the sole manifest transaction owner, including submission journal writes; MissionSubmissionService decides when to invoke those named operations. No second manifest writer, local run ledger or new schema version.

1. Start reads the validated Project and checks the existing Host active-run state. Host admission remains the final arbiter against races.
2. ProjectService prepares the immutable journal using CommandId, PreviousCommandId and input under the existing lease. Mission, input and Project goal are captured exactly as today. Release the lease before HTTP.
3. MissionSubmissionService ensures the deterministic Project container through ConversationHostClient and verifies returned purpose/Project identity. All container/journal writeback goes through ProjectService.
4. Send the existing Host `StartProjectMissionRunRequest`. Accepted means a durable Host receipt, not completed reasoning or local effects.
5. Definitive rejection records Rejected. Transport failure, cancellation after dispatch, malformed acceptance or ambiguous response retains Prepared with the existing typed uncertainty/error result. Do not invent rejection or success.
6. Retry reuses exactly the stored CommandId and payload. Check the Host command receipt first; reconcile its existing fields. If no receipt exists, resubmit the same command. A changed command/payload or mismatched receipt is an explicit conflict. A new run uses a new command, never a mutated retry.
7. A Host acceptance followed by a local write failure leaves the Host run intact and local receipt unresolved. Return the existing error and recover with same-command receipt reconciliation. No rollback of remote acceptance, automatic new command or billing-token substitution.

Prepared/Accepted/Rejected describe the local submission, not run status. Host's existing run status/event model stays authoritative. Worker checkpoints describe execution recovery, not another public run state machine.

## Observation and tool delivery

`ConversationTailReader` moves to `Application/Adapters/Conversations`. Keep one tail per existing scoped conversation use, ordered sequence checking, duplicate suppression and reconnect from last applied cursor. A required protocol handler runs **before** cursor advance; if it fails, retry from that event. Optional UI projection must not become the acknowledgement for tool-result delivery.

`RunObservationService` owns only readiness, cursor/tail lifetime, refresh and invalidation. Preserve the current one-second active/Prepared and five-second idle refresh, one queued invalidation plus a dirty replacement, generation checks and authoritative re-read. The scope composes `ProjectMissionToolRefusal` as a separate required protocol hook before the observation callback. Refusal still returns the same deterministic error result and checks conflicts as today; it has no capability dependency. This removes hidden execution-protocol responsibility from a read service without adding a second competing tail.

`LegacyJanusToolDelivery` owns existing ToolRequested/ToolResult interpretation, request-ID result cache, Implementer/nonempty/supported-tool checks, conversion through ToolExecutorRegistry and deterministic result submission. It receives only Bob's dispatcher/declarations, never providers. ConversationService retains conversation identity; the adapter receives the current conversation ID explicitly for result submission. All local policy decisions still happen inside Bob.

Preserve legacy tool-result behavior during the move, including its existing in-memory cache and error reporting. The current handler catches a reporting HTTP failure; therefore its tail can advance without a confirmed result receipt. **Do not claim durable delivery or exactly-once effects for that legacy path.** Adding a durable result journal/replay protocol is outside this behavior-preserving extraction. Project Mission refusal has its stronger existing retry-before-cursor behavior and must retain it. Neither path may substitute OpenHands' immediate dispatch acknowledgement for a real execution result.

The two legacy Mission protocol clients retain the current per-prompt construction, transcript growth within that prompt, tool-result round trips and cloud client-token/enrichment distinction. No transcript is silently promoted to session persistence. There is no new shared agent scheduler; Worker/Core continue to own durable mission reasoning.

## Failure and precondition matrix

| Condition / failure | Owner and containment | Caller result / recovery | Required observation |
|---|---|---|---|
| Valid Project vs invalid/missing manifest | ProjectService + file transaction | Existing Created/Opened/GoalRequired or typed error; no authority on failed open | Positive open; malformed/version/absent-manifest cases leave expected files and no execution session |
| Competing manifest mutation or stale command | ProjectService lease + immutable journal | Existing Busy/Changed/SubmissionChanged; refresh/reconcile | Two writers cannot produce mixed selection/input/receipt |
| Lost submission reply or local acceptance write failure | MissionSubmissionService + Host command dedupe | Prepared/uncertain or existing write error; same-command retry | Exactly one Host run, matching recovered receipt, no new command |
| Foreign container, run or mismatched receipt | Application verified reads / Host authorization | Existing conflict/not-found; no cross-Project history or result mutation | Correct Project succeeds; foreign/mismatched identifiers fail |
| Tail disconnect/gap/failed refusal | Tail + protocol handler | Existing error/reconnect; retain cursor before unhandled event | Duplicate/gap/failed-refusal replay cannot execute tools or skip refusal |
| Stale read response or slow observer | RunObservationService + read generation | Preserve current valid state; bounded refresh/re-read | Out-of-order reply cannot overwrite newer selected run; bounded pending invalidation |
| Document traversal/symlink/oversize/hash/binary | ProjectContentService private reader | Existing Document* error; user selects valid content | Valid document succeeds; each applicable rejection reads no unrelated content |
| Wrong Janus participant or Project Mission tool request | Janus adapter / ProjectMissionToolRefusal | Existing error result, zero provider calls | Allowed legacy request reaches policy; wrong participant and Project Mission never dispatch |
| Policy denial / declined or stale confirmation | Bob + Application confirmation bridge | Existing denied result; stale Accepted=false | Direct and model-mapped requests cannot bypass denial; valid approval executes once |
| Dispatch/prompt racing replacement or shutdown | Application attachment + Bob admission/drain | Explicit stale/closed failure or admitted operation settles before disposal completes | Test both race orderings; no orphan tail, pending confirmation or post-close provider start |
| Application Host fails readiness/exits | Supervisor process boundary | Existing startup/error surface and owned-child cleanup | Published launch + failed readiness/Host exit leaves no supervised orphan |
| Provider/transport failure in legacy protocol | Existing protocol adapter / Bob | Existing visible error; no new retry or billing semantics | Preserve cloud token through tool continuations; no automatic uncertain side-effect replay |

Controlled negative tests prove their named boundary only. Default-path acceptance remains separately mandatory under the implementation spoke.
