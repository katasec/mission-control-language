# Phase 42.3 — Tool-capable enriching responder (the load-bearing seam)

> **Status: DONE (2026-07-16).** All 10 tasks (0–9) built and verified — the Done-when passed LIVE:
> `ClaudeCode_MultiToolTask_ThroughForgeServe_AgenticMission` drives the real `claude` CLI through a real
> spawned `forge serve` (Anthropic wire, `agentic` mission Enrich→Respond(`role: agent`)→Verify) — the
> planted magic word arrived **only** via a real tool round-trip, the answer carries the `VERIFIED:` stamp
> (post-agent ran on the final continuation), and the enrich counter file reads exactly 1. CI role:
> `MockClaudeHostTests` (mock host that EXECUTES Read calls; chained two-hop plant) gates every commit.
> Unit rails: `ThreeSegmentExecutorTests` (enrich-once, verify-on-final-continuation, cache-miss
> regrounding, repair-loop re-entry emitting a new `tool_use`, P/F hash sanity, duplicate counter),
> `RequestClassifierTests` (fixture-regression + HTTP dispatch), `ToolMappingTests`, `AnthropicServerToolTests`
> (wire-level tool_use non-streaming + SSE). Suites: MCL 255 pass / oai-server 28 pass, zero warnings.
>
> **Implementation notes vs the design below (deviations + live findings):**
> - **Agent marker:** `role: agent` frontmatter (Phase 25a `role: judge` precedent) — explicit opt-in, not
>   last-step convention. Tools attach to that expert's provider call only; PipelineRunner short-circuits on
>   its tool call; `MissionResult.ToolCalls` carries `FunctionCallContent` to the wire.
> - **Enrichment cache seam:** OaiServer's `ISessionStore` is OaiMessage-typed (wrong shape) — the cache got
>   its own neutral seam `IEnrichmentCache` (+ `InMemoryEnrichmentCache`, TTL + bounded) in ForgeMission.Core;
>   42.6 swaps in a shared store.
> - **Classifier rule amended from fixture evidence** (aux-state-check.json): `tools: 0` + `thinking: disabled`
>   ⇒ aux housekeeping — the CLI's state-check call has NO `output_config.format`. Plain API clients omit
>   `thinking` entirely and stay Mission.
> - **Live finding (task 8): tool_result role mapping** — the Anthropic wire packs `tool_result` into USER
>   messages; providers 400 unless they arrive as TOOL-role messages. `BuildChatHistory` splits them out.
> - **Live finding: `claude -p` denies file tools by default** — the authoritative test passes
>   `--dangerously-skip-permissions`; 42.2's launcher UX should surface this.
> - **`duplicate_continuation` observed value: 0 in all live runs** (counter fires only in the synthetic
>   duplicate test). Per §4: the machinery was not warranted — replay stays unbuilt.
> - `forge serve` wires an aux passthrough `IChatClient` from the default provider profile; without one,
>   aux gets canned replies (never the mission). `Katasec.AnthropicServer` 0.1.6 published (lockstep 0.1.6).
>
> Original design brief (2026-07-15): Make the mission a **tool-capable responder that enriches** — so Claude
> Code stays a *real coding agent* (Read/Edit/Bash) while the mission remains the brain. The LLM lives
> *inside* the mission as a **tool-capable terminal expert**; the mission enriches (retrieval, guards) once
> per user turn, the expert drives the tool loop with the client, then the mission verifies. This is the
> hardest engineering in Phase 42 and the seam that carries `local → cloud` unchanged.
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:**
> [42.1](phase-42.1-anthropic-serve-responder.md) (Anthropic serve + full history) · **Blocks:**
> [42.2](phase-42.2-forge-claude-launcher.md) (launcher ships only when tools round-trip — resequenced
> 2026-07-16), [42.6](phase-42.6-hosted-endpoint-ttfa.md) (the agentic hosted experience), [42.7](phase-42.7-codex-responses-door.md)
> (same seam on the Responses wire) · **AOT rules:** [CLAUDE.md](../../CLAUDE.md).
>
> **Done when:** the real `claude` CLI, pointed at a local `forge serve` fronting a mission with a
> tool-capable terminal expert, completes a **multi-tool task** (e.g. "read X, edit Y, run tests") — every
> `tool_use` round-trips correctly, the mission's enrichment (e.g. a retrieval/guard step) runs **exactly
> once per user turn** (not per tool round-trip), the **post-agent `Verify` segment runs exactly once on the
> final continuation**, and a verified answer returns. Proven by **two test roles** (task 8): the **real
> `claude` CLI test is authoritative** (SkippableFact — needs keys + the binary), and a **CI-runnable mock
> host that actually executes tool calls** gates every commit. Both assert enrich-once **and** verify-runs,
> and both pass only on **planted tool-derived content** (the no-false-green rule) — never the CLI's status
> fields, which report protocol success even when the tool loop is broken.

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

