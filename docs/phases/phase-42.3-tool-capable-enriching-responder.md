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
> once per user turn** (not per tool round-trip), and a final verified answer returns. Proven by an
> integration test that drives a real (or faithfully mocked) tool loop and asserts enrich-once.

## The mental model (locked)

One user turn is **N+1 requests**, because every tool round-trip is a fresh `POST /v1/messages`:

```
call 1   last message = USER text     → enrich (retrieval/guard) ONCE, run terminal expert, it asks Read(x)  → reply tool_use
call 2   last message = tool_result   → RESUME the terminal expert (skip enrich) → asks Edit(y)              → reply tool_use
call 3   last message = tool_result   → RESUME → asks Bash(tests)                                            → reply tool_use
call 4   last message = tool_result   → RESUME → done                                                        → reply final text
```

The mission is the responder; the tool-capable terminal expert is the loop hub; enrichment scaffolds
*around* it. See the hub [§3](phase-42-forge-cloud.md) diagram.

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

### 3. The re-entrancy gate (enrich once, resume the expert)

The gate keys off the **last message**:

```
last message role/type:
  user text     → NEW TURN     → run the FULL mission: enrich (retrieval/guard) → terminal expert(with tools)
  tool_result   → CONTINUATION → run ONLY the terminal expert, seeded with the tool transcript
```

- **State carry:** two viable strategies; pick per the store decision below.
  - **(a) Reconstruct-from-request (preferred, stateless):** every request carries the full history
    including prior `tool_use`/`tool_result`, so the tool transcript is free. The only thing *not* in the
    request is the enrichment output (e.g. retrieved context). Stash the enrichment result **into the
    conversation** on call 1 (e.g. as a synthetic system/context block the mission emitted), so subsequent
    calls re-derive it from the request with no server state. Cleanest for multi-replica cloud.
  - **(b) Session store:** key by `X-Session-Id` (or a hash of the conversation prefix), store the
    enrichment output + any loop state in `ISessionStore`, reload on continuation. Simpler to reason about;
    needs a **shared** store in cloud (see 42.6). Use the existing `ISessionStore` seam so local = in-proc,
    cloud = swapped implementation.
- **Detection helper** on the parsed request: `IsToolContinuation(request)` ⇒ last content block is a
  `tool_result`. This is the single branch that makes enrich-once correct — today's `LastUserMessage` would
  feed a `tool_result` as the goal (nonsense), so this replaces that logic on the Anthropic wire.

> **Why not pause the pipeline:** re-entrancy is achieved by **re-running with the gate**, not by suspending
> a C# coroutine. On a continuation we run *only* the terminal expert (a cheap single-expert pass) with the
> accumulated transcript — the expensive enrichment is skipped by the gate. This keeps the runtime stateless
> and cloud-scalable.

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
5. **The re-entrancy gate:** implement `IsToolContinuation`-driven branching — new turn runs the full
   mission, continuation runs only the terminal expert. Implement strategy (a) reconstruct-from-request as
   the default; wire `ISessionStore` into `AnthropicServer.Build` as the (b) fallback seam.
6. **Integration test:** drive a real `claude` tool loop (or a faithful mock host that executes tool calls)
   against `forge serve` fronting a mission whose enrichment step **increments a counter / logs once** —
   assert the counter is `1` across an N-tool task (enrich-once), and that Read/Edit/Bash round-trip.
7. **Suite green, zero warnings.** Publish/bump `Katasec.AnthropicServer` if consumed via NuGet.

## Out of scope

- The Responses-wire (`/v1/responses`) version of this seam — **42.7** (reuses tasks 1's neutral layer).
- Hosting, multi-replica shared store selection — **42.6** (this spoke leaves the store *injectable*; 42.6
  picks the cloud implementation).
- MCP door (no tool round-trip needed there — the host owns the loop) — **42.8**.
