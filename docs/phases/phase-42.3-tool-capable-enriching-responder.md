# Phase 42.3 — Tool-capable enriching responder (the load-bearing seam)

> **Status: Design (2026-07-15).** Make the mission a **tool-capable responder that enriches** — so Claude
> Code stays a *real coding agent* (Read/Edit/Bash) while the mission remains the brain. The LLM lives
> *inside* the mission as a **tool-capable terminal expert**; the mission enriches (retrieval, guards) once
> per user turn, the expert drives the tool loop with the client, then the mission verifies. This is the
> hardest engineering in Phase 42 and the seam that carries `local → cloud` unchanged.
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:**
> [42.1](phase-42.1-anthropic-serve-responder.md) (Anthropic serve + full history) · **Blocks:**
> [42.6](phase-42.6-hosted-endpoint-ttfa.md) (the agentic hosted experience), [42.7](phase-42.7-codex-responses-door.md)
> (same seam on the Responses wire) · **AOT rules:** [CLAUDE.md](../../CLAUDE.md).
>
> **Done when:** the real `claude` CLI, pointed at a local `forge serve` fronting a mission with a
> tool-capable terminal expert, completes a **multi-tool task** (e.g. "read X, edit Y, run tests") — every
> `tool_use` round-trips correctly, the mission's enrichment (e.g. a retrieval/guard step) runs **exactly
> once per user turn** (not per tool round-trip), the **post-agent `Verify` segment runs exactly once on the
> final continuation**, and a verified answer returns. Proven by an integration test that drives a real (or
> faithfully mocked) tool loop and asserts **both** enrich-once **and** verify-runs.

## The mental model (locked)

One user turn is **N+1 requests**, because every tool round-trip is a fresh `POST /v1/messages`:

```
call 1   last message = USER text     → enrich (retrieval/guard) ONCE, run terminal expert, it asks Read(x)  → reply tool_use
call 2   last message = tool_result   → RESUME the terminal expert (skip enrich) → asks Edit(y)              → reply tool_use
call 3   last message = tool_result   → RESUME → asks Bash(tests)                                            → reply tool_use
call 4   last message = tool_result   → RESUME → no more tools → run VERIFY → reply final text
```

The mission is the responder; the tool-capable terminal expert is the loop hub; enrichment scaffolds
*around* it. See the hub [§3](phase-42-forge-cloud.md) diagram.

### The three-segment execution model (the gate is NOT binary)

A mission is `Enrich → TerminalExpert → Verify`. The naive gate — *"user text ⇒ full mission;
`tool_result` ⇒ terminal expert only"* — **is wrong, and silently breaks verification**: the final answer
emerges on a **continuation** call (call 4 above, last message = `tool_result`), so a
terminal-expert-only continuation would **never run `Verify`**. The whole post-agent segment dies the
moment tools are involved. The runtime must therefore understand **three segments**:

| Segment | Contains | Runs when |
|---|---|---|
| **pre-agent** | enrich: retrieval · classify · guard | **only** when the last message is **user text** (once per user turn) |
| **agent** | the tool-capable terminal expert | **every** call (fresh or resumed) |
| **post-agent** | verify · judge · format · repair | **only** when the agent segment terminates **without** a tool call |

The rule, stated once and precisely:

```
ANY call:
  last msg = user text    → run PRE-AGENT (enrich)          [once per user turn]
  last msg = tool_result  → skip pre-agent

  always → run / resume the AGENT segment (terminal expert, with tools)
      ├─ emits tool_use    → return it IMMEDIATELY · skip post-agent
      └─ emits final text  → run POST-AGENT (verify/judge/repair) → return verified answer
```

So **post-agent runs iff the terminal expert terminates without a tool call** — which may be call 1 (no
tools needed) or call N.

**Subtlety — the segments are not strictly linear.** If `Verify` **fails** and the mission has a
repair/retry loop, the post-agent segment may **re-enter the agent segment**, which can emit a *new*
`tool_use` → returned to the client → the loop continues. Implement this as a re-entrant loop, **not** a
one-way pipeline, or a failed verification will dead-end instead of repairing.

> **Attribution:** this defect (post-agent verification never firing in an agentic flow) was caught in an
> external design review of the Phase 42 docs, 2026-07-15. The original spec's binary gate would have
> shipped a mission whose `Verify` step was dead code whenever tools were in play.

## Context an implementer needs (verified against the code 2026-07-15)

