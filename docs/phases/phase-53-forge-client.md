# Phase 53 — forge-client and the `forge` CLI

**Goal:** one local Forge client, used by every front end. Extract the Desktop's client logic and
Bob into a new `forge-client` repo, then make the `forge` CLI its first new consumer with
`forge chat`: the Forge TUI, cloud conversations in a full-screen terminal app.

> **Status (2026-09-29):** spoke 1 is done and verified; spoke 2 is in design with open questions.

## Why

`ForgeMission.Application` (Projects, mission authoring, runs and conversations, the hands bridge,
the ForgeAPI client) and `ForgeMission.ClientRuntime` (Bob) are UI-agnostic client logic that
happens to live in forge-desktop. Phase 52.1 made every server path client-agnostic: ForgeAPI
messages, platform-key auth, owner scoping, per-segment billing. Extracting the client core gives
the Desktop and the `forge chat` TUI one implementation instead of one each — the same one-owner,
no-duplicate-path rule applied to the runner in 52.1.

## Locked direction (2026-09-29)

| Decision | Detail |
|---|---|
| One universal control plane | The Desktop (GUI) and the TUI consume the same facility over the same wire protocol (ForgeAPI messages): interoperable, no client-specific server paths. |
| Mission conversations are the chat facility | All chat goes through mission conversations (`CreateMissionConversation` → `SubmitMissionTurn` → event stream). This is what forge-conversations exists for. Project runs are not a chat path. |
| The TUI is the pressure test | Building a second, independent front end on only the published protocol exposes anything the Desktop relied on that is not truly universal (found so far: missing conversation memory; acceptance run through Project runs instead of mission conversations). |
| The Desktop is paused | Deferred until the control plane is proven through the TUI. When reselected, its chat moves to mission conversations and its default-path acceptance is restated around a mission-conversation turn that checks memory. |

## Target structure

```
forge-client/                  new repo — the local Forge client
  Katasec.Forge.Hands          Bob: local tools, policy, confirmation, audit, sandbox
  Katasec.Forge.Client.Contracts  client service DTOs and events (from Application.Transport)
  Katasec.Forge.Client         client core, incl. Bob session lifetimes (depends on Hands, Contracts)
    Projects/  Missions/  Conversations/  Hands bridge/  Adapters/ (ForgeAPI client)
```

| Rule | Detail |
|---|---|
| Hands is its own package | It is the local tool-execution authority (a security boundary): own README, tests, sandbox profiles. Client depends on Hands, never the reverse. It moves to its own repo only if a non-.NET consumer appears. |
| Dependency direction | forge-mcl Core ← forge-client ← { forge-mcl CLI, forge-desktop }. Separate packages, so no package cycle. |
| What stays in forge-desktop | The UI shell (MAUI Host, Presentation) and — until the deferred Phase 52 spokes — the Application Host, Supervisor, and Orchestration. |

## Spokes

Numbers are the execution order.

| # | Spoke | Outcome | State |
|---|---|---|---|
| 1 | [forge-client extraction](phase-53.1-forge-client-extraction.md) | `forge-client` repo with Hands, Client.Contracts, and Client packages; forge-desktop consumes them with no behaviour change. | Done ([record](phase-53.1-forge-client-extraction_completed.md)) |
| 2 | [Conversation memory](phase-53.3-conversation-memory.md) (file 53.3) | The model sees prior turns in a mission conversation; composed server-side by the conversation grain. | Done ([record](phase-53.3-conversation-memory_completed.md)) |
| 3 | [`forge chat`](phase-53.2-forge-chat.md) (file 53.2) | First release: a basic `forge chat` proving the client talks to the cloud, chat works turn by turn, and conversations are durable; later steps build toward the TUI mockup. | First release done ([record](phase-53.2-forge-chat_completed.md)); later steps need design |
| 4 | [Naked default mission](phase-53.4-naked-default-mission.md) (file 53.4) | `forge chat` opens into a single-expert Anthropic mission; missions choose a provider through server-side named profiles. | Done ([record](phase-53.4-naked-default-mission_completed.md)) |
| 5 | [TUI first slice](phase-53.5-tui-first-slice.md) (file 53.5) | `forge chat` becomes a full-screen XenoAtom TUI: header, streaming transcript, composer, key bar. | Done ([record](phase-53.5-tui-first-slice_completed.md)) |
| 6 | [TUI theming](phase-53.6-tui-theming.md) (file 53.6) | Swappable themes by data only; light (default) and dark, chosen in `~/.forge/config.json`. | Done ([record](phase-53.6-tui-theming_completed.md)) |
| 7 | [Markdown replies](phase-53.7-tui-markdown.md) (file 53.7) | Replies render Markdown with the theme's tokens via XenoAtom's `MarkdownControl` and a forge code-block renderer. | Done ([record](phase-53.7-tui-markdown_completed.md)) |
| 8 | [Token streaming](phase-53.8-token-streaming.md) (file 53.8) | Replies stream into the TUI as they are generated; deltas are live-only over the existing conversation path. | Build-ready |

## Replaces

- Backlog "Split Bob (ClientRuntime) out of forge-desktop" — folded into 53.1.
- Phase 52's remaining spokes (52.2–52.4) are deferred to the backlog in favour of this phase.
