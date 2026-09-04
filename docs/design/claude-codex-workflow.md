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
  completion summary Codex can review. Every reply intended for human relay uses the mandatory
  relay shape below.

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

### Mandatory relay shape (Codex → Claude, then Claude → operator → Codex)

Every relay prompt from Codex begins with this instruction, before the task-specific content:

```
RELAY FORMAT — MANDATORY

This message is from Codex and your reply will be copied by a human back to Codex.
Reply with exactly one fenced code block. Inside that block, put your complete response in a
simple ASCII text box. Do not put prose, a Markdown table, or any other content outside that box.

Use this shape (replace the content, keep the box):
+----------------------------------------------------------------------------+
| [YOUR RESPONSE]                                                            |
+----------------------------------------------------------------------------+
```

Codex includes it on assignments, plan approvals, revision requests, corrections, review requests,
and completion-summary requests. Claude follows it for every corresponding reply. If the reply is
not in that shape, Codex asks Claude to re-send it in that shape; the operator never has to manually
reformat a response.

---

## Task assignment template (Codex → Claude)

Copy/paste and fill in the bracketed parts. Point at docs rather than restating their content, so
there is exactly one place each decision can drift out of date.

```
RELAY FORMAT — MANDATORY

This message is from Codex and your reply will be copied by a human back to Codex.
Reply with exactly one fenced code block. Inside that block, put your complete response in a
simple ASCII text box. Do not put prose, a Markdown table, or any other content outside that box.

Use this shape (replace the content, keep the box):
+----------------------------------------------------------------------------+
| [YOUR RESPONSE]                                                            |
+----------------------------------------------------------------------------+

TASK ASSIGNMENT

Role: implementer. Do not write or modify any code until I approve your plan.

Read first (do not summarize these back to me):
- AGENTS.md
- docs/plan.md
- docs/design/default-path-acceptance.md
- [docs/phases/phase-N.M-<slug>.md]
- [docs/design/<relevant>.md]
- For every user-visible Desktop or ForgeUI change: also
  `docs/design/desktop-interaction-principles.md` and
  `docs/design/ui-design-system.md`.

Task:
[1-3 sentences: what to build, scoped to one task from the spoke's task list]

Scope card:
- Tangible user-visible/process outcome:
- Expected files and tests:
- Dependencies and explicitly deferred work:
- Why this is the smallest task that delivers the outcome:

Done when:
[the "Done when" condition from the spoke, or "see spoke doc, task N"]

Default-path acceptance (every task):
- Default facts: [published artifact, relevant overrides explicitly absent, normal dependency
  route, and starting-state fact from default-path-acceptance.md; or N/A for documentation-only
  work]
- End-to-end action and named success/failure observation:
- Controlled/stubbed tests, if any, and why they are not acceptance evidence:

Shared-action ownership (when the task adds or changes a user action):
[for each action: shared contract, outcome/failure semantics, authorization
 owner, and why Presentation contains no business rule]

UI task contract (every user-visible Desktop or ForgeUI change):
- Visual disposition: binding reference(s), viewport(s), required states, and
  each reference element classified as owned, deferred, blocked, or omitted.
  A no-visual-change task records an explicit N/A or existing-renderer reuse rationale.
- Theme and accessibility: selected theme/selector; semantic-token mapping with
  light/dark values and provenance; foreground/background contrast pairs for
  every state.
- Evidence plan: browser-first four-corner viewport matrix, continuous resize,
  long-content and zoom/text-scaling checks, text-fit checks, then packaged-app
  parity last. Name the evidence paths to be produced.

Precondition test matrix (every stateful action):
[each named precondition, its positive test, its negative test, and the named
 expected result]

Revision record (every revision after the first):
[changed decisions, files, and evidence since the preceding version; the prior
 artifact title/version. If none changed, say so rather than resending it.]

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

Precondition test matrix:
[named positive and negative result for every precondition in the approved plan,
 or N/A with a reason]

Default-path acceptance (every task):
[published artifact and defaults actually used; normal dependency/version; action and named
result; PASS/FAIL. For documentation-only work, N/A. List every override/test double separately
and confirm it is not cited as acceptance evidence.]

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

UI visual acceptance (user-visible Desktop/ForgeUI tasks only):
[browser-first evidence paths and PASS/FAIL for every approved state and
 viewport check; text-fit result; packaged-app parity result. Do not claim
 completion or request operator acceptance until this is PASS.]
```

---

## Notes

- This protocol is per-task, not per-session — a phase gets one assignment per task from its
  spoke's task list, not one assignment for the whole spoke.
- The templates are copy/paste-shaped for a human relaying between two chat surfaces; nothing here
  assumes Claude and Codex are wired together programmatically.
- **Relay shape is a protocol, not a preference.** The mandatory relay instruction appears in every
  Codex → Claude prompt, including a short follow-up that approves a plan or requests a correction.
  It avoids responses such as Markdown tables that are hard for the operator to relay intact.
- **A revision is a delta, not a retransmission.** Lead every revised plan with its changed
  decisions, files, and evidence plus the prior artifact title/version. If that delta is empty,
  report that no revised artifact exists; do not consume a review turn with an identical relay.
- **Always output the filled-in template inside a fenced code block containing an ASCII text box,**
  not as plain prose — that is what makes it a single clean copy/paste between desktop apps. If a
  response is not in that shape, re-send it correctly rather than relaying it as-is.
- Scope: this doc governs Claude/Codex handoff mechanics only. General design-before-implementation
  policy lives in [AGENTS.md → Design first](../../AGENTS.md#design-first); general response-shape
  conventions (independent of who's implementing) live in
  [collaboration-style.md](collaboration-style.md).
