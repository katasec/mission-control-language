# Phase 57 — `forge chat` live stream survives idle

> **Status: ✅ complete 2026-10-02.** Bug resolved (Ameer). Evidence: [completed record](phase-57-chat-idle-stream_completed.md). Origin: found in [Phase 56](phase-56-tui-graphics.md)
> Task 2 acceptance. Pre-existing on forge-mcl `main` `cc998ac`.

**Goal:** a `forge chat` window left idle for any length of time still shows new replies, with no
restart.

## What goes wrong

```mermaid
sequenceDiagram
  participant C as forge chat (client)
  participant A as ForgeAPI
  participant H as Conversation Host
  C->>A: GET events?after=N (live stream)
  A->>H: same request
  Note over H: StartAsync, but headers stay unsent until the first event
  Note over C: no new events for 100 s
  C--xA: HttpClient gives up waiting for headers (100 s)
  Note over C: TaskCanceledException(TimeoutException) escapes the reconnect loop; the stream is dead and nothing shows
```

## Evidence (read-only investigation 2026-10-01 – 2026-10-02)

Scratchpad paths below were session-local and are not kept; the facts are recorded here.

| Fact | Source |
|---|---|
| Idle windows stop showing replies; restart shows them all; same on `main` and the Phase 56 branch | pty runs, 180 s and 120 s idle: never; 0 s idle: 5–6 s. Supervisor scratchpad `stall/` |
| The live stream never receives response headers; the client aborts at exactly +100 s. The replay stream (which has events) gets headers in 0.1 s. | Logging proxy between `forge` and the real ForgeAPI, scratchpad `prx/` |
| The Host calls `response.StartAsync` and relies on it to send headers; Kestrel sends them only on the first flush, which happens in `WriteEventAsync` | forge-conversations `ConversationSseWriter.cs:58`, `:139`; Kestrel probe, scratchpad `kst/` |
| ForgeAPI flushes its own headers only after the Host's arrive | forge-platform `WireProxy.cs:78-83`, `ConversationHostQueries.cs:30` |
| `HttpClient.Timeout` covers only the wait for headers; reading the body after `ResponseHeadersRead` is not timed | scratchpad `tmo/`, `tmo2/` |
| The reconnect loop retries only `HttpRequestException`/`IOException`; a header timeout is `TaskCanceledException` with an inner `TimeoutException` | forge-mcl `ForgeChat.cs:405` |
| Nothing shows on screen because a method that throws `OperationCanceledException` ends **Canceled**, and the TUI checks only `IsFaulted` | forge-mcl `Tui/ChatTui.cs:88`, `:145-155` |
| Azure Container Apps ingress drops a connection after 240 s idle; with headers flushed, that cut arrives as EOF/`IOException`, which already reconnects | [Ingress overview](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview) |

## Decisions

| # | Decision | Why |
|---|---|---|
| S1 | **The Host flushes headers as soon as the stream opens**: `await response.Body.FlushAsync(ct)` right after `StartAsync`, and the misleading comment fixed. | The root cause; one line at the owner. |
| S2 | **The client treats a cancellation that isn't the session's as a lost connection**: `OperationCanceledException when !ct.IsCancellationRequested` reconnects through the existing path and shows the existing `connection lost; reconnecting` line. Ctrl-D still exits quietly. | Any future silent stall becomes visible and recovers, instead of killing the stream. |
| S2b | **Line mode stops visibly.** It has no reconnect (`lost: null`), so a lost connection (`OperationCanceledException` not from the session, or `IOException`) ends with `chat failed: connection lost (<reason>)` on stderr and exit 1, not a stack trace. Ctrl-C still cancels the turn (exit 130). (Task 2 plan review) | Today these crash past `RunAsync`. One catch at the owning boundary. |
| S3 | **No heartbeat now.** | With S1, an ingress cut at 240 s reconnects (S2 and the existing `IOException` path). A heartbeat needs a timer interleaved with the event reader; revisit only if reconnect lines become a nuisance ([backlog](../backlog.md)). |
| S5 | **Idle sleep** (Ameer, 2026-10-02). When the live stream ends and **no turn is in flight** (no turn running in the conversation, including another window's, and no pending reply in this window), the TUI does **not** reconnect. It shows `idle — reconnects when you type`. Any key press except Ctrl-D wakes it: first a catch-up read from the saved cursor, then the live stream reopens, so nothing is missed. An Enter that wakes the window waits for the catch-up, then follows the normal send rule (so it can't submit into a turn another window started). `connection lost; reconnecting` shows only for a transport failure during a turn. One owner, `ChatLink`, holds the connection state (testable without a terminal). While a turn is in flight it reconnects at once, as today. | A forgotten open window must not keep ForgeAPI (min replicas 0) awake: today it reconnects every 240 s forever. No heartbeat (S3 stays rejected: it would keep idle windows alive), no server change. Trade-off: a sleeping window shows replies to messages sent from another window only when touched. |
| S4 | **No forge-client or forge-platform change**, and the client timeout stays at the default. | Once headers flush, the 100 s wait never applies to the stream. |

