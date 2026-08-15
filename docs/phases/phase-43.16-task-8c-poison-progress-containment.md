# Phase 43.16 Task 8c — Poison-progress containment

> **Status: ✅ Done — merged and deployed 2026-08-15.**
> Prerequisite to
> [Task 8's core product proof](phase-43.16-janus-desktop-local-poc.md#8-core-product-proof--done-2026-08-16):
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
8. **Post-attempt cleanup barrier — the immediate per-role drain (decision 6) is first-line
   cleanup, not the full guarantee.** The Worker and Conversation-service round-trip Jobs run
   concurrently as two separate Kubernetes Jobs; the service Job's own immediate drain can
   complete *before* the Worker Job — still running — has processed an ambiguously-sent command
   and emitted its progress probe. A closing barrier runs after **every** attempt (success or
   failure alike): both original round-trip Jobs are waited to terminal state and deleted first
   (so neither can send anything more), then two new, role-isolated, cleanup-only Jobs
   (`verifier-conversation-service-cleanup-job.yaml`, `verifier-worker-cleanup-job.yaml`) each
   drain only their own queue with their own role's Secret — no send, no Storage, no cross-role
   credential, no third combined-credential Job. Both cleanup Jobs must pass before the attempt can
   be retried (fresh probe ID) or the Deployments restored; if either fails, `kind-up` stops
   immediately — no retry, both Deployments stay at 0, non-zero exit — since a failed cleanup means
   the queues' state can no longer be proven at all.

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

    dev/350-conversation-data/kind/verifier/verify.py                    (edit)
    dev/350-conversation-data/kind/verifier/test_drain.py                (new)
    dev/350-conversation-data/kind/verifier/test_service_roundtrip.py    (new)
    dev/350-conversation-data/kind/verifier/test_cleanup.py              (new)
    dev/350-conversation-data/kind/verifier-conversation-service-cleanup-job.yaml (new)
    dev/350-conversation-data/kind/verifier-worker-cleanup-job.yaml      (new)
    dev/350-conversation-data/scripts/kind-up.sh                         (edit)
    dev/350-conversation-data/README.md                                  (edit)

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
unexpected body; drain reports failure after exhausting its bounded retry budget. `test_service_
roundtrip.py` — fake-callable coverage proving the service role's drain always runs, even when
`send_command` raises after an ambiguous send (the exact shape the incident needs closed);
receive-only-attempted-after-a-successful-send; a passing round trip still fails the probe if the
drain alone fails. `test_cleanup.py` — fake-`DrainOutcome` coverage of `run_cleanup_role`, the
control flow shared by both post-attempt cleanup-only roles. No test sends real poison to a real
application queue.

A separate, throwaway bash sequencing simulation (fake `kubectl`/`kind`/`docker`/`az` on `PATH`,
scenario-controlled per-Job wait exit codes, real `kind-up.sh` truncated just before the
application-image section and run for real) proved three orderings against the actual script, not
a reimplementation of it: (A) a fully-passing attempt still runs the post-attempt cleanup barrier
before `probe_succeeded=1`; (B) either cleanup Job failing stops the script immediately with no
retry and no second probe ConfigMap; (C) a failed original round trip still runs the barrier, and
retries with a fresh probe ID once the barrier passes. This simulation is not checked into the
repo — its throwaway harness lives outside forge-infra and is not part of this task's shipped
artifact — but it is what caught a real bug during this correction: `run_cleanup_barrier`
re-enabling `set -e` internally before its own `return` statement silently killed the whole script
via `errexit`, undetected by `bash -n` or by manual reading, only surfaced once the simulation
actually exercised the failure path.

`dotnet build src/ForgeMission.slnx` / `dotnet test src/ForgeMission.slnx` clean, same bar as every
prior task. `python3 -m unittest test_drain test_service_roundtrip test_cleanup` (run from
`dev/350-conversation-data/kind/verifier/`) clean. `bash -n dev/350-conversation-data/scripts/
kind-up.sh` clean.

## Kind rollout and historical incident — verified (2026-08-15)

Both implementation PRs merged first: MCL at
`5afa2cc1d029b1d6adb8ddde25d5636a10bc84bf` and forge-infra at
`c00dd89f5dde1519011634e37c55a8558ca31768`. The authorized
`make 350-conversation-kind-up` run from that clean MCL `main` completed with exit code 0:

- both Deployments scaled to zero and their pods disappeared before verifier Jobs ran;
- verifier attempt 1/3 passed, including `service-cleanup` and `worker-cleanup`
  `probe_session_drain: PASS (drained 0)`;
- both Deployments returned to one replica on
  `forge-conversation-{host,worker}:5afa2cc...`, with zero restarts; and
- the deployed Host discarded poison message
  `71fcc8b5-29e3-4551-a3ea-40dca4d15695` exactly once as `InvalidJson`, with no manual broker
  settlement and no recurring retry loop.

Conversation `173da2e0-248e-5637-ac1b-4c8fea4ad05a` remains a historical stranded artifact at
`lastSequence: 139`, `waitingForTool`. Clearing the unrelated poison message freed the shared
consumer slot; it could not resume this already-idle conversation because no matching tool result
was queued. Advancing it was therefore never a valid Task 8c containment criterion and no recovery
feature is introduced here.

## Done when

Implementation-complete (already satisfied):

- Both classifiers implemented exactly as scoped; `MaxConcurrentSessions` unchanged at 1 on both
  consumers; no Host<->Worker cross-reference; all log lines use fixed categories only.
- All named tests (both repos) pass; `dotnet build`/`dotnet test` and
  `python3 -m unittest test_drain test_service_roundtrip test_cleanup` (run from
  `dev/350-conversation-data/kind/verifier/`) all clean.
- forge-infra's role-separated exact-session draining, quiesce-before-probe sequencing, and
  post-attempt cleanup barrier all implemented per the locked decisions above; README documents
  the new local-Kind downtime behavior and the two-stage (immediate drain + cleanup barrier)
  guarantee.

All Task 8c conditions above are satisfied. The completed Task 8 core product proof is recorded in
the [Phase 43.16 hub](phase-43.16-janus-desktop-local-poc.md#8-core-product-proof--done-2026-08-16).