The rule, stated once and precisely (amended 2026-07-16: **classification comes first** — see Design §0;
the segment rule below applies only to requests classified as MISSION requests):

```
ANY call:
  CLASSIFY (Design §0):  AUX request → dispatched to its typed handler; the mission NEVER runs
                         MISSION request ↓

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
- **The CLI's status fields report *protocol* success, not *task* success (probed live 2026-07-16, real
  `claude` 2.1.195 vs today's tool-less server).** Asked to read a file, the model replied *"Let me check
  the contents of the file"*, called nothing (no tools existed to call), ended the turn — and the CLI
  emitted `exit 0`, `is_error: false`, `subtype: "success"`. A hollow answer is **indistinguishable from a
  complete one** in every status field. This is why task 8's pass criterion is planted content, not green.

## Design

### 0. The request pipeline — middleware, classifier, aux dispatch (DECIDED 2026-07-16)

**Every request flows through one composed middleware pipeline; a classifier then decides what the request
*is* before any mission logic runs.** This extends the design strategy already locked in Phase 41 (context =
untyped bag, OWIN `AppFunc` model) — OWIN *is* .NET's embodiment of Go-style `http` middleware composition,
and the server is already an ASP.NET `WebApplication`. **Composition, not inheritance.**

```
EVERY request        ── cross-cutting, unconditional middleware
  │   logging · timestamp · request-id
  │   (cloud registers MORE middleware in the SAME pipeline: auth → principal,
  │    metering — 42.5/42.6. This is `local ≡ cloud` expressed structurally.)
  ▼