- **`AnthropicServer` is text-only today.** [`BuildChatHistory`](../../../oai-server-dotnet/src/Katasec.AnthropicServer/AnthropicServer.cs)
  extracts **text** from each message and **drops the `tools` field entirely**; the response only ever emits
  a single text content block. So: (a) inbound `tools` + `tool_result` blocks are ignored, (b) there is no
  path to emit a `tool_use` block. **Both directions must be built.**
- **`MissionChatClient` ignores `ChatOptions`** and returns `result.Text`
  ([`Adapters/MissionChatClient.cs`](../../src/ForgeMission.Core/Adapters/MissionChatClient.cs)). Tools arrive
  via `ChatOptions.Tools` (`Microsoft.Extensions.AI` `AITool` / `AIFunction`); function calls come back as
  `FunctionCallContent` and results go in as `FunctionResultContent`. None of this is wired.
- **`ISessionStore` already exists** in `Katasec.OaiServer` (`Session`, `X-Session-Id`, `LocalFileSessionStore`)
  — the natural home for re-entrancy state. `AnthropicServer.Build` does **not** currently take a session
  store; add one (mirror `OaiServer.Build`'s optional `ISessionStore`).
- **The pipeline runs start→finish today.** `PipelineRunner.RunAsync` executes the whole mission and returns
  — there is no "pause at the terminal expert, resume later" concept. The re-entrancy design does **not**
  require pausing the C# pipeline mid-run; instead it **re-derives** state per request (see design).

## Design

### 1. Tool round-trip in `AnthropicServer` (both directions)

- **Inbound:** parse the Anthropic request's `tools` (name, description, `input_schema`) into
  `Microsoft.Extensions.AI` tool declarations, and map `tool_use` / `tool_result` content blocks in the
  message history into `FunctionCallContent` / `FunctionResultContent`. Pass tools via `ChatOptions.Tools`.
- **Outbound:** when the mission's terminal expert yields a `FunctionCallContent`, serialize it as an
  Anthropic `tool_use` content block (with `stop_reason: "tool_use"`); when it yields text, serialize a text
  block with `stop_reason: "end_turn"` (today's behaviour). Streaming must emit the correct
  `content_block_start`/`_delta`/`_stop` for a `tool_use` block, not just text.
- This is the **Responses-wire twin** of 42.7 — keep the mapping logic factored so both wires share the
  neutral `Microsoft.Extensions.AI` middle.

### 2. The terminal expert becomes tool-capable

- `MissionChatClient` stops discarding `ChatOptions`. It forwards `ChatOptions.Tools` into the mission run
  so the **terminal (last) expert** — a `kind: llm` expert — issues its provider call **with the tools
  attached**, letting the underlying `IChatClient` emit `FunctionCallContent`. When the model calls a tool,
  the mission run **returns that function-call as the response** (it does not try to execute it — the *client*
  runs Read/Edit/Bash).
- Non-terminal (enrichment) experts never see the tools; they run as today. Only the terminal expert is
  tool-capable. (Convention, documented; optionally a `role: agent` / `tool_capable: true` frontmatter flag
  to mark it explicitly.)

### 3. The re-entrancy gate + carrying the enrichment across continuations

**Detection helper** on the parsed request: `IsToolContinuation(request)` ⇒ the last content block is a
`tool_result`. This single branch drives the three-segment model above — and note today's
`LastUserMessage` would feed a `tool_result` in as the goal (nonsense), so this **replaces** that logic on
the Anthropic wire.

**The tool transcript is free; the enrichment is not.** Every request carries the full history including
prior `tool_use`/`tool_result`, so resuming the agent segment needs no server state. The *only* thing not
in the request is the **pre-agent output** (retrieved context, guard verdict) — and it must survive to
call N or the answer loses its grounding.

**Rejected — stash it in the conversation.** The tempting "emit the enrichment as a synthetic context block
so the client round-trips it back" **does not survive contact with real clients**: there is no
client-round-tripped *hidden* channel in the Anthropic/Responses wires, and anywhere it *would* round-trip
(an assistant turn) is **user-visible** — the user watches their agent emit a wall of retrieved context.
Do not build this without proving the round-trip against the real `claude`/`codex` CLIs first; a mock host
will happily preserve server-injected content that a real client drops.

**Chosen — a content-addressed enrichment cache.**

```
key   = hash(conversation prefix up to and including the last USER message)
value = the pre-agent output (retrieved context, guard verdict, classify flags)
```

- On a **new user turn**: run pre-agent → store under the key.
- On a **continuation**: the prefix is unchanged ⇒ **same key** ⇒ cache hit ⇒ enrichment recovered without
  re-running it.
- **No client cooperation, no session header, nothing user-visible.** The client stays stateless; the
  server derives identity from content it already sent.
- **Multi-replica safe** when the cache is shared — local = in-proc/`LocalFileSessionStore`, cloud = shared
  (Rooms PG / cache) via the existing **`ISessionStore` seam**. This is the seam 42.6 swaps; keep it
  injectable.
- Cache miss (eviction, cold replica) must **degrade correctly**: re-run pre-agent rather than answer
  ungrounded. Correctness first, cost second.

**Idempotency.** A network retry can resubmit the same `tool_result`. The content-addressed key makes the
*enrichment* naturally idempotent (same prefix → same value), but the **agent segment is not**: re-running
the terminal expert is a fresh non-deterministic LLM call that costs money and may emit a *different* tool
call. Derive a continuation identity (conversation id + turn + `tool_use_id` + sequence) and either replay
the prior response or reject the duplicate. **Left explicitly open — decide in design review** (see
Out of scope / Open questions).

> **Why not pause the pipeline:** re-entrancy is achieved by **re-running with the gate**, not by suspending
> a C# coroutine. On a continuation we skip the pre-agent segment and resume only the agent segment (a cheap
> single-expert pass) with the accumulated transcript. This keeps the runtime stateless and cloud-scalable.

## Tasks (chronological)

1. **Neutral tool mapping layer** (shared by both wires): Anthropic `tools`/`tool_use`/`tool_result` ↔
   `Microsoft.Extensions.AI` `AITool` / `FunctionCallContent` / `FunctionResultContent`. Unit-test the
   round-trip on captured fixtures.
2. **`AnthropicServer` inbound:** parse `tools` + tool-result history; pass through `ChatOptions.Tools`;
   surface a `IsToolContinuation` check.
3. **`AnthropicServer` outbound:** emit `tool_use` content blocks (non-streaming + streaming SSE) with the
   right `stop_reason`; keep text path intact.
4. **`MissionChatClient` tool-capable terminal expert:** forward `ChatOptions.Tools` to the terminal expert
   only; return `FunctionCallContent` verbatim when the model calls a tool.
5. **The three-segment executor:** implement `IsToolContinuation`-driven branching per the model above —
   pre-agent only on user-text turns; agent segment always; **post-agent iff the agent terminates without a
   tool call**; post-agent may **re-enter** the agent segment (repair loop), so build it re-entrant, not a
   one-way pipeline.
6. **Content-addressed enrichment cache:** key = hash(conversation prefix through the last user message);
   store the pre-agent output behind the **`ISessionStore` seam** (in-proc local, shared in cloud — 42.6
   swaps it). Cache miss ⇒ re-run pre-agent (never answer ungrounded).
7. **Integration test — two assertions, both load-bearing:**
   (a) **enrich-once** — drive a real `claude` tool loop (or a faithful mock host that executes tool calls)
   against `forge serve` fronting a mission whose enrichment step **increments a counter**; assert the
   counter is `1` across an N-tool task, and that Read/Edit/Bash round-trip.
   (b) **verify-runs** — assert the **post-agent segment executes exactly once, on the final continuation**
   (the regression this spoke exists to prevent: `Verify` silently never firing when tools are in play).
   Add a repair-loop case: a failing `Verify` re-enters the agent segment and can emit a new `tool_use`.
8. **Suite green, zero warnings.** Publish/bump `Katasec.AnthropicServer` if consumed via NuGet.

## Out of scope / open questions

- The Responses-wire (`/v1/responses`) version of this seam — **42.7** (reuses task 1's neutral layer).
- Hosting, multi-replica shared cache implementation — **42.6** (this spoke leaves the store *injectable*).
- MCP door (no tool round-trip needed there — the host owns the loop) — **42.8**.
- **OPEN — continuation idempotency.** A retried `tool_result` re-runs the agent segment: a fresh
  non-deterministic LLM call that costs money and may emit a *different* tool call. Decide in design review:
  derive a continuation identity (conversation + turn + `tool_use_id` + sequence) and **replay** the prior
  response, or **reject** the duplicate.
- **OPEN — prove the client round-trip before relying on any in-conversation state.** Test against the real
  `claude`/`codex` CLIs, not a mock (a mock preserves server-injected content that a real client drops).
