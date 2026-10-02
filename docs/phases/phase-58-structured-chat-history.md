# Phase 58 — Structured chat history

> **Status: build plan approved 2026-10-03 (R1–R4 approved by Ameer).** Next: T1 and T2 (independent).

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

## Design decisions

| # | Question |
|---|---|
| D1 | ✅ **Decided 2026-10-03 (Ameer):** the Host stops flattening. The `MissionInput` body becomes a JSON message list (`role` + content per turn) in a format the Host owns; the runner reads it as messages. Host and runner ship together, with no dual format (no legacy paths). |
| D2 | ✅ **Decided 2026-10-03 (Ameer):** reuse the existing structured path. The runner turns the Host's message list into a `Conversation` in `context["conversation"]` (the 42.1 mechanism); the newest user message stays the mission's root input (as `MissionChatClient` does). forge-mcl `DirectExpertRunner` sends `system + conversation.Messages` for any step that has a conversation, not only tool steps (today plain chat falls through to `BuildMessages`: one user message). Refined by D3: the conversation holds only the **earlier** turns; the new message reaches step 1 as its normal input. |
| D3 | ✅ **Decided 2026-10-03 (Ameer), option B:** one rule for every step: `system` + earlier turns (`user`/`assistant`) + **this step's own input** (`context["output"]`) as the last user message. Step 1's input is the new message; a later step's input is the previous step's output (e.g. Janus Reviewer gets the chat so far, then the Proposer's draft). No step loses its input; every request ends with a user message. Rejected: history only for step 1 (later steps would have no chat context). Works for every provider through the provider-neutral `IChatClient` list (forge-mcl `ChatClients.cs`); D6 checks Anthropic and one OpenAI-style provider. |
| D4 | ✅ **Decided 2026-10-03 (Ameer):** (1) tool turns unchanged: history keeps each turn's user text + final answer, not the tool calls in between; (2) a turn with no reply (failed, rejected, interrupted, or completed without an answer) is the user message followed by a placeholder **assistant** message (`(no reply: run failed)` / `(no reply: rejected)` / `(no reply: run interrupted)`), replacing today's marker inside the user message, so turns alternate; (3) trimming unchanged: 32 KB budget, drop whole oldest turns (user + reply), measured on the JSON message list. |
| D5 | ✅ **Decided 2026-10-03 (Ameer):** delete Ameer's plain `forge chat` conversation (the one with the invented turns) by a **one-off manual removal** from the Host's Azure storage (grain state + message bodies), so no delete feature distracts from this phase. Type-2 exception: scope is that one conversation; done only after the fix ships and only with Ameer's explicit go-ahead at that time; list exactly what will be removed and make sure the grain is not active before deleting. No product code. Removal condition: none (one-off). Nothing in the Host or client can delete a conversation today (checked 2026-10-03). |
| D6 | ✅ **Decided 2026-10-03 (Ameer):** three layers. (1) **Default path (closes the phase):** the runner logs each model call's *shape* only, always on, no setting, never content, e.g. `model request: system + 7 messages [user, assistant, …, user]`, read from the runner's Azure logs. (2) **Tests:** forge-mcl tests capture the real HTTP request body for Anthropic and one OpenAI-style client: system in its own field, alternating messages, no role-labelled transcript in any message, placeholder replies for no-reply turns. (3) **Live repeat:** Ameer's Go → C# → bash → nodejs sequence in `forge chat` on the installed build after release: no invented turns. |

**Out of scope (Ameer, 2026-10-03):** timestamps are never sent to the model; message content stays exactly what the user and the experts wrote. Showing times in `forge chat` is a separate backlog item.

Gates: [Security Architecture](../design/security-architecture.md),
[Engineering Philosophy](../design/engineering-philosophy.md),
[Default-Path Acceptance](../design/default-path-acceptance.md) (`forge chat` defaults).

## Plan refinements (from the code, approved 2026-10-03)

| # | Finding | Refinement |
|---|---|---|
| R1 | Reusing `context["conversation"]` for every step would change `forge serve` / `forge claude`: its Anthropic door puts the full client transcript there (forge-mcl `MissionChatClient.cs:118,164-171`), and pre-agent steps would start receiving it, scaffolding included. | A **dedicated** typed context object for durable chat history: `ChatHistory` (earlier turns only) under its own key. `context["conversation"]` and `{{conversation}}` keep today's meaning. Refines D2. |
| R2 | `--hands` runs take Core's root-scoped path, which seeds the context without `ContextObjects` (forge-mcl `PipelineRunner.cs:99-100, 980-983`); history placed there would never reach the model in `--hands` mode. | The root-scoped path carries `ChatHistory` too. Child missions still inherit nothing (unchanged). |
| R3 | Today's body already ends with the new message (forge-conversations `ConversationGrain.cs:1207`), and the runner reads only one of `MissionInput` / `Goal` (forge-runner `MissionCommandProcessor.cs:88`). | The body holds **earlier turns only**. The runner reads `Goal` as the root input and, when present, `MissionInput` as history. The JSON type lives in **Conversations.Contracts** (forge-conversations' own package, so the Host owns the format; the runner already consumes it). |
| R4 | No dual format, so old Host + new runner (or the reverse) can't both work. Precedent: Phase 53.3 deployed runner, then Host. | One release window in dev: deploy the runner, then the Host straight away, with no chat turns in between. A runner that receives the old flat text fails the turn with a clear error; nothing tries to parse both formats. |

