# Dotnet De-slop — Stage 2: Platform Best Practices

> **v1.** The **second stage** of a multi-stage de-slop. Runs on the clean floor
> [Stage 1](dotnet-deslop.md) leaves. Hand-rolled plumbing is replaced by the facility .NET
> provides, used the way Microsoft recommends. Point it at a folder; area lenses **discover**
> deviations; you **address** them one at a time, **coarse → fine**, over three internal
> phases (composition → app-type idioms → tests).
>
> **Rule: if .NET provides it, use theirs — the way Microsoft recommends.** Like cloud shared
> responsibility: the platform (DI, config, logging, HTTP, serialization, cancellation,
> project layout, web APIs, testing) is Microsoft's job; the author owns only what the
> platform doesn't cover.
>
> **Retain the app's own behavior; adopt the platform's.** The app's own behavior — its
> domain logic, the output it writes, the files it writes, the exit codes it returns — stays
> identical. What a platform facility does by design is Microsoft's: adopting the facility
> adopts it (e.g. case-insensitive config keys, Ctrl-C via System.CommandLine's termination
> token, the JSON configuration provider's parsing). Record it in the commit; don't STOP.
>
> | Stage | Behavior | What changes |
> |---|---|---|
> | 1 — Cleanup | retained | relocation only |
> | **2 — Platform Best Practices** | **app's own retained** | hand-rolled plumbing → platform facilities, with their semantics |
> | later — author territory | changes | bugs in our code, features, product decisions |
>
> If adopting a practice would change the **app's own** behavior, it **STOPs and records why,
> verbatim** — that change belongs to the later stage.
>
> Uses dotnet/skills + Microsoft Learn (`microsoft-docs`). Agent-agnostic (Copilot or Claude Code).

## Prerequisites

Same skill packs as [Stage 1](dotnet-deslop.md#prerequisites). Stage 1 is complete on `{scope}`.

Skills this playbook invokes: `csharp-expert` · `microsoft-docs` · `csharp-refactoring` · `dotnet-webapi` · `dotnet-aot-compat` · `optimizing-ef-core-queries` · `directory-build-organization` · `msbuild-modernization` · `scaffold-dotnet-test-project` · `code-testing` · `test-anti-patterns` · `test-smell-detection`.

## Core rules

- **Retain the app's own behavior; adopt the platform's** (see above). Logging: the platform
  logger may add diagnostic lines on stderr; stdout is the app's own output and never changes.
- **The rule beats the row** — if the backlog's prescription would change the app's own
  behavior, adopt the Microsoft-recommended form that doesn't, or STOP.
- **One practice per carve** — adopt one platform practice per pass, then stop.
- **Each carve ends green and committed** — build `-warnaserror`, no new test failures against
  the run's baseline, then commit.
- **Pin, then swap** — characterization tests pin the app's own behavior before the swap and
  must pass unchanged after it. Tests that touch global console state (`Console.SetOut`/
  `SetError`) go in one non-parallel test collection.
- **Microsoft prescribes; we don't invent** — the target is what Microsoft recommends (via
  the skill, checked against Microsoft Learn), not a home-grown design.
- **One host is the composition root** — every app type composes through
  `Host.CreateApplicationBuilder`: DI, configuration (one `IConfiguration`: JSON + env
  providers) and logging all come from that one host. A CLI passes `Args = []` (the command
  parser owns arguments) and routes the console logger to stderr with a Warning minimum level
  (so platform Information logs — e.g. `IHttpClientFactory` per-request lines — stay quiet).
- **Fit the facility to the workload** — when .NET offers more than one facility, use the one
  Microsoft recommends for that workload (e.g. who owns the lifetime: a short-lived command
  vs a long-running server's `RunAsync`), per unit. Never "A or B". The host owns shutdown
  only for a server the app's one host runs; a server a subcommand starts after parsing runs
  on the command's token. Never build a second host for it.
- **No net complexity** — the adopted facility must be no more complex than the hand-rolled
  code it replaces. The Check step enforces it; if it can't be met, simplify within the
  practice or route the row.
- **Route, don't drop** — a finding that isn't this phase's goes to the phase or stage that
  owns it, by name.
- **Coarse before fine** — composition first; everything else registers through it.
- **The supervisor owns repo state** — carve agents never stash, pop, checkout, reset, clean
  or commit; to compare with the baseline they read `git show HEAD:<path>`. If a carve agent
  fails mid-run (crash, network), the supervisor discards its partial changes and retries
  once; a second failure stops the run.
- **Record races, don't pin them** — behavior that varies run to run (e.g. an exit code that
  depends on signal timing) is noted in the run-log; pins assert only what is stable. A
  difference that only shows up in a race (e.g. a value changed by another process
  mid-command) is not a change to the app's own behavior: record it, don't STOP.

## Inputs

**You provide: a folder on a branch, and the Stage-1 run.**

| Placeholder | Meaning |
|---|---|
| `{repo}` | Path to the target repo checkout — e.g. `~/progs/forge-mcl` |
| `{scope}` | Folder — e.g. `src/ForgeMission.Cli` |
| `{branch}` | Branch the carves land on |
| `{hub}` | Path to this hub checkout (holds the personas + design docs) — e.g. `~/progs/mission-control-language` |
| `{stage1-run}` | Stage-1 run folder — its `run-log.md` holds the STOP reasons, verbatim |
| `{test-filter}` | Set at run start: excludes tests that need live services — e.g. `Category!=Integration` |

**Produced by discovery (per backlog row) — you never type these:**

| Value | Meaning |
|---|---|
| `{issue}` | What is hand-rolled or non-standard — e.g. "services wired as hand-built statics". |
| `{locations}` | Symbol + file (line numbers drift as carves land) — e.g. `ForgeExec.RunAsync` (Commands/ForgeExec.cs:16-23). |
| `{practice}` | The facility to adopt, for this workload — e.g. "MS DI: one `ServiceCollection` at the CLI root". |
| `{reference}` | The Microsoft Learn page that prescribes it. |

## How it runs

**Run start (once):**
1. **Live-test filter.** Find the trait that marks tests needing live services or credentials
   (e.g. `[Trait("Category", "Integration")]`) and record the filter in the run-log header —
   `{test-filter}`, e.g. `Category!=Integration`. Every test run uses it. Live tests exercise the
   app, not the playbook: they are never part of a run and are left to whoever works on the app.
2. **Baseline.** `dotnet build -warnaserror && dotnet test --filter "{test-filter}"` from `{repo}`;
   record the failing tests in the run-log header. A carve passes only if it adds no failure to it.
3. **Row 0 — shared test harness** (one carve, before any other): a process runner, an isolated
   HOME, local HTTP stubs, and a **non-forwarding proxy that every probe and test is forced
   through** (`HTTP_PROXY`/`HTTPS_PROXY`), so no carve can reach a live service even by mistake.
   Row 0 also moves the repo's existing ad-hoc process runners onto the harness (assertions
   unchanged): the guard only holds if the harness is the only way tests run the app. It also
   gives ad-hoc probes a one-line entry, so a quick probe through the harness is as easy as
   running the app by hand.
   Every later carve extends this harness; none copies it.

