# Phase 41.7 — Streaming search progress + timeout hardening

> **Status: Step-level streaming spine DONE + LIVE-VERIFIED — 2026-07-12.** Tasks 1, 3, 4, 5, 6 (step-level)
> done and **verified live end-to-end on `forge.katasec.com`** (`@grok` World-Cup query → live chip → grounded
> answer; 71s round-trip, member debited). Deployed: `forge-runner:0.6.0` rev `--0000009`, `forge-ui:0.4.1`
> rev `--0000017` (0.4.1 = chip-animation follow-up). **Task 2 (Grok SSE sub-search adapter) deferred** per
> the spec's own ship-order. See [Progress](#progress-2026-07-12) below.
> · **Parent:** [Phase 41 — Live Retrieval](phase-41-live-retrieval-scout.md) ·
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

## Progress (2026-07-12)

The **provider-agnostic step-level streaming spine** is built end-to-end and locally verified. This is
the 80/20 the spec called out as landable first: it removes the frozen spinner and hardens the timeout,
independent of any Grok-specific work.

**Built:**
- **Task 1** — `WebSearchProgress` record + default `IWebSearch.SearchStreamAsync` (yields nothing) in
  [src/ForgeMission.Scout/IWebSearch.cs](../../src/ForgeMission.Scout/IWebSearch.cs). Provider-neutral shape
  ready for the Grok adapter; existing backends compile unchanged (default interface method).
- **Engine hook** — `PipelineRunOptions.OnStepStart(expertName, kind)` fired *before* each step runs in
  [PipelineRunner.cs](../../src/ForgeMission.Core/Runtime/PipelineRunner.cs), so "Searching the web…" lands
  *during* the ~40s search, not after. Provider- and mission-agnostic.
- **Task 3** — streaming runner leg. `RunStreamEvent`/`RunProgress` in
  [RunContracts.cs](../../src/ForgeMission.Runner.Contracts/RunContracts.cs); `MissionRunHandler` refactored
  into a shared `ExecuteAsync` core + `RunStreamAsync` (Channel + 15s heartbeat); new `POST /run/stream`
  NDJSON endpoint (per-event flush). Buffered `POST /run` kept verbatim for the CLI.
- **Task 4** — `MissionRunnerClient.RunStreamAsync` consumes the NDJSON with
  `HttpCompletionOption.ResponseHeadersRead` (body stays a live stream); `RoomAgentInvoker` relays each
  progress event over the broadcaster with a kind→label map.
- **Task 5** — `RoomBroadcaster.PublishAgentProgressAsync` + `AgentProgress` event; `RoomConversation.razor`
  renders the transient label on the pending bubble (`@handle Searching the web…`), cleared on answer/fail.
- **Task 6** — 15s heartbeat keeps bytes flowing across the search step's server-side silence; the client
  reads with `ResponseHeadersRead` so `HttpClient.Timeout` guards only connect+headers, and the stream body
  is bounded by `RoomAgentInvoker`'s 3-min run CTS. Timeout chain documented below.

**Verified locally (network-free where possible):**
- Engine spine: unit test `OnStepStart_FiresPerBeginningStep_InOrder_WithKind` asserts the exact
  `classify → route → search → answer` start sequence with kinds (taken branch only). Suite 220 pass / 0 fail.
- Transport: booted the runner and `curl -N POST /run/stream` → `Content-Type: application/x-ndjson`,
  `Transfer-Encoding: chunked`, terminal `{"Type":"error",…}` event; buffered `/run` still returns 404 for
  an unknown mission (CLI contract intact). All projects build with **zero warnings**.

**Deployed (2026-07-12):**
- `forge-runner:0.6.0` (git tag `forge-runner-v0.6.0`, ACA rev `ca-forge-runner-dev--0000009`) — boot log
  confirms `loaded 5 mission(s): …, Grok` + `Now listening`; the new `POST /run/stream` endpoint is serving.
- `forge-ui:0.4.0` (git tag `forge-ui-v0.4.0`, ACA rev `ca-forge-ui-dev--0000016`) — boot log confirms
  `runner advertises 5 mission(s): …, Grok`; `https://forge.katasec.com` → 302 (login), custom domain intact.
- `forge-infra` GH vars bumped to match (`FORGE_RUNNER_IMAGE=forge-runner:0.6.0`, `FORGE_UI_IMAGE=forge-ui:0.4.0`)
  so a future `500-app` Bicep deploy won't revert the roll.
- Deploy order honoured: **runner first** (endpoint exists) **then UI** (consumer). Backward-compatible —
  the runner kept the buffered `/run`, so no hard cutover window.

**Done-when CLEARED — live end-to-end verified (2026-07-12):** `@grok "When is the next World Cup game?"`
in a room showed the live "Searching the web" chip on the pending bubble, then the grounded answer. Runner
log: `POST /run/stream — 200 — application/x-ndjson 71260ms`; ForgeUI log: run completed, member
`Debited 14571µ$ — Grok 4841+140 tok / 71.23s / grok-4.5`. So the full round-trip (stream → progress chip →
grounded answer → billing) works in prod. The frozen-spinner problem is gone.

**Follow-up fix — chip animation (`forge-ui:0.4.1`, rev `--0000017`):** the first live run exposed that the
step-start label, though correct, was *visually* static — a ~71s search step sat on "Searching the web…"
with only a 6px opacity-pulse, so it still read as frozen. Fixed: strip the trailing "…" from labels + append
staggered bouncing dots (markup-based CSS, verified rendering mid-bounce in the browser preview). Note the
**~71s** search latency: narrating *inside* that one long step is exactly what Task 2 (Grok SSE) adds; until
then the animated dots carry the "still working" signal.

**Deferred:**
- **Task 2 — Grok SSE sub-search adapter** (the "Searched web: N results" sub-lines). Deferred per the
  spec's ship order; `WebSearchProgress` + `SearchStreamAsync` are in place as its landing seam. When built,
  it threads `WebSearchProgress` from the `kind:search` step → a runner sub-progress event over the same rails.

**Timeout chain (Task 6 audit):**
| Hop | Limit | After 41.7 |
|---|---|---|
| ACA HTTP ingress (`ca-forge-runner-dev`) | ~240s request | idle can't fire — heartbeat every 15s keeps bytes flowing; confirm total-request ceiling ≥ worst-case search on deploy |
| `MissionRunnerClient` HttpClient | 4 min | with `ResponseHeadersRead`, guards only connect+headers; body read is unbounded by it |
| `RoomAgentInvoker` run CTS | 3 min | the real end-to-end ceiling on a single run (bounds the stream body) |
| SignalR keep-alive (browser leg) | default | unchanged; the room connection already long-lived (38.1) |

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
