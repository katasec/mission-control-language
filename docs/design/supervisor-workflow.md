# Supervisor workflow

> **Status: governing implementation workflow.** Provider-neutral: the agent the operator works in
> (Claude, Codex, or another) is the supervisor and uses its own subagents. Checks fan out to
> parallel subagents, each carrying a [persona](../../personas/README.md) inline; the supervisor
> merges their results into one decision per stage. Design and code each have one author.

## Shape

```mermaid
flowchart TD
    S[0 Scope + parallel investigators] --> D[1 Design: one designer]
    D --> DR{Simplicity + Ownership} --> P[2 Plan: implementer]
    P --> PR{Simplicity + Ownership} --> I[3 Implement: new implementer]
    I --> CR{Simplicity + Ownership + Code style} --> RD[4 Ready to merge]
    RD --> M[5 Merge, publish, deploy] --> A[6 Accept on default path] --> C[7 Closure]
```

## Roles

| Role | Persona | Does | Never |
|---|---|---|---|
| Supervisor | This document | Scopes, merges each fan-out into one decision, approves, accepts. | Implements, delegates its decision, or takes a subagent's claim as evidence. |
| Investigator | None | Gathers facts for the scope, one repo or question each. | Edits, or decides a design question. |
| Designer | [designer](../../personas/designer.md) | Proposes the design in its reply. | Edits, or decides what the scope leaves open. |
| Implementer | [implementer](../../personas/implementer.md) | Plans; after approval, makes only the approved change. | Edits before approval, broadens scope, approves or accepts its own work. |
| Reviewer | [simplicity](../../personas/simplicity-reviewer.md), [ownership](../../personas/ownership-reviewer.md), [code style](../../personas/code-style-reviewer.md) | Returns one verdict per check. | Edits, or approves. |

Only one implementer edits at a time; everyone else is read-only, so they can run in parallel.

## Persona rule

Every assignment starts with the **full text** of its persona file. A link is not enough: rules
that live only in a linked file get skipped. Reviewers' per-check verdicts show every guardrail was
covered; designers and implementers list only the principles that changed a decision, and which.

## Stage tags

Every stage and every revision round is a **new** subagent launch, tagged so
[task-timing](../../tools/task-timing/README.md) can time it from the agents' own session logs. A
revision carries the previous output and the correction inline; never continue a finished
subagent for a new stage or round.

- **Claude:** the description starts `[<stage>(:<role>)(:r<N>)] <task>`, e.g.
  `[review-plan:ownership:r2] 64.2 task 3`. Round 1 has no `r` part.
- **Codex:** `spawn_agent`'s `task_name`, letters, digits and `_` only: `-`→`_`, `:`→`__`, then
  `__<task>`, e.g. `review_plan__ownership__r2__64_2_task_3`.
- **Stages:** `investigate:<repo>`, `design`, `review-design:<role>`, `plan`, `review-plan:<role>`,
  `implement`, `review-code:<role>`. Roles: `simplicity`, `ownership`, `style`.

## Required loop

0. **Scope.** The supervisor writes the spoke's requirement, `Done when`, default path, and the
   Security Architecture, Engineering Philosophy and UI gates that apply. User-visible work names
   its reference images, viewports, owned slice, states, theme selector and light/dark token map;
   other visible elements are deferred or omitted. Type-1 decisions go to the operator. Facts from
   more than one repo or question come from parallel investigators. An open architecture,
   ownership, contract, failure or visual question blocks the next stage.
1. **Design.** One designer receives the design assignment, naming each affected repo by absolute
   path from the [README repository list](../../README.md#where-the-code-lives). The simplicity and
   ownership reviewers check it in parallel; the supervisor sends one correction or locks the design
   in the spoke. Skip when the spoke already locks the design, or for documentation-only and
   Phase 50 move tasks; record why.
2. **Plan.** One implementer receives the plan assignment and does not edit. The simplicity and
   ownership reviewers check the plan in parallel; the supervisor also checks compatibility,
   security, Native AOT, default path and UI gates, then sends **one** combined correction or
   explicit approval. If the same point fails twice, return to Design for it.
3. **Implement.** A new implementer receives the implementation assignment. A material deviation,
   including a visual mismatch, returns to the supervisor before the change grows. The simplicity,
   ownership and code style reviewers then check the real diff in parallel; the supervisor fixes or
   dismisses each finding with a reason and sends one combined correction.
4. **Ready to merge.** The supervisor completes the readiness checklist below.
5. **Merge, publish, deploy.** Merge the product PRs (code, infrastructure, configuration) and
   publish or deploy through the normal route, so the default path uses the real artifacts.
6. **Accept.** The supervisor runs the default-path acceptance below. A failure is fixed through
   this loop, not routed around.
7. **Closure.** See [Closure](#closure).

## Assignments

Each assignment is the persona's full text, then:

```text
DESIGN — READ-ONLY. Do not create or modify any file; reply with the design.
Read: [active spoke], [README of every touched component]
Requirement: [word for word]
Scope, non-goals, locked decisions: [...]
Done when: [verbatim]
Return the output defined in the persona.
```

```text
PLAN ONLY — do not create or modify any file until I approve the plan.
Read: AGENTS.md, docs/design/default-path-acceptance.md, [active spoke], [component READMEs]
Task: [bounded outcome and component fit]
Scope and non-goals: [...]
UI, if user-visible: [reference images, viewports, states, owned and omitted elements, theme
selector, light/dark token map, contrast pairs]. Map every owned element; invent nothing.
Done when: [verbatim]
Return the plan output defined in the persona.
```

```text
REVIEW — READ-ONLY. Role: [simplicity | ownership | code style] reviewer.
Requirement: [word for word]
Under review: [design text | plan text | branch and repos to diff]
Return the output defined in the persona, with evidence from the actual code or diff.
```

```text
[approved plan]
PLAN APPROVED. Implement only this plan. Do not broaden scope or settle a new design question by
inference; stop and report a material deviation. UI work matches the named references and uses the
named theme and semantic tokens only, no component-local visual values. Return the completion
summary with actual evidence. Do not mark the task complete.
```

**Completion summary** (implementer): task; what changed, per file; verification commands and
results; positive and negative paths; principles that changed a decision; deviations; open
questions; `Done when` evidence per condition.

## Checklists

**Ready to merge** — a named observation for each applicable item:

- every code-review ⚠️ is fixed or dismissed with a reason;
- only the approved scope changed, and component fit still holds;
- public, wire, persistence, ownership, credential and failure boundaries match the design;
- focused, full and Native AOT checks pass, with zero warnings;
- UI work matches its references in a live browser check of the branch, at every owned viewport
  and state, using theme tokens only, light and dark;
- branch and commits meet the continuity protocol.

**Accept** — after merge and publish or deploy:

- the published default path passes for every user-visible, runtime, integration or deployment
  change ([Default-Path Acceptance](default-path-acceptance.md)); controlled evidence is labelled
  and never substituted;
- packaged UI parity holds where applicable.

## Closure

1. Run [task-timing](../../tools/task-timing/README.md) with every product PR. Timing runs from the
   first tagged subagent to the last product merge.
2. Put its table in the task's completion record, in the spoke docs PR that closes the task, and
   merge it. That PR is not timed.
3. End every touched repo on a clean, current `main` with no task worktrees.

A **documentation-only task** has no product PR: run task-timing without `--pr` before opening its
one PR, and include the table in it.

The former [Claude/Codex workflow](claude-codex-workflow.md) is retired and grants no exception.
