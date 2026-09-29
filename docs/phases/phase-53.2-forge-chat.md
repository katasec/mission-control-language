# Phase 53.2 — `forge chat`

> **Status: design (2026-09-29). Not build-ready** — see [Open questions](#open-questions). Starts
> after [53.1](phase-53.1-forge-client-extraction.md). Hub: [Phase 53](phase-53-forge-client.md).

**Goal:** `forge chat` opens the Forge TUI, a full-screen interactive terminal app in the style of
Claude Code, Codex, and Grok CLI. It runs multi-turn cloud conversations with a mission, using
`Katasec.Forge.Client`. No backend changes.

## Locked decisions

| Area | Decision |
|---|---|
| Front end (2026-09-29) | One TUI, launched by the `forge` CLI as `forge chat`. There is no separate TUI app or command, and no one-shot mode. `chat`, not `code`, because most MCL missions are not coding; a coding-focused entry would be a preset of `forge chat`, not a second TUI. |
| Server path | ForgeAPI conversation messages (52.1), authenticated by the `forge login` platform key. |
| Client | `Katasec.Forge.Client` only; the CLI adds no second ForgeAPI client. |
| Conversation path | Mission conversations (`CreateMissionConversation` → `SubmitMissionTurn` → event stream): history and paused tools stay server-side. |
| AOT | The `forge` CLI is Native AOT; the Client and Hands packages must build AOT-clean for it (source-generated JSON already used by the contracts). |

## Default path

| Fact | Value |
|---|---|
| Artifact | The released `forge` binary, after `forge login`, no `FORGE_*` overrides. |
| Action / result | `forge chat` with a mission, send two turns; the second reply reflects the first; the member is debited once per segment. |

## Open questions

1. **Mission source.** A local `.mcl` mission, a built-in (e.g. `websearch`), or both for the first
   version?
2. ~~UX~~ — closed 2026-09-29: full-screen TUI; see Locked decisions.
3. **Local tools.** Does the first version support mission hands (Bob in the terminal, with
   confirmation prompts), or refuse tool-using missions until a later task?
4. **Resume.** Can `forge chat` reopen an existing conversation (list and pick), or is that later?
5. **How the CLI consumes Core.** The `forge` CLI project-references Mcl.Core, while
   `Katasec.Forge.Client` brings Mcl.Core as a package, so the CLI build would carry two copies of
   `ForgeMission.Core.dll`. Decide how the CLI consumes Core. (Raised by 53.1 Q4; does not block 53.1.)
6. **TUI library.** Which terminal UI library, given the `forge` binary is Native AOT (the
   library must build AOT-clean)? Candidates: Spectre.Console, Terminal.Gui v2, hand-rolled ANSI.
