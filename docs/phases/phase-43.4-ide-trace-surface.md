# Phase 43.4 — IDE trace surface (`forge trace`)

**Status: Design — later workbench iteration, not the initial durable Janus conversation proof.**
Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). The initial observational Janus
group-chat renderer and durable event contract are [43.16](phase-43.16-janus-desktop-local-poc.md);
this spoke builds the richer workbench as another projection of that same conversation after the
proof. It depends on [43.3](phase-43.3-mission-attach-point.md)'s completed attach/switch
foundation, **not** its deferred sdlc-agent catalog work. Promoted from
[docs/brainstorm/forge-trace-ide-surface.md](../brainstorm/forge-trace-ide-surface.md) (design
conversation, 2026-07-20); that doc is now a stub pointing here.

**This spoke is explicitly iterative, not a fixed task list.** The exit criterion is "arrived at a
workbench layout that feels right," reached by building real Avalonia mockups and iterating — not
by checking off a predetermined feature set. Treat the sections below as the design baseline to
iterate *from*, not a spec to build *to* on the first pass.

## The problem this solves

Design/review/approval between a human and multiple AI collaborators (e.g. one mission role as
implementer, another as reviewer/architect) is currently conducted by copy-pasting text between
chat surfaces. Even inside one tool, a chat textbox has no concept of anchoring, state, or
suspension — it can't trace a multi-turn design discussion, inspect a diff next to the argument
about it, or gate an irreversible step on a human decision.

## The core idea: a debugger, not a chat log

`forge trace` is an IDE surface for watching/steering a running mission, anchored to its actual
artifacts, not a bespoke chat schema:

- **Outline** = the mission's own pipeline steps (`Architect`, `CriticalReviewer`, `Synthesiser`,
  `QualityJudge`, ...), derived from the `.mcl` source itself.
- **Thread** = a rendering of consecutive `StepEnvelope`s
  ([StepEnvelope.cs](../../src/ForgeMission.Core/Runtime/StepEnvelope.cs)) as the pipeline actually
  ran, anchored to the file/line a step's output touched.
- **Gate** = a human step where the pipeline genuinely suspends (see
  [43.5](phase-43.5-human-in-the-loop.md)), not a UI illusion.
- **Code pane** = whatever file/diff the current step touched, with inline comment markers tied to
  specific turns.

### The debugger framing

| Debugger concept | Maps to |
|---|---|
| Call stack / step list | The outline — the pipeline's steps |
| Execution trace | The thread pane — consecutive `StepEnvelope`s |
| Breakpoint | A `kind: human` step — genuinely suspends (`Suspended` outcome, [43.5](phase-43.5-human-in-the-loop.md)) |
| Locals/watch window | The context bag (`feedback`, `mode`, ...) |
| Edit-and-continue | Human types feedback → resume seeded with it |
| Source view tied to the current frame | The code pane — whatever file the current step touched |

Grounding: [missions/sdlc-agent/mission.mcl](../../missions/sdlc-agent/mission.mcl)'s `DesignMode`
(`Architect -> CriticalReviewer -> Synthesiser -> QualityJudge`, `loop(2)`) is close to exactly the
propose/critique/revise/gate-check shape this surface visualizes — see
[sdlc-meta-mission.md](../design/sdlc-meta-mission.md) and
[interaction-modes.md](../design/interaction-modes.md) for the classifier-router pattern behind it.
**Nearer-term grounding: [43.15 — Janus](phase-43.15-janus-inter-agent-mission.md)** is the
deliberately minimal mission (`Proposer -> Approver -> Implementer`, multi-provider) built to give
this surface real content to render before the full `sdlc-agent` is in scope. Its initial group-chat
renderer is 43.16; start the richer workbench iteration against Janus after that proof, not against
`sdlc-agent`.

## Full solution access + trust gradient

A skeptical human, especially early on, will want raw file access — not just the diff scoped to
the active step. **Decision (2026-07-20, provisional, carried forward): full access model,
unscoped** — no permissions/visibility layer for now (YAGNI; revisit if a real need surfaces).

Two modes, both first-class, cheap to switch between:
- **Scoped/curated** — diff view tied to the active step, inline comment markers, gate card.
- **Raw/full** — plain source (no diff coloring, no comment markers, "read-only · browsing"), full
  file tree, unscoped. Still cross-links back to a relevant task if one exists, without forcing it.

## Dockable workbench, not a fixed 3-pane layout

Borrowed from Visual Studio's docking model: panel groups are **tabbed** (Solution Explorer /
Mission / Trace sharing one dock zone), **relocatable** (Trace can leave the sidebar and live as a
document-area tab), and **floatable** (a panel can be pulled loose to stay on top). The stage
indicator (Design → Review → Implement → Verify) lives as a toolbar dropdown, the same slot VS uses
for its Debug/AnyCPU config. This directly answers "where does each surface permanently live" —
Solution Explorer, Mission outline, and Trace are interchangeable surfaces a person docks, tabs, or
floats depending on the task at hand, not fixed panes.

