# Phase 43.22 task 3 — Runtime application completion record

Verified 2026-09-05. The active contracts and design remain in
[Mission application and read session](phase-43.22-runtime-application.md).

## Delivered boundary

`ProjectMissionApplication` coordinates a single immutable submission through the manifest and
existing Host client. It prepares before network work, persists only matched receipts, reconciles
a lost response by command ID, and never owns UI state, a provider, or tool capability.

`ProjectMissionReadSession` owns one session-bounded tail, Host read windows and lightweight
invalidations. It has a bounded fallback refresh, does not retain historical trace bodies, and
is disposed with its session. `ProjectMissionToolRefusal` is the only tool path; it receives no
workspace or dispatcher and sends the stable refusal through the existing Host route.

## Failure and complexity review

| Failure | Owner and observed result | Recovery / negative evidence |
|---|---|---|
| Lost Host acceptance response | Application retains `Prepared` and returns `SubmissionUncertain`. | Explicit Retry reconciles the same command without another start. |
| Definitive Host rejection | Application stores a terminal rejection with its mapped stable code. | It never replaces immutable input or selection. |
| Host unavailable before a tail connects | Read session returns `HistoryUnavailable` metadata. | It disposes the failed tail; a later state read establishes a new subscription. |
| Tail gap or failed refusal hook | Tail publishes an error and retains the last sequence. | Reconnect/replay retries the same event; no event-ID history is retained. |
| Unexpected tool request | Tool refusal owns the stable idempotent result. | No filesystem or terminal dispatcher is reachable. |

Parent review returned the initial implementation for missing refresh/subscription behavior, visible
tail failures and a failed-readiness recovery path. The accepted revision separates these concerns
into the application, read session and tail reader. Added production functions were reviewed
against the complexity gate and retain one ownership boundary each; no exception was recorded.

## Verification

| Check | Observation |
|---|---|
| Runtime feature and compatibility tests | `dotnet test src/ForgeMission.Tests/ForgeMission.Tests.csproj --no-restore --filter "FullyQualifiedName~ProjectMissionApplicationTests|FullyQualifiedName~ProjectMissionReadSessionTests|FullyQualifiedName~ConversationTailReaderTests|FullyQualifiedName~ProjectMissionToolRefusalTests|FullyQualifiedName~ProjectRunReadStateTests|FullyQualifiedName~ConversationRuntimeSessionTests|FullyQualifiedName~ConversationSessionSlotTests|FullyQualifiedName~ProjectMissionControlSessionTests|FullyQualifiedName~ConversationHostClientProjectTests"` — **33 passed, 0 failed**. |
| Repository build | `dotnet build src/ForgeMission.slnx --no-restore` — **0 warnings, 0 errors**. |
| Diff hygiene | `git diff --check` passed. |

Default-path Desktop acceptance remains task 5's aggregate gate.
