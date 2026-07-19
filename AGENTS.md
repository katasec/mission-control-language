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

---

## How to orient at the start of a session

1. Read this file.
2. Read [docs/plan.md](docs/plan.md) — the hub. It's a **light table of contents**: links + a
   one-line status per phase, nothing more. It tells you what phases exist, which are done, and
   which are active.
3. Read the spoke doc for the current phase — linked from `docs/plan.md` — for the actual detail:
   design, decisions, task status.
4. Read [docs/design/architecture.md](docs/design/architecture.md) if you need component
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

**Status honesty matters more than the format.** "Done" means verified — a test result, a live log
line, a deployed artifact confirmed by a real check — not "written" or "code merged." A doc that
says a database is deployed because the Bicep was authored, when the DB was never actually applied,
is worse than no doc at all — it actively misleads the next agent into skipping verification.

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
5. Commit + push **everything, across every touched repo** (this may span more than one repo).
   End on 0 uncommitted / 0 unpushed per repo. Never an empty commit.

---

## Checkpoint skill

The `/checkpoint` skill (`~/.claude/skills/checkpoint/SKILL.md`) is what actually runs the session
continuity protocol above — it's the executable form of it, not a separate idea. Invoke it at
session end, before a context reset, or whenever asked to "checkpoint" / "save everything" /
"session continuity" / "handoff" / "capture our work". It's idempotent: running it again with
nothing changed is a safe no-op.

---

## How work is structured

### Design first
Design decisions are captured in `docs/design/` or the relevant phase spoke before implementation.
If something is unclear, check there first. If it's not documented, raise it before implementing.

### Phases and tasks
Work is broken into phases, each with a spoke document in `docs/phases/`. Phases have a
dependency-ordered spoke list; spokes have a chronological task list. Don't skip ahead of declared
dependencies.

### Completion conditions
Each phase/spoke doc defines a "Done when" condition. Don't mark it done in `docs/plan.md` until
that condition is actually met and verified — not just implemented.

---

## How to run the build and tests

```bash
make install              # native AOT publish → ~/.local/bin/forge (osx-arm64)
make demo-naive           # end-to-end smoke test (forces a full rebuild)
dotnet build src/ForgeMission.slnx
dotnet test src/ForgeMission.slnx
```

All tests must pass before marking any task complete. Never mark a task done if tests are failing.

---

## AOT-first — standing rules for all new code

**Every change must remain Native AOT-safe.** The `forge` CLI binary publishes with
`<PublishAot>true</PublishAot>`. Violations cause ILC (IL Compiler) warnings or runtime crashes.

This binds on code compiled into the AOT binary (CLI, and the runner/host where they share Core).
It does **not** bind on `kind: exec` subprocess tooling — a python script run out-of-process is
never linked into the AOT image, so pick those libraries on merit (correctness/licensing), not
AOT-compatibility. This is a selection criterion for any new .NET package, not just a code pattern:
favor AOT-compatible libraries wherever possible, and surface the tradeoff before adopting one
that isn't clean, rather than silently taking on suppressions.

### JSON / STJ

**Never** use `new JsonSerializerOptions { ... }` at runtime in AOT code — a bare
`JsonSerializerOptions` without a `TypeInfoResolver` crashes under AOT. Use STJ source generation:

```csharp
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(MyType))]
internal partial class MyTypeContext : JsonSerializerContext { }
```

Pass `MyTypeContext.Default.Options` wherever `JsonSerializerOptions` is needed. For
`IChatClient.GetResponseAsync<T>`, always pass the source-gen options:

```csharp
var response = await chatClient.GetResponseAsync<T>(messages, MyTypeContext.Default.Options, cancellationToken: ct);
```

### YAML (YamlDotNet)

YamlDotNet uses reflection internally. Preserve any POCO that flows through `ISerializer`/
`IDeserializer` with:

```csharp
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MyPoco))]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Type preserved via DynamicDependency")]
private static readonly IDeserializer Deserializer = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .IgnoreUnmatchedProperties()
    .Build();
```

### Reflection / dynamic dispatch

Avoid `Type.GetType(string)`, `Activator.CreateInstance`, or `Assembly.GetTypes()`. Prefer
`IChatClient` (`Microsoft.Extensions.AI`) — it is AOT-safe by design.

### Warning suppression (already in Cli.csproj)

```xml
<IlcSuppressWarnings>IL3050</IlcSuppressWarnings>
<NoWarn>$(NoWarn);IL3050;IL2104;IL3053</NoWarn>
```

