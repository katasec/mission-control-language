# Phase 43.16 Task 5 — Service Bus delivery and Janus Worker

> **Status: implementation-ready 2026-08-13.** Parent: [43.16 Durable Janus conversation proof](phase-43.16-janus-desktop-local-poc.md).

## Scope

Build the internal at-least-once delivery path only: Host command dispatch/progress consumption and a separate Janus Worker. Exclude Task 6 HTTP/SSE, Task 7 Client Runtime/rendering, Task 8 Kind manifests/images/live proof, and forge-infra changes. Automated tests prove application delivery/recovery; Task 8 proves the real Azure Service Bus and Kind path.

## Projects and packages

Add `ForgeMission.ConversationWorker` and `ForgeMission.ConversationWorker.Tests` to `ForgeMission.slnx`. Worker references only Conversations.Contracts, Core, and ChatClients; it must not reference ConversationHost, Orleans, Azure Storage, Client Runtime, Presentation, Rooms, a Desktop workspace, or a capability executor. It loads the checked-in read-only Janus mission/mcl.lock from `ConversationWorker__JanusMissionDirectory` and accepts only `MissionRef == "Janus"`; that packaged definition is not local-machine authority.

Add `Azure.Messaging.ServiceBus` `7.20.2` and `Microsoft.Orleans.Reminders.AzureStorage` `10.0.0` to Host, and Azure.Identity `1.21.0` plus `Azure.Messaging.ServiceBus` `7.20.2` to Worker. The Host configures the reminder provider with its existing TableServiceClient and a distinct `OrleansConversationReminders` table. Extend source boundary tests: Worker may name only the two Azure packages and no Host/Storage/Orleans package; Contracts stays dependency-free.

## Configuration and envelope

`ConversationServiceBusOptions` has exactly:

    FullyQualifiedNamespace
    MissionCommandQueueName = mission-command
    ProgressQueueName = conversation-progress
    MissionCommandSendConnectionString
    MissionCommandListenConnectionString
    ProgressSendConnectionString
    ProgressListenConnectionString

For each required direction select the scoped connection string or `FullyQualifiedNamespace` with DefaultAzureCredential; fail startup if neither or both are present. Host constructs only command Send/progress Listen; Worker constructs only command Listen/progress Send. Task 8 supplies the four scoped Kind values; no forge-infra change occurs here.

Commands and progress use source-generated Contracts JSON. Command body is `ConversationCommand`, `MessageId = CommandId:N`, `SessionId = ConversationId:N`; progress analogously uses EventId. Host writes trusted application property `tenant_id` from ConversationAddress; Worker preserves it. Host validates non-empty tenant and matching body/session/message IDs before a grain call.

## Host ownership

Add `IConversationCommandDispatcher`, `AzureServiceBusConversationCommandDispatcher`, `ConversationProgressHandler`, `ConversationProgressConsumer`, `ConversationProgressDeadLetterConsumer`, and the options type under `ConversationHost/Messaging`.

Dispatcher is the only mission-command sender. Evolve Task 4’s pending transition with a separate optional `DispatchCommandJson`; DispatchState becomes `NotDispatched` / `BrokerAccepted`. `AssociatedCommandJson` remains only UserMessage idempotency data. AcceptCommand stores the full StartMission command on its queued transition: append/notify first, send while NotDispatched, persist BrokerAccepted after broker acknowledgement, then clear. The checkpoint retains that active-start-command JSON until the run is terminal, so it can construct one safe continuation without querying another store.

After a matching ToolResult is durably appended, the grain derives a `ContinueAfterTool` command from the retained StartMission command. Its command ID is `ConversationDeterministicIds.Continuation(toolResultEventId)`: RFC-4122 UUID-v5, namespace `d6a0c730-a25d-5cbf-a047-8ee9a1c0f171`, name `continuation:{eventId:N}`. It preserves the original mission, goal, and capability declarations and carries that ToolResult. This continuation is held in that ToolResult transition's `DispatchCommandJson`, so it follows the same append → broker-acknowledge → clear outbox path. A mismatched tool result remains Rejected and creates no command. Put this UUID-v5 algorithm in a dependency-free `ConversationDeterministicIds` Contracts helper; `Progress(commandId, ordinal)` uses name `progress:{commandId:N}:{ordinal}`, `ToolRequest(commandId, ordinal)` uses `tool-request:{commandId:N}:{ordinal}`, and `DeadLetter(eventId, kind)` uses `dead-letter:{kind}:{eventId:N}`, under the same namespace. Host and Worker use it for continuation, progress ordinals, tool correlation, and dead-letter IDs. Recovery resends only the same command ID while NotDispatched and clears BrokerAccepted without resend. This is an explicit application outbox, not a Table/Service-Bus transaction.

