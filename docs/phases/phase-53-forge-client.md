# Phase 53 — forge-client and the `forge` CLI

**Goal:** one local Forge client, used by every front end. Extract the Desktop's client logic and
Bob into a new `forge-client` repo, then make the `forge` CLI its first new consumer with
`forge chat`: cloud conversations from the terminal.

> **Status (2026-09-29):** spoke 1 is build-ready; spoke 2 is in design with open questions.

## Why

`ForgeMission.Application` (Projects, mission authoring, runs and conversations, the hands bridge,
the ForgeAPI client) and `ForgeMission.ClientRuntime` (Bob) are UI-agnostic client logic that
happens to live in forge-desktop. Phase 52.1 made every server path client-agnostic: ForgeAPI
messages, platform-key auth, owner scoping, per-segment billing. Extracting the client core gives
the Desktop, the CLI, and a future TUI one implementation instead of one each — the same one-owner,
no-duplicate-path rule applied to the runner in 52.1.

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
| 1 | [forge-client extraction](phase-53.1-forge-client-extraction.md) | `forge-client` repo with Hands, Client.Contracts, and Client packages; forge-desktop consumes them with no behaviour change. | Build-ready |
| 2 | [`forge chat`](phase-53.2-forge-chat.md) | The `forge` CLI runs multi-turn cloud conversations through Katasec.Forge.Client. | Design |

## Replaces

- Backlog "Split Bob (ClientRuntime) out of forge-desktop" — folded into 53.1.
- Phase 52's remaining spokes (52.2–52.4) are deferred to the backlog in favour of this phase.
