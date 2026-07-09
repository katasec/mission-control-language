# AGENTS.md — Operating Instructions for FML

This file tells you how to work on this repository. Read it before doing anything else.

---

## What this project is

Forge Mission Language (FML) is a minimal language for expressing structured reasoning through the composition of experts. Three primitives: `mission`, `expert`, `|>`. See [README.md](README.md) for the full picture and [docs/design/language.md](docs/design/language.md) for the grammar and syntax decisions.

---

## How to orient at the start of a session

1. Read this file
2. Read [docs/plan.md](docs/plan.md) — the hub. It tells you what phases exist, which are done, and which are active
3. Read the spoke doc for the current phase — linked from `docs/plan.md` — to see which tasks are done and which are next
4. Read [docs/design/architecture.md](docs/design/architecture.md) if you need to understand component boundaries

Do not load everything at once. Start from the hub and follow links only when the task requires it.

---

## How work is structured

Work follows a strict methodology. Do not deviate from it.

### Design first
All design decisions are captured in `docs/design/` before implementation begins. If something is unclear, check there first. If it is not documented, raise it before implementing.

### Phases
Work is broken into phases. Each phase has a spoke document in `docs/phases/`. Phases must be completed in order — each phase produces something independently testable before the next begins.

### Tasks
Each phase doc contains a task table with statuses. Tasks within a phase must be done in the order listed — they are in sequential dependency order.

### Completion conditions
Each phase doc defines a completion condition. Do not mark a phase Done in `docs/plan.md` until that condition is met.

---

## How to update status

When you start a task, update its status in the phase spoke doc from `Not Started` to `In Progress`.

When you complete a task, update it to `Done`.

When all tasks in a phase are done and the completion condition is met:
- Update the phase spoke doc with a `## Result` section summarising what was built and test outcomes
- Update `docs/plan.md` to mark the phase `Done`
- Commit and push

Status values: `Not Started` | `In Progress` | `Done`

---

## How to run the build and tests

```bash
dotnet build src/ForgeMission.slnx
dotnet test src/ForgeMission.slnx
```

All tests must pass before marking any task complete. Never mark a task done if tests are failing.

---

## Project structure

```
README.md               — what FML is and why it exists
AGENTS.md               — this file
docs/
  plan.md               — hub: phase list with statuses and links
  design/               — design decisions (language, architecture, MAF research, methodology)
  phases/               — one spoke per phase with task lists and statuses
src/
  ForgeMission.Core/    — parser, expert loader, pipeline runner, MAF adapter
  ForgeMission.Cli/     — CLI entry point
  ForgeMission.Tests/   — xUnit tests
missions/               — example missions (build-operator added in Phase 6)
runs/                   — gitignored, output of fml run
```

---

## Architecture — the short version

```
CLI
 └→ Pipeline Runner
      └→ Parser           (pure C#, no dependencies)
      └→ Expert Loader    (resolves markdown files)
      └→ IExpertRunner
           └→ MAF Adapter (only file that touches Microsoft Agent Framework)
```

MAF is an internal implementation detail. It must not appear above the adapter layer. See [docs/design/architecture.md](docs/design/architecture.md) for full detail.

---

## Conventions

- **No Co-Authored-By lines in commits.** Commits are attributed to the repo owner only.
- **PascalCase for expert and mission names.** Enforced by the parser — lowercase identifiers are a parse error.
- **Lowercase keywords** (`mission`, `expert`). These are part of the language, not user-defined names.
- **No business logic in the CLI.** The CLI wires up dependencies and delegates to Core. Nothing else.
- **MAF stays behind `IExpertRunner`.** The parser, AST, pipeline runner, and CLI must have zero knowledge of MAF.
- **Report results straight — no cliffhangers.** State what is done and proven, then stop. Do not end a wrap-up with a manufactured "one thing left" / "the only unverified atom is…" hedge to look thorough or to invite another turn. If something is genuinely outstanding, name it once as a plain fact — not as a teaser. Never pad a summary to fill space or burn tokens.

---

## During a session — capturing learnings

As you work, you will encounter design decisions, implementation surprises, failed approaches, and useful discoveries. These must not stay only in your context — they must be written down before the session ends.

**Where to write them:**

| What | Where |
|------|-------|
| New or revised design decisions | Relevant file in `docs/design/` |
| Implementation notes for a phase (e.g. a package behaved unexpectedly) | `## Notes` section of the relevant phase spoke doc |
| A decision that affects the overall plan | `docs/plan.md` — add a `## Notes` section if needed |
| A failure or dead end worth remembering | The phase spoke doc under a `## Dead Ends` section |

Write these incrementally as they happen, not as a batch at the end. If your context fills and you cannot finish the session, the docs must be current enough for a fresh agent to continue without losing anything.

---

## At the end of a session — session continuity protocol

Agent performance degrades as context fills. Sessions are intentionally bounded. At the end of every session you must do the following, in order:

### 1. Flush learnings to docs

- Write any new design decisions, notes, or dead ends to the appropriate docs (see above)
- Commit and push

### 2. Update hub and spokes

- Mark all completed tasks `Done` in the phase spoke doc
- Add a `## Result` section to the phase spoke doc if the phase is complete
- Update `docs/plan.md` to reflect the current phase status
- Commit and push

### 3. Verify tests pass

```bash
dotnet test src/ForgeMission.slnx
```

Do not hand off if tests are failing. Fix them first or document exactly why they are failing and what is needed to fix them.

### 4. Generate a handoff prompt

Create a handoff prompt that a fresh agent can paste into a new session to rehydrate context and continue without being briefed from scratch.

The handoff prompt must be a single copyable code block at the end of your response, in this format:

~~~
```
You are continuing work on the Forge Mission Language (FML) project.

Repository: ~/progs/fml
Start by reading AGENTS.md, then docs/plan.md, then the spoke doc for the current phase.

Current state:
- Phases complete: [list them]
- Current phase: [phase name and number]
- Last task completed: [task description]
- Next task: [task description]

Key context from this session:
- [Any design decision made that is not yet in the docs]
- [Any in-progress work that is partially done]
- [Any known issues or blockers]
- [Anything else a fresh agent needs to know to continue without asking]

All tests are passing / [describe failing tests if any].

Pick up from [next task] and continue following the methodology in AGENTS.md.
```
~~~

The handoff prompt is not a summary for the human — it is a machine-readable orientation for the next agent. Write it to be consumed, not read. Be specific and complete.

Leave the hub small and current. Leave the handoff prompt ready to copy.
