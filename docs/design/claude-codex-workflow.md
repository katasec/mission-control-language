# Claude ↔ Codex Workflow — Design/Implementation Handoff

**Governing principle: design finishes before implementation starts, so a handed-off task runs
start-to-finish, unblocked.** The underlying rule lives in
[AGENTS.md → Design first](../../AGENTS.md#design-first); this doc is the mechanics of handing a
finished design to Claude and getting back verified, working code.

## Current mode: roles swapped (trial, started 2026-08-11)

The original split (Claude architects, Codex implements) is flipped for a few weeks as a manual
test of MCL's own premise: experts and their roles in a pipeline should be swappable without
breaking the pipeline itself. If this workflow only works with one specific model in the architect
seat, that's worth knowing. Everything below describes the **current, swapped** mode:
**Codex architects, Claude implements.**

To revert: swap "Codex" and "Claude" back in the Roles section, the loop, both templates, and the
"If you are ___ reading this" callout below, and undo the matching flip in
[AGENTS.md → Roles](../../AGENTS.md#roles--codex-architect--claude-implementer).

---

## Roles

- **Codex** — architect, designer, reviewer. Finishes the design docs, writes the task assignment,
  reviews Claude's implementation plan before approving it, and reviews Claude's completion summary
  before approving the task as done.
- **Claude** — implementer. Never writes or modifies code without an explicitly approved plan.
  Always replies to a task assignment with a plan first, and to a finished implementation with a
  completion summary Codex can review.

**If you are Claude reading this because you just opened this repo:** this is your role. Wait for a
task assignment from Codex before doing anything else — do not start implementing on your own
initiative, even if a gap looks obvious.

---

## The loop

1. Codex finishes the design (per AGENTS.md's Design first rule — no open architecture questions)
   and sends Claude a task assignment using the template below.
2. Claude replies with an implementation plan only — no code yet. For a Desktop/native-host task,
   the plan starts with the **Desktop Design and Implementation Quality Gate** result from
   [Engineering Philosophy](engineering-philosophy.md#desktop-design-and-implementation-quality-gate).
3. Codex reviews the plan. If it's wrong, incomplete, or exposes a design gap, Codex sends
   corrections and Claude revises (repeat until approved). A plan that surfaces an unresolved design
   question goes back to design — it is never silently implemented around.
4. Once Codex approves, Claude implements.
5. Claude delivers a completion summary using the template below.
6. Codex reviews the summary against the task's "Done when" condition before marking the task
   done. For a Desktop/native-host task, Codex also re-runs the Desktop Quality Gate against the
   approved plan, changed boundary, and named observation; a failed answer rejects the work.
   Verification means a named check (test result, command output, a live run) — matching AGENTS.md's
   status-honesty rule — not "looks complete."

---

## Task assignment template (Codex → Claude)

Copy/paste and fill in the bracketed parts. Point at docs rather than restating their content, so
there is exactly one place each decision can drift out of date.

```
TASK ASSIGNMENT

Role: implementer. Do not write or modify any code until I approve your plan.

Read first (do not summarize these back to me):
- AGENTS.md
- docs/plan.md
- [docs/phases/phase-N.M-<slug>.md]
- [docs/design/<relevant>.md]

Task:
[1-3 sentences: what to build, scoped to one task from the spoke's task list]

Done when:
[the "Done when" condition from the spoke, or "see spoke doc, task N"]

Constraints:
[anything task-specific not already covered by AGENTS.md or the spoke - keep
 this short, or omit the section]

Next step:
Reply with an implementation plan only: files you will touch or create, your
approach, sequencing, and any assumption or open question not already answered
in the docs above. For a Desktop/native-host task, start with the five PASS/FAIL
answers in Engineering Philosophy's Desktop Design and Implementation Quality Gate.
Wait for my explicit approval before implementing.
```

---

## Completion summary template (Claude → Codex)

```
IMPLEMENTATION SUMMARY

Task: [one line, matches the assignment]

What changed:
[files touched/created, one line each]

Verification:
[the actual check performed - test run output, command output, a live check -
 not "should work"]

Done when - met?
[yes/no against the spoke's "Done when" condition, with the evidence above]

Deviations from the approved plan:
[none, or what changed and why]

Open questions / follow-ups:
[none, or what's left - do not claim the task is done if this section
 describes something the "Done when" condition actually requires]

Desktop Quality Gate (Desktop/native-host tasks only):
[the five PASS/FAIL answers from Engineering Philosophy, with the boundary
test and published-app observation]
```

---

## Notes

- This protocol is per-task, not per-session — a phase gets one assignment per task from its
  spoke's task list, not one assignment for the whole spoke.
- The templates are copy/paste-shaped for a human relaying between two chat surfaces; nothing here
  assumes Claude and Codex are wired together programmatically.
- **Always output the filled-in template inside a fenced code block**, not as plain prose — that's
  what makes it a single clean copy/paste between desktop apps. Codex in particular tends to forget
  this; if a task assignment or completion summary shows up unfenced, re-send it fenced rather than
  relaying it as-is.
- Scope: this doc governs Claude/Codex handoff mechanics only. General design-before-implementation
  policy lives in [AGENTS.md → Design first](../../AGENTS.md#design-first); general response-shape
  conventions (independent of who's implementing) live in
  [collaboration-style.md](collaboration-style.md).
