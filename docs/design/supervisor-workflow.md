# Supervisor workflow

> **Status: governing implementation workflow (rewritten 2026-10-31).** Modelled on what worked in
> the [dotnet de-slop playbooks](../playbooks/dotnet-deslop.md): named dotnet skills at every stage,
> one small change per commit, tests pinned before a change, and a read-only check for added
> complexity. The earlier design → plan → review-rounds loop is gone; it produced long contract
> documents and invented design. Provider-neutral: the agent the operator works in is the
> supervisor and uses its own subagents.

## Core rules

- **Skills are a hard stop.** Each stage names its skills. Before starting, check that every one
  can be invoked. If one is missing, stop and tell the operator; never substitute your own judgement,
  another skill or a manual equivalent. Every subagent return names the skills it actually invoked.
- **Microsoft prescribes; we don't invent.** Where .NET provides a facility, use it the way
  Microsoft recommends (`csharp-expert` routes to the specialist, `microsoft-docs` grounds it). Where
  our code already does it, reuse it. New code fills only a gap that neither covers.
- **Decisions come from the spoke or the operator.** The task's spoke states the decisions. A
  change that needs a decision the spoke doesn't make **STOPs**, and the reason is recorded verbatim
  and taken to the operator. Never infer it from git history, deleted documents or a subagent's
  suggestion.
- **One row, one commit.** Work is a backlog of rows, each describable in one sentence and landable
  in one green commit. Never batch rows; a big multi-issue diff is how slop gets in.
- **Pin, then change.** Tests that pin what must not change are written first and pass on `HEAD`.
  A deliberate behavior change gets its own labelled commit.
- **Delete before add.** Remove the dead or duplicated path the change replaces in the same task.
- **No net complexity.** The change is no more complex than what it replaces; the Check enforces it.
- **The supervisor owns repo state and evidence.** Subagents never stash, reset, checkout, clean or
  commit. A subagent's claim is not evidence: the supervisor reads the diff and runs the gates.
- **Stay in scope.** Findings outside the row are listed, not fixed, and routed to the spoke or
  backlog by name.

## Roles

| Role | Persona | Does | Never |
|---|---|---|---|
| Supervisor | this document | Selects the task, builds the backlog, fills prompts from it, verifies, commits, merges, accepts. | Edits product code, decides a design question the spoke leaves open, or accepts a claim without checking it. |
| Lens | the stage's skills; [ownership](../../personas/ownership-reviewer.md) when placement is in question | Discovers, read-only, and returns a table. | Edits. |
| Implementer | [implementer](../../personas/implementer.md) | Carves one row at a time; the same agent takes the next row of its group. | Commits, widens scope, or settles a decision by inference. |
| Check | [simplicity](../../personas/simplicity-reviewer.md) + [code style](../../personas/code-style-reviewer.md) | Reviews the uncommitted diff, read-only: PASS or FAIL with evidence. | Authors or edits. |

## Persona rule

Every subagent assignment includes the **full text** of each persona it uses, after its stage tag.
A link is not enough: rules that live only in a linked file get skipped.

## Stage tags

Tag each assignment `[<stage>:<role>:r<N>] <task>`; round 1 may omit `r1`. Stages: `select`,
`discover`, `pin`, `carve`, `verify`, `check`, `live`, `close`. Roles: `supervisor`, `lens`,
`implementer`, `check`. Timing uses the assignment boundaries, per
[task-timing](../../tools/task-timing/README.md).

## Loop