### Multiple durable mission conversations — required navigation behaviour

**Decision (2026-08-30):** The outer user-facing unit is a **Project**, analogous to a Visual
Studio project or JetBrains solution. A Project owns one durable **Mission Control** conversation,
its mission assets/context, and its named runs. Janus is a reusable mission/team definition—not a
project or conversation name—and a run is one execution of it. For example, `Golang API` and
`TypeScript API` are separate Projects when they have distinct purpose, membership, or lifecycle;
each Project has its own Mission Control and its own runs. The Project is defined by a Forge-owned
manifest of metadata and references to relevant files/directories, not by a Git repository. It may
span zero, one, or many repositories; Git is attached context, never the identity boundary.
The manifest is a local project file, analogous to a `.csproj` or IntelliJ project configuration,
not a hosted shared workspace with membership or Project-level roles. The baseline actor is the
local Forge user; any sensitive external action is governed by its explicit capability/approval
policy and local credentials, not by a Project-owner/contributor access model. Version control can
share the manifest and its assets normally; hosted real-time collaboration is a separate future
product decision.
Related work becomes a later run in the same Project. A Go API and TypeScript API that genuinely
share one product context and lifecycle may instead be workstreams in one Project.

Each run records an immutable launch snapshot: the project context and mission-definition version
used for that execution. This prevents later changes to `mission.mcl`, prompts, profiles, or
project context from rewriting the meaning of an earlier trace. On completion a run returns a
concise outcome—artifacts, verification evidence, and summary—to its Project Mission Control;
Mission Control remains the human-facing REPL, rather than becoming the full agent transcript.

**Lightweight context provenance (decision, 2026-08-30):** A launch snapshot records only values
available without meaningful extra work: selected paths/directories, a Git revision when a
referenced repository has one, and an identifier/content hash for an explicitly attached file or
generated artifact when already available. It must not crawl or hash an entire workspace merely to
launch. Credentials, secret values, and secret-derived material are never recorded in the snapshot
or trace.

**Break-glass stop (target; runtime deferred):** The trace mock establishes a visible red **Stop
run** control adjacent to `Live`, not hidden behind an overflow menu and not conflated with a human
gate. The underlying durable control feature is explicitly out of scope for this UI exercise and
is the next backlog item after it. When selected, it must prevent queued/future turns, request
cancellation of the active provider/tool operation, enter `Stopping…`, and durably record the
request, observed result, and known partial effects. It must never imply rollback of non-atomic
external effects. **Pause after step** and **Add guidance** remain separate, non-emergency
intervention paths.

**Run control vs. mission workflow (decision, 2026-08-30):** Do not hardcode Janus stages, role
names, step count, order, or transitions into the runner or workbench. The future platform
run-control lifecycle supplies stable scheduling/safety semantics; separately, the versioned
mission definition supplies arbitrary named stages, experts, steps, gates, and ordering. A Project
can rename, add, remove, or reorder its workflow for future runs. Every launched run pins that
definition snapshot, so the trace renders the workflow it actually executed even after later edits.
The UI shows both run-control state and the current definition-driven stage; neither layer
substitutes for the other.

**Recovery (decision, 2026-08-30):** `Stopped by user`, `Failed`, and `Interrupted` runs remain
immutable, append-only history. Recovery creates a new, separately named run linked to its prior
run; no uncertain in-flight work is replayed automatically. A future checkpoint feature may offer
an explicit resume only from a recorded, verified safe boundary.

**Current governance scope — composed wrapper (decision, 2026-08-30):** MCL provides named
mission composition, not inheritance syntax. The UI exercise therefore uses the conceptual entry
mission `GovernedJanus = PlatformPreflight -> Janus -> PlatformFinalise`. It gives reusable,
visible, versioned homes for security/policy checks and reporting, without hardcoding them into the
workbench. Composition alone cannot enforce a hard budget or cancel an in-flight host operation;
the durable runtime enforcement layer is deferred to the next backlog item after this UI exercise.

The vertical activity rail must expose a **Conversations** (or **Missions**) view. At normal
Desktop/workbench width it opens a named, Rooms-like list of durable records beside the selected
Forge Trace. The exact list is Project-scoped where appropriate: it may show that Project's named
runs, while the Project switcher shows `Golang API` and `TypeScript API`. Selecting a row changes
the active trace; it never hides or merges the other rows. The list is visible by default but
manually collapsible exactly like VS Code's primary sidebar; the activity rail remains visible to
restore it. When width is constrained, collapse the inspector before the conversation list; hide
the list automatically only on genuinely narrow/mobile layouts. The selected trace remains a
dockable document surface next to source/diff/artifact tabs — it is not a generic chat-room
rendering.

