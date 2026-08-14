# Phase 43.16 Task 8c — Poison-progress containment

> **Status: implementation in progress (2026-08-15).** Prerequisite to
> [Task 8's live product proof](phase-43.16-janus-desktop-local-poc.md#8-product-proof-and-evidence):
> corrects a real defect discovered during Task 8's rerun (after
> [Task 8b](phase-43.16-task-8b-janus-one-tool-per-turn.md) landed), where an orphaned raw
> `kind-verifier-*` probe message on the shared `conversation-progress` queue permanently starved a
> perfectly healthy conversation. Kept explicitly separate from Task 8's evidence-only run, the same
> way Task 8a and Task 8b were.

## The finding that opened this task

Task 8's rerun (after Task 8b) validly reproduced observations #1-#4 for real — a genuine
revision-then-approval cycle, and, new since Task 8b, multiple sequential Implementer tool
hand-offs completing a real multi-file plan (`rate_limiter.py`, `app.py`) with zero "exactly one
tool call" errors. Observation #5 (client-side disconnect/replay) was rigorously proven: 26
`toolRequested`/26 `toolResult` events, 1:1 matched, zero duplicates; the DOM-rendered row count
matched the raw event log exactly after a local port-forward outage.

Observation #6 (controlled Host interruption) is where it stopped. Two controlled
`conversation-host` pod deletions were performed, timed against the live SSE stream to catch a
provider call in flight. The first recovered cleanly to `waitingForTool` (the provider call had
already completed before the pod actually terminated). The second left conversation
`173da2e0-248e-5637-ac1b-4c8fea4ad05a` permanently stuck at `lastSequence: 139, status:
waitingForTool` — no new events, ever again, without intervention.

Root cause, found via a bounded, non-locking Service Bus peek (no receive/lock/complete/settle of
any kind):

    fail: ForgeMission.ConversationHost.Messaging.ConversationProgressConsumer[0]
          Unhandled failure processing progress message '71fcc8b5-29e3-4551-a3ea-40dca4d15695';
          leaving unsettled for broker retry.
    System.Text.Json.JsonException: 'k' is an invalid start of a value.
      at ConversationProgressHandler.HandleAsync(...) in ConversationProgressHandler.cs:line 26

