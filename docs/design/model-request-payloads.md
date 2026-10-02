# Model request payloads — reference practices

> **Status: reference guidance (2026-10-03), not yet a locked design.** Input for the backlog item
> "Send chat history as structured messages, per provider API docs". Drawn from two read-only
> reference harnesses: DeepSeek Harness (`~/progs/deepseek-harness`) and opencode
> (`~/progs/opencode`). Neither flattens live history.

## The bug this guards against

```
 today (forge)                                  reference harnesses
 ┌──────────────────────────────┐               ┌──────────────────────┐
 │ user: "user: hi              │               │ system  (system slot)│
 │        assistant: hello      │               │ user    "hi"         │
 │        user: now nodejs"     │  ──should be─▶│ assistant "hello"    │
 └──────────────────────────────┘               │ user    "now nodejs" │
   one message; the model continues             └──────────────────────┘
   the script and invents user turns              one wire message per turn
```

forge-conversations `ConversationGrain.RenderMissionInput` builds `ChatMessage`s, then
`Conversation.ToString()` (forge-mcl `Core/Runtime/Conversation.cs`) flattens them to
`user: …\n\nassistant: …`, which reaches the model as one user message.

## Practices both harnesses follow

| Rule | DeepSeek Harness | opencode |
|---|---|---|
| **History is a list of role-tagged messages with typed parts; each maps to its own wire message.** Never a transcript inside one message. | `packages/llm/llm/src/message.ts:129-138`; loop `packages/llm/llm-deepseek/src/serialize.ts:203-232` | `packages/opencode/src/session/message-v2.ts:131-415` → AI SDK `convertToModelMessages` (`:407`) |
| **System text goes in the provider's system slot**, first, not inside a user message. | `serialize.ts:344-347`; doc `docs/subsystems/llm-streaming.md:513` | joined into ≤2 system messages, `src/session/llm/request.ts:58-111` |
| **Tool calls and results are typed blocks paired by call id**; each result is its own `tool` message after the call. | `serialize.ts:166-172, 222-229` | `message-v2.ts:315-323` |
| **No dangling tool calls**: an interrupted call gets a synthetic error result (providers reject unanswered calls). | `packages/core/session/src/repair.ts:19-25` | `message-v2.ts:349-360` ("[Tool execution was interrupted]") |
| **No empty or null content** (providers return 400): send `""`/a placeholder or drop the empty part. | `serialize.ts:176-184` (`content: ""`), `:227` (`(no output)`) | `src/provider/transform.ts:168-187` (Anthropic) |
| **Reasoning is replayed per provider rules** (DeepSeek `reasoning_content`; Anthropic signed thinking only for the same model). | `serialize.ts:185-190` | `message-v2.ts:245, 362-376`; `transform.ts:182-214, 303-353` |
| **Compaction keeps call/result pairs intact**; old tool output is replaced by a placeholder, not deleted. | `docs/subsystems/compaction.md:86-88` | `message-v2.ts:293-296` |
| **Tests assert the outgoing request shape** per provider. | `serialize.spec.ts` | `test/provider/transform.test.ts` |

The one place opencode writes a `[User]: … [Assistant]: …` transcript is the one-shot compaction
call (`src/session/compaction.ts:54-85`), where it is explicitly material to summarise, not a live
conversation.

## Provider-specific details seen (not yet forge decisions)

- Prompt caching: opencode sets at most 4 breakpoints, on the first 2 system messages and the last 2
  messages (`transform.ts:358-403`).
- Tool-call ids are sanitised per provider (Claude `[a-zA-Z0-9_-]`, Mistral 9 alphanumeric chars;
  `transform.ts:224-263`).
- Image parts a provider can't take inside a tool result move to a following user message (both
  harnesses).

## What forge's design must decide

The body shape that carries structured history from the Host to the runner (today a string
`MissionInput` body), and the evidence: the outgoing provider request JSON shows one message per
turn, system text in the system field, and typed tool blocks.
