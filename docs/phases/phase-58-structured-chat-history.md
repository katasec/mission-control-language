# Phase 58 — Structured chat history

> **Status: selected 2026-10-03; design in progress (D1–D4 decided).** Not build-ready: the open design questions
> below must be closed first.

## Problem

```
 today                                            wanted
 ┌───────────────────────────────────┐            ┌────────────────────────────┐
 │ user: "user: show me Go …         │            │ system   (system field)    │
 │        assistant: # Go …          │ ─────────▶ │ user      "show me Go …"   │
 │        user: now nodejs"          │            │ assistant "# Go …"         │
 └───────────────────────────────────┘            │ user      "now nodejs"     │
   one message: the model continues the            └────────────────────────────┘
   script and invents user turns                     one message per turn
```

forge-conversations `ConversationGrain.RenderMissionInput` builds `ChatMessage`s and then flattens
them with forge-mcl `Core/Runtime/Conversation.ToString()` (`user: …\n\nassistant: …`). The result is
the string `MissionInput` body, which reaches the model as one user message. Seen 2026-10-03: asked
only for a Node.js hello world, the model's reply invented 23 `user: show me hello world in <lang>`
turns, and its next answer treated them as Ameer's requests.

## Goal

Every model request is shaped as the provider's API documents it: history as separate
`user`/`assistant` messages, system text in the system field, tool calls and results as typed blocks
paired by id. Reference practices: [model-request-payloads.md](../design/model-request-payloads.md).

## Open design questions (close before any build)

| # | Question |
|---|---|
| D1 | ✅ **Decided 2026-10-03 (Ameer):** the Host stops flattening. The `MissionInput` body becomes a JSON message list (`role` + content per turn) in a format the Host owns; the runner reads it as messages. Host and runner ship together, with no dual format (no legacy paths). |
| D2 | ✅ **Decided 2026-10-03 (Ameer):** reuse the existing structured path. The runner turns the Host's message list into a `Conversation` in `context["conversation"]` (the 42.1 mechanism); the newest user message stays the mission's root input (as `MissionChatClient` does). forge-mcl `DirectExpertRunner` sends `system + conversation.Messages` for any step that has a conversation, not only tool steps (today plain chat falls through to `BuildMessages`: one user message). Refined by D3: the conversation holds only the **earlier** turns; the new message reaches step 1 as its normal input. |
| D3 | ✅ **Decided 2026-10-03 (Ameer), option B:** one rule for every step: `system` + earlier turns (`user`/`assistant`) + **this step's own input** (`context["output"]`) as the last user message. Step 1's input is the new message; a later step's input is the previous step's output (e.g. Janus Reviewer gets the chat so far, then the Proposer's draft). No step loses its input; every request ends with a user message. Rejected: history only for step 1 (later steps would have no chat context). Works for every provider through the provider-neutral `IChatClient` list (forge-mcl `ChatClients.cs`); D6 checks Anthropic and one OpenAI-style provider. |
| D4 | ✅ **Decided 2026-10-03 (Ameer):** (1) tool turns unchanged: history keeps each turn's user text + final answer, not the tool calls in between; (2) a turn with no reply (failed, rejected, interrupted, or completed without an answer) is the user message followed by a placeholder **assistant** message (`(no reply: run failed)` / `(no reply: rejected)` / `(no reply: run interrupted)`), replacing today's marker inside the user message, so turns alternate; (3) trimming unchanged: 32 KB budget, drop whole oldest turns (user + reply), measured on the JSON message list. |
| D5 | Existing stored conversations already contain invented turns. Leave them as they are (no legacy paths), or not? |
| D6 | Evidence: how we capture the outgoing provider request JSON on the default path to prove one message per turn. |

**Out of scope (Ameer, 2026-10-03):** timestamps are never sent to the model; message content stays exactly what the user and the experts wrote. Showing times in `forge chat` is a separate backlog item.

Gates: [Security Architecture](../design/security-architecture.md),
[Engineering Philosophy](../design/engineering-philosophy.md),
[Default-Path Acceptance](../design/default-path-acceptance.md) (`forge chat` defaults).

## Done when

On the default `forge chat` path, the captured provider request for a multi-turn chat has one
message per prior turn with the right roles, system text in the system field, and no role-labelled
transcript inside any message; and a repeat of the 2026-10-03 hello-world sequence produces no
invented turns.