CLASSIFIER           ── structural metadata ONLY, never prompt-text sniffing
  ├─► MISSION request     (the core — the user's task)
  │     ├─ user turn          → pre-agent + agent (+ post-agent if no tool call)
  │     └─ tool continuation  → agent (+ post-agent if no tool call)
  └─► AUX request         (everything the client needs that is NOT the task itself)
        └─ dispatched BY TYPE to a registered handler (a mux, Go-style) — the mission NEVER runs
```

**Aux dispatch — per-type handlers, not a blanket bypass (DECIDED 2026-07-16):**

| Aux type | Detected by | Handler |
|---|---|---|
| **probe** (`HEAD /`) | method + path | static `200` — no model involved (the CLI probes before first use) |
| **title-gen** | `output_config.format` = `{title}` schema + `tools: 0` | **passthrough** to the underlying provider model — a tiny call that returns exactly the schema the CLI demands; sessions get real titles |
| **unknown structured-output** | `output_config.format` present + `tools: 0`, schema unrecognized | **default = passthrough** — CLI housekeeping we haven't cataloged yet degrades gracefully instead of breaking on canned guesses |
| **state-check** (agent-state classification) | `thinking: disabled` + `tools: 0` (NO `output_config.format`!) | **passthrough** — see fixture evidence below |
| *future types (compaction, summaries, …)* | added per capture evidence | each gets its own registered handler |

> **Fixture evidence (2026-07-16 capture, checked in as `aux-state-check.json`):** the CLI's CALL 4 is an
> agent-state classification call ("read the tail of what the agent said, decide which of four states…") with
> `tools: 0`, `thinking: disabled`, `max_tokens: 1024`, non-streaming — but **no `output_config.format`**, so
> the original format-based rule misses it and it would run the full mission. Amended rule (structural only):
> **`tools: 0` AND `thinking` present-with-`disabled` ⇒ aux.** The `thinking` clause is what protects plain
> API clients (curl/python/42.1-style callers): they omit the `thinking` field entirely, so they still
> classify as MISSION. The CLI always sends `thinking` (adaptive on real turns, disabled on housekeeping).

**Naming (decided):** the category is **`aux`** — broad enough for types that aren't housekeeping, defined
relative to the core (auxiliary *to the mission*), and it avoids "meta", which would collide with the
Anthropic wire's literal top-level `metadata` field. Earlier working names ("sidecar", "housekeeping",
"system") are retired.

**Why the aux category exists (probed live 2026-07-16):** the CLI's *first* `POST /v1/messages` in a
session is not the user's turn — it is Claude Code requesting a **session title** (`output_config.format`
demands `{title: string}` JSON, `tools: 0`, 1.3KB "generate a title" system prompt). The mission gate as
originally designed sees "last message = user text" and would run the **full mission** on it: billed
enrichment + judge loops for a 7-word title, prose returned where the CLI demands schema JSON, and the
enrich-once counter reads 2 — failing this spoke's Done-when on the first turn of every real session.
Title-gen is the aux type we *caught*; the CLI will have others (compaction, summaries) — hence a category
with typed dispatch, not a special case.

**Classifier signals (from the live capture — structural, versioned by fixture):**

| Signal | title-gen (aux) | real user turn |
|---|---|---|
| `output_config.format` (schema demanded) | **present** (`{title}` required) | absent |
| `tools` | **0** | 57 |
| `thinking` | disabled | adaptive |
| system prompt | 1.3KB task instruction | 28.6KB agent prompt |

Rule: `output_config.format` present **+** `tools: 0` ⇒ aux (structured-output type) — the client is
demanding schema-shaped output, which a mission structurally does not produce. Do **not** key on the
`<session>` wrapper or other prompt text (CLI implementation details). **The classification rules are
data-driven from captured fixtures**: on a CLI version bump, re-run the capture, diff, adjust — the
fixture set is the classifier's regression suite.

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

**Tool surface — essentials allowlist (DECIDED 2026-07-16, from the live wire capture).** The real CLI
declares its **entire** tool set on every request — probed: **57 tools**, including the user's claude.ai
MCP connectors (`mcp__claude_ai_Gmail__*`, Calendar, Drive). The terminal expert sees only the essentials:

> **`Read` · `Edit` · `Write` · `Bash`** — filtered at the neutral mapping layer (task 1), inbound only.

Nobody server-side ever executes a tool, under any design — the server is a **relay**: declarations in
(filtered to the model) → the model's `tool_use` back to the client → the **client executes locally** →
`tool_result` relayed upstream. Outbound needs no filter; the model can only call what it was shown.

Why filter rather than forward all 57:
1. **Safety** — forwarding `mcp__claude_ai_Gmail__create_draft` lets the mission's model emit a Gmail write
   the client *would execute* (it declared the tool). The allowlist makes that **structurally impossible**,
   not behaviorally unlikely.
2. **Privacy** — the user's connector inventory is never disclosed to the mission's (possibly
   non-Anthropic) provider.
3. **Cost** — otherwise ~28KB of tool schemas ride every provider call.
4. **Capability** — non-Anthropic models drive 4 well-chosen tools far better than 57 alien schemas.

The majority Claude Code use case — read / search / edit / write / run — round-trips **uninhibited**:
the probe shows CLI 2.1.195 declares no Grep/Glob at all (search happens via `Bash` + ripgrep), so these
four cover the whole coding loop. Knowingly excluded: `Agent` (subagents), client-side `WebFetch`/
`WebSearch` (server-side retrieval via Scout is the mission's value — it should own that), `NotebookEdit`
(same trust class as `Edit`; add if needed), harness niceties (`Task*`, `Skill`, `Cron*`). Fixed default
list; a per-mission override is **deferred until a mission needs it** (fewer knobs).

**Graceful unknown-tool handling:** the CLI's ~28KB system prompt still *references* tools the model can no
longer see, so the model may occasionally attempt one — answer with an error `tool_result` (teaching it the
tool is unavailable), never a crash.

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
key  `P` = hash(CANONICALIZED conversation prefix up to and including the last USER message)
value    = the pre-agent output (retrieved context, guard verdict, classify flags)
```

**Canonicalize before hashing** (decided 2026-07-16, shared with §4): normalize to messages + tool blocks
only — never raw request bytes, and **exclude `system` and `tools`**. Not a precaution — an observed
necessity: the live capture caught the CLI embedding a **per-call-varying build stamp in the system
prompt** (`cc_version=2.1.195.eb8` on one call, `…b4f` on the next, same session), and `tools`/system size
also swing between calls (0↔57 tools, 1.3KB↔28.6KB system). A raw-body or system-inclusive hash therefore
changes **every call** → the cache never hits → enrichment silently re-runs and re-bills on every
continuation. Do not "simplify" the projection away.

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

> **Why not pause the pipeline:** re-entrancy is achieved by **re-running with the gate**, not by suspending
> a C# coroutine. On a continuation we skip the pre-agent segment and resume only the agent segment (a cheap
> single-expert pass) with the accumulated transcript. This keeps the runtime stateless and cloud-scalable.

