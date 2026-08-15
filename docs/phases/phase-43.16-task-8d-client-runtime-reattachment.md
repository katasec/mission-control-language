# Phase 43.16 Task 8d — Durable Client Runtime reattachment and tool-result recovery

> **Status: implementation complete, awaiting merge (2026-08-15).** Prerequisite to Task 8c's last
> observation (see
> [phase-43.16-task-8c-poison-progress-containment.md](phase-43.16-task-8c-poison-progress-containment.md#kind-rollout-and-live-recovery--done-partially-2026-08-15))
> and, longer term, to reopening any Desktop conversation across a restart. MCL only — no
> forge-infra changes.

## The finding that opened this task

Task 8c's live recovery observation required conversation
`173da2e0-248e-5637-ac1b-4c8fea4ad05a` — stuck since an earlier controlled Host interruption test —
to advance once the poison message blocking it was cleared. It did not. Live diagnosis (GET
`/conversations/{id}` against the freshly-rolled-out Host, repeated over 20 minutes with zero grain
log activity) confirmed the conversation was simply idle: its own local Client Runtime process
believed it had already reported the pending `Write` tool result and had nothing queued to resend.
Attempting to reconnect the original (still-running) Client Runtime process's own browser tab to
observe this live surfaced two further, structural facts, not implementation bugs to patch:

1. There is no route or backend client method anywhere in the Client Runtime that lets a fresh
   session reattach to an *existing* `ConversationId`. `ConversationRuntimeSession` mints a new
   `Guid` on every first prompt; `ConversationHostClient` never called the Host's own
   `GET /conversations/{id}` at all before this task.
2. Tool-result idempotency (`_toolResultCache`, keyed by `RequestId`) was purely in-memory, cleared
   the moment the process exits. Task 7's own spoke already named this exact gap as explicitly
   deferred: "a restart while a tool is executing is an explicitly deferred local-execution
   recovery problem." This task is what resolves it.

Separately, reloading the stale browser tab against the long-idle Client Runtime process hit a
static-asset SRI/hash mismatch. Root-caused, not hypothesized: `ForgeMission.ClientRuntime.
Presentation.dll`'s build output carried a filesystem mtime landing squarely inside a concurrent
`dotnet build`/`dotnet test` of the whole solution (run for Task 8c's own re-verification) against
that project's live output directory, while an independent `dotnet run` Kestrel host from hours
earlier kept serving its now-stale in-memory manifest. Reproduced identically on a brand-new tab,
ruling out browser caching. **Verdict: development-only asset drift** from two independent local
dev-loop processes sharing one mutable `bin`/`obj` directory — not a product risk. Task 8a/8b's
packaged Desktop build publishes once to an immutable, self-contained output and never shares a
live filesystem with a concurrently-rebuilding solution; the Kind-hosted Host/Worker path uses
versioned, SHA-tagged container images, never live-rebuilt in place. No repair proposed or made.

## Locked decisions

1. **Full SSE replay from sequence zero, never seeded from the Host's snapshot.** `AttachAsync`
   calls `GET /conversations/{id}` once, for existence validation and current status only — never
   to seed a replay starting point. `_lastSequence` stays at its default 0, so the Host's own
   existing replay-from-sequence SSE mechanism rebuilds the full browser transcript, and every
   historical `ToolRequested` event replays through the same `HandleToolRequestedAsync` live
   traffic already uses — no separate replay code path.
2. **Four-state durable ledger, not a boolean cache.** `ConversationToolResultLedger`, per
   `(ConversationId, RequestId)`: `Started` (written before the executor runs) -> `Executed` (tool
   finished, result held) -> `Acknowledged` (a matching `ToolResult` event was observed, live or
   replayed). On a replayed `ToolRequested`: `Executed`-not-`Acknowledged` resubmits the held
   result via the *existing* deterministic `ClientToolResult(requestId)` CommandId — safe
   unconditionally, since Host command handling is already idempotent (at-least-once, peek-lock).
   `Acknowledged` does nothing. `Started`-only **fails closed**: never re-executes, never
   fabricates a Host result, publishes one local-only fact and stops — no confirmation UI, no
   human-control workflow. This is the one case this task does not attempt to recover
   automatically; the only implemented/proven live path is *tool completed locally, the result
   post was lost, Client Runtime restarted, the result is resent exactly once*.
3. **Session/mission admission, not bare `ConversationId` possession.** Resuming requires the
   caller's already-established `ClientRuntimeSession.Runtime == SessionRuntimeKind.
   DurableConversation` *and* that session's own `MissionRef` to equal the resume record's
   `MissionRef` — a workspace match alone is insufficient. `GET`-shaped intent, `POST`-shaped
   transport: `ResumeCandidatesRequest`/`ResumeConversationRequest` both carry only `SessionId`;
   `WorkspaceRoot`/`MissionRef` are always derived server-side from the looked-up session, never
   accepted from the caller. This is a **UX/discovery constraint on the Client Runtime's own UI,
   not a security boundary** — the Host's `GET /conversations/{id}` and SSE endpoints remain
   unauthenticated (Task 6's already-recorded Type-2 exception, unchanged scope). A caller with
   direct Host access is unaffected by this task.
4. **Resume records persist permanently; no deletion in this task.** `ConversationResumeStore` has
   no `Delete` — a completed conversation's transcript must stay reopenable after any number of
   Desktop restarts. A history/clear action is explicitly out of scope, left for a separate,
   not-yet-designed task.
5. **Sensitive local tool output, minimized exposure window.** A `ToolExecutionResult` can hold
   file or command content. Resume *metadata* (`ConversationId`/`WorkspaceRoot`/`MissionRef`/
   `Status`/`CreatedAtUtc`) never contains it. The ledger's raw `ResultContent`/`ResultIsError`
   persist only while `Executed` and not yet `Acknowledged` — the exact window a resend might be
   needed — under a user-profile application-data directory, outside the git-tracked workspace.
   `MarkAcknowledgedAsync` strips them immediately, retaining only `RequestId`/state/timestamps for
   dedupe. No result content is ever logged. Trust assumption: standard OS file permissions on the
   local user's own profile directory — the same boundary already relied on for reading the
   workspace's own source files. No new identity/credential model, no cloud storage.
6. **Transcript ownership stays with Presentation.** `ConversationRuntimeSession` never
   constructs/resets `ConversationTranscript` — `Home.razor` does, immediately before a successful
   resume call, then rebuilds it purely from replayed `ClientRuntimeEvent`s delivered through the
   same event-loop subscription live events already use.
7. **No new generic transport verb.** `IClientRuntimeChannel` stays POST-only
   (`SendAsync`/`Subscribe`); both new routes (`/transport/resume-candidates`,
   `/transport/resume`) are `MapPost`, matching every existing `/transport/*` route.

## Files

```
src/ForgeMission.ClientRuntime/Services/
  ConversationResumeStore.cs          (new)
  ConversationToolResultLedger.cs     (new)
  ConversationRuntimeSession.cs       (edit — AttachAsync, four-state ledger branch, +3 ctor params)
  ConversationHostClient.cs           (edit — GetConversationAsync, GetAsync helper)
src/ForgeMission.ClientRuntime/Transport/
  ClientRuntimeSessionStore.cs        (edit — AttachExistingConversationAsync,
                                        GetResumeCandidatesAsync, ResumeConversationAsync)
  ClientRuntimeEndpoints.cs           (edit — /transport/resume-candidates, /transport/resume)
src/ForgeMission.ClientRuntime/Program.cs (edit — DI registration)
src/ForgeMission.ClientRuntime.Transport/
  ClientRuntimeContracts.cs           (edit — ResumeCandidate/*Request/*Response, corrected project)
  ClientRuntimeJsonContext.cs         (edit — source-gen registration for every new type)
  HttpClientRuntimeChannel.cs         (edit — route switch)
src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor (edit — resume banner, reset-then-rebuild)

src/ForgeMission.Tests/ClientRuntime/
  ConversationResumeStoreTests.cs             (new)
  ConversationToolResultLedgerTests.cs         (new)
  ConversationRuntimeSessionTests.cs           (edit — AttachAsync + ledger-branch tests)
  ConversationSessionSlotTests.cs              (edit — AttachExistingConversationAsync tests)
  ClientRuntimeSessionStoreTests.cs            (edit — resume-candidates/resume admission tests)
src/ForgeMission.Tests/Presentation/
  HomeResumeTests.cs                           (new, bUnit, fake IClientRuntimeChannel)
```

No forge-infra changes. No Host wire-contract changes — `GET /conversations/{id}` and the
sequence-scoped SSE replay already existed and already did everything needed; only a new Client
Runtime-side caller (`ConversationHostClient.GetConversationAsync`) was added.

## Tests

**`ConversationResumeStoreTests`** — round-trip; a session for workspace A never sees workspace B's
or mission M's records; a terminal conversation's record is still returned (no deletion); repeated
`UpsertAsync` preserves the original `CreatedAtUtc` while still updating `Status`.

**`ConversationToolResultLedgerTests`** — state transitions `absent -> Started -> Executed ->
Acknowledged`; `MarkAcknowledgedAsync` clears `ResultContent`/`ResultIsError` while the entry stays
queryable as `Acknowledged`; entries for different `ConversationId`s never collide.

**`ConversationRuntimeSessionTests`** (extended) — `AttachAsync` validates existence via GET and
replays from sequence zero, never the snapshot's `LastSequence`; an unknown `ConversationId` throws
and never starts a tail. Four ledger-branch tests: `Executed`-not-`Acknowledged` resubmits the held
result without invoking the tool executor (asserted via the existing counting-dispatcher fixture);
`Acknowledged` neither executes nor resubmits; `Started`-only fails closed (no execution, no
fabricated Host POST, one local `Error` `ClientRuntimeEvent` published).

**`ConversationSessionSlotTests`** (extended) — `AttachExistingConversationAsync` on a closed slot
is rejected with no Host call and no created session (mirrors the existing replaced-session test);
on a slot that already has a session, rejected; on a fresh slot, succeeds and never calls
`SendAsync`/`StartAsync`/`SubmitCommandAsync` (asserted via an empty `PostBodies`).

**`ClientRuntimeSessionStoreTests`** (extended) — `GetResumeCandidatesAsync` returns empty for a
non-`DurableConversation` session; returns only records matching both the session's `WorkspaceRoot`
and `MissionRef`. `ResumeConversationAsync` for a record scoped to a different workspace returns
`null` and never even calls the session factory (an `UnreachableHandler` proves the Host is never
touched); for a known, in-scope record, attaches and returns the Host's live status.

**`HomeResumeTests`** (new, bUnit `BunitContext`, a fake `IClientRuntimeChannel` — never a real HTTP
call) — the resume banner shows candidates the fake channel returns, only after a Janus workspace
setup; clicking a candidate sends `ResumeConversationRequest` with the exact session/conversation
IDs; the transcript resets to empty synchronously on click, before any replayed event lands;
replayed `ClientRuntimeEvent`s (published through the fake channel's own `Subscribe` stream — the
same path live events use) rebuild it.

`dotnet build src/ForgeMission.slnx --no-restore`: 0 warnings, 0 errors. `dotnet test
src/ForgeMission.slnx --no-restore`: 748 passed, 0 failed (721 prior baseline + 27 new).

## Security-architecture gate

- **Owner**: Client Runtime owns the new local resume/ledger state; the Conversation service
  remains sole owner of durable event history — no new data owner introduced.
- **Public entry point**: none. Both new routes are on the existing `127.0.0.1`-only
  `/transport/*` surface, never cluster- or internet-facing.
- **Tier**: unchanged — Tier-3 local dev tool.
- **Tier-3 stores**: local filesystem only, under the user's own profile directory. No new network
  transport, no new cloud storage.
- **Cross-context access**: none — Client Runtime still never touches Table/Blob directly; still
  only calls the Host's existing HTTP/SSE surface.
- **Secrets/credentials**: none added. Resume records hold a `ConversationId` (a routing address,
  not a secret) plus non-sensitive metadata. Raw tool-result content is the one sensitive payload
  this task handles, and its retention window is minimized (decision 5) rather than avoided
  entirely, since a resend genuinely needs it until acknowledged.
- **Type 1 or Type 2**: Type 2 — the *same* exception already recorded for Task 6 (local adapter,
  fixed `dev` tenant, never authenticates), not widened by this task. Removal condition identical:
  once a Tier-1 ForgeAPI/ForgeUI adapter authenticates and passes server-trusted identity, the Host
  stops trusting a bare `ConversationId` and this local session/mission gate is superseded (kept
  only as a UX cache, or removed).

## Engineering-philosophy gate

- **Unrelated work**: none — scoped entirely to Client Runtime's session/tool-result lifecycle,
  zero Host/Worker touch.
- **New knobs**: two new stores, both directly required by the stated idempotency/reattach
  requirements — no config flags, no feature toggle. Resume is structurally unreachable without an
  existing local record for the caller's exact workspace+mission.
- **Structural vs. warning**: idempotency gates the `ExecuteToolAsync` call site itself (a ledger
  lookup before the call, not a log line after); the session/mission admission rule gates candidate
  enumeration and the resume action itself, independently re-validated server-side.
- **Scattered dependency access**: local file I/O lives only inside the two new store classes.
- **Implicit consequential behavior**: none — a `Started`-only (interrupted mid-action) tool call
  is never silently retried; it fails closed with one visible local fact.
- **Discoverability**: `AttachAsync`/`AttachExistingConversationAsync` are named entry points
  parallel to the existing `SendAsync`/`SendPromptAsync`, discoverable the same way.
- **Proof of success**: the tests above, plus the live observations named below.

## Live verification (after merge/deploy — not part of this task's own Done-when)

Deferred to a separate, later, explicitly Codex-authorized Kind rollout + live check, mirroring
Task 8c's own rollout discipline:

1. Kill a live Client Runtime process with a tool result `Executed` locally but its report to the
   Host lost. Relaunch, open the same workspace/mission. Browser shows the resume candidate.
   Resume. GET confirms existence. Transcript resets and replays from sequence zero (screenshot).
   The replayed `ToolRequested` resubmits the held result via its deterministic CommandId without
   re-executing the tool (no duplicate local side effect — e.g. no second file-write timestamp).
   Conversation advances past its previously-stuck point. Host's durable event log shows exactly
   one `ToolResult` event for that `RequestId`.
2. A second, unrelated workspace/mission session's resume-candidates list never includes the
   first's conversation (UX-correctness check, not a security claim — see decision 3).
3. Task 8c's fifth observation, via a freshly-scripted equivalent repro (decision matches Task 8c's
   own corrected wording) — a new conversation deliberately interrupted the same way, recovered
   through this resume flow.

## Done when

- All named tests pass; full-solution build/test clean (confirmed above).
- This spoke's locked decisions match the shipped code exactly.
- PR opened, MCL only, docs + code together — this task does not merge itself.
- Live verification (above) remains explicitly deferred to a separate authorization; not required
  for this task's own Done.
