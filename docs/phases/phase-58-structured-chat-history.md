# Phase 58 — Structured chat history

> **Status: selected 2026-10-03; design in progress (D1 decided).** Not build-ready: the open design questions
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
| D2 | Where history becomes provider messages: the runner's pipeline input, the expert step, or the provider client? How does a mission's root input relate to history? |
| D3 | Do multi-expert missions (e.g. Janus `Proposer -> Reviewer`) get the history, and with what roles? |
| D4 | Tool turns, failed/interrupted turns (today a text marker), and history trimming: what replaces each? |
| D5 | Existing stored conversations already contain invented turns. Leave them as they are (no legacy paths), or not? |
| D6 | Evidence: how we capture the outgoing provider request JSON on the default path to prove one message per turn. |

Gates: [Security Architecture](../design/security-architecture.md),
[Engineering Philosophy](../design/engineering-philosophy.md),
[Default-Path Acceptance](../design/default-path-acceptance.md) (`forge chat` defaults).

## Done when

On the default `forge chat` path, the captured provider request for a multi-turn chat has one
message per prior turn with the right roles, system text in the system field, and no role-labelled
transcript inside any message; and a repeat of the 2026-10-03 hello-world sequence produces no
invented turns.