The underlying Project, conversation, and run lists are service queries/projections over canonical
durable state, never UI-owned second transcripts. See the [project Mission Control and Janus runs
brainstorm artifact](../brainstorm/mission-conversations/README.md) and its seven-step visual
reference.

## Relationship to `human-in-the-loop` (43.5)

[43.5](phase-43.5-human-in-the-loop.md) is the mechanical spec this depends on: `kind: human`
reusing existing roles (no new `role:` needed — `role: judge` fail/pass + prose-output already
cover it), a `channel:` field resolved like `provider:` is in
[`ProviderClientBuilder.BuildChatClient`](../../src/ForgeMission.Cli/ProviderClientBuilder.cs), and
suspend/resume via a `Suspended` `StepEnvelope` outcome. `forge trace` is naturally a **new
channel**, not a competing product — it gets resume-token/webhook plumbing for free and only owns
rendering.

Confirmed mechanism: a human's "Request changes" writes to `context["feedback"]`, the same slot
`role: judge` failures already use to drive `loop(N)` retries
([PipelineRunner.cs:71,189-190](../../src/ForgeMission.Core/Runtime/PipelineRunner.cs)). No new
protocol needed for the human case.

**Open tension, not yet resolved:** `ForgeMission.Rooms` (`MemberKind.Human`/`Agent`,
[MemberKind.cs](../../src/ForgeMission.Rooms/MemberKind.cs), `Room`, `Message`) already exists,
live, with real server-side state — the "easy channel." But its chat-room rendering was explicitly
ruled out as the wrong UX for this. The reusable part is suspend/resume + the channel abstraction,
not the Rooms chat surface itself — `forge trace` is a sibling rendering, deliberately not the room
view.

## Cross-platform exploration already done (validated the concept, not the final tech)

Static HTML/CSS mockups (not working code) were built and rendered to PNG to validate the concept
visually before committing to a platform — superseded at the time by the since-shelved Avalonia
decision (see [Phase 43 hub](phase-43-forge-desktop.md)), and now, after the 2026-07-27 pivot back
to a web-rendered client (Electron/Blazor Server — see
[forge-desktop-client-runtime.md](../design/forge-desktop-client-runtime.md)), closer in spirit to
the actual build tech again. Kept here as visual reference for the iteration loop, not as working
code:

- **Web-console concept** — outline/BRD · code diff · anchored thread, collapsible panes borrowed
  from VS/Xcode auto-hide. ![Web-console concept](../brainstorm/images/web-console-concept.png)
- **Native macOS concept** — traffic-light chrome, translucent sidebar, system-blue accents.
  ![macOS concept](../brainstorm/images/macos-concept.png)
- **Native WinUI/Fluent concept** — Mica chrome, Segoe UI Variable, Cascadia Code, InfoBar-style
  gate. ![WinUI concept](../brainstorm/images/winui-concept.png)
- **Request-changes flow** — human-composes-feedback → turn-lands-on-the-trace sequence.
  ![Request-changes flow](../brainstorm/images/request-changes-flow.png)
- **WinUI solution explorer** — the raw/full file-browsing mode, matched to this repo's actual
  `src/` structure. ![WinUI solution explorer](../brainstorm/images/winui-solution-explorer.png)
- **VS-style dockable workbench** — tabbed/relocatable/floatable panel groups.
  ![Dockable workbench](../brainstorm/images/dockable-workbench.png)

A real SwiftUI source file was also sketched and typechecked clean against the macOS 14 SDK — not
wired into a project, not verified to run, and now moot given the Avalonia decision; kept only as
a historical note, not a starting point.

## Iteration approach for this spoke

1. Build a rough mockup of the dockable workbench (outline + thread + code pane, no gate logic yet)
   inside the current [43.11 — Blazor WASM + Photino shell](phase-43.11-wasm-photino-shell.md).
2. Run a real Janus mission session through it, evaluate against the debugger-concept table above —
   does each row actually map cleanly in the built UI, or does the analogy break somewhere real
   usage reveals?
3. Iterate layout/interaction based on what breaks. Repeat until the workbench survives a real
   multi-round Janus negotiation without the human reaching for raw source out of frustration
   (a rough, honest signal — not a number, but a real bar).
4. Only then layer in the Gate (needs [43.5](phase-43.5-human-in-the-loop.md)) and full/scoped
   toggle.

## Open questions / not yet decided

- Whether `kind: human` needs config beyond `channel:` for trace's anchor metadata (file/line, not
  just a channel-rendered prompt) — sharper here than in 43.5 because trace's anchoring is more
  structured than a Slack/email prompt.
- Whether `loop(N)` retries render as repeated history on the trace or collapse.
- Whether the Rooms domain model (`Member`/`Message` types) is reused as trace's persistence layer
  or trace gets its own — leaning toward reuse of suspend/resume + channel abstraction only.
- No fixed timeline — this spoke ends when iteration converges, not on a calendar date.