Then three phases, in order. **Each phase: Discover → Address → re-discover to confirm.**
Each phase runs **one lens per area**, all using `csharp-expert` (routes to the specialist for
the app type) and `microsoft-docs` (grounds the prescription), plus the phase's specialists.

| Phase | Areas (one lens each) | Specialist skills |
|---|---|---|
| **1 — Composition** | DI + `HttpClient` registration · configuration + options · logging · cancellation | — |
| **2 — App-type idioms & project shape** | project/folder layout + build config · endpoint/CLI idioms · serialization · data access · trim/AOT | `dotnet-webapi`, `optimizing-ef-core-queries`, `directory-build-organization`, `msbuild-modernization`, `dotnet-aot-compat` |
| **3 — Tests** | test project layout · isolation + fixtures · test smells | `scaffold-dotnet-test-project`, `test-anti-patterns`, `test-smell-detection`, `code-testing` |

---

## Discover (per phase)

Four steps. The output is the phase's **backlog** (one row per one-carve-sized practice) and
`routed.md` (findings that belong elsewhere, each with its destination).

### D1 — Run the lenses (parallel, read-only)

One subagent per area, all in parallel.

**Prompt (per area):**
```
Lens: {area} — Stage 2 (Platform Best Practices), Phase {phase}.
Repo: {repo}   Branch: {branch}   Scope: {scope}. Read-only. Don't edit.
Skills: invoke `csharp-expert` (it routes to the specialist for this app type){, plus {specialist skills}}.
Ground every prescription in Microsoft Learn via `microsoft-docs`.

Find every place inside {scope} where the code hand-rolls {area}, or uses the .NET facility
for it in a way Microsoft doesn't recommend.
- Report ONLY {area}. Report ONLY issues whose fix lands inside {scope}.
- Prescribe the facility Microsoft recommends for this workload (short-lived command vs
  long-running server, etc.), per unit. One prescription per row — never "A or B".
- Behavior impact: the app's own behavior (domain logic, output it writes, files it writes,
  exit codes it returns) must stay the same. Semantics the platform facility brings by design
  are Microsoft's — write them as "platform: <what>". If the app's own behavior would change,
  write "app: <exactly what>". Otherwise "none".
- Composition root is decided: one `Host.CreateApplicationBuilder` per app; prescribe against it.
  A quickstart's standalone-app shape (its own host, its own `Main`) is not the prescription
  for a subcommand of this app.
- When options bind from configuration, say which source they bind from (the root or a named
  section) and what can then reach the setting (e.g. unprefixed env vars, `appsettings.json`).
- A row that adopts a platform extension point (output writers, factories, providers) names its
  consumer: the code that sets a non-default value. With no consumer, it isn't a row.
- A row that removes an exception type lists every catch site, not only the throw sites.
- A "pass the token" row covers every awaited call in the unit (file or class), not just the
  ones seen.
- When two platform APIs fit, prescribe the one with fewer branches.
- Before prescribing a build setting, check its effective value (`dotnet msbuild -getProperty`);
  don't report a default the SDK already applies.
- Name each location by symbol and file (lines may drift).
Return ONLY a table:
issue | locations (symbol, file:lines) | practice to adopt | Learn reference | behavior impact (none | platform: … | app: …) | severity
Name the skills you actually invoked.
```