The message is a raw, non-JSON body — confirmed by SHA-256 digest and byte length (50 bytes,
matching `"kind-verifier-" + a 36-character UUID` exactly) — from `forge-infra`'s Kind verifier
(`kind-up.sh`/`verify.py`). Its SessionId matches the `kind-verifier-<uuid>` pattern, provably
different from any real ConversationId (`ConversationId:N` — 32 lowercase hex chars, no prefix, no
dashes — is the only valid SessionId format for a real `ConversationProgress` message, per
`ConversationProgressEnvelopeValidator`). Enqueued at 11:20:12Z, well before this session's Task 8
rerun (goal submitted 11:34:17Z) — a leftover from an earlier, already-self-healing-looking
verifier retry (`progress_receive_roundtrip: FAIL (SessionCannotBeLockedError)` on attempt 1/3,
recorded without incident in Task 8b's own rollout evidence).

## Why a structurally healthy conversation can be starved by an unrelated message

`ConversationProgressConsumer.cs:26-32` configures `MaxConcurrentSessions = 1,
MaxConcurrentCallsPerSession = 1` — exactly one Service Bus session processed at a time, matching
"Janus v1 has exactly one active run per conversation" (correct *within* one conversation's own
session). It does not account for a **different** session — entirely unrelated to any real
conversation — poisoning the single shared processing slot. `ConversationProgressHandler.
HandleAsync` deserializes the message body (`ConversationProgressHandler.cs:26`) *before* any
envelope/tenant validation ever runs, so a body that isn't even valid JSON throws immediately,
outside any of the "structurally invalid, safe to discard" handling the class already had for
*validated-but-mismatched* envelopes. The exception propagates to
`ConversationProgressConsumer.ProcessMessageAsync`'s catch block, which logs and leaves the message
unsettled — retried forever, in the same locked session, which — with `MaxConcurrentSessions=1` —
means no other session, including a perfectly healthy real conversation's, ever gets a turn.

The identical bug shape exists on the Worker side: `AzureServiceBusMissionCommandConsumer.
ProcessCommandAsync` (mission-command queue) has the same inline deserialize-then-throw-and-
leave-unsettled pattern, with the same `MaxConcurrentSessions=1` blast radius, for a
`kind-verifier-*` body landing on `mission-command` instead.

## Locked decisions

1. **Classify before any side effect, on both queues.** Both consumers gain a shared-per-project
   (never shared between Host and Worker) classification step: invalid JSON, null JSON, missing
   `tenant_id`, and any envelope (SessionId/MessageId) mismatch are unaddressable poison input —
   completed with no grain call, no session-state load, no processor invocation, no publisher call,
   and no retry. A message that classifies as addressable is unchanged from today's behavior:
   normal processing, normal retry-then-DLQ semantics for a genuine post-classification failure.
2. **`MaxConcurrentSessions` stays 1 on both consumers.** The fix is correct poison
   classification, not dilution of the one-conversation/one-run ordering boundary Janus v1 relies
   on.
3. **Explicit outcome, not string-overloaded rejection.** The Host's
   `ConversationProgressHandlingResult` becomes `(Outcome: Applied | Rejected | Discarded, Reason)`
   — a grain-level rejection and a poison discard are never conflated into the same ambiguous
   field, and both are logged distinctly.
4. **Fixed-category log reasons only — never interpolated identifiers.** Both envelope validators
   drop their free-text `FailureReason` (confirmed dead after this task: no remaining call site
   reads it) in favor of a closed category enum
   (`MissingTenantProperty`/`SessionIdMismatch`/`MessageIdMismatch`), extended by each classifier
   with the two pre-envelope categories (`InvalidJson`/`NullBody`). Every log line from this path
   uses only the fixed category name plus the broker `MessageId` for operational correlation —
   never message body text, `SessionId`, `tenant_id`, or any other property value.
5. **Host DLQ terminal-fact outcomes stay independently visible.** The extracted
   `ConversationProgressDeadLetterHandler`'s result names the Error fact's and the Failed fact's
   grain outcomes separately — a rejected terminal fact is never folded into a blanket "Applied."
6. **Verifier isolation is role-separated exact-session draining, not a shared queue-wide gate.**
   No new queue, credential, RBAC role, or Manage right. No third verifier Job holding both roles'
   credentials — each existing role drains only its own exact pinned probe session, on its own
   queue, with its own already-granted credential. The hard proof of an empty session is the
   drain's own receive-until-empty loop while quiesced — never a separate non-locking peek claiming
   to prove exclusivity a peek cannot actually guarantee. An optional, informational, non-blocking
   sweep may report counts of `kind-verifier-*`-pattern SessionIds only — never inspects or acts on
   a real conversation's session, never logs a body or tenant value.
7. **Application Deployments are quiesced for the verifier window.** `conversation-host` and
   `mission-worker` are scaled to 0 and confirmed gone (or already absent, on a first-ever cluster)
   before either verifier role runs — removing the actual race this incident's timeline points to
   (a live `NEXT_AVAILABLE_SESSION` consumer competing with the verifier's pinned-session receiver
   for the same queue). Deployments are restored to exactly one replica each only after both roles'
   own-session drains succeed; on any failure within a bounded retry/time budget, both stay at 0
   and the script exits non-zero — deliberately disruptive for the local Kind target, and
   documented as such, in preference to silently letting verifier traffic reach a live queue again.

## Files

mission-control-language:

    src/ForgeMission.ConversationHost/Messaging/
      ConversationProgressMessageClassifier.cs   (new)
      ConversationProgressHandler.cs             (edit)
      ConversationProgressDeadLetterHandler.cs   (new)
      ConversationProgressDeadLetterConsumer.cs  (edit — thin SDK wrapper)
      ConversationProgressConsumer.cs            (edit — distinct outcome logging)
      ConversationProgressEnvelopeValidator.cs   (edit — category enum, drops free-text reason)
    src/ForgeMission.ConversationHost.Tests/
      ConversationProgressEnvelopeValidatorTests.cs   (edit)
      ConversationProgressMessageClassifierTests.cs   (new)
      ConversationProgressHandlerTests.cs              (new)
      ConversationProgressDeadLetterHandlerTests.cs    (new)

    src/ForgeMission.ConversationWorker/
      Properties/AssemblyInfo.cs                       (new)
      Messaging/
        ConversationCommandMessageClassifier.cs        (new)
        ConversationCommandDeadLetterHandler.cs         (new)
        AzureServiceBusMissionCommandConsumer.cs        (edit)
        ConversationCommandEnvelopeValidator.cs         (edit — category enum)
    src/ForgeMission.ConversationWorker.Tests/
      ConversationCommandEnvelopeValidatorTests.cs      (edit)
      ConversationCommandMessageClassifierTests.cs      (new)
      ConversationCommandDeadLetterHandlerTests.cs      (new)
      AzureServiceBusMissionCommandConsumerCoreTests.cs (new)

forge-infra:

    dev/350-conversation-data/kind/verifier/verify.py       (edit)
    dev/350-conversation-data/kind/verifier/test_drain.py   (new)
    dev/350-conversation-data/scripts/kind-up.sh             (edit)
    dev/350-conversation-data/README.md                       (edit)

No Bicep changes. No new queue, credential, RBAC role, or Manage right anywhere.

## Tests

**Host:** `ConversationProgressMessageClassifierTests` (pure, synthetic
`ServiceBusModelFactory.ServiceBusReceivedMessage` instances — invalid JSON, null JSON, missing
tenant, SessionId mismatch, MessageId mismatch, valid envelope). `ConversationProgressHandlerTests`
(a throwing `IGrainFactory` double proves each poison shape never reaches a grain call; a real
Azurite-backed grain proves addressable Applied/Rejected outcomes are unchanged; a transient
post-classification failure still propagates for broker retry, unchanged). `ConversationProgress
DeadLetterHandlerTests` (throwing-grain poison coverage; Azurite-backed happy path proving
Error-then-Failed facts land in order; a scripted grain double proving a rejected Error fact still
lets the Failed fact attempt run, both outcomes independently visible). `ConversationProgress
EnvelopeValidatorTests` updated to assert the new `.Failure` category enum, not free text.

**Worker:** `ConversationCommandMessageClassifierTests` (mirrors the Host's, for
`ConversationCommand`). `ConversationCommandDeadLetterHandlerTests` (a fake
`IConversationProgressPublisher` records calls — poison shapes assert zero publishes; addressable
shape asserts Error-then-Failed publishes in order). `AzureServiceBusMissionCommandConsumerCoreTests`
(via the new `Properties/AssemblyInfo.cs` friend declaration, calls the internal
`ProcessCommandCoreAsync` directly with a `loadSessionAsync` delegate that throws if ever invoked —
proving structurally that poison input short-circuits before any session-state touch; a valid shape
proves `MissionCommandProcessor` still runs, reusing the existing `FakeExpertRunner` fixture).
`ConversationCommandEnvelopeValidatorTests` updated the same way as the Host's.

**forge-infra:** `test_drain.py` — deterministic, `unittest.mock`-based, no real broker: drain
succeeds on an already-matching message; drain retries then succeeds after a transient receiver
failure (the actual finally/retry path, exercised for real, not just present in source); drain
reports success on an already-empty session; drain fails closed (does not complete) on an
unexpected body; drain reports failure after exhausting its bounded retry budget. No test sends
real poison to a real application queue.

`dotnet build src/ForgeMission.slnx` / `dotnet test src/ForgeMission.slnx` clean, same bar as every
prior task. `python3 -m unittest dev/350-conversation-data/kind/verifier/test_drain.py` clean.

## Kind rollout and live recovery (after code review/merge only)

Both repos' PRs reviewed and merged first — no `make 350-conversation-kind-up`, no message
settlement, and no Task 8 rerun performed as part of this task's own implementation. Once
authorized as a separate step: `make 350-conversation-kind-up` from a clean `main` checkout
exercises the full new quiesce -> verify -> restore sequence for real. Evidence to capture:

- both Deployments observed scaled to 0 and pods gone before the verifier Jobs run;
- each role's own-session drain succeeding (its receive-until-empty loop terminating clean);
- both Deployments restored to exactly one replica, `kubectl rollout status` succeeding;
- the specific stranded message `71fcc8b5-29e3-4551-a3ea-40dca4d15695` being safely discarded by
  the *deployed Host's* new classification behavior — a log line naming it exactly once, not the
  prior ten-times-and-counting retry loop, and no manual broker settlement of any kind;
- conversation `173da2e0-248e-5637-ac1b-4c8fea4ad05a` confirmed advancing past `lastSequence: 139`
  with no duplicate action taken on it.

This rollout and Task 8's own full live-proof rerun are both separate, later, explicitly
Codex-reauthorized steps — not part of this task's own Done-when.

## Done when

- Both classifiers implemented exactly as scoped; `MaxConcurrentSessions` unchanged at 1 on both
  consumers; no Host<->Worker cross-reference; all log lines use fixed categories only.
- All named tests (both repos) pass; `dotnet build`/`dotnet test` and
  `python3 -m unittest test_drain.py` all clean.
- forge-infra's role-separated exact-session draining and quiesce-before-probe sequencing
  implemented per the locked decisions above; README documents the new local-Kind downtime
  behavior.
- Both repos' PRs opened (this task does not merge them).