| Stage | Who | Skills | Gate |
|---|---|---|---|
| 0. Select | supervisor | — | One task from the spoke, its decisions cited, a one-line `Done when`. Baseline recorded: `dotnet build -warnaserror` and `dotnet test` from each touched repo, failing tests listed. |
| 1. Discover | lenses, parallel, read-only | `csharp-expert`, `microsoft-docs`; `dotnet-webapi`, `dotnet-aot-compat` or others where the area needs them | A backlog: one row per carve, with exact symbols and files, code to reuse, code to delete, and the Microsoft Learn page for any platform facility. Sequenced foundation-first. |
| 2. Pin | implementer | `code-testing` | Tests pin the behavior the row must keep; they pass on `HEAD`. Reuse the repo's test harness; never copy it. |
| 3. Carve | implementer | `csharp-refactoring` | The row's change only; builds with zero warnings; affected tests and pins pass. Not committed. |
| 4. Verify | supervisor | — | `git status` shows only the carve. Full build `-warnaserror` and test suite: no failure added to the baseline; pins unchanged. |
| 5. Check | check, read-only | `simplify`; `code-review` (low); `security-review` when Hands, credentials, network or execution are touched | PASS: no net complexity, no second path to anything, no change outside the row. On FAIL, one correction to the same implementer; a second FAIL goes to the operator. |
| 6. Commit | supervisor | — | One row, one commit; the next row starts on a clean tree. |
| 7. Live | supervisor | `run` | When the backlog is empty: the real CLI or service on the [default path](default-path-acceptance.md), with the action and observation recorded. |
| 8. Close | supervisor | — | PR merged after its checks pass; published or deployed through the normal route; evidence as one line in the spoke's `_completed`; every touched repo on a clean, current `main`. |

Native AOT is checked once, after the managed loop is stable, as the final artifact gate; not on
every row. If previously passing code fails, compare the checkout, commands, dependencies and
environment with the passing run before blaming the code.

Documentation-only tasks are edited and validated by the supervisor; the product stages are N/A
and recorded as such.

## Prompts

Fill every blank from the backlog row or the run header. Nothing invented.

**Discover (one per lens):**
```text
[discover:lens] Lens: {area}. Repo: {repo}  Branch: {branch}  Task: {task}. Read-only. Don't edit.
Decisions (do not reopen): {spoke decisions for this task}
Skills: invoke `csharp-expert` (it routes to the specialist){, plus {skills}}. Ground every
platform facility in Microsoft Learn via `microsoft-docs`.
Find what must change for {task}: existing code to reuse, code to delete, and where .NET already
provides the facility. One prescription per row, never "A or B".
Return ONLY a table: row | symbols (file:lines) | reuse | delete | Learn reference | needs a decision? (none | what)
Name the skills you actually invoked.
```

**Carve (one row):**
```text
[carve:implementer] {persona: implementer, full text}
Repo: {repo}  Branch: {branch}
Read and follow: {hub}/docs/design/code-style.md, {hub}/docs/design/engineering-philosophy.md
Skills: `code-testing` for the pins, `csharp-refactoring` for the change.
ROW #{n}: {row}
  Symbols: {symbols}   Reuse: {reuse}   Delete: {delete}   Reference: {reference}
  Next rows (stay out of their lane): {next rows}
RULES: this row only; pin first, the pins pass on HEAD; if the row needs a decision the spoke
doesn't make, STOP and report it verbatim; don't change repository state; zero warnings.
RETURN ONLY: files changed · 3-5 line summary · skills invoked · build and test results ·
behavior changed (none | what) · STOP reason (or none) · other issues noticed (not fixed)
```

**Check (after Verify):**
```text
[check:check] {personas: simplicity-reviewer and code-style-reviewer, full text}
Repo: {repo}. Read-only. Under review: the uncommitted diff for row #{n}: {row}
Skills: invoke `simplify` and `code-review` (low){, and `security-review`}.
Is the change no more complex than what it replaces, needed for this row, and free of a second
path to anything? Return PASS or FAIL, each persona's verdict table with evidence from the diff,
and for FAIL the smallest change that would make it PASS. Name the skills you invoked.
```

## Records

Per task, in the spoke's run folder or `_completed` file: the baseline, the backlog with rows ticked
as committed, and one line per row:

```text
Row:      #{n} {row}
Commit:   <sha>
Skills:   <invoked>
Verify:   build 0 warnings · tests <n passed> · no new failures vs baseline · pins unchanged
Check:    PASS | FAIL → corrected → PASS
```

The former [Claude/Codex workflow](claude-codex-workflow.md) is retired and grants no exception.
