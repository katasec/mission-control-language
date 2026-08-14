# Phase 43.16 Task 7 — Client Runtime and group-chat rendering: completed evidence

Task 7 is **done and verified (2026-08-14)**. Its implementation is `18b9eba`; the active Task 7
spoke retains the locked ownership/contract design needed by Task 8.

## Delivered

- `ConversationHostClient` is the sole Client Runtime class that knows the Task 6 HTTP/SSE
  projection (`StartConversationRequest`/`SubmitConversationCommandRequest`/
  `SubmitToolResultRequest`, and SSE frame parsing), using `ConversationContractsJsonContext`
  throughout.
- `ConversationRuntimeSession` owns the client-side durable session: start-vs-follow-up choice,
  retained `ConversationId`, a background tail with a fixed 250 ms reconnect and EventId dedupe,
  and the expected-tool-request hand-off — a strict `Participant == Implementer` / non-empty
  request-id-and-name / `ToolExecutorRegistry.CanExecute` check, then the existing
  `ICapabilityDispatcher` path, then a `SubmitToolResultRequest` whose `CommandId` is the new
  deterministic `ConversationDeterministicIds.ClientToolResult(requestId)`.
- `ConversationSessionSlot` makes durable prompt admission, lazy session creation, and
  mission-switch replacement disposal one serialized operation (`SendPromptAsync`), closing a
  real race found during review: a prompt that had already obtained a `ClientRuntimeSession`
  reference just before a replacement disposed it could otherwise still create and run an
  orphaned durable session/tail the store no longer tracked. The same serialization makes two
  concurrent first prompts resolve to exactly one `StartConversationRequest` and one follow-up.
- `ConversationTranscript` is a pure, dependency-free projection from the durable event stream to
  one ordered group-chat model (EventId dedupe, typing→message replace-in-place, contiguous
  same-participant/attempt merge, `Rejected`/`RevisionRequested→Failed` → "not approved", a tool
  row keyed by `ToolRequestId`); `ConversationTranscriptView` renders it. Janus is now a picker
  entry (`SessionRuntimeKind.DurableConversation`); ChatGPT/Websearch keep their existing
  `MissionRuntimeSession`/`CloudMissionRuntimeSession` paths and turn renderer unchanged.
- A dedicated `ConversationRelayJsonContext` (mirroring `ConversationContractsJsonContext`'s
  camelCase/string-enum options) serializes the local `ClientRuntimeEvent` envelope and its
  embedded `ConversationEvent` payload — no runtime-constructed `JsonSerializerOptions` or
  resolver chain, and no change to any other Client Runtime type's existing wire shape.

## Verification

- `dotnet build src/ForgeMission.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test src/ForgeMission.slnx --no-restore`: passed with 0 failures across all five test
  projects — 688 total, 677 passed, 11 skipped (the pre-existing xAI-credit-exhaustion skips,
  issue #7, unrelated to this task).
- Named regression coverage: `ConversationRuntimeSessionTests` (start/follow-up with capability
  declarations, real-shaped multi-frame SSE parsing/relay, reconnect-from-last-sequence with no
  duplicate relay or re-execution, expected-tool-request → dispatcher → stable
  `ClientToolResult` command id, unsupported-tool → error without executing, `DisposeAsync`
  cancels an in-flight dispatch with no further execution/post afterward);
  `ConversationSessionSlotTests` (a prompt that obtained an already-replaced session is rejected
  with no Host call or created session; two concurrent first prompts produce exactly one Start
  and one follow-up with one retained `ConversationId`; concurrent/repeated `DisposeAsync` is
  idempotent and disposes the underlying session only once); `ConversationTranscriptTests` and
  `ConversationTranscriptViewTests` (projection/rendering correctness, incl. no duplicate
  bubble/tool row on a twice-applied event); the extended
  `ClientFacingProjects_DoNotNameConversationHostOrleansOrAzureSdk` boundary theory (now also
  covering `ForgeMission.ClientRuntime.Transport`).

## Lifecycle race found and fixed during review

The first submitted implementation exposed `ConversationSessionSlot.GetOrCreate` and
`ConversationRuntimeSession.SendAsync` as two separate steps called from the `/transport/prompt`
endpoint. Review found this let a prompt that had already completed
`ClientRuntimeSessionStore.TryGet` — but not yet called `GetOrCreate` — run after a concurrent
mission-switch replacement had already disposed that session's (still-empty) slot: `GetOrCreate`
would then lazily create and start a fresh `ConversationRuntimeSession` the store no longer
tracked and would never dispose, free to execute a local tool after the user had switched away.
The same missing serialization meant two concurrent first prompts could both observe a null
`ConversationId` and each start their own conversation.

The fix collapses admission, lazy creation, and `SendAsync` into
`ConversationSessionSlot.SendPromptAsync`, the slot's only entry point, serialized by one
`SemaphoreSlim` shared with `DisposeAsync`. Whichever of a prompt or a replacement's disposal
reaches the gate first now fully determines the outcome for the other: if disposal wins, it
closes the slot before the prompt is admitted and the prompt is rejected outright (no Host call,
no session, no tail); if the prompt wins, disposal blocks on the same gate until that admitted
call returns, then disposes whatever session it just created. `DisposeAsync` is idempotent
(guarded by the same closed flag), so a second or concurrent call is a safe no-op. Three new
tests reproduce and lock in this fix — see `ConversationSessionSlotTests` above.