### 4. Continuation idempotency — DECIDED (design session, 2026-07-16)

**The problem.** A network retry can resubmit the same `tool_result`. The content-addressed key makes the
*enrichment* naturally idempotent (same prefix → same value), but the **agent segment is not**: re-running
the terminal expert is a fresh non-deterministic LLM call that costs money and may emit a *different* tool
call. Claude Code **retries `429`/`5xx` automatically**, so on a flaky provider day every blip silently
re-runs the agent segment — and a mission with a judge/repair loop is several provider calls, not one.

**Decision: build `P`, do NOT build `F` replay in v1. Instrument instead.**

| Key | Definition | Purpose | v1? |
|---|---|---|---|
| **`P`** | hash(canonicalized conversation prefix **through the last user message**) | enrichment cache (enrich-once) | **YES — required for correctness.** Without it, continuations answer ungrounded. |
| **`F`** | hash(canonicalized **full** message array) | response replay (idempotency) | **NO — deferred.** Design recorded below; build on evidence. |

**Why deferred, not rejected.** The honest harm analysis: if the response never reached the client, the
client never executed the tool, so a *different* tool coming back on retry is not inconsistent — just
different. The dominant harm is **double billing and wasted work, not corruption.** (The narrow corrupting
window is real but rare: the response *did* arrive, the client executed `Bash`/`Edit`, and it retried
anyway — a re-run can hand back a second mutating tool call that also executes.) Weighed against that: `F`
costs two TTL regimes, a canonicalization spec, server-side buffering of streamed responses so they can be
replayed, and an unhandled concurrency gap. **That is real machinery for a failure we have never observed,
in a system with no users yet.**

**What makes this a decision and not an omission — ship the counter:**

> When a request arrives whose **`F` matches an `F` seen within the last 5 minutes**, log/emit a
> `duplicate_continuation` counter (mission, handle, whether the prior call was billed). Do not act on it.
> **Then go look at it.** Non-zero in real use ⇒ build replay (below). Zero after real usage ⇒ the machinery
> was never warranted.

The counter is ~10 lines on top of hashing code `P` already requires. This replaces speculation with a
number, at no cost to the v1 build.

**Meanwhile, the bound on the harm is 42.5/42.6's per-request spend cap** — retry-multiplied spend is
capped by the same hard per-request ceiling that closes the live Phase 39 hole. Idempotency is an
*efficiency* fix on top of that ceiling, not the ceiling itself. This is why it can wait.

**The deferred design (pre-decided — do not re-derive):**

- **Replay, do not reject.** A `409` on a retry has **no handler in the real `claude` CLI** — rejecting
  converts a recoverable network blip into a failed turn. Replay is also correctly *unbilled*.
- **Key on `F`, do not invent a continuation identity.** The earlier proposal (`conversation id + turn +
  `tool_use_id` + sequence`) invents an identity that content-addressing already provides, and leans on the
  client sending a stable conversation id it may not send. `F` is the same mechanism as `P` at a different
  prefix depth — one store, one hashing path.
