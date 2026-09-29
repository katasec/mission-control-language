# Phase 53.5 — `forge chat` TUI, first slice

> **Status: build-ready (2026-09-30).**
> Hub: [Phase 53](phase-53-forge-client.md). Builds on [53.2](phase-53.2-forge-chat.md) and
> [53.4](phase-53.4-naked-default-mission.md).

**Goal:** `forge chat` becomes a full-screen TUI in the shape of the
[target mockup](../design/forge_tui_mission_chat_mockup.html), starting with the chat itself: header,
streaming transcript, composer, and key bar. Everything else in the mockup comes later.

## Scope

| In this slice | Later (own design) |
|---|---|
| XenoAtom.Terminal.UI (core package) full-screen layout | Left chat list, mission switching |
| Header: `forge │ PROJECT chat` left; `CHAT · V1 · APPROVED · anthropic` right (the provider profile; the model name is runner config the client never sees) | Tabs (Explorer / Missions / Settings) |
| Transcript of typed blocks: **You** (right-aligned pill) and **participant card** (expert name, streamed text), plus a muted progress line ("Answerer is replying …") | Mission graph and artifact images (kitty graphics), "Needs you" gate, inline tool lines |
| Composer: `› Message Chat v1…`; Enter sends; Shift+Enter newline via the kitty keyboard protocol | Markdown rendering of replies |
| Key bar: `enter send · shift+enter newline · pgup/pgdn scroll · ctrl-c stop run · ctrl-d quit` | Light theme |
| Replay of the reopened conversation as blocks | |
| The duplicate final reply fixed in the renderer | |

## Locked decisions

| Area | Decision |
|---|---|
| Visual reference (Ameer, 2026-09-30) | [forge_tui_first_slice_mockup.html](../design/forge_tui_first_slice_mockup.html): accepted as drawn. The wider [target mockup](../design/forge_tui_mission_chat_mockup.html) stays the long-term direction. |
| Piped input (Ameer, 2026-09-30) | When stdin or stdout is not a terminal, `forge chat` keeps today's line mode unchanged (acceptance scripts use it). The TUI runs only on a terminal. |
| Ctrl-C (Ameer, 2026-09-30) | Ctrl-C stops a running turn (existing `CancelAsync`) and stays in the TUI; when idle it does nothing. Ctrl-D quits. |
| Streaming state (Ameer, 2026-09-30) | Replies arrive whole (the runner sends each step's text as one message). While a turn runs, the card shows the expert name with a `▌` body and the progress line; the full text replaces it in one update. Token streaming is a later step ([backlog](../backlog.md)). |
| Plan rulings (supervisor, 2026-09-30) | The mockup's card border `#2a3850` and prompt `#24d5ee` become tokens `cardBorder` and `prompt`. Enter during a running turn is ignored and the text kept. A final result that differs from the last step's text gets its own card titled with the mission name; a final `Error` repeating a step error is shown once. Ctrl-C on a turn that was already running at reopen does nothing (no turn id in the snapshot), as in line mode. |
| Library | XenoAtom.Terminal.UI, core package only (no `.Graphics`/SkiaSharp). Locked in [53.2](phase-53.2-forge-chat.md). |
| Terminal | Ghostty and Kitty; no fallback for other terminals ([53.2](phase-53.2-forge-chat.md)). |
| Owner | forge-mcl `ForgeMission.Cli` (presentation only). `Katasec.Forge.Client` and the server are unchanged; the TUI uses the same Client calls `ForgeChat` makes today. |
| Blocks from events | One mapping, from the conversation's typed events: `UserMessage` → You; `ParticipantStarted` → a participant card titled with the expert name (`Chat:Answerer` → `Answerer`); `ParticipantMessage` with an `Attempt` → that card's text; `Error` → an error line; terminal `RunStatus` other than completed → a `(run failed)` / `(run interrupted)` line. |
| Duplicate final reply | The runner sends each step's message (with `Attempt`) and then the mission's final result (no `Attempt`, forge-runner `MissionCommandProcessor.cs:203`). The renderer shows the final result only when its text differs from the last step's message. No server change. |
| Theme | One dark theme as a named token map taken from the mockup: `bg #0f1622`, `text #c9d4e3`, `muted #6b7a91`, `accent #4f9bff`, `success #8cc152`, `warning #f0b35a`, `bright #e8eef7`, `border #243044`, `bar #1a2230`, `selection #16345a`. Components use only these tokens ([UI Design System](../design/ui-design-system.md) rule, applied to the TUI). |

## Gates

| Gate | Result |
|---|---|
| Security | N/A: presentation only; no new endpoint, credential or data. |
| Engineering philosophy | One event→block mapping reused for replay and live streaming; no new Client API; no settings. |
| Default path | `forge` from `make install` on merged forge-mcl `main`, in Ghostty, after `forge login`: `forge chat` opens full-screen, replays the conversation as blocks, streams a new reply into a participant card, shows each reply once, Ctrl-C stops a running turn. |
| Visual acceptance | Supervisor captures the Ghostty window (`screencapture -l`, method from the 2026-09-30 visibility spike) for the replay and post-turn states and compares them with the mockup; Ameer reviews typing, flicker and keys live. |

## Tasks

| # | Task | Repo |
|---|---|---|
| 1 | TUI for `forge chat` per this spoke, with tests of the event→block mapping (including the duplicate-reply rule) | forge-mcl |
| 2 | Visual and default-path acceptance (supervisor screenshots + Ameer review) | — |

## Done when

1. `forge chat` in Ghostty shows the layout above; a reopened conversation replays as blocks; a new
   turn streams into a participant card.
2. Each reply appears once; the mapping tests cover it.
3. Ctrl-C stops a running turn and stays in the TUI; Ctrl-D quits.
4. Build has zero warnings; Native AOT publish of the CLI is clean.
5. Supervisor screenshots match the [first-slice mockup](../design/forge_tui_first_slice_mockup.html), and Ameer accepts the live review.

## Open questions

None.