`ConversationGrain` implements `IRemindable` with one fixed `mission-command-outbox` reminder. Before persisting any NotDispatched transition, it registers that reminder; an orphaned reminder is harmless because its callback finds no pending dispatch and unregisters. The callback runs the same repair path and leaves the reminder registered on a failed send. It unregisters only after BrokerAccepted has been persisted and the pending transition cleared. This is the durable retry driver; activation repair remains a fast path, not the only recovery path.

The Host progress processor is peek-lock, auto-complete false, one concurrent session/call. Its SDK adapter calls the typed handler, which calls ConversationGrain.RecordProgressAsync. It completes only Appended/AlreadyRecorded; typed Rejected results complete and log because retry cannot make them valid. Other failures remain unsettled for broker retry. Azure.Messaging.ServiceBus 7.20.2 exposes `SubQueue` only on the non-session `ServiceBusProcessorOptions`, and a dead-letter subqueue is not session-enabled. Therefore the progress-DLQ consumer is a plain `ServiceBusProcessor` for the original queue with `SubQueue = SubQueue.DeadLetter`, `PeekLock`, auto-complete false, and one concurrent call. It turns only a valid envelope—non-empty tenant and body `ConversationId`/`EventId` matching the incoming SessionId/MessageId—into stable UUID-v5-derived Error then RunStatus(Failed) facts. A malformed or unaddressable DLQ message is structured-logged then completed.

## Worker ownership and recovery

Add Worker `Messaging/AzureServiceBusMissionCommandConsumer.cs`, `Messaging/AzureServiceBusConversationProgressPublisher.cs`, `Janus/JanusMissionExecutor.cs`, `Janus/JanusPipelineProgressMapper.cs`, and `Janus/WorkerSessionState.cs`.

The Worker uses peek-lock with auto-complete false. Its source-generated Service Bus session state is bounded: current command/run IDs, phase (`ExecutingProvider`, `WaitingForTool`, `Terminal`), next progress ordinal, one pending serialized progress fact, approved Janus plan, and outstanding tool call. It is recovery metadata only—never a transcript, credential, workspace path, or conversation-store data.

Each progress fact becomes pending state with `ConversationDeterministicIds.Progress(commandId, ordinal)`, is sent, then clears/increments. Restart resends the same pending fact. For the one supported tool request, `ordinal` is the current `NextProgressOrdinal` of that ToolRequested fact (never a fixed `0`); its `ConversationToolRequest.RequestId` is `ConversationDeterministicIds.ToolRequest(commandId, ordinal)`. Before serializing its pending progress, persist both that RequestId and the provider call ID in session state with phase WaitingForTool; then use the normal pending-progress → send → clear sequence. Before any provider invocation, Worker persists ExecutingProvider. A redelivery in that phase emits stable RunStatus(Interrupted) and completes the command; it never invokes a provider again. A redelivery of the command already named by a WaitingForTool or Terminal state simply completes.

A matching new ContinueAfterTool command is different from that duplicate: it must match the stored RequestId and RunId, then replace `CurrentCommandId` with its own command ID, clear the outstanding-tool fields, persist ExecutingProvider, and invoke only Implement. Any other command while WaitingForTool (including a duplicate StartMission) is a no-op complete. A continuation whose current state is not WaitingForTool, whose run is different, or whose result ID does not match produces no progress and completes.

JanusMissionExecutor uses MclParser, LockFileIO, ExpertLoader, ForgeTomlReader, and `ChatClients.Build` with the parsed provider profile. Provider keys come only from Worker environment. Capability declarations become declaration-only AITools, never executable tools.

