# Codex supervisor workflow

> **Status: governing implementation workflow.** This replaces the former external
> Claude/Codex relay for all implementation work. It preserves the same separation of design,
> adversarial approval, implementation, and acceptance inside Codex.

## Roles

| Role | Authority | May not do |
|---|---|---|
| Supervising Codex | Finish the design, write the scope card, challenge and approve/reject the implementation plan, and accept/reject completion. | Implement the task it is supervising, delegate its final decision, or treat a subagent's claim as acceptance evidence. |
| Investigating Codex subagent | Gather bounded source evidence, trace dependencies, or challenge a proposed design. | Edit files, decide an open design question, or approve a plan. |
| Implementing Codex subagent | Produce a file-by-file plan, then make only the supervisor-approved change and return evidence. | Edit before approval, broaden scope, self-approve, or mark work complete. |

The supervisor may create or correct design and planning documentation. Every code, infrastructure,
or executable-configuration task has exactly one implementing subagent at a time; other subagents
are read-only investigators or reviewers. Shared worktree access makes this serialization a
correctness requirement, not a convention.

## Required loop

1. **Scope.** The supervisor completes the relevant spoke's design, component-fit, security,
   engineering-philosophy, default-path, and UI gates. It writes a bounded scope card and `Done
   when` condition. An unresolved architecture, ownership, contract, or failure question blocks
   delegation.
2. **Plan.** The supervisor assigns one bounded implementing subagent through the collaboration
   tool. The assignment explicitly says **plan only; do not edit**. The subagent returns touched
   paths, sequence, tests, default-path facts, failure containment, and every assumption/open
   question.
3. **Adversarial approval.** The supervisor tests the plan against the spoke, component ownership,
   public/wire/persistence compatibility, Security Architecture, Engineering Philosophy, Native
   AOT, default-path acceptance, and UI gates where applicable. It either rejects with a concrete
   correction or sends explicit approval. Silence, a summary, or a request to continue is not
   approval.
4. **Implementation.** Only after explicit approval may that subagent edit. It works on the
   approved branch and reports actual commands, observations, failures, and deviations. Any
   material deviation returns to the supervisor before the change expands.
5. **Acceptance review.** The supervisor independently inspects the diff and completion evidence.
   It checks every `Done when` item, required negative proof, default-path observation, and UI
   acceptance where applicable. It accepts, rejects for correction, or records a genuine deferment.
   An implementer never accepts its own work.

## Supervisor assignment

Send this through the collaboration tool to the implementing subagent. Keep the task linkable to
the active spoke instead of duplicating decisions in the message.

```text
TASK ASSIGNMENT — PLAN ONLY

Role: implementing Codex subagent. Do not create or modify any file until I explicitly approve
your plan in a later message.

Read first:
- AGENTS.md
- docs/plan.md
- docs/design/default-path-acceptance.md
- [active spoke]
- [relevant design docs and component READMEs]

Task:
[bounded outcome and component-fit statement]

Scope and non-goals:
[approved boundaries, dependencies, and exclusions]

Done when:
[verbatim task condition or pointer]

Return only:
1. files to change/create and why;
2. implementation sequence;
3. focused/full/AOT and default-path verification plan;
4. failure-boundary and negative-path coverage; and
5. every unresolved question or assumption.

Do not edit files or run a mutating command. Wait for explicit supervisor approval.
```

## Approval message

The supervisor sends this only after it has accepted the plan. Any correction requires a revised
plan and another explicit approval.

```text
PLAN APPROVED

Implement only the approved plan and scope. Do not broaden the task or resolve a new design
question by inference. If a material deviation is required, stop and report it before editing
beyond the approved boundary.

When finished, return the completion summary below with actual evidence. Do not mark the task
complete.
```

## Subagent completion summary

```text
IMPLEMENTATION SUMMARY

Task:
[one line]

What changed:
[one line per file or component]

Verification:
[actual commands and observed results]

Precondition and negative-path matrix:
[each named positive/negative result, or N/A with reason]

Default-path acceptance:
[artifact, absent overrides, normal dependency, safe state, action, observed outcome; or N/A for
documentation-only work. Label every controlled override/test double as non-acceptance evidence.]

UI acceptance, if applicable:
[reference, viewport/state evidence, browser-first checks, packaged parity, PASS/FAIL]

Done when — evidence against each condition:
[met/not met]

Deviations from approved plan:
[none, or exact deviation and its status]

Open questions / follow-ups:
[none, or why the task cannot be accepted]
```

## Supervisor acceptance checklist

Before accepting, the supervisor records a named observation for each applicable item:

- the subagent changed only the approved scope and all component-fit statements remain true;
- public, wire, persistence, ownership, credential, and failure boundaries match the active design;
- focused, full, and Native AOT checks pass when the task changes code;
- the published default path passes for every user-visible, runtime, integration, or deployment
  change; controlled evidence is labelled and not substituted;
- Desktop/ForgeUI work has Presentation-surface parity, visual-reference PASS, responsive evidence,
  and packaged parity; and
- the diff, documentation, branch, commit, pull request, merge, and clean-main state meet the
  repository continuity protocol.

## Migration from the former workflow

[The former Claude/Codex workflow](claude-codex-workflow.md) is retained only as a pointer for old
links and historical context. It is not an implementation authority. Phase 46's Codex-supervised
procedure is the validated predecessor of this repository-wide workflow, not a special exception.