## Build plan

Tasks run in this order; each is one subagent task under the [supervisor workflow](../design/supervisor-workflow.md), on an `adeen/` branch, one PR per repo.

| Task | Repo | Work | Done when |
|---|---|---|---|
| T1 | forge-conversations | Contracts **0.8.0**: a `MissionHistory` body type (list of `{ role: user\|assistant, text }`) with source-generated JSON. Host: `ComposeMissionInputAsync` writes earlier turns only as that JSON (D1, R3); no-reply turns become user + placeholder assistant (D4); trimming measured on the JSON (D4); drop the `CoreConversation.ToString()` use (Core stays referenced for package admission). Update the tests that assert the flat format (`ConversationApiTests.cs:324-396`, `ConversationGrainOutboxTests.cs`, `ConversationContractsRoundTripTests.cs`). Publish Contracts 0.8.0 (tag `forge-conversations-v0.8.0`). | Tests pass; Contracts 0.8.0 on the feed. |
| T2 | forge-mcl | Core **0.1.4**: `ChatHistory` context object (R1); `DirectExpertRunner` (`RunAsync` and `StreamAsync`) sends `system + history + user(context["output"])` for every step when it is present (D3); the root-scoped path carries it (R2). Tests: `DirectExpertRunnerTests`; request-body captures for Anthropic (existing `CaptureAnthropicRequestAsync` pattern, `ChatClientsTests.cs:120-183`) and a new OpenAI-style capture: system in its own field, alternating roles, no role-labelled transcript, placeholder replies (D6 layer 2). Publish Core 0.1.4 (tag `core-v0.1.4`). | Tests pass, 0 warnings; Core 0.1.4 on the feed. |
| T3 | forge-runner | **0.20.0**: Core 0.1.4 + Contracts 0.8.0; `MissionCommandProcessor` reads `Goal` as root input and `MissionInput` (if any) as `MissionHistory` → `ChatHistory`; `GenericDurableMissionExecutor` passes it through. Shape log (D6 layer 1): a logging `DelegatingChatClient` in `RunnerExpertRunnerFactory.Build`, always on, roles and counts only, e.g. `model request: system + 7 messages [user, assistant, …, user]`. Tag `forge-runner-v0.20.0` (CI builds the image). | Tests pass; image `forge-runner:0.20.0` in ACR. |
| T4 | forge-conversations | Host image **0.9.0** (local build, Dockerfile.conversationhost, as today). | Image in ACR. |
| T5 | forge-infra | Release window (R4): `make 500-app-what-if` / `make 500-app` with runner 0.20.0, then `make 525-conversation-app-what-if` / `make 525-conversation-app` with Host 0.9.0. Then D6 layers 1 and 3 on the default `forge chat` path. | Shape log lines in `log-forge-dev` show one message per turn; Ameer's Go → C# → bash → nodejs repeat has no invented turns. |
| T6 | Host storage | D5 one-off removal, only with Ameer's go-ahead then: list the rows of that conversation in table `forgeconversationevents` (PartitionKey `v1\|{tenant}\|{conversationId:N}`) and its blobs in `forgeconversationartifacts/{tenant}/{conversationId:N}/`, confirm the grain is idle, then delete. | Conversation gone; `forge chat` starts a fresh one. |

## Gate review

| Gate | Answer |
|---|---|
| [Security](../design/security-architecture.md) | No new entry point, datastore access or tier change. The one cross-context contract change (the `MissionInput` body format, Contracts 0.8.0) is a Type-1 decision, locked here. Shape logs carry roles and counts, never message text. |
| [Engineering philosophy](../design/engineering-philosophy.md) | No new settings (the shape log is always on). No dual format or legacy path. A named owner per piece: the Host owns the history format, Core owns how a step builds its messages, the runner owns the log. "Done when" names the observation. |
| [Default path](../design/default-path-acceptance.md) | `forge chat` from `make install` on forge-mcl `main` against ForgeAPI; the client itself is unchanged. Evidence: T5. |

## Done when

On the default `forge chat` path, the captured provider request for a multi-turn chat has one
message per prior turn with the right roles, system text in the system field, and no role-labelled
transcript inside any message; and a repeat of the 2026-10-03 hello-world sequence produces no
invented turns.
