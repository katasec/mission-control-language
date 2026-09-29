# Phase 53.3 — Conversation memory

> **Status: done (2026-09-29), verified live.** Evidence:
> [phase-53.3-conversation-memory_completed.md](phase-53.3-conversation-memory_completed.md).
> Hub: [Phase 53](phase-53-forge-client.md). **Must land before [53.2](phase-53.2-forge-chat.md)'s
> acceptance** ("the second reply reflects the first" cannot pass without it).

**Goal:** a mission conversation's model sees the conversation so far. Today each turn runs with
only its own text: the transcript is stored but never given back to the model.

## Verified problem

| Step | Code | Passes |
|---|---|---|
| Host builds a mission turn | forge-conversations `ConversationGrain.cs:383-385` (`AcceptMissionConversationTurnAsync`) | only this turn's text as `Goal` |
| Command | `ConversationCommand` (Contracts) | `Goal`; no history field |
| Runner | forge-runner `MissionCommandProcessor.cs:99` → `GenericDurableMissionExecutor.cs:25-27` | `Goal` as the single root input |

Storage (durable transcript, Host Table Storage) is correct. **Memory** (the model receiving prior
turns) is missing. No client cache or client-side memory is needed or wanted: the server already
holds everything.

## Locked decisions (verified against the guardrails, 2026-09-29)

| Area | Decision |
|---|---|
| Facility | **Mission conversations are the single conversation facility** for every front end (TUI now, Desktop when reselected). Memory applies to `SubmitMissionTurn` → `AcceptMissionConversationTurnAsync` only. Project runs are independent runs by design and are not a chat path. |
| Owner | `ConversationGrain` composes memory from its own store. The runner only receives text. |
| Contract | `ConversationCommand` gains one optional field, `MissionInput` (the composed model input). `Goal` keeps one meaning: what the user typed — it stays the stored user message, the retry source, and the run title. Putting history in `Goal` was rejected: it nests history into every stored message. |
| Read | Page backward from the latest sequence with the existing event-store range read (`ReadRangeAsync`); the same store the display queries use. No full replay per turn, no new store, no new query. |
| What counts as a turn | Group by `TurnId`, keep only the latest attempt. Include its answer only when that attempt ended `Completed`; the answer is its last `ParticipantMessage` (the runner emits the final result last). Exclude `Error`, tool, and Hands events. |
| Turns that did not complete (Ameer, 2026-09-29) | Include the user's text, followed by a fixed marker taken from the latest attempt's terminal status: `(no reply: run failed)`, `(no reply: run interrupted)`, `(no reply: rejected)`. The model then sees what the user sees (Phase 45 keeps failed turns visible) and does not assume it answered. No setting, no model call. |
| Rendering | Reuse Core's `Conversation` renderer (`ForgeMission.Core.Runtime.Conversation`, present in the published Mcl.Core 0.1.0), prior turns first, the new message last. |
| Budget | Computed per command: `MaxStartCommandJsonBytes` (32 KB) minus the serialized command without `MissionInput`. Drop oldest turns first; never drop the new message; if under ~1 KB remains, send the new message alone. No setting. |
| Runner | One line: `MissionCommandProcessor.cs:99` passes `command.MissionInput ?? command.Goal`. |
| Rejected alternatives | Core `context["conversation"]` (only read in tool-mode steps; not inherited by child missions; lost across a Hands pause); a new shared composer (Core's renderer already exists; Rooms' assembler is a different capability: another store, several named senders, reply-to). |

## Tasks

| # | Task | Repo |
|---|---|---|
| 1 | Add optional `ConversationCommand.MissionInput`; publish the next `Katasec.Forge.Conversations.Contracts` version | forge-conversations |
| 2 | `ConversationGrain` composes `MissionInput` (read, turn grouping, unfinished-turn marker, budget, Core renderer); Done-when tests 1–2 | forge-conversations |
| 3 | Runner passes `command.MissionInput ?? command.Goal`; consume the new Contracts version | forge-runner |
| 4 | Deploy Host and runner through forge-infra `make` targets (what-if first); run the live check (Done when 3) | forge-infra |

## Done when

1. A test in forge-conversations: turn 2's `MissionInput` contains turn 1's user text and final
   answer; a retried turn appears once; a failed turn appears as its user text plus `(no reply: run failed)`.
2. A budget test: a long history drops oldest turns and never the new message.
3. Live, through ForgeAPI mission-conversation messages: turn 1 "my name is Ameer", turn 2 "what is
   my name?" → the reply says Ameer.