- **Hash a canonicalized projection, not raw bytes.** The whole scheme rests on "the retry is identical". If
  the client varies anything incidental between attempts (header order, a timestamp, `tools` array order), a
  raw-body hash **misses and silently double-runs** — the exact failure it exists to prevent. Normalize to
  messages + tool blocks only. (Same normalizer serves `P`.)
- **TTL ≈ 5 minutes.** `F` hashes *content*, so two fresh conversations asking the same question collide.
  Without an expiry this stops being an idempotency window and becomes a semantic answer cache that freezes
  an answer for the next asker — which users of a non-deterministic system will not expect. 5 min absorbs any
  realistic retry and expires long before it can surprise. **Note `P` and `F` want different TTLs** (turn-
  length vs retry-window): same `ISessionStore`, two key namespaces, two expiries.
- **Known accepted gap:** two *genuinely concurrent* duplicates both miss and both run. Retries fire after a
  timeout and are therefore serialized in practice. The fix, if it ever bites, is an in-progress marker —
  do not build it speculatively.

> **Attribution / reasoning lens:** the deferral came from applying Claude Code's own design philosophy to
> this spoke — ship the dumb thing, instrument it, let real usage justify the machinery. The earlier
> recommendation (build both caches up front) was over-built for a problem with zero observations. The
> counter is the compromise that keeps the decision reversible on evidence rather than on argument.

## Tasks (chronological)

0. **Capture harness + sanitized fixtures (the 2026-07-16 probe made permanent).** Resurrect the
   `WireCaptureTests` probe as a permanent capture tool; check the captured request set into
   `tests/fixtures/anthropic-wire/` — it is load-bearing for the §0 classifier regression, task 1's mapping
   tests, and 42.1's goal-extraction test. **Sanitize before check-in (requirement, not suggestion):** the
   raw capture contains personal data — `metadata.user_id` (device/account UUIDs), memory-index and
   CLAUDE.md contents, connected MCP tool names (Gmail/Calendar/Drive). Scrub ids and replace reminder
   payloads with same-shaped placeholder text, **preserving block structure and sizes**. **Re-capture +
   diff on every CLI version bump** — the classifier and goal-extraction rules are observed regularities,
   versioned by fixture.
1. **Neutral tool mapping layer** (shared by both wires): Anthropic `tools`/`tool_use`/`tool_result` ↔
   `Microsoft.Extensions.AI` `AITool` / `FunctionCallContent` / `FunctionResultContent`, **applying the
   essentials allowlist inbound** (`Read`/`Edit`/`Write`/`Bash`; MCP `mcp__*` and everything else never
   forwarded — see §2). Unit-test the round-trip **and the filter** on captured fixtures (real capture from
   the 2026-07-16 probe: 57 declared tools incl. Gmail/Calendar/Drive connectors).
2. **`AnthropicServer` inbound:** parse `tools` + tool-result history; pass through `ChatOptions.Tools`;
   surface a `IsToolContinuation` check.
3. **`AnthropicServer` outbound:** emit `tool_use` content blocks (non-streaming + streaming SSE) with the
   right `stop_reason`; keep text path intact.
4. **`MissionChatClient` tool-capable terminal expert:** forward `ChatOptions.Tools` to the terminal expert
   only; return `FunctionCallContent` verbatim when the model calls a tool.
5. **The request classifier + aux dispatch + three-segment executor:** first the classifier (Design §0) —
   middleware-composed, structural-metadata rules, regression-tested against the captured fixtures. AUX
   requests dispatch by type to registered handlers (probe → static `200`; title-gen → provider passthrough;
   unknown structured-output → passthrough default), per §0's dispatch table — the mission never runs.
   Then, for MISSION requests only, `IsToolContinuation`-driven branching per the model above — pre-agent
   only on user-text turns; agent segment always; **post-agent iff the agent terminates without a tool
   call**; post-agent may **re-enter** the agent segment (repair loop), so build it re-entrant, not a
   one-way pipeline.
