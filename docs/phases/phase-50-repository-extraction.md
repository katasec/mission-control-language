# Phase 50 — Repository extraction

**Goal:** split this repo into small, single-purpose repos that build on their own and share code
through packages.

**Not a rewrite or redesign. Move code only; everything must still build and pass tests. Breakage
is expected and useful — report it, don't guess around it.**

> **Status (2026-09-26):** `forge-mcl`, `forge-runner`, and `forge-conversations` are extracted
> and package-based. `forge-platform` is in progress: Billing is extracted; API move is at Task 2.
> Then Rooms → Desktop. Live Mission Chat remains deferred until that sequence is complete.

| # | Repository | Local repo path | Purpose / bounded owner | Intended contents | Current state |
|---|---|---|---|---|---|
| 1 | `forge-mcl` | `/Users/ameerdeen/progs/forge-mcl` | Mission Control Language and generic execution support | Parser, Core, ChatClients, Scout, MissionRegistry, Serve, Docker, CLI, tests, packages | ✅ **Extracted.** Packages published at `0.1.0`. |
| 2 | `forge-runner` | `/Users/ameerdeen/progs/forge-runner` | Stateless hosted mission execution | Runner, Contracts, tests, image workflow, baked fallback missions | ✅ **Extracted.** Runner and Contracts packages published at `0.1.0`. |
| 3 | `forge-conversations` | `/Users/ameerdeen/progs/forge-conversations` | Durable conversation admission, state, and dispatch | Conversation contracts, host, worker, tests, presentation RCL | ✅ **Extracted.** Contracts and Presentation packages published at `0.1.0`; package-only consumers and the canonical zero-argument Desktop/Kind path verified 2026-09-24. |
| 4 | [`forge-platform`](phase-50.4-forge-platform.md) | `/Users/ameerdeen/progs/forge-platform` | API, accounts, platform keys, billing, and ledger | API, Billing, platform contracts and tests | ✅ `ForgeMission.Billing` — moved; `Katasec.Forge.Billing` `0.1.1` published by workflow; all consumers on `0.1.1`<br>🔄 `ForgeMission.Api` — Task 1 ✅ (preflight, `nuget.config`); next: Task 2<br>⬜ `ForgeMission.Rooms.Tests/Api/` (API tests) — to move with the API<br>⬜ `Dockerfile.forgeapi`, `forge-api-image.yml` — to move with the API<br>📄 Tasks and details: [phase-50.4-forge-platform.md](phase-50.4-forge-platform.md) |
| 5 | `forge-rooms` | `/Users/ameerdeen/progs/forge-rooms` | Collaboration domain and browser product | Rooms, Rooms.Data, ForgeUI, tests | ⏸ **Starts after #4 is complete.**<br>⬜ `ForgeMission.Rooms` — to move<br>⬜ `ForgeMission.Rooms.Data` — to move<br>⬜ `ForgeUI` — to move<br>⬜ `ForgeMission.Rooms.Tests` — to move<br>⬜ `Dockerfile.forgeui`, `forge-ui-image.yml` — to move with ForgeUI |
| 6 | `forge-desktop` | `/Users/ameerdeen/progs/forge-desktop` | Local application and supervision | Application, ClientRuntime, Presentation, Desktop, Orchestration | ⏸ **Starts after #5 is complete.**<br>⬜ `ForgeMission.Application` — to move<br>⬜ `ForgeMission.Application.Host` — to move<br>⬜ `ForgeMission.Application.Transport` — to move<br>⬜ `ForgeMission.Application.TransportProbe` — to move<br>⬜ `ForgeMission.ClientRuntime` — to move<br>⬜ `ForgeMission.Presentation` — to move<br>⬜ `ForgeMission.Orchestration` — to move<br>⬜ `ForgeMission.Desktop` — to move<br>⬜ `ForgeMission.Desktop.Contracts` — to move<br>⬜ `ForgeMission.Desktop.Host` — to move<br>⬜ `ForgeMission.Desktop.Photino` — to move<br>⬜ `ForgeMission.Desktop.Installer` — to move<br>⬜ `ForgeMission.ProjectServiceProbe` — to move<br>⬜ `ForgeMission.Tests` — to move<br>⬜ `desktop-build.yml` — to move with Desktop |
| 7 | `mission-control-language` | `/Users/ameerdeen/progs/mission-control-language` | Agent mission control | Hub/spoke plans, agent rules, agents, missions | ⏸ **Starts after #6 is complete.**<br>Stays: `docs/`, `AGENTS.md`/`CLAUDE.md`, `skills/`, `agents/`, `missions/`<br>To place later: `clients/`, `editors/`, `html/`, `scripts/`, `docker-compose.yml`, `Makefile` |

## Extraction order

Numbers match the table rows. Each step starts only after the previous one is complete.

| # | Step | State |
|---|---|---|
| 1–3 | Extract `forge-mcl`, `forge-runner`, `forge-conversations` | ✅ Done |
| 4 | Extract `forge-platform` — Billing, then API | 🔄 In progress: Billing ✅, API Task 1 ✅, Task 2 next |
| 5 | Extract `forge-rooms` | ⏸ After #4 |
| 6 | Extract `forge-desktop` | ⏸ After #5 |
| 7 | `mission-control-language` holds only agent mission control; place the remaining files | ⏸ After #6 |

Live Mission Chat is intentionally outside this sequence. Resume it only after the componentization
is complete, in the repository that owns the surviving Desktop implementation.

## Extraction protocol

Each repo has a task file (e.g. [phase-50.4-forge-platform.md](phase-50.4-forge-platform.md)). It lists what moves.

1. Do only the tasks in the task file, in order.
2. Move, never copy. Moved source stays byte-identical unless the task names the edit.
3. **Expect the first build to fail — that's the point.** Failures show what the move really
   needs. Report each exact error; the fix becomes a new task. Fix only what an error shows is
   broken — never something you inferred.
4. Both repos build and pass tests. Add no new sibling source links.
5. PR per repo, merge, update the map row, end on clean `main`.