### D2 — Merge, dedupe, route

1. Collapse issues flagged by more than one lens into one row.
2. Read the STOP reasons in `{stage1-run}/run-log.md`. Add those that are a platform practice
   for this phase's areas as rows (cite the Stage-1 issue #).
3. Route — don't drop — into `routed.md`, each with its destination:
   - impact `app: …` → **later stage (author territory)**, with the stated change;
   - an extension-point row with no named consumer → **later stage**, "no consumer";
   - a row that removes an exception caught as control flow that shapes output → split by
     command now, or route;
   - outside this phase's areas → **the Stage-2 phase that owns it**;
   - Stage-1 STOPs that are product/feature/cross-repo decisions → **later stage**.

The impact column is advisory — the carve-time STOP is the gate.

**Open questions (never block the run):** if lenses conflict on a foundational choice that the
playbook's rules don't settle (composition, hosting, the shape of configuration or lifetime),
the supervisor takes the option most consistent with the rules, records it in `questions.md`
(question, options, default taken, rows it touches) and carries on. At the end of the run the
operator answers them all at once; if an answer differs from the default and touches rows, the
playbook runs again. Everything the rules already settle, the supervisor resolves alone.

### D3 — Split oversized issues

Every row must be **one-carve-sized** — one practice, landable in one green commit. Split by
owner or unit (one service / one command), never by sub-step of one flow — rows that can't
land apart are one row. A row that adds validation to an options type first checks which
consumers read that type; validation must not reach a consumer that never needed it, so use
one options type per consumer set.