JanusPipelineProgressMapper is fixed to current Janus: participant starts become ParticipantStarted; successful completion becomes ParticipantMessage, with Approver also Approval(Approved) and the Approver's own `Envelope.Text` persisted as the approved plan; failed Approver becomes ParticipantMessage plus Approval(RevisionRequested, reason-or-text); other failed completion becomes Error; exactly one PipelineToolRequested becomes ToolRequested; mission pass/fail becomes terminal Completed/Failed; exceptions become Error then Failed. Deltas never persist. Unknown experts, zero/multiple tool calls, or a tool request without approved plan fail visibly.

`PipelineRunner` returns `MissionStatus.Pass` for an agent tool pause, so a result with non-empty `ToolCalls` is never terminal: Worker publishes the one ToolRequested fact and leaves its state WaitingForTool. It emits RunStatus(Completed) only for a pass result with no tool calls; a fail result emits Failed. This prevents a ToolRequested/Completed contradiction and preserves the only safe continuation path. A caught executor/provider exception is known failure, so it emits Error then Failed through the same progress outbox; only a process death/cancellation after the persisted ExecutingProvider boundary becomes Interrupted on redelivery. A session-state write or progress-publish exception is **not** an executor failure: it must escape and leave the command unsettled, retaining the existing pending fact rather than replacing it with an Error/Failed pair.

Worker rejects every command whose `MissionRef` is not exactly `Janus` before provider execution, emitting deterministic Error then Failed progress. ContinueAfterTool is accepted only for current WaitingForTool when ToolResult.RequestId matches state. It runs only `Implement` with the stored approved plan, StartAtAgent, mission path `[Janus, Implement]`, declaration-only tools, and reconstructed assistant function-call/user function-result conversation. A mismatch produces no progress. General pipeline checkpoint/resume remains deferred. Like the Host DLQ, the command-DLQ processor is plain rather than session-aware: it is created for the original queue with `ServiceBusProcessorOptions.SubQueue = SubQueue.DeadLetter`, PeekLock, auto-complete false, and one concurrent call. It validates MessageId/SessionId against its addressable command body before emitting Error/Failed progress, while malformed commands are logged and completed.

## Gates

| Gate | Locked answer |
|---|---|
| Owner | ConversationHost alone allocates sequence/writes Table/Blob; Worker owns only an execution attempt and Service Bus session recovery. |
| Tiers | Queues are Tier 3. Host sends command/listens progress; Worker listens command/sends progress. No public entry point or Service Bus Manage right changes. |
| Credentials | Kind uses scoped SAS values; deployed apps use separate managed identities. Worker has no Storage/Orleans/Host credential. |
| Type | Store ownership and Worker exclusion are Type 1; session recovery and one-Silo concurrency are reversible Type 2 details behind adapters. |

## Verification

Use small in-memory dispatcher/handler/publisher seams for application integration; this is not an Azure-emulator claim. Prove:

1. queued command is durable before send; the outbox reminder is registered before its NotDispatched checkpoint, send failure recovery retries the same ID, and BrokerAccepted recovery does not resend;
2. duplicate command/progress creates one transition, sessions stay isolated, a matching tool result creates one deterministic continuation command, and an unexpected tool result adds neither event nor command;
3. mapper emits ordered Janus revision/approval facts with no deltas and retains the successful Approver text as the approved plan;
4. Worker restart at ExecutingProvider emits one Interrupted fact without executor replay, resends pending progress with the same ID, and retains the matching-ordinal deterministic tool request/provider-call correlation before publishing ToolRequested;
5. a non-empty ToolCalls result publishes no terminal status; matching continuation changes the current command and runs only Implementer, while mismatch/duplicate StartMission does nothing; caught provider errors emit Error then Failed, only cancellation/death uses Interrupted, and state-save/publish failures retain their pending fact and retry rather than being converted to Error/Failed; and
6. Worker has no Host/Orleans/Storage/Client Runtime dependency.

Run `dotnet build src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx`.

**Done when:** these tests and the full suite pass. No Task 6/7/8, mission-definition, or forge-infra change is included.