These cover YamlDotNet assembly-level warnings. Do **not** add new suppressions without a
`[DynamicDependency]` or a concrete explanation.

---

## Local dev environment — shell + provider keys

The maintainer's default shell is **PowerShell (`pwsh`)**, and all provider keys are already
exported there — you do not need to ask for keys, they exist. Full detail (which keys, the
Bash-doesn't-inherit-pwsh trap, the pull-through-pwsh recipe) is in
[docs/design/deploy.md → Local dev environment](docs/design/deploy.md#local-dev-environment--shell--provider-keys-read-this-before-running-anything-locally)
— not duplicated here.

---

## Release workflow

Releases are cut via GitHub Actions (`workflow_dispatch`):
1. Enter the version (e.g. `0.1.3`) in the Actions UI.
2. The workflow tags the commit, opens a draft release, and attaches
   `forge-osx-arm64`, `forge-linux-x64`, and `forge-win-arm64.exe`.
3. Review the draft on GitHub, then publish.

Semver: patch bump for bug fixes and backwards-compatible changes; minor for new user-visible
language features; major for breaking `.mcl` syntax changes.

---

## Deploying the hosted app (forge-infra) — separate from the release workflow above

The CLI binary release above (GitHub Releases) is **not** how the hosted app (ForgeUI, ForgeAPI,
the runner) reaches Azure. That is a **separate repo, `katasec/forge-infra`** (layered Bicep +
Makefile, checked out at `~/progs/forge-infra`), and every deploy goes through it.

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

## Supported providers

`ProviderClientBuilder` in `src/ForgeMission.Cli/ProviderClientBuilder.cs` maps the `provider`
field in `forge.toml` to an `IChatClient`. Adding a new provider is a single switch case + one
private method — no new packages needed for OpenAI-compatible APIs.

| `provider` value | API | SDK used |
|---|---|---|
| `openai` / `azure` | OpenAI / Azure OpenAI | `OpenAI` NuGet |
| `anthropic` | Anthropic Claude | `Anthropic` NuGet |
| `ollama` | Ollama (local) | `OpenAI` NuGet (pointed at localhost) |
| `xai` | xAI Grok | `OpenAI` NuGet (pointed at api.x.ai/v1) |

**Adding a new OpenAI-compatible provider** (e.g. Groq, Together, Mistral):
1. Add a case to the switch in `ProviderClientBuilder.cs`.
2. Add a private method pointing `OpenAIClientOptions.Endpoint` at the provider's base URL.
3. No new NuGet packages required.

---

## Conventions

- **No Co-Authored-By lines in commits.** Commits are attributed to the repo owner only.
- **PascalCase for expert and mission names, camelCase for variables/parameters, lowercase
  keywords** (`mission`, `loop`, `when`, `using`, `parallel`, `let`, `env`). Enforced by the parser
  — wrong case is a parse error.
- **Runtime and data keys are `snake_case`** — reserved runtime keys (`output`, `feedback`,
  `max_loops`) and any key produced by `exec`/`onnx`/`json_extract` steps.
- **No business logic in the CLI.** The CLI wires up dependencies and delegates to Core.
- **`IExpertRunner` is the only interface** between the CLI and the AI provider. Keep it free of
  provider-specific types.
- **Language files use the `.mcl` extension**, binary is `forge`. Expert markdown files live under
  `experts/<ExpertName>/expert.md`. Lock file is `mcl.lock` (relative paths, generated by
  `forge init`). Reserved context variables: `apiKey`, `model`, `provider`, `endpoint`.
- **Progressive Disclosure — code reveals intent in layers.** Outline-first files, small named
  functions, top-down ordering, early returns over nesting, isolated side effects, explicit error
  handling, zero warnings, no speculative abstractions. Full rules in
  [docs/design/code-style.md](docs/design/code-style.md).
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
README.md               — what MCL is and why it exists
AGENTS.md                — this file (canonical; CLAUDE.md symlinks here)
docs/
  plan.md                — hub: light TOC, phase list with statuses and links
  design/                — cross-cutting design decisions (language, architecture, deploy, code-style, ...)
  phases/                — one hub + spokes per phase, task lists and statuses
src/
  ForgeMission.Core/      — parser, expert loader, pipeline runner
  ForgeMission.Cli/       — CLI entry point (forge)
  ForgeMission.*.Tests/   — test projects
missions/                — example + built-in missions
runs/                    — gitignored, output of `forge run`
```