**Gates.** Security: N/A — no new entry point, store, identity or secret; the Host response
changes only in when its headers are sent. Engineering philosophy: the fix is at the owner (Host
writer), plus one failure boundary made visible (S2); no new knob. Failure boundary: a stream that
fails without the user stopping it now shows the reconnect line and resumes; Ctrl-D is unchanged.
Default path: the deployed Host image and `forge` from `make install` on merged forge-mcl `main`,
default endpoint.

## Tasks

| Task | Repo | Done when |
|---|---|---|
| 1 — Host flushes headers (S1). **Done:** forge-conversations [#18](https://github.com/katasec/forge-conversations/pull/18) `918bcc2`; image `forge-conversation-host:0.8.1` (`sha256:c226e259…93fb`), revision `--0000013` Healthy, deployed 2026-10-01 20:51Z via `make 525-conversation-app`; forge-infra [#39](https://github.com/katasec/forge-infra/pull/39) open | forge-conversations, then forge-infra | A loopback Kestrel test opens `events?after=<last>` on an idle conversation with `ResponseHeadersRead` and a 2 s client timeout and gets 200 (fails before the fix). Build 0 warnings, tests pass. New Host image deployed with `make 525-conversation-app-what-if` then `make 525-conversation-app` (operator approves the deploy). |
| 2 — Client reconnects on a non-session cancellation (S2, S2b). **Done:** forge-mcl [#34](https://github.com/katasec/forge-mcl/pull/34) `209682c`; 5 new tests red→green, suite 530 passed | forge-mcl | Unit tests: a fake stream that throws `TaskCanceledException(new TimeoutException())` reconnects once and reports it; repeated timeouts each reconnect; a session cancel still ends quietly; without a reconnect handler (line mode) the failure reaches `RunAsync`, which prints the S2b message and returns 1. Build 0 warnings, tests pass, AOT 0 IL warnings. |
| 3 — Acceptance. **Done.** 180 s: PASS (supervisor, Ghostty, installed `forge` `209682c`). 300 s: PASS, two timestamped runs: the ingress cuts the quiet stream at 240.0 s (Host log), the client reconnects with the same cursor 1–2.6 s later, and the reply is drawn 3.3 s and 9.5 s after sending. Every stream ends 200, no 499. (A first supervisor run was misread as a failure: escape-stripping placed the progress line after the cursor-addressed reply.) | — |
| 4 — **Idle sleep** (S5). **Done:** forge-mcl [#35](https://github.com/katasec/forge-mcl/pull/35) `4f5e0ce`, see [completed](phase-57-chat-idle-stream_completed.md#task-4)., replacing the earlier "clear the notice" task. Includes: the `connection lost; reconnecting` notice appears only when a reconnect is attempted during a turn, and is removed on the first event after it (`ChatTui.cs:276` adds it and nothing removes it today). | forge-mcl | Unit tests: stream ends with no turn in flight → no reconnect, idle note shown; a key press → reconnect from the cursor, note gone; stream ends during a turn → reconnect at once; Ctrl-D while asleep exits quietly. Live, Ghostty: idle 300 s → idle note; type and send → reply shown; ForgeAPI logs show no stream open while asleep. |

## Next

None — phase complete.
