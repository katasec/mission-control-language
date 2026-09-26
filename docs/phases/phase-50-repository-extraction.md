# Phase 50 — Repository extraction

**Goal:** split this repo into small, single-purpose repos that build on their own and share code
through packages.

**Not a rewrite or redesign. Move code only; everything must still build and pass tests. Breakage
is expected and useful — report it, don't guess around it.**

> **Status (2026-09-26):** `forge-mcl`, `forge-runner`, and `forge-conversations` are extracted
> and package-based. `forge-platform`, `forge-rooms`, and `forge-desktop` are extracted. Next: row 7, tidy this repo. Live Mission Chat remains deferred until that sequence is complete.

| # | Repository | Local repo path | Purpose / bounded owner | Intended contents | Current state |
|---|---|---|---|---|---|
| 1 | `forge-mcl` | `/Users/ameerdeen/progs/forge-mcl` | Mission Control Language and generic execution support | Parser, Core, ChatClients, Scout, MissionRegistry, Serve, Docker, CLI, tests, packages | ✅ **Extracted.** Packages published at `0.1.0`. |
| 2 | `forge-runner` | `/Users/ameerdeen/progs/forge-runner` | Stateless hosted mission execution | Runner, Contracts, tests, image workflow, baked fallback missions | ✅ **Extracted.** Runner and Contracts packages published at `0.1.0`. |
| 3 | `forge-conversations` | `/Users/ameerdeen/progs/forge-conversations` | Durable conversation admission, state, and dispatch | Conversation contracts, host, worker, tests, presentation RCL | ✅ **Extracted.** Contracts and Presentation packages published at `0.1.0`; package-only consumers and the canonical zero-argument Desktop/Kind path verified 2026-09-24. |
| 4 | [`forge-platform`](phase-50.4-forge-platform.md) | `/Users/ameerdeen/progs/forge-platform` | API, accounts, platform keys, billing, and ledger | API, Billing, platform contracts and tests | ✅ **Extracted.** `ForgeMission.Billing` (package `Katasec.Forge.Billing` `0.1.1`), `ForgeMission.Api` + tests, API image build (`forge-api` `0.3.2` pushed from forge-platform).<br>📄 Details: [phase-50.4-forge-platform.md](phase-50.4-forge-platform.md) |
| 5 | [`forge-rooms`](phase-50.5-forge-rooms.md) | `/Users/ameerdeen/progs/forge-rooms` | Collaboration domain and browser product | Rooms, Rooms.Data, ForgeUI, tests | ✅ **Extracted.** `ForgeMission.Rooms`, `ForgeMission.Rooms.Data`, `ForgeUI`, `ForgeMission.Rooms.Tests`, dev tooling, ForgeUI image build (`forge-ui` `0.6.2` pushed from forge-rooms).<br>📄 Details: [phase-50.5-forge-rooms.md](phase-50.5-forge-rooms.md) |
| 6 | [`forge-desktop`](phase-50.6-forge-desktop.md) | `/Users/ameerdeen/progs/forge-desktop` | Local application and supervision | Application, ClientRuntime, Presentation, Desktop, Orchestration | ✅ **Extracted.** All 14 Desktop projects, `Makefile`, AOT scripts, `.vscode/`, `desktop-build.yml` (both bundles built from forge-desktop).<br>📄 Details: [phase-50.6-forge-desktop.md](phase-50.6-forge-desktop.md) |
| 7 | `mission-control-language` | `/Users/ameerdeen/progs/mission-control-language` | Agent mission control | Hub/spoke plans, agent rules, agents, missions | ⬜ **Next.** Task file to be written.<br>Stays: `docs/`, `AGENTS.md`/`CLAUDE.md`, `skills/`, `agents/`, `missions/`<br>To place later: `clients/`, `editors/`, `html/`, stale `AGENTS.md` sections |

## Extraction order

Numbers match the table rows. Each step starts only after the previous one is complete.

| # | Step | State |
|---|---|---|
| 1–6 | Extract `forge-mcl`, `forge-runner`, `forge-conversations`, `forge-platform`, `forge-rooms`, `forge-desktop` | ✅ Done |
| 7 | `mission-control-language` holds only agent mission control; place the remaining files | ⬜ Next — task file to be written |

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