### D4 — Sequence (foundation-first)

Order by **dependency, then severity**. The composition root (one host) comes first; then one
configuration root (JSON + env providers) before any row that reads settings; registration
before injection into consumers; a token's source (the command action) before the code it
flows into. A row that *removes* a hand-rolled source of a value goes after the row that
supplies the platform source; rows that only add a consumer of it may go before. An enabler
that only pays off once its dependents land (e.g. a longer shutdown grace window before
actions honor the token) goes after them.

For each row record the **files it touches**. Rows are grouped by owner (the same files) for
one implementer per group; groups whose file sets are disjoint may run **in parallel**, each in
its own worktree, merged in sequence order.

## Address (one issue at a time)

Work the backlog top to bottom. For each issue: **pin → swap → verify → check → commit.**

- **One implementer per group.** The first row of a group launches the carve prompt; each later
  row in the group goes to the same agent as a follow-up (the next backlog row, filled the same
  way) — it already holds the code and the rules. A new group starts a new agent.
- **Fast loop, full gate.** Carve agents build and run only the tests they touched plus their
  pins; the supervisor runs the full filtered suite at Verify.
- **Trivial rows** (one-line settings) in the same group may be done in one agent turn, still
  one commit per row. The supervisor checks trivial rows inline, without a Check subagent.
- **Build-config rows** for a natively compiled app also run a publish, once per group rather
  than per row, unless the row changes linking or trimming.
- **Idle lanes take the next group.** When one lane finishes, the next group whose files are
  disjoint from the running lanes starts there.

| Step | What | Gate |
|---|---|---|
| Carve | run the carve prompt below (one subagent) | practice adopted, app's own behavior retained; stops before commit |
| Verify | `git status` shows the carve; `dotnet build -warnaserror && dotnet test --filter "{test-filter}"` from `{repo}` (supervisor) | 0 warnings; no new failures vs baseline; pinned tests unchanged |
| Check | the check prompt below (one read-only subagent, simplicity-reviewer persona) | PASS: no net complexity; no change to the app's own behavior |
| Commit | one practice, one commit; platform semantics adopted named in the message | tree clean → next issue |

