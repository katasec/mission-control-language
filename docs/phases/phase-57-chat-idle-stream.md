# Phase 57 — `forge chat` live stream survives idle

> **Status: design locked (2026-10-02); plans next.** Origin: found in [Phase 56](phase-56-tui-graphics.md)
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
| 1 — Host flushes headers (S1) | forge-conversations, then forge-infra | A loopback Kestrel test opens `events?after=<last>` on an idle conversation with `ResponseHeadersRead` and a 2 s client timeout and gets 200 (fails before the fix). Build 0 warnings, tests pass. New Host image deployed with `make 525-conversation-app-what-if` then `make 525-conversation-app` (operator approves the deploy). |
| 2 — Client reconnects on a non-session cancellation (S2) | forge-mcl | Unit tests: a fake stream that throws `TaskCanceledException(new TimeoutException())` reconnects once and reports it; repeated timeouts each reconnect; a session cancel still ends quietly; without a reconnect handler (line mode) the failure reaches `RunAsync`, which prints the S2b message and returns 1. Build 0 warnings, tests pass, AOT 0 IL warnings. |
| 3 — Acceptance | — | Default path, in Ghostty: idle 180 s, then send — the reply appears with no reconnect line. Idle 300 s, then send — at most one reconnect line, and the reply appears. ForgeAPI logs show no 499 at about 100 s. |

## Next

Assign Tasks 1 and 2 (separate repos) to implementing subagents, plan only.
