# Phase 58 — Mission runs in the `forge chat` TUI

> **Status:** UI direction agreed with Ameer 2026-10-02; slice 1 not yet build-ready (the
> implementation plan and gate review come next). This is the terminal form of the
> [Mission Control brainstorm](../brainstorm/mission-conversations/README.md): consumption before
> authoring, built additively — ship a few windows, use them, then adapt.

## The model

```
Parent chat (one per project)             ≈ a main Claude Code session
 ├─ you + Forge talk, refine the work
 ├─ Forge proposes a run → you confirm     ≈ spawning a subagent
 │    ▸ run card: name · status · latest line
 │         └─ Enter → trace (full screen): experts' messages + actions, composer to steer
 └─ run outcome comes back                 ≈ the subagent's final report
```

- **Run** — one execution of a mission (workflow), e.g. "Implement rate limiter #1".
- **Trace** — that run's record: what the experts said (exact messages), what they did (tool
  calls, files, gate results), and what you typed into it. One run, one trace. Like a CI job and
  its log.

Forge differs from cmux, Supacode and JetBrains Air: those manage many independent agent sessions;
none has a parent chat that launches and summarises workflow runs. Their card and attention
details are borrowed below (research 2026-10-02).

## Decisions

| # | Decision |
|---|---|
| 1 | One parent chat per project; it can launch any mission, repeatedly or mixed. |
| 2 | The parent LLM proposes a run (a tool call); you confirm before it starts. |
| 3 | The UI allows concurrent runs. Avoiding file collisions is the operator's job, as with several Claude Code sessions on one repo. |
| 4 | Run cards sit inline in the parent chat; Enter opens the trace full screen with a composer; Esc returns. |
| 5 | The parent gets the run's outcome only, plus a "read run trace" tool it uses when you ask. |
| 6 | Typing in a trace is guidance for the next step; a stop key interrupts the current step. |
| 7 | After stop, the run waits for you: redirect it or end it. Partial effects stay and are recorded; nothing rolls back. |
| 8 | "Needs you" covers runtime pauses (stop, failure, tool permission) and human gates declared in the mission. |
| 9 | Approval of a mission is outside the UI question: slice 1 runs any local mission file. |

## Slice 1 — the three windows

| Window | Does |
|---|---|
| Parent chat | Today's `forge chat`, plus a tool the LLM uses to propose a run of `missions/janus`; you confirm. |
| Run card | Inline: run name, status (running / done / failed), latest line. |
| Trace | Enter on a card: the run's messages and actions, live; composer sends guidance for the next step; Esc back. |

Test mission: [Janus](../../missions/janus/mission.mcl) (Proposer ↔ Approver loop, then Implementer).

**Done when:** from a real `forge chat` window, Forge proposes a Janus run, you confirm, its card
updates live, Enter shows the Proposer/Approver/Implementer messages as they happen, a typed
message reaches the next step, Esc returns, and the outcome lands in the parent chat.

## Later — decided after using slice 1

| Item | Needs |
|---|---|
| Stop / interrupt (decisions 6–7) | Durable Stop in the runtime ([backlog](../backlog.md)) |
| Human gates; approve key, or send back via `{{feedback}}` (decision 8) | A new MCL step kind — MCL has only automated `rule`/`judge` gates today |
| Runs list; jump-to-next "needs you" key | — |
| Diff size and cost on cards; files changed in the outcome | — |
| "Read run trace" tool (decision 5) | — |
| Missions pulled from an OCI registry | — |
| Approved-only missions | — |

## Open before build

- Implementation plan: which forge-mcl, forge-client and Host pieces carry a run that the parent
  chat launches, and how the TUI follows its events. Starts from the owning repos' READMEs.
- Gate review (security, engineering philosophy, default path).
