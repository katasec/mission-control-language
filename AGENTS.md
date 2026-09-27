# AGENTS.md — Operating Instructions for MCL

This file tells you how to work on this repository. Read it before doing anything else.
It is the canonical file — `CLAUDE.md` is a symlink to this one, so Claude Code (which
auto-loads `CLAUDE.md`) and any other AGENTS.md-reading tool see the same instructions.

---

## What this project is

MCL (Mission Control Language) is a declarative pipeline language where `.mcl` files compose AI
experts into structured workflows. The CLI binary is `forge`. Runtime is .NET 10 Native AOT.
Composition operator is `->` (not `|>` — that was replaced in Phase 25). See
[README.md](README.md) for the full picture and [docs/design/language.md](docs/design/language.md)
for the grammar and syntax decisions.

## Where the code lives

This repo holds no product source. It is agent mission control: plans, agent rules, agents, and
missions. Each component lives in its own repo — see the
[repository map](docs/phases/phase-50-repository-extraction.md) for status and detail.

| Repo | Purpose |
|---|---|
| [`forge-mcl`](https://github.com/katasec/forge-mcl) | MCL language, CLI (`forge`), generic execution support |
| [`forge-runner`](https://github.com/katasec/forge-runner) | Stateless hosted mission execution |
| [`forge-conversations`](https://github.com/katasec/forge-conversations) | Durable conversation admission, state, and dispatch |
| [`forge-platform`](https://github.com/katasec/forge-platform) | API, accounts, platform keys, billing, ledger |
| [`forge-rooms`](https://github.com/katasec/forge-rooms) | Collaboration domain and browser product (Rooms, ForgeUI) |
| [`forge-desktop`](https://github.com/katasec/forge-desktop) | Local application and supervision (Desktop, Application) |
| [`forge-infra`](https://github.com/katasec/forge-infra) | Azure deployment configuration |
| `mission-control-language` (this repo) | Agent mission control |

Do not restore source from any of those repos here or add sibling project references.

---

## How to orient at the start of a session

1. Read this file and [Default-Path Acceptance](docs/design/default-path-acceptance.md). It is the
   repository-wide definition of the supported user defaults and the evidence required to prove
   them. A documentation-only task records N/A; every other task first determines whether this
   gate applies before taking action.
2. Before planning a code change, open the owning repo from [Where the code lives](#where-the-code-lives)
   and read its README and the nearest component README for every affected path.
3. Read [docs/plan.md](docs/plan.md) — the active-work hub. It's a **light table of contents**:
   links + a one-line status per active phase, nothing more. It answers only "what is next?".
   Read [docs/backlog.md](docs/backlog.md) or [docs/plan_completed.md](docs/plan_completed.md)
   only when the task requires deferred or historical context.
4. Read the spoke doc for the current phase — linked from `docs/plan.md` — for the actual detail:
   design, decisions, task status.
5. Read [docs/design/architecture.md](docs/design/architecture.md) if you need component
   boundaries, or another `docs/design/*.md` file if the task touches that area.

Do not load everything at once. Start from the hub and follow links only when the task requires it.

---

## Documentation strategy — hub/spoke, TOC + detail

`docs/plan.md` is the **authoritative index and nothing else** — links plus at most a one-line
status per phase. All depth (architecture, decisions, task breakdowns, evidence, gotchas) lives in
the linked docs it points to, never inlined into `plan.md` itself:

- **Hub** — `docs/plan.md`. Active items + status, kept small enough to scan every time it loads.
- **Spokes** — `docs/phases/phase-N-<slug>.md` (vision, locked decisions, dependency-ordered task
  list) and `docs/phases/phase-N.M-<slug>.md` (design → chronological tasks with file paths, real
  APIs, and a "Done when" — written so an agent can execute from the doc alone).
- **Cross-cutting design** — `docs/design/*.md` (architecture, language grammar, code style, deploy
  runbook, etc.) — things that aren't tied to one phase.

When designing a new feature: create the hub + spokes, then update `plan.md`'s top pointer + phases
index. If a `plan.md` cell grows past 1–2 lines or starts explaining *how* rather than linking to
where the *how* lives, that content belongs in the spoke, not the hub.

**No sub-phase-level detail in `plan.md`, in any form — one row per top-level phase, and that row
describes the phase's overall state only.** This means no separate rows for sub-phases
(`phase-N.M-<slug>.md` is never linked directly from the phases index), but just as importantly, no
sub-phase detail smuggled into prose either — a status or description cell that names which specific
numbered sub-items are done/deferred/outstanding (`"41.1/41.2/41.7 ✅ LIVE"`, `"Spokes 1–4 done"`) is
the same violation in a different shape, and just as easy to miss on review since it still reads as
"one row." If you're about to type a sub-phase/spoke/task number into `plan.md`, stop — it belongs in
the phase's own hub, not here. Whether a phase's sub-phases live as sections in one hub file or split
into their own spoke files is that phase's internal decision; `plan.md` never needs to know or
reflect it either way.

**When fixing a reported instance of a documentation-hygiene problem (stale status, duplicated
detail, a bloated cell), don't stop at the reported instance.** Re-derive the general rule behind
what was flagged and sweep the *whole* file against it before calling the cleanup done. This exact
class of `plan.md` bloat (sub-phase detail leaking back in) was reported and re-fixed four separate
times in one cleanup pass because each fix addressed only what was pointed out, not everywhere the
same shape occurred.

**Status honesty matters more than the format.** "Done" means verified — a test result, a live log
line, a deployed artifact confirmed by a real check — not "written" or "code merged." A doc that
says a database is deployed because the Bicep was authored, when the DB was never actually applied,
is worse than no doc at all — it actively misleads the next agent into skipping verification.

**Plan routing:** `docs/plan.md` contains selected active work only; move deferred work to
[docs/backlog.md](docs/backlog.md) and verified or superseded work to
[docs/plan_completed.md](docs/plan_completed.md). A superseded plan must name and link to its
replacement rather than silently disappear.

### Spoke shape — lookup table, not narrative; completed work moves out

A spoke's active body should read as a **lookup table**: what's done (one line + evidence pointer),
what's blocked and why, what's next. Not a chronological log of everything that happened while
building it. The point is to minimize what has to be loaded and re-derived from — a wall of
narrative is exactly what produces the failure this rule set keeps naming: a status or decision that
*reads* settled because it's mixed in with a hundred other lines, when it was never actually closed
out.

**When a task, investigation, or decision is genuinely done and verified**, move its narrative
detail out of the spoke into a sibling `phase-N[.M]-<slug>_completed.md`, and leave a one-line
pointer in the active spoke (`Task 4 — done, see phase-42.6-hosted-endpoint-ttfa_completed.md#task-4`).
This is not archival for its own sake — it means a fresh agent (or session) doesn't load
already-resolved history into context by default, and doesn't have to read 600 lines to find the ~50
that are still open. Move to `_completed`:
- Finished, verified tasks — keep the one-line status + evidence pointer in the active spoke; the
  full build narrative (what was tried, gotchas, verification detail) goes in `_completed`.
- Resolved investigations/incidents (a bug that's fixed, a DB-wipe root-caused and structurally
  prevented) — the postmortem narrative goes in `_completed`; the active spoke keeps only "fixed,
  see `_completed` for the investigation" if it's even still relevant to mention.
- Superseded design sections — once a redesign is itself the current truth, the old design's
  writeup (kept today as "what this supersedes") goes in `_completed`, not the active doc.

**Stays in the active spoke, never moves to `_completed`:** anything a *future, not-yet-done* task
depends on — locked decisions, type/DTO definitions still being built against, the "Done when"
condition, open gaps blocking the next task. A completed task's evidence can move out; the design it
was built on cannot, if later tasks still reference it.

---

## Agent memory — scratch space, not storage

Agent memory (`project_*.md` files under the session's memory directory) is **not durable**. The
[`/checkpoint` skill](#checkpoint-skill) deletes every `project_*.md` file at the end of each
session it runs in — after folding anything real (a design decision, a status fact, a gotcha) into
the hub/spoke or an appropriate `docs/design/*.md` file first. Nothing project-shaped should be
treated as safely stored in memory long-term; if it matters past this session, it needs to be in a
doc before the session ends, not left for memory to carry forward.

This does **not** apply to `feedback_*.md` / `reference_*.md` memory — working-style preferences
and cross-project facts that have no doc home by design. Those persist normally.

---

## Session continuity protocol

Agent performance degrades as context fills, so a session is treated as a bounded unit of work with
a clean handoff — a fresh agent, or the user just asking **"what's next?"**, should be able to
resume at full capacity from the hub/spoke docs alone, with nothing lost. In order, at the end of a
session:

1. Reconcile everything done/decided/discovered this session into the hub + spoke — status,
   evidence, decisions, gotchas, deployed artifact versions.
2. Fold durable agent-memory facts into the hub/spoke (see above), then let them be deleted.
3. Make **"what's next"** unambiguous in the hub's top so a fresh agent can resume from the plan
   alone.
4. Verify tests pass. Don't hand off with known-failing tests undocumented.
5. Commit + push **everything, across every touched repo** (this may span more than one repo),
   then create a PR and merge it into `main` once its required verification passes. A task is not
   fully delivered while its completed work sits only on a feature branch. Apply this per repo:
   one task may require one PR in each repository it changed. Never commit directly to `main`, and
   never merge work whose required checks are failing or still unverified. End on `main` with 0
   uncommitted / 0 unpushed per repo. Never an empty commit.

## Branch isolation

Start every implementation task on a new `codex/` branch before writing code or task documentation.
Do not work directly on `main`: an isolated branch keeps incomplete work from contaminating the
known-good baseline and contains regression risk until the task has been reviewed and merged. Reuse
an existing branch only when explicitly continuing the same unfinished task; otherwise create a new,
task-named branch from the current intended base.

---

## Checkpoint skill

The `/checkpoint` skill (`~/.claude/skills/checkpoint/SKILL.md`) is what actually runs the session
continuity protocol above — it's the executable form of it, not a separate idea. Invoke it at
session end, before a context reset, or whenever asked to "checkpoint" / "save everything" /
"session continuity" / "handoff" / "capture our work". It's idempotent: running it again with
nothing changed is a safe no-op.

**Self-install on a fresh machine.** Skills live in `~/.claude/skills/` — a per-machine, user-level
location, not something `git clone` brings with it. This repo carries its own copy at
[`skills/checkpoint/SKILL.md`](skills/checkpoint/SKILL.md) specifically so a new clone (a new
laptop, a fresh CI box) can self-install it. Before the first `/checkpoint` invocation in a
session, check whether `~/.claude/skills/checkpoint/SKILL.md` exists; if it doesn't, copy this
repo's `skills/checkpoint/SKILL.md` there (create the directory if needed), tell the user you did
so, then proceed. Keep the two copies in sync — if you ever edit the live skill at
`~/.claude/skills/checkpoint/SKILL.md`, copy the change back into `skills/checkpoint/SKILL.md` in
this repo (and vice versa) so the repo copy doesn't drift stale.

---

## How work is structured

> **Exception — Phase 50 repository extraction.** The gates below (design first, security,
> engineering philosophy, default-path acceptance, supervisor workflow) apply to all work *except*
> Phase 50 moves. Those change no behaviour; follow only the
> [Phase 50 protocol](docs/phases/phase-50-repository-extraction.md#extraction-protocol).

### Design first
Design decisions are captured in `docs/design/` or the relevant phase spoke before implementation
**starts, not partway through it.** An open architecture question, a deferred decision, or a
"resolve during implementation" note means the design isn't finished — it's not a detail
implementation can fill in on its own. If something is unclear, check there first; if it's not
documented, raise it and get it resolved before handing off any implementation work. The payoff:
a task that's handed off can run start-to-finish, unblocked, without stopping mid-build to ask a
design question that should have been closed out beforehand.

For every user-visible Desktop or ForgeUI change, read
[Desktop Interaction Principles](docs/design/desktop-interaction-principles.md) and the
[UI Design System](docs/design/ui-design-system.md) before design or implementation. They govern
the binding visual reference, responsive evidence, and theme/token ownership; the task assignment
must name them explicitly rather than leaving their relevance to inference.

**Browser verification is the supervisor's responsibility.** For a user-visible web-rendered
surface, the supervisor personally inspects the running HTTP surface with browser tooling and
compares it with the binding reference before recording visual acceptance. An implementer's
screenshot, code review, or automated test is supporting evidence only; none can substitute for
that live inspection.

For every task that changes user-visible, runtime, integration, or deployment behaviour, the
already-required [Default-Path Acceptance](docs/design/default-path-acceptance.md) gate applies.
The supported default configuration is a product fact, not a convenient test option: an overridden
URL, stub, injected setting, or hand-prepared dependency may prove a lower test layer but can never
close the task. The plan and completion evidence must name and exercise the applicable default path;
a missing default definition is a design gap, not permission to substitute one.

### Architecture-security gate

Every design must pass [Security Architecture](docs/design/security-architecture.md) before an
implementation handoff. This is mandatory for hosted changes and applies proportionately to every
other change: state whether the tier/data/identity questions are not applicable rather than silently
skipping them. Tier boundaries, bounded-context/data ownership, public entry points, and
cross-context contracts are Type-1 decisions and must be locked before code/IaC. A Type-2 exception
must state its exact scope, reversal path, and removal condition in the active spoke. Do not approve
a plan that lets an internet-facing component hold direct datastore access, lets one context query
another's store, or leaves an architecture-security answer for implementation to decide.

### Engineering-philosophy gate

Every design and implementation handoff must also pass [Engineering
Philosophy](docs/design/engineering-philosophy.md). Treat its bad-smell review as a build-readiness
gate: lock named ownership and failure boundaries, reject unjustified knobs and speculative
abstractions, prefer structural containment to warnings or remembered procedures, and name the
verification observation in “Done when.” Record any material exception and its removal path in the
active spoke; do not defer it to implementation.

### Roles — supervisor / subagent implementer

All implementation work follows the [supervisor workflow](docs/design/supervisor-workflow.md).
It is provider-neutral: whichever LLM agent the operator is working in (Claude, Codex, or another)
is the supervisor and uses its own subagents. The operator may run some tasks in one agent and
others in another. The supervisor owns design, scope, adversarial plan review, and final
acceptance. A bounded subagent owns implementation of one explicitly approved task. The supervisor
may write or correct design and planning documentation; a subagent may investigate without edits,
but may not modify code, infrastructure, or executable configuration until the supervisor has
explicitly approved its plan.

The required loop is **scope → subagent plan → supervisor adversarial approval → implementation →
subagent evidence summary → supervisor acceptance review**. The implementer never approves its own
plan, resolves an open design question by inference, broadens scope, or marks a task complete. The
supervisor independently checks the diff and evidence against the task's `Done when` condition.
Internal supervisor/subagent handoffs use the agent's own subagent tools and do not need a human
relay.

### Phases and tasks
Work is broken into phases, each with a spoke document in `docs/phases/`. Phases have a
dependency-ordered spoke list; spokes have a chronological task list. Don't skip ahead of declared
dependencies.

### Completion conditions
Each phase/spoke doc defines a "Done when" condition. Don't mark it done in `docs/plan.md` until
that condition is actually met and verified — not just implemented.

---

## Local dev environment — shell + provider keys

The maintainer's default shell is **PowerShell (`pwsh`)**, and all provider keys are already
exported there — you do not need to ask for keys, they exist. Full detail (which keys, the
Bash-doesn't-inherit-pwsh trap, the pull-through-pwsh recipe) is in
[docs/design/deploy.md → Local dev environment](docs/design/deploy.md#local-dev-environment--shell--provider-keys-read-this-before-running-anything-locally)
— not duplicated here.

---

## Deploying the hosted app (forge-infra)

The hosted app (ForgeUI, ForgeAPI, the runner) reaches Azure only through the **separate repo
`katasec/forge-infra`** (layered Bicep + Makefile, checked out at `~/progs/forge-infra`).

- **Only use the `make` targets** (`100-base`, `150-ci`, `300-data`, `400-appenv`, `450-migrate`,
  `500-app`, `500-app-bump-image`, `500-app-deploy-image`) — never raw `az deployment` commands or
  a hand-rolled script. Layer order and full command reference are
  `forge-infra/README.md`'s job, not duplicated here.
- **Run `make <layer>-what-if` before any secret-bearing or app-layer deploy** (`300-data`,
  `500-app`) — no exceptions.
- **Deploying a migration job definition and starting a migration are two separate deliberate
  steps** — `make 450-migrate` only updates the job definition, it never runs it. This split is
  the structural fix for a prior dev-DB-wipe incident (see
  [phase-42.6 completed doc](docs/phases/phase-42.6-hosted-endpoint-ttfa_completed.md#migration-job-db-wipe--defused-2026-07-18-structurally-fixed-2026-07-19))
  — do not re-couple them.
- Topology, gotchas, and the full command reference: [Deploy Runbook](docs/design/deploy.md),
  which itself defers to `forge-infra/README.md` as the source of truth for commands.

---

## Conventions

- **No Co-Authored-By lines in commits.** Commits are attributed to the repo owner only.
- **PascalCase for expert and mission names, camelCase for variables/parameters, lowercase
  keywords** (`mission`, `loop`, `when`, `using`, `parallel`, `let`, `env`). Enforced by the parser
  — wrong case is a parse error.
- **Runtime and data keys are `snake_case`** — reserved runtime keys (`output`, `feedback`,
  `max_loops`) and any key produced by `exec`/`onnx`/`json_extract` steps.
- **Language files use the `.mcl` extension**, binary is `forge`. Expert markdown files live under
  `experts/<ExpertName>/expert.md`. Lock file is `mcl.lock` (relative paths, generated by
  `forge init`). Reserved context variables: `apiKey`, `model`, `provider`, `endpoint`.
- **Progressive Disclosure — code reveals intent in layers.** Outline-first files, small named
  functions, top-down ordering, early returns over nesting, isolated side effects, explicit error
  handling, zero warnings, no speculative abstractions. Full rules in
  [docs/design/code-style.md](docs/design/code-style.md).
- **Match response shape to what's asked — verbosity is a defect, not thoroughness.** Answer scoped
  questions directly and stop; lead multi-point answers with a compact table (one row per
  independent idea, not per sentence); merge issue→response / request→open-question pairs into one
  row instead of padding row count. Full rules in
  [docs/design/collaboration-style.md](docs/design/collaboration-style.md).
- **Use plain, direct, concrete English.** Lead with the action or answer. Avoid abstract,
  nominalized, corporate, or AI-speak phrasing when ordinary words say it more clearly. Full
  examples are in [docs/design/collaboration-style.md](docs/design/collaboration-style.md).
- **Report results straight — no cliffhangers.** State what is done and proven, then stop. Don't
  end a wrap-up with a manufactured "one thing left" hedge to look thorough or invite another turn.
  A genuine limitation is one plain line, not a teaser.
- **Never claim "deployed" or "verified" without a named observation** — a command output, a test
  result, a live log line. An untested inference gets stated as an inference, not a fact. This
  applies doubly to Azure/infra permission claims: a 403 on one API says nothing about a different
  API — verify each capability by trying it.
- **"Build" and "design" are different labels — don't mark a task build-ready until its dependent
  types/decisions actually resolve, not just look decided.** A task can read as implementation-ready
  (named classes, a service signature, a concrete flow) while still depending on an undefined
  response type, a dangling "see note below" that was never written, or a field left over from a
  decision that got reversed elsewhere in the same doc. A future agent reading "(build first)" will
  go straight to code and improvise the gaps rather than stop — which produces code that looks
  decided when it wasn't, the same failure this whole rule set exists to prevent, just moved from
  docs into code. Before marking anything build-ready: check every type/response shape it references
  is actually defined in the doc, not just named.

---

## Project structure

```
README.md        — what MCL is and why it exists
AGENTS.md        — this file (canonical; CLAUDE.md symlinks here)
docs/
  plan.md        — active-work hub: current phases only
  backlog.md     — deferred candidates, paused work, and external conditions
  plan_completed.md — verified completed work and superseded-plan mappings
  design/        — cross-cutting design decisions
  phases/        — one hub + spokes per phase, task lists and statuses
agents/          — agent definitions
missions/        — example + built-in missions
skills/          — repo copies of agent skills (e.g. checkpoint)
clients/, editors/, html/ — to be placed (Phase 50 row 7)
```
