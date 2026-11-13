# Forge — Product

The test for every keep/delete and build decision across the Forge repos. If a component, test or
doc does not serve what is described here, it goes. Decided with the operator on 2026-11-14; the
question-by-question record is in [Phase 79](../phases/phase-79-product-focus.md#open-questions).

## What Forge is

**MCL is to AI workflows what Terraform is to cloud.** A mission composes experts into a workflow
with loops, branches and parallel steps; missions chain other missions. Missions ship over OCI like
container images, so a workflow is pulled and run, not rebuilt. Any LLM backs them: the provider is
set in `forge.toml`, not in the mission.

## Why it matters

- **Expert machinery for everyone.** The skill stacks and working methods that only power users can
  wire up and supervise today become OCI missions anyone can run, with editions per stack (.NET,
  TypeScript, Go, …). The expertise lives in the mission, not in the person running it.
- **No more babysitting the process.** The stages, gates and roles are declared in the mission
  instead of hoped for in a prompt.
- **Fix the machinery, not the output.** When a result is wrong, the user does not hand-edit the
  generated code. They correct the mission (experts, context, instructions, gates) and rerun it.
  Better machinery yields better code.

## The loop

1. **Consume or author** a mission.
2. **Run** it.
3. **Read the flight recorder**: the complete, permanent record of what each expert was given, what
   it did, which tools it ran and what came back.
4. **Correct** the mission where the record shows it went wrong.
5. **Rerun**, until the output is right.

## Fundamentals

| Part | Role | State |
|---|---|---|
| MCL language | Defines missions | Built (forge-mcl) |
| `forge` CLI | Runs missions, pulls them over OCI | Built |
| Forge cloud runs | Where missions execute | Built |
| Hands | Run tools on the user's machine | Built (files) |
| MCP wrapper | Forge inside the user's own agent | Built |
| Flight recorder | Durable, full-fidelity run record | Designing: [Phase 78](../phases/phase-78-run-detail-capture.md) |
| Code-execution isolation | Mission code never reaches platform keys or other users' runs | Research only: [references](code-execution-isolation-references.md) |

## Stage 1 — MVP

**Who:** the operator only. **Surface:** runs start from the `forge` CLI; a read-only web viewer in
ForgeUI shows the flight recorder (Runs list and run detail, from the
[Phase 78 mockups](../phases/phase-78-run-detail-capture.md#mockups-high-level-reference-not-final)),
reusing ForgeUI's login, accounts, billing and design system. Correction means editing the mission
locally and rerunning from the CLI.

**Proving mission:** the existing [supervisor workflow](supervisor-workflow.md), ported to MCL with
the same stages, gates, roles and skills, so Forge is dogfooded on its own development.

**Done when:**
1. A parity check passes: one very small task run by subagents once with the markdown workflow and
   once with the MCL mission shows similar quality and behaviour (an assessment; LLMs are not
   deterministic).
2. The operator runs the mission on a real task, finds in the viewer where it went wrong, edits the
   mission, reruns, and sees the improvement.

## Out of stage 1

- In the viewer: Discuss, Missions, Prepare run, Pause/Stop, human decisions, acceptance criteria.
- `forge chat` TUI: deleted, recoverable from a git tag when needed.
- Forge Desktop: removed; its repo is archived on GitHub.
- Forge Rooms: deleted from ForgeUI.

## Multi-user gate

Forge is always meant to be multi-user, but stage 1 has one user and sign-up is not restricted
(accepted risk: the system is not public). **Before any second user:** code-execution isolation is
designed and built, and sign-up is revisited. Anyone running anyone's mission from OCI means running
untrusted code by design.

## How the work is ordered

**Code is memory.** Agents predict new code from the code they read, so building on slop copies and
multiplies it. The order is therefore:

1. Audit every Forge repo against this doc, and delete what does not serve it. Delete rather than
   archive; git history is the archive. Docs keep only current truth and active work.
2. Deslop in place, or rewrite clean, what survives.
3. Build stage 1 on the clean code.

Detail and status: [Phase 79](../phases/phase-79-product-focus.md).
