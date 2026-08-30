# Brainstorm: project Mission Control and Janus runs

**Status: interaction direction captured 2026-08-30.** This is product-design input to
[Phase 43.4 — IDE trace surface](../../phases/phase-43.4-ide-trace-surface.md), not an
implementation handoff by itself. The phase spoke carries the requirements an implementer must
build against.

## The model

A user works in a **Project**: the durable, goal-oriented workspace analogous to a Visual Studio
project or JetBrains solution. It has one enduring **Mission Control** conversation, its mission
assets, context, and a history of work.

```
Forge workspace
└── Project — e.g. Todos API, Golang API, TypeScript API
    ├── Mission Control — the long-lived human ↔ Forge REPL
    ├── Mission assets — mission.mcl, expert prompts, profiles, context, checks
    └── Runs — named executions launched from the project
        └── Janus trace — Proposer → Approver → Implementer
```

This names the layers deliberately. In MCL, a `mission` is an executable definition; **Janus** is
a reusable mission/team definition, not a project or a conversation name. A **run** is a specific
execution of that definition. The user-facing project and run titles describe the work, such as
`Golang API` and `Implement Todos API`.

Related work becomes a later run in the same project. A distinct objective with its own repository,
context, or lifecycle becomes a new project. If the Go API and TypeScript API are parts of one
product with shared context and lifecycle, they can instead be workstreams inside one project.

## The chronological experience

### 1. Choose a project

Forge opens on Projects. The user selects an existing project—such as `Golang API` or
`TypeScript API`—or creates a new one. The activity rail is persistent; its flyouts are manually
collapsible, like VS Code.

![Step 1 — choose Golang API or TypeScript API from the Projects flyout](../images/mission-project-flow-01-choose-project.png)

### 2. Create a project from a goal

The user can begin with a natural-language goal, for example: “Build a mock Todos API.” They name
the project and optionally attach a repository, product documents, or an API specification. This
creates the durable project boundary; it does not silently start an implementation run.

![Step 2 — create a Todos API project from a goal and initial context](../images/mission-project-flow-02-create-project.png)

### 3. Refine the work in project Mission Control

Every project has its own Mission Control: a long-lived, REPL-like conversation for refining the
brief, constraints, and intended outcome. It is the default human-facing chat and is deliberately
not polluted by the full agent-to-agent transcript.

![Step 3 — Mission Control refines the Todos API brief with the project context in view](../images/mission-project-flow-03-mission-control.png)

### 4. Shape the mission assets

Mission Control helps create and evolve the project’s reusable assets: `mission.mcl`, expert
instructions, profiles, context sources, and outcome checks. This is a first-class workbench
surface, not hidden configuration. The definition stays editable before the user launches work.

![Step 4 — Mission Workshop makes Janus roles, checks, and context explicit](../images/mission-project-flow-04-mission-workshop.png)

### 5. Launch a named Janus run

When the project context is ready, the user launches a run with a meaningful title such as
`Implement Todos API`. Forge records the run’s project context and pins the mission-definition
version used for it. Later edits affect future runs, never rewrite the history or meaning of an
existing one.

![Step 5 — launch a named Janus run with its definition version and approved context pinned](../images/mission-project-flow-05-launch-run.png)

### 6. Inspect and steer the Janus trace

The run opens as an inspectable Forge Trace: its own surface, separate from Mission Control. A
Runs flyout lists multiple named executions—shown here with `Golang API — v1 endpoints` and
`Todos API — Implement Todos API` visible together. The selected trace shows the real
Proposer → Approver → Implementer exchange, state, artifacts, and a genuine safe-boundary guidance
control. It also shows three deliberately different intervention paths: **Add guidance** for the
next safe boundary, **Pause after step** to finish the active bounded action before stopping, and
the prominent red **Stop run** break-glass control for drift or an unsafe direction. Selecting a
run never hides, merges, or replaces the other run records.

![Step 6 — a named Runs flyout and selected Janus trace with safe-boundary guidance](../images/mission-project-flow-06-run-trace.png)

### 7. Return the outcome to Mission Control

On completion, the run returns a concise outcome card to the project: artifacts, verification
evidence, and a summary. Mission Control becomes the place to understand the result, refine the
project, and launch the next related run—rather than making the user reconstruct the project state
from a raw transcript.

![Step 7 — completed run outcome returns to Mission Control and suggests the next run](../images/mission-project-flow-07-outcome.png)

## Workbench behaviour implied by the flow

1. **Mission Control is project-scoped.** Opening `Golang API` and `TypeScript API` opens
   different durable control conversations and context, rather than two tabs over one global chat.
2. **Run traces are inspectable documents.** They are dockable alongside source, diffs, artifacts,
   and mission-definition views; they are not generic chat rooms.
3. **The activity rail provides navigation, not a permanent second transcript.** At ordinary
   desktop width, the Projects or Runs flyout is visible by default when its rail icon is selected.
   It can be manually collapsed exactly like VS Code’s primary sidebar, while the activity rail
   remains available to restore it. On constrained widths, collapse the inspector before the
   active flyout; hide it automatically only on genuinely narrow/mobile layouts.
4. **Durability remains server-owned.** The workbench discovers projects, conversations, and runs
   through projections of canonical durable state. It must not maintain a second UI-owned
   transcript store.
5. **A run has an immutable launch snapshot.** Its project context and mission-definition version
   are recorded when it starts, so its trace and outcomes remain reproducible and intelligible.
6. **Stopping is real, visible, and auditable.** `Stop run` remains adjacent to the Live status,
   not inside an overflow menu. It immediately blocks queued/future turns and requests cancellation
   of the active provider or tool operation. Forge shows `Stopping…` until the runner reaches a
   terminal boundary; it never claims work was undone when an external tool could not be cancelled.
   The trace preserves the stop request, partial artifacts, and terminal `Stopped by user` outcome.
   Continuing work requires an explicit new run or a deliberate resume from a recorded safe
   checkpoint—never an invisible continuation.

## Product and runtime fit

The existing durable Conversation service provides the right lower-level identity boundary: a
`ConversationId` owns an ordered event transcript and can contain more than one run. The proposed
Project layer is the user-facing owner of that control conversation, mission assets, and related
runs; it is later work beyond the initial Janus proof. The project and run lists must be queries or
projections over canonical durable state—not parallel browser-side histories.
