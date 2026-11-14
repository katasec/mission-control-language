# Phase 79 — Product focus: define, clean up, deslop

> **Status: steps 1–2 done; steps 3–4 run per repo (2026-11-14).** Product:
> [product.md](../design/product.md). Audit and decisions: [phase-79-audit.md](phase-79-audit.md).
> **Next:** delete forge-mcl's agreed list (its decisions are all answered); see
> [Incremental deletes](#incremental-deletes).

## Goal

Make every Forge repo serve one product and nothing else. Months of exploring UX surfaces (Forge
Desktop, the `forge chat` TUI, Forge Rooms, and others) left code, tests and docs that no longer
match the goal and confuse future agents. Remove them, then decide per component whether the code
that remains is deslopped in place or rewritten clean.

## The product (operator, 2026-11-14)

Summary; the authoritative version is [product.md](../design/product.md).

- **MCL is to AI workflows what Terraform is to cloud.** Missions compose experts into loops; missions
  chain; they ship over OCI like container images; any LLM backs them through the provider in
  `forge.toml`.
- **Expert machinery for everyone.** The skill stacks and methods only power users run today become
  OCI missions anyone can run, with stack editions (.NET, TypeScript, Go, …).
- **Fix the machinery, not the output.** When a result is wrong, the user corrects the mission
  (experts, context, instructions, gates) and reruns it. They do not hand-edit the generated code.
- **The loop:** consume or author a mission → run it → watch the flight recorder → correct the
  mission → rerun, until the output is right.
- **Fundamentals:** MCL language, `forge` CLI, Forge cloud runs, Hands on the client, an MCP
  wrapper. Mission runs and MCP exist; the flight recorder is
  [Phase 78](phase-78-run-detail-capture.md) (draft); mission correction is not designed yet.

## Steps

| # | Step | Done when | Status |
|---|---|---|---|
| 1 | **Product doc**: the loop, the fundamentals, the focused UX, what is in and out | `docs/design/product.md` merged and linked from `plan.md`; every open question below answered | Done: [product.md](../design/product.md), merged with this phase spoke |
| 2 | **Cleanup audit**, read-only: every Forge repo's code, tests and docs marked keep / delete / unsure against the product doc, with a reason; includes `backlog.md` | One table per repo, recorded in [phase-79-audit.md](phase-79-audit.md) | Done (2026-11-14): [phase-79-audit.md](phase-79-audit.md) |
| 3 | **Operator approval** of the audit, **per repo**, just before that repo's delete; every "unsure" row in it decided | That repo's list agreed and its decisions answered in [phase-79-audit.md](phase-79-audit.md#decisions-needed) | In progress: forge-mcl and forge-client agreed |
| 4 | **Delete, one repo at a time** (see [Incremental deletes](#incremental-deletes)). Delete rather than archive; git history is the archive | Every row in the incremental table merged and proven | Pending: forge-mcl is next |
| 5 | **Hosted teardown** of removed surfaces through `forge-infra` `make` targets, what-if first; published packages deprecated, not deleted | Named resources gone, confirmed by a what-if showing no drift | Pending |
| 6 | **Clean-room assessment** per kept component: run the [deslop playbook](../playbooks/dotnet-deslop.md) Discover stage read-only; mostly local findings → deslop in place, wrong structure → rewrite clean using existing tests and contracts as the spec | A decision per component, recorded here | Pending |
| 7 | **Execute** the deslop or clean-room decisions | Per component, defined in step 6 | Pending |
| 8 | **Build stage 1** on the clean code: Phase 78 recorder, the ForgeUI viewer, the supervisor workflow ported to MCL, the parity check | The stage-1 condition in [Q9](#open-questions) | Pending |

## Incremental deletes

Operator rule (2026-11-14): **one repo at a time, proven before the next.** Agree the repo's list
with the operator (one question at a time), delete it on an `adeen/` branch, prove it, merge, then
move on. A failure then points at one delete, not a pile of them.

**Proof for every repo** (all must pass before merge):
1. `dotnet build -warnaserror` and the full test suite of the repo (plus Native AOT publish for
   forge-mcl).
2. **A real `forge run`** of a small mission from a CLI built from the changed code, against the
   default cloud endpoint, with Hands writing a file in the project folder. For repos that deploy
   (forge-runner, forge-platform, forge-conversations, forge-rooms), the run goes through the newly
   deployed service, via the forge-infra `make` targets with what-if first.
3. Every consumer repo still builds against any package version this repo publishes.

| # | Repo | Decisions needed first | List | Status |
|---|---|---|---|---|
| 1 | forge-mcl | D1, D4 | [audit](phase-79-audit.md#forge-mcl) | **Agreed; ready.** Tag `forge-chat-final` first; move `ForgeChat.Print` into `run --verbose`; strip Theme; then delete |
| 2 | forge-client | D2, D3 | [audit](phase-79-audit.md#forge-client) | List agreed; D2, D3 open. Needs #1 merged (forge-mcl stops calling chat APIs) |
| 3 | forge-rooms (ForgeUI) | D5, D6, D7 | [audit](phase-79-audit.md#forge-rooms-forgeui) | Not reviewed. Extract the account model before deleting Rooms |
| 4 | forge-platform | D3 | [audit](phase-79-audit.md#forge-platform) | Not reviewed. Lockstep with a Contracts bump in #5 |
| 5 | forge-conversations | D3, D10 | [audit](phase-79-audit.md#forge-conversations) | Not reviewed |
| 6 | forge-runner | — | [audit](phase-79-audit.md#forge-runner) | Not reviewed. Needs #3 and #4 merged (they stop calling `/run`); move `RunWorkspace` first |
| 7 | forge-infra | D8, D9 | [audit](phase-79-audit.md#forge-infra) | Not reviewed. Live teardown is step 5 |
| 8 | mission-control-language | D11 | [audit](phase-79-audit.md#mission-control-language-this-repo) | Not reviewed. Fold decisions into design docs before deleting |

## Rules

- **Ask one question at a time** when reviewing with the operator.
- **Docs keep rule:** a doc stays only if it describes current truth the product needs or active
  work. Finished-phase narratives, superseded designs, old evidence, trails and spikes are deleted.
- **Deletion before deslop, deslop before new code.** Step 6 runs on what survives step 4; stage 1
  (step 8) is built only on deslopped or rewritten code. Code is memory: agents copy what they read.
- **Gates:** steps 1–3 are documentation only (default-path acceptance and security: N/A). Steps 4,
  5 and 7 change behaviour and pass the usual gates, scoped in their own spokes when selected.

## Open questions

Asked one at a time; answers are recorded here as decisions.

| # | Question | Answer |
|---|---|---|
| Q1 | What is the focused UX, and where does it live? | **Stage 1 (MVP): runs start from the CLI; a web app shows the telemetry** (the flight recorder), drawn from the [Phase 78 mockups](phase-78-run-detail-capture.md#mockups-high-level-reference-not-final). Purpose: run a few real missions, read their telemetry, and refine the dev loop. Correction in stage 1 = edit the mission locally and rerun from the CLI. Proposed scope, pending operator confirmation: the web app is read-only and shows only the Runs list (mockup 4) and run/Jobs detail (mockups 6–7); Discuss, Missions, Prepare run, decisions and Pause/Stop wait for a later stage. |
| Q2 | Where does the telemetry web app live? | **In ForgeUI** (forge-rooms repo), reusing its login, accounts, billing and design system. The telemetry viewer is a new, small part of ForgeUI. **The Rooms feature is deleted.** |
| Q3 | Does the `forge chat` TUI stay? | **Delete it**, after tagging the last commit that has it (e.g. `forge-chat-final`) so it can be recovered from git. Not extracted: a parked repo would rot as forge-client changes. Recovery later means porting to the client of that time. `forge run` must stay whole (Phase 76 made chat and run share one client path). |
| Q4 | What happens to the `forge-desktop` repo? | **Removed from the Forge repo list** (AGENTS.md, README and every other reference). On GitHub it is **archived** (read-only, reversible), not deleted, unless the operator later asks to delete it. Its backlog items go with it. |
| Q5 | Docs keep rule: delete finished-phase narratives (`_completed` files, `plan_completed.md`) and drop the `_completed` convention from AGENTS.md? | **Yes.** When work is done its narrative is deleted; git keeps it. Delete every `_completed` file and `plan_completed.md`, and replace the AGENTS.md convention. Before deleting, fold any decision still in force (e.g. the migration-job split that AGENTS.md links to) into the relevant `docs/design/` file. |
| Q6 | Who uses the stage-1 MVP? | **Only the operator for now; the product is always meant to be multi-user.** So code-execution isolation ([references](../design/code-execution-isolation-references.md), still research only) is a fundamental, not hardening: it does not block stage 1, but it is a hard gate before any second user. Proposed, pending operator confirmation: name that gate in the product doc, design isolation early (where mission code runs decides what the cleanup keeps), and add a cheap interim guard (`exec` admitted only from the operator's packages, run with a cleared environment). The unmerged `adeen/phase-78-exec-process` decision is reviewed in the audit against the isolation design. **Confirmed (Q7):** get stage 1 working for the operator first, then design and build isolation. No other users are registered until isolation is done; the interim guard is dropped. |
| Q8 | How is "no other users" enforced while sign-up exists? | **Not enforced; accepted risk** (operator): the system is not public or advertised. No sign-up allowlist in stage 1. Revisit with isolation, before any second user. |
| Q9 | Which mission(s) prove stage 1 works? | **The .NET supervisor mission**, so Forge is dogfooded on its own development. No such mission exists yet: [Phase 72](phase-72-supervisor-comparison.md) has only a partial MCL harness (design plus reviews, no implementation stage). Stage-1 work, once the recorder and viewer work: **port the existing [supervisor workflow](../design/supervisor-workflow.md) to MCL** (same stages, gates, roles and skills; not a new design). **Parity check:** one very small task run twice by subagents, once following the existing markdown workflow and once through the MCL mission via `forge run`; compare for similar quality and similar behaviour (stages followed, gates run, skills used). LLMs are not deterministic, so this is an assessment, not an exact match. Reuse [Phase 72](phase-72-supervisor-comparison.md)'s two-script comparison harness where it fits. Stage 1 is done when the parity check passes and the operator then runs the mission on a real task, finds in the web viewer where it went wrong, edits the mission, reruns, and sees the improvement. |
| Q10 | What order: cleanup first, or stage 1 first? | **Full cleanup first** (operator): audit and delete, then deslop or clean-room rewrite, then build stage 1 (steps 1–8 in order). Reason: **code is memory.** LLMs predict text from the code they read, so building on slop replicates and multiplies it and creates more deslop later. |