6. **Content-addressed enrichment cache (`P`):** key = hash(**canonicalized** conversation prefix through the
   last user message — normalize to messages + tool blocks, **never raw request bytes**); store the pre-agent
   output behind the **`ISessionStore` seam** (in-proc local, shared in cloud — 42.6 swaps it). Cache miss ⇒
   re-run pre-agent (never answer ungrounded).
7. **`duplicate_continuation` counter — the idempotency evidence hook (~10 lines, reuses task 6's hasher).**
   Also compute `F` = hash(canonicalized **full** message array). If `F` was seen within ~5 min, emit a
   `duplicate_continuation` counter (mission, handle, whether the prior call was billed) — **and do nothing
   else.** Replay is deliberately **not built in v1** (see §4); this counter is the trigger that decides
   whether it ever is. **Report the observed value** when reporting this spoke done — a zero means the
   machinery was never warranted; non-zero means build the pre-designed replay in §4.
8. **Integration test — two assertions, both load-bearing:**
   **No-false-green rule (DECIDED 2026-07-16): the pass criterion is planted tool-derived content, never
   status fields.** `exit 0` / `is_error: false` / `subtype: "success"` all pass **today**, against the
   tool-less server (see Context — probed live). Plant a fact the model cannot know — a magic word inside a
   file — so it can appear in the answer **only** via a real tool round-trip; assert on that content. For
   the multi-tool case, **chain the plant** (e.g. `Bash` output is only correct after `Edit` fixed the
   script) so every hop is load-bearing. Status fields stay as fail-fast guards, never as proof.
   **Two test roles (DECIDED 2026-07-16):** the **real-CLI test is authoritative** — SkippableFact (keys +
   `claude` on PATH), run before release and on CLI version bumps alongside a fresh capture (task 0). A
   **mock host that ACTUALLY EXECUTES tool calls** is the CI complement, gating every commit — valid
   *because* the pass criterion is planted content: a status-field mock could false-green, an executing
   mock cannot (the plant is unreachable unless the loop truly works). The mock is frozen beliefs about the
   client; the real CLI catches the client evolving.
   (a) **enrich-once** — drive the tool loop against `forge serve` fronting a mission whose enrichment step
   **increments a counter**; assert the counter is `1` across an N-tool task, and that Read/Edit/Bash
   round-trip.
   (b) **verify-runs** — assert the **post-agent segment executes exactly once, on the final continuation**
   (the regression this spoke exists to prevent: `Verify` silently never firing when tools are in play).
   Add a repair-loop case: a failing `Verify` re-enters the agent segment and can emit a new `tool_use`.
9. **Suite green, zero warnings.** Publish/bump `Katasec.AnthropicServer` if consumed via NuGet.

## Out of scope / open questions

- The Responses-wire (`/v1/responses`) version of this seam — **42.7** (reuses task 1's neutral layer).
- Hosting, multi-replica shared cache implementation — **42.6** (this spoke leaves the store *injectable*).
- MCP door (no tool round-trip needed there — the host owns the loop) — **42.8**.
- ~~OPEN~~ **DECIDED 2026-07-16 — continuation idempotency → see [§4](#4-continuation-idempotency--decided-design-session-2026-07-16).**
  Build the `P` enrichment cache; **do not build `F` replay in v1**; ship the `duplicate_continuation`
  counter (task 7) and let real usage decide. The replay design is pre-recorded in §4 (replay-not-reject,
  key on `F`, canonicalized projection, ~5 min TTL) — **do not re-derive it**; build it only if the counter
  is non-zero. Retry-multiplied spend is bounded meanwhile by 42.5/42.6's hard per-request cap.
- ~~OPEN~~ **DONE 2026-07-16 — the client round-trip was probed against the real `claude` CLI** (2.1.195,
  request-capture middleware on the live fixture). Findings folded in throughout: the aux category +
  classifier (§0), the essentials allowlist (§2), canonicalization evidence (§3), goal extraction (42.1),
  parse-permissively field list (42.1), routing confirmation (42.6), and the two-role no-false-green test
  rules (Done-when + task 8). The capture becomes permanent via task 0; re-probe on CLI version bumps. The
  **`codex` CLI round-trip remains unprobed** — run task 0's harness against it when 42.7 starts.
