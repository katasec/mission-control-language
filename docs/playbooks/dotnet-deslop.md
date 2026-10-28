# Dotnet De-slop — Stage 1: Behavior-Preserving Cleanup

> **v1.** The **first stage** of a
> multi-stage de-slop. This stage is *behavior-preserving cleanup* — "clean the floor" so
> signal separates from noise before any design work. Point it at a folder; skill/persona
> lenses **discover** issues; you **address** them one at a time, **coarse → fine**, over
> three internal phases (structure/idiom → redundancy/complexity → tests).
>
> **Out of scope (a later stage):** re-engineering and design — e.g. adopting MS DI,
> consolidating duplicate paths, adding/removing features. An item that can't be done as a
> pure relocation (it needs a design decision, or it changes observable behavior) **STOPs and
> records why, verbatim** — that reason is the only thing carried forward; it is not
> characterized or scoped here.
>
> Uses [personas](../../personas/README.md) + dotnet/skills. Agent-agnostic (Copilot or Claude Code).

## Prerequisites

Install the skill packs first (global, all skills, for whichever agent you run):

```bash
npx skills add dotnet/skills --global --agent claude-code codex github-copilot --skill '*' -y
npx skills add microsoft/skills --global --agent claude-code codex github-copilot --skill '*' -y
```

Skills this playbook invokes: `csharp-expert` · `csharp-refactoring` · `dotnet-webapi` · `crap-score` · `test-anti-patterns` · `test-smell-detection` · `code-testing`. The `*-reviewer` lenses are in-repo [personas](../../personas/README.md) — no install.

## Core rules

- **One issue per carve** — fix one backlog item per pass, then stop. Not "clean the file."
- **Each carve ends green and committed** — build `-warnaserror` + tests pass, then commit.
- **Refactors preserve behavior** — behavior changes go in their own commit, labeled.
- **Coarse before fine** — structure first; it reshapes everything, so later lenses run on
  the new shape, not the old mess.

## Inputs

**You provide: a folder on a branch.**

| Placeholder | Meaning |
|---|---|
| `{repo}` | Target repo — e.g. `forge-mcl` |
| `{scope}` | Folder to clean — e.g. `src/ForgeMission.Cli` |
| `{branch}` | Branch the carves land on — e.g. `adeen/dotnet-deslop-test-1` |
| `{hub}` | Path to this hub checkout (holds the personas + design docs) — e.g. `~/progs/mission-control-language` |

**Produced by discovery (per backlog row) — you never type these:**

| Value | Meaning |
|---|---|
| `{issue}` | One concrete issue a lens found — e.g. "triplicated mission-loading path". |
| `{locations}` | Where it sprawls, as the lens found it — e.g. `Program.cs:149-193, 848-930, 1497-1540`. |
| `{owner}` | The code component that should own the behavior (per the ownership atlas). Not the person running this. |

## How it runs

**Run start (once):** the supervisor runs `dotnet build -warnaserror && dotnet test` from
`{repo}` and records the failing tests in the run-log header. That is the **baseline**; a
carve passes only if it adds no failure to it.

Three phases, in order. **Each phase: Discover → Address → re-discover to confirm.**
Earlier phases change what later ones find, so don't skip ahead.

| Phase | Discover with | Fixes |
|---|---|---|
| **1 — Structure & idiom** | `csharp-expert` (entry) + `dotnet-webapi` where relevant + `ownership-reviewer` + `code-style-reviewer` | folder/project structure, DI, idiomatic patterns, ownership/placement, dup paths that are really ownership mistakes |
| **2 — Redundancy & complexity** | `csharp-refactoring` + `crap-score`/complexity gate (>15) + `simplicity-reviewer` | collapse remaining dup, split god-methods, cut cyclomatic complexity |
| **3 — Tests** | `test-anti-patterns` + `test-smell-detection` → `code-testing` | remove slop tests, strengthen the rest |

**Tests as safety net:** in P1/P2 the existing suite is your net — keep it green, don't
delete tests; add characterization tests in-carve if a move is risky. Test *cleanup* is P3.

---

## Discover (per phase)

Four steps. The output is the phase's **issue backlog** — one row per one-carve-sized issue.

### D1 — Run the lenses (parallel, read-only)

Run each of the phase's lenses over `{scope}`. They're independent and read-only, so run
them as **parallel subagents** — one per lens — and keep only their findings. `csharp-expert`
is the entry point for the idiom lens (it routes to the specialist that owns each area).