**Carve prompt (one issue → one subagent). Every blank fills from the backlog row + run header — nothing invented:**
```
You are the IMPLEMENTER for ONE carve in Dotnet De-slop Stage 2: Platform Best Practices.
Repo: {repo}   Branch: {branch}   Scope: {scope}

Read and follow (do not restate):
- {hub}/personas/implementer.md
- {hub}/docs/design/code-style.md
- {hub}/docs/design/engineering-philosophy.md

Skills: `csharp-expert` routes to the specialist; `microsoft-docs` to confirm the prescription
against {reference}; `csharp-refactoring` for the moves.

ISSUE TO CARVE — backlog run {run-id}, phase {phase}:
  #{n} [{sev}] {issue}
  Locations: {locations}
  Practice to adopt: {practice}
  Reference: {reference}
  Next rows (stay out of their lane): {next rows: # + one line each}

RULES (non-negotiable):
- Retain the app's own behavior: its domain logic, the output it writes, the files it writes,
  the exit codes it returns. Semantics the platform facility brings by design are Microsoft's —
  adopt them and report them; do not STOP for them. The platform logger may add diagnostic
  lines on stderr; stdout never changes.
- If adopting {practice} would change the app's own behavior, adopt the Microsoft-recommended
  form that doesn't; if none exists, STOP and report why, verbatim — never bundle it.
- Adopt {practice} as Microsoft prescribes it. Do not invent your own design.
- Before the swap, add characterization tests that pin the app's own behavior; they must pass
  unchanged after it. Tests that touch Console.SetOut/SetError go in the non-parallel collection.
  Reuse the repo's shared test harness (process runner, isolation, stubs) — extend it, never copy it.
  Pin only behavior this carve touches: one test per new path; extend an existing test rather
  than adding a near-copy; don't re-prove rules another test already covers.
  If the swap changes a signature, pin through the public entry point (process/harness) so
  the pin runs on HEAD first; if that isn't possible, say so in RETURN.
- Scope is THIS issue only. Don't fix other backlog items you notice; list them instead.
- Do NOT delete tests (that's Phase 3) — except a test your own carve turned into an exact
  copy of another; merge those.
- A difference that only shows up in a race is not a STOP: report it under platform semantics.
- Do NOT change repository state: no stash, pop, checkout, reset, clean or commit. To compare
  with the baseline, read `git show HEAD:<path>`.
- Never run the app against live services or with the operator's real credentials — not even
  with placeholder keys. Every probe and test goes through the shared harness (isolated HOME,
  local stubs, non-forwarding proxy).
- Known Check failures — avoid them up front: no pipeline or abstraction without a consumer;
  no new platform log noise; no second way to reach a value that already has one; no copied
  test helpers or near-copy tests.
- House style: progressive disclosure, small named functions, explicit errors, <=2 nesting, side effects isolated, zero warnings.

DO:
1. Pin the app's own behavior at {locations} (characterization tests).
2. Replace the hand-rolled code with {practice}.
3. Run from {repo}:  dotnet build -warnaserror, then ONLY the affected test classes and your
   pins: dotnet test --filter "<those classes>&{test-filter}". The supervisor runs the full suite.
4. Do NOT commit.

RETURN ONLY:
- Files changed (paths)
- 3-5 line summary: what was replaced with which platform facility
- Skills invoked + the Learn page you followed
- Build result (warnings) + test result (pass/fail, any failure outside the baseline: {baseline}); pinned tests unchanged (yes/no)
- Platform semantics adopted (or "none")
- STOP reason, verbatim (or "none")
- Other backlog issues noticed (don't fix them)
```
**Check prompt (after Verify, before Commit; one read-only subagent):**
```
Role: simplicity reviewer. Read {hub}/personas/simplicity-reviewer.md. Read-only. Don't edit.
Repo: {repo}   Under review: the uncommitted diff (`git diff` + new files) for carve #{n}: {issue}
Practice adopted: {practice}
Backlog context: next rows {next rows} — don't flag a planned in-between state as bloat, but do
flag work that belongs to one of those rows.
Question: is the adopted platform facility no more complex than the hand-rolled code it
replaced (types, indirections, lines, concepts a reader must hold)? Is every change needed
for this practice?
Return: PASS or FAIL, the persona's verdict table with evidence from the diff, and for FAIL
the smallest change that would make it PASS.
```
On FAIL: the supervisor sends one correction to the same implementer (simplify within the
practice) and re-checks; a second FAIL routes the row to the later stage with the reason.

When the phase backlog is empty, re-run Discover for that phase to confirm clean, then
advance to the next phase.
---

## Where results live

In this hub, grouped by playbook → target → **timestamped run**:

```
docs/playbooks/runs/dotnet-deslop-stage-2/<repo>-<scope-slug>/<YYYY-MM-DD-HHMMSS>/
  phase-1-backlog.md   ← discovered practices, ticked off as carved
  phase-2-backlog.md
  phase-3-backlog.md
  routed.md            ← findings routed elsewhere, each with its destination
  questions.md         ← open questions: default taken, rows touched; answered at run end
  run-log.md           ← baseline in the header; one run record appended per carve
```

Each backlog + run-log header records: run-id, repo, scope, branch, lenses, `{stage1-run}`,
baseline. Code changes stay as commits in the target repo, cited here by sha.

## Run record

Append to `run-log.md` per carve; keep it short.

```
Phase:        <1 | 2 | 3>
Issue:        {issue}
Locations:    {locations}
Practice:     {practice}
Reference:    {reference}
Commits:      <sha>
Behavior:     retained | platform: <semantics adopted> | STOP: <reason, verbatim>
Verify:       build 0 warnings · tests <n passed> · no new failures vs baseline · pinned unchanged
Check:        PASS | FAIL → corrected → PASS | routed: <reason>
Duration:     <mm:ss>
```
