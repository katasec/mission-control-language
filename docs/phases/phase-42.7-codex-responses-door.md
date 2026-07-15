# Phase 42.7 — Codex door (`/v1/responses`)

> **Status: Design (2026-07-15).** Add the **second door**: OpenAI's Codex, which speaks the **Responses
> API**, pointed at a forge mission. The responder path is nearly free — `OaiServer` already serves
> `/v1/responses` — so this spoke is mostly the **function-call round-trip** (the Codex twin of 42.3) plus a
> `forge codex` launcher.
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:**
> [42.3](phase-42.3-tool-capable-enriching-responder.md) (the neutral tool-mapping layer + re-entrancy gate
> it factored out), [42.4](phase-42.4-container-convergence.md) (one image serves `/v1/responses` too) ·
> **AOT rules:** [CLAUDE.md](../../CLAUDE.md).
>
> **Done when:** the real `codex` CLI, configured to point at a forge mission (env or `~/.codex/config.toml`),
> completes a task through the mission — with function calls round-tripping — and `forge codex @websearch`
> launches it wired to the hosted endpoint with the platform key (no ChatGPT login).

## Context an implementer needs (verified vs OpenAI docs 2026-07-15)

- **Codex is Responses-API-only.** As of ~Feb 2026 Codex removed Chat Completions; `wire_api = "responses"`
  is the only accepted value and the default. So **only `/v1/responses` matters** — forge's
  `/v1/chat/completions` is unused by Codex.
- **`OaiServer` already serves `/v1/responses`** (`HandleResponsesAsync` in the sibling `oai-server-dotnet`
  repo — streaming + non-streaming, the full `response.created → output_text.delta → done → completed` SSE
  sequence). It is **text-only** today: parses `input`, emits `output_text` — **no `function_call` /
  `function_call_output` round-tripping.** That is the gap.
- **Codex CLI redirect (both work):**
  - env: `OPENAI_BASE_URL=http://…/v1` + `OPENAI_API_KEY=<token>` (repoints the built-in provider).
  - config: `~/.codex/config.toml` `[model_providers.forge]` with `base_url`, `env_key`,
    `wire_api="responses"`, `requires_openai_auth=false` (so it doesn't assume an `sk-` OpenAI key), selected
    by top-level `model_provider="forge"`.
  - **Custom providers auth with a plain bearer token — no ChatGPT login** → the platform-key story works
    verbatim.
- **Surface reach:** Codex CLI ✅, VS Code ⚠️ (shares `config.toml` but a new-session model-select bug —
  issue #4558/#6963), desktop ❌ (ChatGPT sign-in), web ❌. Same shape as Claude Code — CLI is the target.

## Design

**Reuse, don't rebuild.** 42.3 factors the tool mapping through the neutral `Microsoft.Extensions.AI` middle
(`FunctionCallContent` / `FunctionResultContent`) and defines the **re-entrancy gate** (user-text vs
continuation). This spoke maps that same middle to the **Responses wire**:

- **Inbound:** parse Responses `input` items — user messages **and** `function_call_output` items (the Codex
  equivalent of `tool_result`) — into `ChatMessage`s + `FunctionResultContent`; parse the request's `tools`
  into `ChatOptions.Tools`. `IsToolContinuation` ⇒ last input item is a `function_call_output`.
- **Outbound:** when the terminal expert yields a `FunctionCallContent`, emit a Responses **`function_call`**
  output item (non-streaming + the correct streaming events) instead of `output_text`; text path unchanged.
- **Enrich-once gate:** identical to 42.3 — new user turn runs the full mission, `function_call_output`
  continuation resumes only the terminal expert.

**`forge codex` launcher** — the OpenAI-wire twin of `forge claude` (42.2): ephemeral serve (in-proc /
`--container`) → export `OPENAI_BASE_URL` + `OPENAI_API_KEY` → `exec codex` → teardown. Hosted mode:
`forge codex @websearch` → `OPENAI_BASE_URL=https://forge.katasec.com/@websearch`, platform key as token.
Also emit a `~/.codex/config.toml` `[model_providers.forge]` snippet via `forge codex --print-config` for
users who prefer the persistent, robust wiring over the ephemeral launcher.

## Tasks (chronological)

1. **Responses tool round-trip in `OaiServer.HandleResponsesAsync`:** inbound `tools` +
   `function_call_output`; outbound `function_call` items (non-streaming + streaming). Reuse the 42.3 neutral
   mapping layer; add the Responses-specific serialization only.
2. **Re-entrancy gate on the Responses wire:** `IsToolContinuation` via `function_call_output`; new-turn vs
   continuation branching (shared logic with 42.3).
3. **`MissionChatClient`** already forwards `ChatOptions.Tools` after 42.3 — verify the Responses path uses
   it (the terminal expert is wire-agnostic).
4. **`forge codex` launcher** (env-wire, in-proc + `--container`, teardown) + `--print-config` snippet.
5. **Integration test:** drive the real `codex` CLI (or a faithful Responses mock host) against `forge serve`
   fronting a mission; assert function-call round-trip + enrich-once. `SkippableFact` gated on `codex` on
   PATH + keys.
6. **Publish/bump `Katasec.OaiServer`** if consumed via NuGet; suite green, zero warnings.
7. **Hosted:** `forge codex @websearch` against `forge.katasec.com` (42.6 routing already handles
   `/v1/responses`).

## Out of scope

- Chat Completions support for Codex — **dead** (Codex removed it); don't build for it.
- VS Code new-session model bug — an upstream Codex issue; document the "start via CLI, continue in IDE"
  workaround, don't work around it in forge.
- Desktop Codex — OAuth-walled; reachable only via MCP (42.8) later.
