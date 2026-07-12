# Phase 41.7 — Streaming search progress + timeout hardening

> **Status: Design (spec written, not built) — 2026-07-12.** · **Parent:** [Phase 41 — Live Retrieval](phase-41-live-retrieval-scout.md) ·
> **Depends on:** [41.2](phase-41.2-search-expert-kind.md) (`@grok` live) · **Revisits:** the 39.1 runner
> transport decision (synchronous HTTP → streaming). · **Code style:** all new code follows
> [Progressive Disclosure](../design/code-style.md) (outline-first, small named functions, zero warnings).
>
> **Done when:** a room `@grok` on a search query shows **live progress** ("Classifying… → Searching the
> web… → Answering…", and ideally the sub-search lines) instead of a frozen spinner, and the run **cannot die
> on an idle timeout** — verified end-to-end at `forge.katasec.com`. The progress mechanism is
> **provider-neutral**, so future OpenAI/Claude search backends stream through the same rails unchanged.

## 1. Why — two problems, one fix

Search turns take **~40–60s** (Grok's server-side search loop + classify + grounded answer). Today the
room shows a spinner the whole time, and the run is a **single blocking `POST /run`** (39.1 sync-HTTP). Two
consequences:

1. **UX:** a 40–60s frozen spinner reads as "hung / broken." Grok's own UI avoids this by streaming its
   intermediate steps (the screenshot: *"Searched X: from:… · Searched web: … 2 results"*).
2. **Timeout risk:** one long-held blocking request is exposed to every **idle** timeout in the chain (ACA
   ingress, HttpClient, any proxy). The 4-min `MissionRunnerClient` ceiling gives headroom *today*, but a
   frozen connection is fragile and a longer search (multi sub-search + slow answer) narrows it.

**Streaming fixes both:** progress events flow to the room as they happen (UX), and continuous bytes on the
runner→orchestrator connection keep it active so it can't be reaped on idle (timeout). It does **not** need
gRPC — plain **HTTP streaming (NDJSON/SSE / `IAsyncEnumerable`)** over the existing HTTP, relayed to the
room over the **SignalR** hub that already carries chat (38.1).

## 2. The reuse decision (the load-bearing design choice)

There are two granularities of progress, and reuse is designed in by *where* the abstraction lives:

- **Step-level ("Classifying / Searching / Answering") — engine-level, provider-agnostic, reusable beyond
  search.** `PipelineRunner` already fires `OnStepComplete(expertName, envelope)` after each step (and
  `IExpertRunner.StreamAsync` exists; `--steps` already streams to stderr). Today `MissionRunHandler`
  *buffers* these into `trace` and returns at the end. Streaming = forward each as it fires. Works for **any**
  mission (`@guard`, `@assistant`, …) and **any** search backend, because it's engine + transport, nothing
  Grok-specific.

- **Sub-search ("Searched web: N results") — provider-specific events, made reusable at the `IWebSearch`
  seam.** Define a **provider-neutral progress shape** and stream it from `IWebSearch`, exactly parallel to
  how `WebSearchResult` is a neutral shape every backend *fills*:
  ```csharp
  public sealed record WebSearchProgress(
      string Kind,          // "searching_web" | "searching_x" | "reading" | "results"
      string? Detail,       // e.g. the query, or a URL being read
      int? ResultCount);

  public interface IWebSearch
  {
      Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct = default);
      // NEW — optional streaming variant; default impl yields nothing then the result.
      IAsyncEnumerable<WebSearchProgress> SearchStreamAsync(WebSearchRequest request, CancellationToken ct = default);
  }
  ```
  Then the **transport + SignalR chip + orchestrator consumer are all shared**; each backend only writes a
  small **adapter** that maps its native stream into `WebSearchProgress` — the same small job as mapping its
  result into `WebSearchResult`.

  **This is why it's reusable:** OpenAI and Anthropic emit the *same shape* of streaming web-search events as
  Grok (server-side tools streaming `web_search_call` items over SSE), so their progress adapters are
  near-clones of Grok's. Raw APIs (Tavily/Exa) have no server-side loop to narrate → they emit a thin
  `searching → results` and still satisfy the interface, no special-casing. **The one-time transport cost is
  paid once and amortized across every provider and every mission.**

## 3. Architecture (faithful to what's there)

```
Room UI  ◄──SignalR (38.1, already realtime)──  ForgeUI orchestrator  ◄──HTTP STREAM (new)──  Runner
 progress chips on the pending                    RoomAgentInvoker relays each                 MissionRunHandler
 agent message; final text                        event → hub; final → message                 forwards OnStepComplete
 replaces them                                    (MissionRunnerClient consumes stream)         + IWebSearch progress
```

- **Browser leg: already solved** (SignalR). No new transport.
- **Runner leg: the change** — `POST /run` (sync `RunResponse`) → a streaming response (NDJSON lines:
  `{"type":"progress",…}` events, then a final `{"type":"result", …RunResponse}`). ASP.NET does this natively
  (`IAsyncEnumerable` / write-to-body). Keep the sync `/run` for non-interactive callers, or make streaming
  the default and buffer for the CLI.

## Tasks (chronological)

### Task 1 — Provider-neutral progress at the `IWebSearch` seam
Add `WebSearchProgress` + `SearchStreamAsync` to `src/ForgeMission.Scout/IWebSearch.cs` with a default
interface method that yields nothing then defers to `SearchAsync` (so existing backends compile). **Done
when:** the interface compiles; a stub backend can emit progress events.

### Task 2 — Grok streaming adapter
Implement `GrokWebSearch.SearchStreamAsync` against xAI's **streaming** Responses API (SSE) — map each
`web_search_call` action (`query`, `sources`, result counts) → `WebSearchProgress`, then the final message →
`WebSearchResult`. Keep the non-streaming `SearchAsync` for callers that don't stream. **Done when:** a live
call prints progress events then the grounded answer.

### Task 3 — Stream the runner leg (revisit 39.1 transport)
Change `MissionRunHandler` + the `/run` endpoint to emit an **NDJSON/SSE stream**: forward each
`OnStepComplete` as a step-progress event and each `WebSearchProgress` (threaded from the `kind: search`
step) as a sub-progress event, then a terminal `result` event carrying the existing `RunResponse`. **Done
when:** `curl` of the streaming endpoint shows progress lines then the result; the buffered contract still
works for the CLI.

### Task 4 — Orchestrator relay over SignalR
`MissionRunnerClient` consumes the stream; `RoomAgentInvoker` relays each progress event to the room hub as
a **transient update on the pending agent message**, and replaces it with the final text on the `result`
event. **Done when:** a room mention emits progress messages then the answer.

### Task 5 — Room UI progress chips
Render progress on the pending agent bubble (spinner → "Searching the web…" → "Searched web: N results" →
final answer). Reuse existing message/trust rendering; progress is transient (not persisted). **Done when:**
`@grok <current-events>` shows live chips at `forge.katasec.com`.

### Task 6 — Timeout audit + hardening
Enumerate and verify every timeout in the chain, and confirm streaming removes the **idle**-timeout risk:
- **ACA HTTP ingress** request timeout on `ca-forge-runner-dev` (default ~240s — confirm; raise if needed).
- `MissionRunnerClient` (currently 4 min) + any `HttpClient` on the stream.
- SignalR keep-alive / client timeout for the long-lived room connection.
Streaming keeps bytes flowing so idle reaping can't fire; total duration stays bounded by the max request
timeout (heartbeat/keep-alive events if a gap can exceed it). **Done when:** a long search completes in a
room with no timeout, and the timeout chain is documented.

## Design decisions & caveats
- **Progress lives at the same seam as the result** (`IWebSearch`) → OpenAI/Claude search progress "just
  works" through shared rails later; each new provider only maps its own event stream. Mirrors
  `WebSearchResult` + source-attribution.
- **Step-level progress is engine-level** (`OnStepComplete`) → reusable for *every* multi-step mission, not
  just search. Build once, all agents benefit.
- **No gRPC.** HTTP streaming over the existing transport; SignalR already owns the browser leg.
- **Streaming is the timeout fix, not just a UX nicety** — continuous bytes defeat idle timeouts; pair with a
  keep-alive event if progress can go quiet longer than the ingress idle window.
- **Progress is transient** — not persisted to the room history; only the final message is durable.
- **Ship order:** step-level chips (Tasks 3–5, the 80/20, provider-agnostic) can land *before* the Grok
  sub-search adapter (Task 2) — even just "Searching the web…" during the `kind: search` step removes the
  frozen-spinner problem.
