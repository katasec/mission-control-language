# Phase 53.2 — `forge chat`

> **Status: design (2026-09-29). Not build-ready** — see [Open questions](#open-questions). Starts
> after [53.1](phase-53.1-forge-client-extraction.md). Hub: [Phase 53](phase-53-forge-client.md).

**Goal:** `forge chat` runs a multi-turn cloud conversation from the terminal, using
`Katasec.Forge.Client`. No backend changes.

## Locked decisions

| Area | Decision |
|---|---|
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
2. **UX.** Interactive REPL, single message (`forge chat <mission> "…"`), or both?
3. **Local tools.** Does the first version support mission hands (Bob in the terminal, with
   confirmation prompts), or refuse tool-using missions until a later task?
4. **Resume.** Can `forge chat` reopen an existing conversation (list and pick), or is that later?
5. **How the CLI consumes Core.** The `forge` CLI project-references Mcl.Core, while
   `Katasec.Forge.Client` brings Mcl.Core as a package, so the CLI build would carry two copies of
   `ForgeMission.Core.dll`. Decide how the CLI consumes Core. (Raised by 53.1 Q4; does not block 53.1.)