**Prompt (per lens):**
```
Use {lens} on {scope} in {repo}. Read-only. Find {phase focus for this lens}. Don't edit.
Return ONLY a table: issue | locations (file:lines) | severity | suspected owner | lens.
Name the skill/persona you actually invoked.
```

### D2 — Merge + dedupe

Parallel lenses overlap. Collapse any issue flagged by more than one lens into a single
row and note all lenses on it (e.g. `style+idiom`). One defect = one row.

### D3 — Split oversized issues

Every row must be **one-carve-sized** — describable in a sentence, landable in one green
commit. An issue too big for that (e.g. "decompose the god-file") is campaign-sized: break
it into per-unit carves (one command/class at a time) as sub-rows.

### D4 — Sequence (foundation-first)

Order by **dependency, then severity** — not severity alone. An enabler issue (e.g.
introduce DI) comes before the issues that ride on it (extracting commands, moving owners).
Record the order in the backlog; later phases re-discover, so this order is per-phase.

A backlog row **is** an issue. The ordered backlog is the work for this phase.

## Address (one issue at a time)

Work the backlog top to bottom. For each issue: **fix → verify → commit.** One issue per
commit — never batch (a big multi-issue diff is unreviewable, which is how slop gets in).

| Step | What | Gate |
|---|---|---|
| Carve | run the carve prompt below (one subagent) | behavior-preserving; stops before commit |
| Verify | `dotnet build -warnaserror && dotnet test` from `{repo}` | 0 warnings; no new failures vs baseline |
| Check | one `/code-review` glance at the diff (cheap insurance) | no new bugs |
| Commit | one issue, small commit; a forced behavior change gets its own commit | tree clean → next issue |
**Carve prompt (one issue → one subagent). Every blank fills from the backlog row + run header — nothing invented:**
```
You are the IMPLEMENTER for ONE carve in the Dotnet De-slop Playbook.
Repo: {repo}   Branch: {branch}   Scope: {scope}

Read and follow (do not restate):
- {hub}/personas/implementer.md
- {hub}/docs/design/code-style.md
- {hub}/docs/design/engineering-philosophy.md

Skill: invoke `csharp-refactoring` to drive the change.

ISSUE TO CARVE — backlog run {run-id}, phase {phase}:
  #{n} [{sev}] {issue}
  Locations: {locations}
  Target owner: {owner}

RULES (non-negotiable):
- Behavior-preserving ONLY. If a behavior change is unavoidable, STOP and report it — never bundle it.
- Scope is THIS issue only. Don't fix other backlog items you notice; list them instead.
- Keep existing tests green. Do NOT delete tests (that's Phase 3). Add a characterization test only if the carve is risky.
- House style: progressive disclosure, small named functions, explicit errors, <=2 nesting, side effects isolated, zero warnings.

DO:
1. Make the change at {locations}, moving behavior to {owner}.
2. Run from {repo}:  dotnet build -warnaserror && dotnet test
3. Do NOT commit.

RETURN ONLY:
- Files changed (paths)
- 3-5 line summary: what moved where
- Build result (warnings) + test result (pass/fail)
- Any forced behavior change, flagged (or "none")
- Other backlog issues noticed (don't fix them)
```
When the phase backlog is empty, re-run Discover for that phase to confirm clean, then
advance to the next phase.

---

## Where results live

In this hub (the cross-repo control plane), grouped by playbook → target → **timestamped run**:

```
docs/playbooks/runs/dotnet-deslop/<repo>-<scope-slug>/<YYYY-MM-DD-HHMMSS>/
  phase-1-backlog.md   ← discovered issues, ticked off as carved
  phase-2-backlog.md
  phase-3-backlog.md
  run-log.md           ← one run record appended per carve
```
e.g. `docs/playbooks/runs/dotnet-deslop/forge-mcl-cli/2026-10-25-080731/`.

**Each playbook run gets a fresh timestamped folder** (local time, sortable) so re-runs
never overwrite earlier history. Each backlog + run-log header records the run metadata:
run-id, repo, scope, branch, lenses. Code changes stay as commits in the target repo,
cited here by sha.

## Run record

Append to `run-log.md` per carve; keep it short.

```
Phase:        <1 | 2 | 3>
Issue:        {issue}
Locations:    {locations}
Owner chosen: {owner}
Commits:      <refactor sha> + <behavior-change sha, if any>
Verify:       build 0 warnings · tests <n passed>
```
