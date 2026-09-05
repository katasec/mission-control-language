# Phase 43.21 — Mission-run-first Project invocation

> **Status: Task 1 implementation review passed (2026-09-04); Task 2 implemented (2026-09-05) and
> awaiting review.** The combined
> replacement remains unmerged and has no default-path acceptance yet. This replaces the
> user-facing execution model of 43.20 Tasks 2 and 4 without invalidating the durable Conversation
> substrate or Project Explorer work.

## Outcome

A Project is durable local context. Every instruction a person submits invokes one selected, named **Mission Run** beneath that Project.

```text
Project → selected mission → Mission Run → experts → provider(s) → durable trace/outcome
```

The first product catalog is closed and contains exactly two built-in missions:

- `Janus` — the default; its multi-expert mission asset owns its composition.
- `Naive` — a one-expert mission asset; it deliberately performs direct, single-expert reasoning.

There is no `Default` choice, no model picker, no compact-picker description, and no exposed `MissionControl` mission. `Naive` is explicit, never an unnamed fallback. Repository demos and test fixtures are not product choices.

## Locked product contract

### One invocation shape

Selecting a mission is Project-scoped and persistent. Submitting the composer starts one child Mission Run of that selected mission. Presentation never branches execution:

| Selection | Child run | Meaning |
|---|---|---|
| `Janus` | `Janus` mission run | Execute Janus’s Proposer → Approver → Implementer workflow. |
| `Naive` | `Naive` mission run | Execute one zero-tool Controller expert, with no mission-level review path. |

This is a semantic child process, not a requirement to spawn an OS process per invocation. The durable Host/Worker queue remains the execution topology. Both choices must produce the same durable run identity and event stream.

The Project may retain a ConversationHost container only to order and replay its runs. That container executes no provider request and is never shown or named as a mission. A run is the sole user-visible unit that invokes reasoning. A Project Mission container pins **no** `MissionRef` and no capabilities: its snapshot reports a null mission reference, while each child command carries its own allow-listed mission and per-run capability declaration. `ProjectMission` purpose plus a non-null Project ID is its existence invariant; an empty mission string is never used as an ambiguous sentinel.

This MVP permits **one active Mission Run per Project**. A second submit while a child run is queued, running, or awaiting a tool returns the typed `RunAlreadyActive` result and creates no event. Queuing or parallel Project runs is deliberately deferred; it needs a separate run-addressing and UI design.

### Contract and ownership

Presentation owns selection rendering, text entry, busy/error display, and navigation. It talks only to Client Runtime.

| Action | Surface-neutral Client Runtime contract | Owner and rule |
|---|---|---|
| Read | `GetProjectMissionsRequest { sessionId }` → `{ missions { missions[], selected?, hasLegacyHistory }, error? }` | Client Runtime returns the ordered closed catalog `[Janus, Naive]`, the persisted selection, and whether this Project retains legacy history. A surface renders a picker without holding a second copy of the catalog and learns the persisted selection on open rather than assuming one. See the corrupted-selection rule below. |
| Select | `SelectProjectMissionRequest { sessionId, mission }` → `{ selectedMission }` | Client Runtime allow-lists only `Janus` and `Naive`, atomically persists `ProjectManifest.SelectedMission`, and returns the canonical value. Presentation never supplies a path, digest, provider, or expert. |
| Invoke | `StartProjectMissionRunRequest { sessionId, commandId, input }` → `{ runId, acceptedSequence, status }` | Client Runtime derives the persisted mission, Project goal, and parent durable container. `commandId` is generated once at press and reused only for retry. |
| Execute | Host-internal `StartProjectMissionRun { containerId, commandId, mission, projectGoal, input }` | ConversationHost creates/idempotently returns the child run and orders events. It declares zero capabilities for this MVP. Worker resolves the named mission and executes it. Neither accepts an arbitrary provider/model. |

`input` is required bounded user text, distinct from the Project goal. Equal retries return the original run; changed mission or input under the same `commandId` is `Conflict`. Unknown/replaced sessions, unknown mission, blank/oversized input, terminal parent, and undeclared capability/context return typed errors and create no run.

**Corrupted selection — locked.** A manifest whose `selectedMission` is not an allowed built-in mission is the one case where the read returns **both** payloads: `missions[]` is still the ordered `[Janus, Naive]`, `hasLegacyHistory` is still the real flag, `selected` is null, and `error` is `UnknownMission`. This is a deliberate exception to the one-payload-per-response convention every other Project contract follows, and it is the point of the design: if the catalog arrived only on success, the surface would have no way to offer the repair. The selection is never silently substituted with `Janus`. Presentation then shows the typed error, renders no invented selection, keeps the picker usable and keyboard-operable, and disables the run action until an ordinary `SelectProjectMissionRequest` repairs the manifest.

The Worker owns the built-in catalog and resolves only `Janus` and `Naive`. `Naive` is the renamed checked-in zero-tool asset, `mission Naive(projectGoal, task) = { Controller }`; its executor rejects every tool request. Janus remains its existing checked-in mission and provider routing. No UI, Client Runtime, or Host component chooses an expert or provider.

**Capability baseline — locked.** Starting a Project Mission Run grants **no local tool authority**. In this MVP, Client Runtime declares zero capabilities to both Janus and Naive; attached assets/context are descriptive evidence, not execution permission. A later explicit capability-selection and authorization task may introduce a Project-scoped declaration, but it must name the selectable scope, approval, and failure semantics first. Until then, an unexpected tool request is reported as refused and never reaches a local executor. This prevents a default Janus run from probing the machine merely because a Project was opened.

### Migration and removal

`MissionControl` / `ProjectControl` is a legacy compatibility route, not a third mission. Neither new Project nor Presentation surface may use it.

1. Manifest v2 replaces `missionControlConversationId` with `projectMissionContainerId` and adds `legacyProjectControlConversationId`; `selectedMission` remains and defaults to built-in `Janus`.
2. Reading v1 moves its old control-conversation ID into `legacyProjectControlConversationId`. The field remains as a read-only durable-history pointer after legacy-route deletion. Client Runtime creates one new Project Mission container deterministically for later runs; it neither replays old control messages as a current mission nor converts them into runs.
3. After Desktop and TUI use the new contracts, remove the ProjectControl endpoints, Client Runtime session, fixed `MissionControl` Worker resolver/executor, participant label, and user-facing strings in one deletion task. No dual user-facing path or compatibility picker is permitted.

## UI contract

Retain the Task 3 rail structure. Its three entries read exactly **Project Explorer**, **Missions**, and **Settings**, with Settings bottom-aligned as Task 3 fixed it.

**Rail reference ownership.** Task 2 owns the rail's *labels*; its own frames below are the binding reference for them. The 43.20 Task 3 frames remain binding for everything else they show — the Explorer list body, the opened document, and Settings — and their older `Explorer` / `Mission Control` rail text is superseded here rather than left to disagree. At the rail's tokenized lower bound (`--wb-rail-width`, 7.25rem) `Project Explorer` wraps to two lines exactly as `Mission Control` did; wrapping is accepted, clipping and truncation are not, and the fit is a measured browser observation rather than an assumption.

The Missions page has one compact picker associated with the mission-input composer:

```text
Mission: Janus ▼
  Janus
  Naive
```

Janus is visibly selected on a new Project. The popup contains only those two names: no descriptions, model names, expert names, or “Default” row. The accessible label is `Mission`. The primary action is **Run**; it reports `Starting Janus…` or `Starting Naive…`, then renders only that run’s durable activity. It never calls a generic chat/control endpoint or labels a response “Forge”.

Required states: first open with Janus selected; picker open; Naive selected; invalid input; accepted/busy; Janus participant activity; Naive output; typed start failure; selection persistence after reopen; and the retained-history notice when a migrated Project has legacy history. That notice is static text with **no link and no action** — it is never reopened as a current mission. The Task 3 Explorer/Settings references remain binding for their owned slices. Use Workbench tokens and repeat four-corner, continuous-resize, long-text, 125/150/200% zoom, both-mode, and packaged parity checks.

## Architecture and engineering gates

| Gate | Result |
|---|---|
| Product behaviour | PASS — selecting and invoking a named mission creates one observable child run. |
| Ownership | PASS — Presentation renders; Client Runtime persists selection/derives local facts; Host owns run/events; Worker executes; mission assets own expert/provider composition. |
| Replacement boundary | PASS — no Desktop Host ownership, provider credential, or direct datastore access changes. TUI uses the same contracts. |
| Security architecture | PASS — no new public ingress, identity, datastore ownership, or provider-secret route. Selection is allow-listed below Presentation; paths stay below Client Runtime. |
| Engineering philosophy | PASS — one invocation contract replaces adjacent user paths; selection has one persistent owner; legacy compatibility has one removal task. |
| Default path | Applies — zero-argument published Desktop, normal local ConversationHost/Worker, and a disposable new Project visibly start default Janus and produce durable activity. |

### Default-path sequencing exception

The sanctioned local Kind build deliberately accepts only a clean `main` checkout. A branch-built Host or Worker image is therefore a controlled test and cannot close default-path acceptance. The correction has one bounded sequencing exception:

1. Implement and review Task 1 and Task 2 on one descendant branch of this design branch; run their full suite and controlled component/Host tests there.
2. Open one replacement PR that includes the reusable Task 3 Explorer work plus both new tasks. Do **not** merge PR #94 separately.
3. Merge only after code review and all branch-verifiable checks pass. Then rebuild Host/Worker from clean `main` with the sanctioned Kind target and run the zero-argument packaged Desktop through default Janus and explicit Naive invocation.
4. If that observation fails, the correction is not release-ready or complete; repair through a new PR before release. This exception expires when that observation is recorded. It is not a permanent alternate route.

This is a Type-2 operational exception only for the pre-merge image provenance constraint. It changes no production endpoint, credential, or product default.

## Tasks

### Task 1 — Universal durable Project Mission Run — implementation review passed

Add manifest v2 migration, Project Mission-container creation, selection persistence, and the two contracts above. Make `ConversationSnapshot.MissionRef` nullable so a Project Mission container reports no mission; existing Mission Run snapshots remain non-null. Declare zero capabilities for every Project Mission Run in this MVP. Generalize Host/Worker dispatch so Janus and Naive both create ordinary runs with durable `run_id` events. Retain the legacy route only as temporarily unreachable-from-new-contract compatibility until Task 3; Task 2 removes the current Presentation caller. Implement Task 1 and Task 2 on one branch and submit them as the replacement PR defined above.

**Done when:** contract/migration/idempotency tests prove either allow-listed selection creates exactly one identically-shaped child run; invalid selection/input and changed retries produce typed failures with no run; Worker rejects every mission outside the closed catalog; both Project missions receive zero capability declarations and an unexpected tool request is refused without local dispatch; and the full suite passes.

### Task 2 — Mission-first Desktop and TUI surface

> **Corrective status — 2026-09-05: not approved for merge or release.** The Task 2 frames and
> current implementation were reviewed only against their task-local references and did not close
> reference traversal into [Phase 43.4](phase-43.4-ide-trace-surface.md) and the
> [Project Mission Control / Janus-runs artifact](../brainstorm/mission-conversations/README.md).
> Those sources establish that the enduring human ↔ Forge conversation receives concise run
> outcomes, while the chronological expert dialogue belongs in a selected, inspectable Run Trace.
> A corrective design must record the required reference-closure table before any further UI
> implementation is accepted. The current inline expert transcript is not the approved product
> experience; no PR or release gate may treat it as such.

Replace Project Control with Missions and its exact two-name picker. Task 2’s contract set is Task 1’s `SelectProjectMissionRequest` and `StartProjectMissionRunRequest` **plus** the `GetProjectMissionsRequest` read defined above; it uses no other Client Runtime contract, and in particular no control-conversation route. Rename the rail entry, remove the legacy wording, and show live durable activity for the selected run.

On first open, Missions is the active Project destination and Janus is visibly selected. A submit creates one run through `StartProjectMissionRunRequest`; Presentation records only the returned in-memory `runId` to filter the existing durable tail to that one live run. It shows a clean composer again once the run becomes terminal. There is no generic Project conversation, model/expert picker, direct provider call, historical run browser, or synthetic chat transcript. Full reopened history and exact durable Trace remain later work. If a migrated Project has legacy Project Control history, show only truthful static text that it is retained; do not render a dead link or reopen it as a current mission.

#### Task 2 corrective implementation — separate control thread and Run Trace

**Scope:** preserve Task 1's mission-only invocation and its existing durable events; correct only
their Presentation ownership. This is deliberately not the deferred rich workbench: no historical
run browser, replay after reopening, dock rearrangement, source/artifact panes, stop control, or
new Host/Worker/Client Runtime contract is added here.

| Upstream reference | Behaviour it establishes | Disposition here |
|---|---|---|
| [Mission conversations](../brainstorm/mission-conversations/README.md#inspect-and-steer-the-janus-trace) | The human-facing Project conversation lists named runs; choosing a run opens its trace. | **Binding** for control-thread versus trace ownership. |
| [Expanded Janus trace](../brainstorm/images/mission-project-flow-06-run-trace.png) | Expert turns are read chronologically in a selected Run Trace, not in the human conversation. | **Binding** for the trace's information ownership; current Workbench theme/tokens remain authoritative. |
| [Phase 43.4](phase-43.4-ide-trace-surface.md#expert-dialogue-is-first-class-trace-evidence) | Project conversation receives concise run outcomes; exact expert dialogue is trace evidence. | **Binding** for behaviour. Its dockable/history features remain **deferred**. |
| Task 2 frames in `docs/images/phase-43.21/` | Rail, picker, composer, tokens, error handling, and responsive bounds. | **Owned** except the inline transcript portion, which is **superseded** by this correction. |
| 43.20 Task 3 references | Explorer, Project document, and Settings slices. | **Deferred/unchanged**; this task does not alter them. |

**Interaction contract.** `Missions` remains the Project's human control thread and the only
composer/picker surface. It renders the person's submitted instruction and an honest, concise
durable run outcome (mission name plus lifecycle status); it never renders a Proposer, Approver,
Implementer, Naive, approval, or tool event. Each accepted current-session run has one `View run
trace` action. Selecting it opens a `Run Trace` document state with a clear return to `Missions`.
That trace renders the existing exact, chronological durable expert events for that one run,
including live activity and approval/revision rows; it does not rewrite them into a summary. The
prompt itself remains owned by the control thread rather than being duplicated as an expert turn.

**Boundary.** The run already exists and is already identified by `StartProjectMissionRunResponse`;
the existing tail already carries its durable events. Presentation must split those facts into a
control-thread projection and a selected-run trace projection. It must make no direct provider,
expert, model, filesystem, or legacy Project-Control call. Because Task 1 does not yet retain
reopenable run metadata or a trace-read contract, a reopened Project truthfully has no selectable
prior trace in this correction; adding that durable navigation belongs to Phase 43.4.

**Required gates — PASS for this design.** Product behaviour: a person controls a Mission from a
human thread and inspects its experts in a separately opened trace. Owner: Presentation alone;
the Client Runtime's existing surface-neutral start/tail contracts and the Desktop Supervisor/Host
remain untouched. Adapter: no Photino/Host callback, process, credential, or lifecycle change is
proposed; the changed files must stay in Presentation and its tests. Replacement boundary: the
same WebView client can render both projections, while the Supervisor remains framework-free.
Proof: Presentation split tests plus the zero-argument packaged Desktop observation below.
Architecture/security: no bounded context, public entry point, datastore, transport, or credential
changes; Presentation consumes the same Client Runtime channel and cannot access the Conversation
store. This is a Presentation-only Type-2 rendering change. Presentation parity: `View run trace`
changes only local document state; the sole product action remains the existing surface-neutral
`StartProjectMissionRunRequest`, so no TUI capability diverges.

**Default/candidate evidence plan.** Default path remains the zero-argument published Desktop,
its supervised Client Runtime, and normal local Kind ConversationHost/Worker with no endpoint
override, using a dedicated disposable Project. Before PR approval, Desktop, Client Runtime, Host,
and Worker all run from one candidate revision: submit Janus and Naive in the actual Desktop,
open each current-session trace, and observe the distinct control outcome and exact trace events.
After merge, rebuild normal clean-main service provenance and repeat the zero-argument Desktop
action. Browser-first evidence covers 1536×1024 and 800×568, four-corner/continuous resize,
long expert messages, 125/150/200% zoom, both themes, then packaged parity at 800×568.

**Done when:** at the current-session default path, submitting Janus or Naive leaves `Missions`
free of expert turns, shows the submitted instruction and typed lifecycle outcome, and exposes one
named `View run trace` action. That action opens a separate Run Trace which shows the exact ordered
expert dialogue and live state for that run only; return restores the same Project control thread
and composer. Component tests prove the split, no legacy/control/provider/model/expert endpoint is
called, and browser-first responsive plus packaged candidate-stack and post-merge default-path
checks pass against the reference closure above.

#### Task 2 binding visual references — corrected 2026-09-05

Twenty-two frames under `docs/images/phase-43.21/`, named `task2-missions-{wide,compact}-<state>.svg`.
Wide is **1536×1024**, compact is **800×568** — the same acceptance rectangle 43.20 fixed, where
800×568 is the packaged Desktop’s measured usable viewport and a responsive baseline, never a
prescribed window size. The layout must be fluid and bounded across the whole 800–1536 × 568–1024
range and its continuous intermediate sizes; these frames define information priority and
structure, not fixed pixel geometry.

| State | Frames | What it binds |
|---|---|---|
| First open, Janus | [wide](../images/phase-43.21/task2-missions-wide-first-open-janus.svg) · [compact](../images/phase-43.21/task2-missions-compact-first-open-janus.svg) | Missions is the opened view, Janus visibly selected, empty control thread, run action disabled while the instruction is blank. |
| Picker open | [wide](../images/phase-43.21/task2-missions-wide-picker-open.svg) · [compact](../images/phase-43.21/task2-missions-compact-picker-open.svg) | Exactly two rows, current mission checked, active option focus-ringed, popup opens upward and never covers the composer. |
| Naive selected | [wide](../images/phase-43.21/task2-missions-wide-naive-selected.svg) · [compact](../images/phase-43.21/task2-missions-compact-naive-selected.svg) | The button renders the persisted canonical value; focus has returned to the button. |
| Invalid input | [wide](../images/phase-43.21/task2-missions-wide-invalid-input.svg) · [compact](../images/phase-43.21/task2-missions-compact-invalid-input.svg) | The typed Client Runtime failure beside the action that caused it; the instruction is kept. |
| Busy | [wide](../images/phase-43.21/task2-missions-wide-busy.svg) · [compact](../images/phase-43.21/task2-missions-compact-busy.svg) | Accepted: the instruction, a queued outcome, `Starting Janus…`, composer disabled. No trace is offered — the run has produced nothing to read. |
| Janus outcome | [wide](../images/phase-43.21/task2-missions-wide-janus-outcome.svg) · [compact](../images/phase-43.21/task2-missions-compact-janus-outcome.svg) | The control thread after a finished Janus run: instruction, concise outcome, durable counts including **0 tool calls**, and one `View run trace`. **No expert word appears here.** |
| Naive outcome | [wide](../images/phase-43.21/task2-missions-wide-naive-outcome.svg) · [compact](../images/phase-43.21/task2-missions-compact-naive-outcome.svg) | Naive reads identically — its single answer is an expert turn, so it lives in the trace. No exception. |
| Run Trace, live | [wide](../images/phase-43.21/task2-missions-wide-run-trace-live.svg) · [compact](../images/phase-43.21/task2-missions-compact-run-trace-live.svg) | A separate document: back action, run title, live state, the ordered expert turns and approval rows. No composer. |
| Run Trace, completed | [wide](../images/phase-43.21/task2-missions-wide-run-trace-completed.svg) · [compact](../images/phase-43.21/task2-missions-compact-run-trace-completed.svg) | The finished exchange, exactly as the durable events stored it — never summarised or shortened. |
| Start failure | [wide](../images/phase-43.21/task2-missions-wide-start-failure.svg) · [compact](../images/phase-43.21/task2-missions-compact-start-failure.svg) | A typed start failure with no run and no invented outcome. |
| Legacy notice | [wide](../images/phase-43.21/task2-missions-wide-legacy-notice.svg) · [compact](../images/phase-43.21/task2-missions-compact-legacy-notice.svg) | The retained-history line, static, above an otherwise ordinary empty control thread. |

The superseded `janus-activity` and `naive-result` frames are **deleted**, not left to disagree with
the correction: they bound an inline expert transcript on Missions, which is no longer where that
content belongs.

The frames are light-mode, exactly as Task 3’s were: colour is owned by the named Workbench theme’s
token map, which already defines both modes, so a second set of dark frames would duplicate values
the theme owns rather than add evidence. Dark mode is proved by browser inspection instead.

The corrupted-selection repair state has no frame. It differs from the first-open frame only in the
button label (`Mission: none selected`), the error row already bound by the start-failure frame, and
the disabled run action already bound by the first-open frame — it is specified below and covered by
endpoint and component tests rather than by a redundant nineteenth image.

#### Task 2 reference closure

Recorded per the [reference-closure gate](../design/desktop-interaction-principles.md#reference-closure--preserve-product-intent-across-spokes).
The first review of this task did not traverse these links, which is exactly how an inline expert
transcript reached Missions without anyone deciding it should.

| Source | Behaviour it establishes | Classification |
|---|---|---|
| [Mission conversations §6](../brainstorm/mission-conversations/README.md#6-inspect-and-steer-the-janus-trace) | The human-facing Project surface lists runs; selecting one opens its trace, which is not turned into a generic chat room. | **Binding** — control thread vs trace ownership. |
| [The expert conversation is the trace's primary evidence](../brainstorm/mission-conversations/README.md#the-expert-conversation-is-the-traces-primary-evidence) | Expert turns are read chronologically inside the selected trace, and a turn is the original durable message — never summarised, shortened, or substituted. | **Binding** — exactness, and where the turns live. |
| [Expanded Janus trace](../brainstorm/images/mission-project-flow-06-run-trace.png) | The trace's information: role, message, outcome, live state, chronological order. | **Binding** for that content. Its tabs, run inspector, stop-run, add-guidance, source/artifact panes and runs tree are **deferred** to 43.4 and absent rather than drawn. |
| [Phase 43.4 — expert dialogue is first-class trace evidence](phase-43.4-ide-trace-surface.md#expert-dialogue-is-first-class-trace-evidence) | The Project conversation receives concise run outcomes; the exact dialogue is trace evidence. | **Binding** for behaviour; its dockable/history features **deferred**. |
| Task 2 frames in `docs/images/phase-43.21/` | Rail, picker, composer, tokens, error handling, responsive bounds. | **Owned**. The inline-transcript frames are **superseded** and deleted. |
| [Project conversation / Run trace concept](../images/phase-43.21/task2-project-conversation-run-trace-concept.svg) | That the two concerns are separate, and what each holds. | **Binding for intent, not layout** — it draws a side-by-side pane, which is 43.4's deferred docking; this task opens the trace as a separate document instead. |
| 43.20 Task 3 references | Explorer, Project document, Settings. | **Unchanged** — untouched by this task. |

Two consequences recorded rather than left implied:

- **No Naive exception.** Naive's single answer is an expert turn, so it lives in the trace like any
  other. The control thread states `Naive run completed` and the counts; reading the answer is one
  action away. Exempting Naive would have put an expert's exact words back on the control surface.
- **No duration.** The only available source is the spread between durable timestamps written by two
  processes; a figure that can go backwards under clock skew is worse than none, so the outcome
  reports counts and status only.

#### Task 2 component and responsive specification

| Surface / element | Structure and exact behaviour |
|---|---|
| Rail | Unchanged Task 3 structure; the three labels are **Project Explorer**, **Missions**, **Settings**, Settings bottom-aligned, selection marked by the accent marker plus `aria-current="page"`. `Project Explorer` wraps at the rail's lower bound; it must never clip or truncate. |
| Content header | Title **Missions**, subtitle the Project title. No Project path is rendered anywhere, as Task 3 fixed. |
| Control thread | One entry per run this session started: the person's instruction, then a concise outcome card — **`<Mission>` run `<status>`**, a status pill, durable counts (**`N expert turns · N tool calls`**), and one **View run trace** action once the run has produced something to read. It renders no expert message, approval, tool or activity row; that is structural, because the projection it is handed has no field for one. With no run it shows one quiet line: **Run a mission to see it here.** |
| Run Trace | A separate document state reached from an outcome, not from the rail. Header: **← Back to Missions**, the run's title, a **Live** badge until the run is terminal, and one line saying what the surface is. Body: the existing shared transcript renderer over that run's durable events, so a turn reaches the screen byte-identical to what was stored — never summarised or shortened. The submitted prompt is not repeated here; it belongs to the control thread. The composer is **absent**, not disabled. Deferred and therefore not drawn: tabs, run inspector, stop-run, add-guidance, artifact panes, run history. |
| Legacy notice | Shown only when `hasLegacyHistory`, above the control thread, reading exactly: **This Project has earlier legacy history. It is retained and is not shown here.** Static text, no link, no action, no call. |
| Composer row | One row inside the composer bar: mission picker, instruction field, primary action. One row rather than two because the compact baseline is 568px tall and vertical space is the scarce axis; it also puts the picker literally beside the action it governs. |
| Mission picker | A button (`Mission: <name>`, chevron) plus a `role="listbox"` popup of exactly `Janus` and `Naive` — no `Default` row, description, model, provider, or expert name. Accessible label `Mission`; `aria-expanded` and `aria-activedescendant`; the current mission carries a check. Keyboard: `Enter`/`Space`/`ArrowDown` open, `ArrowUp`/`ArrowDown`/`Home`/`End` move, `Enter`/`Space` commit, `Escape` closes and returns focus to the button. A commit sends `SelectProjectMissionRequest` and renders only the canonical response value. It is a custom control, not a native `<select>`, because the required open state must be styleable with Workbench tokens and capturable in the browser. |
| Instruction field | Placeholder **What should this mission do?**; while a run is active it is disabled and reads **Waiting for this run to finish…**, so a disabled control says why. `Enter` submits when the action is enabled. |
| Primary action | **Run**, disabled while the instruction is blank or no mission is selected. In flight it reads **Starting…**; on acceptance **Starting Janus…** / **Starting Naive…**, taken from the response's mission rather than local state, because the selection can change between render and press. Its width is content-driven above a tokenized minimum so the longer busy label widens the control instead of clipping. |
| Errors | Typed `ProjectOperationError` messages render in one row directly above the composer — beside the action that produced them — using `--danger`, `--danger-bg`, `--danger-border`. A failure never clears the instruction and never fabricates activity. |
| Corrupted selection | Per the locked rule above: error row shown, button reads **Mission: none selected**, nothing invented, picker fully usable and keyboard-operable, run action disabled until a selection repairs the manifest. |
| Run filtering | A run enters the projection only because this session started it, and a durable event is filed under its run or dropped — which is what keeps a reopened Project's replayed history from rendering as though it had just happened. Because the tail starts inside the same call that starts the run and replays from sequence 0, events can arrive before the acceptance returns; those observed between press and acceptance are buffered and drained through the same path once the run ID is known, and discarded on failure. On a terminal status the composer becomes empty, enabled and refocused, and the run's outcome and trace stay available for the rest of the session. |

All colour, radius, spacing and type values come from the Workbench theme tokens
(`--bg`, `--surface`, `--surface-active`, `--border`, `--border-strong`, `--text`, `--text-muted`,
`--text-subtle`, `--accent`, `--accent-soft`, `--accent-contrast`, `--success`, `--danger`,
`--danger-bg`, `--danger-border`, `--shadow-lg`, `--focus-ring`, the `--wb-rail-*` family, and the
`--wb-*` geometry ramps). New values are **geometry only** — picker and popup measurements — added
to the existing mode-independent geometry group, so no colour is duplicated into a dark map.
Contrast pairs in play: `--text` on `--surface` 15.9, `--text-muted` on `--surface` 6.9,
`--text-subtle` on `--surface` 4.78, `--accent-contrast` on `--accent` 4.6, `--danger` on
`--danger-bg` 6.8, `--success` on `--surface` 4.9, and the four rail pairs Task 3 recorded.

**Interaction gate — PASS.** *Cooper:* the picker serves the developer's actual moment — pick a
named mission, state the instruction, watch it run — and exposes no provider, model or expert
machinery; the perpetual-intermediate check is met because the choice is two visible names, not a
shortcut. *Rams:* one row of chrome, the smallest that can carry a choice and an action; empty,
disabled, busy, error and corrupted states are all designed, not just the happy path. *Norman:* the
selected mission is always the persisted one, the action states which mission actually started,
impossible actions are disabled rather than failing silently, and the retained-history line promises
nothing because it affords nothing.

**Task 2 default fact — new.** Opening a Project now lands on **Missions** with **Janus** selected,
and the first user action is a named mission run rather than a control turn. Its acceptance is the
post-merge observation defined in the sequencing exception above: the published zero-argument
Desktop, no Conversation/Mission Runtime overrides, Host and Worker rebuilt from clean `main`, one
brand-new disposable Project, submitting without touching the picker and then with `Naive` selected,
and reopening to confirm the selection persisted — each producing durable child-run activity with
zero `ToolRequested`/`ToolResult` events.

#### Task 2 implementation evidence (2026-09-05) — awaiting review

**Tests.** Full solution suite **1158 passed, 0 failed, 11 skipped** (ForgeMission.Tests 789/11,
ConversationHost 191, Rooms 97, ConversationWorker 76, Runner 5). `make desktop` AOT publish clean.
New coverage: `MissionPickerTests` (18, keyboard/ARIA/catalog), the rewritten 43.21 section of
`HomeSessionOperationTests` (selection, corrupted selection, invalid input, busy, run filtering,
pre-acceptance buffering, terminal, retry identity, legacy notice, absence of every legacy request),
five `ProjectTransportContractTests` cases for the new read against the real out-of-process Client
Runtime, and a boundary rule banning `ProjectControl` / `MissionControl` / `mission-control` /
`Mission Control` anywhere in Presentation source text — identifiers, routes, comments and visible
strings alike.

**Browser acceptance — CONTROLLED, and labelled as such.** Driven through the packaged Desktop's
own supervised Client Runtime and its own Kind bridge, with Host and Worker on candidate images
built from this branch. The images are branch-built, so this cannot close default-path acceptance.
Kind was restored to the clean-`main` images (`767c2891…`) afterwards and the rollout verified.

| Check | Result |
|---|---|
| First open, picker open, Naive selected, invalid input, start failure, legacy notice, corrupted selection | PASS against their bound references, all through real pointer activation |
| Keyboard | PASS — `Enter` on the button opens, arrows move the active option, `Enter` commits, `Escape` returns focus to the button, `Tab` closes and moves on to the composer |
| Live Janus run, control thread | PASS — `Janus run completed`, `completed` pill, `5 expert turns · 0 tool calls`, one `View run trace`. **Zero expert bubbles, approvals, tool rows or activity rows in the Missions markup**, measured, not eyeballed. |
| Live Janus run, trace | PASS — `Janus run trace`, participants in order `Proposer → Approver → Proposer → Approver → Implementer`, no user bubble, no lifecycle rows, no composer, rail still marking `Missions` |
| Live Naive runs | PASS — two in one session, each with its own thread entry and its own trace; `1 expert turn · 0 tool calls`; the answer readable only in the trace, exactly as the no-exception rule requires |
| Busy | PASS — instruction plus a `queued` outcome, `Starting Naive…`, composer disabled, and **no trace offered** until the run has produced something to read |
| Back to Missions | PASS — returns to the same two-entry thread with its composer; the trace document is gone |
| Four corners 800×568 / 800×1024 / 1536×568 / 1536×1024 | PASS — no horizontal or vertical document scrolling at any corner; the content region owns overflow |
| Zoom 125% / 150% / 200% | PASS — no overflow at 125/150; at 200% (an effective viewport below the supported rectangle) the page degrades by scrolling, with the outcome title, the trace action and the rail labels all still unclipped |
| Both colour modes | PASS — dark renders the outcome card, status pill and trace action entirely through the named token map; no component-local literal |
| Text fit | PASS — outcome title, counts, status pill and `View run trace` all fit at every corner and every zoom step |

**Packaged-app parity — PASS for the Run Trace (2026-09-05).** Captured by window id from the
zero-argument packaged Desktop, so none of the operator's screen is in the image: the WKWebView
renders the corrected `Run trace` document — its header naming the Project and mission, the ordered
expert turns, and the `Revision requested` row — with the rail still marking `Missions`. That
capture came from the operator driving the app, not from me: the packaged window exposes no
automation protocol, which remains the standing limitation. The Missions control thread's own
packaged rendering has not been captured in this round; its shell, rail, picker and composer are
unchanged from the parity capture already recorded below, but the outcome card has not been seen in
WKWebView.

**Keyboard correction (2026-09-05).** Two defects, both fixed and both proved in the browser:

1. A browser turns Enter/Space on a button into a click, so handling those keys *and* receiving
   that click opened the popup and closed it again in one press — a keyboard user could never open
   it. The duplicate click is now suppressed exactly once, keyed on `pointerdown`: a real pointer
   activation always begins with one and a synthesised click never does, so no genuine click can be
   swallowed. Proved live: Enter on the focused button leaves `aria-expanded="true"`, the popup
   open and focus in the list; a pointer click straight after a key press still toggles normally.
2. `Tab` out of the open list returned focus to the picker button, trapping the person in the
   control. `Tab` now closes without touching focus, and the popup is rendered **after** the button
   in the DOM (it is absolutely positioned, so its order is free) — without that reorder the
   browser's own next-focusable was the button itself. Proved live: `Tab` in the open list leaves
   `document.activeElement === .composer-input`. `Escape` still returns focus to the button.

Worth recording because it changed the design: the browser driver used for this evidence does not
synthesise the native click for Enter on **any** button — a plain, freshly created `<button>` got
zero clicks from it. So "let the button be a button" would have left the whole keyboard path
unverifiable here. Handling the keys and suppressing the duplicate is correct in a real browser and
provable in this one.

**Candidate-stack gate — PASS for the correction (2026-09-05), candidate revision `e400e78`.**
Desktop and its supervised Client Runtime from `make desktop` at that commit;
`forge-conversation-host:e400e78` and `forge-conversation-worker:e400e78` built from it and rolled
into Kind. The zero-argument packaged Desktop started its own Kind bridge and its own Client Runtime
child — no endpoint override anywhere — and Janus and Naive were both driven end to end through that
stack, producing durable child runs whose control-thread outcomes and traces are the evidence above.
Kind restored to the clean-`main` images afterwards, rollout verified.

One observation worth recording, not a defect of this task: restarting the packaged Desktop while a
previous instance's Kind bridge was still closing left the new Supervisor with no bridge of its own —
it probed `18080`, found the dying forward still answering, and skipped starting one. The first run
then failed with the correct typed message. A clean start recovers it. That belongs to the
Supervisor's bridge readiness (43.19/43.17), not to Presentation.

**Earlier candidate-stack pass, revision `7b7c7a2`.** Every process in the
path was built from that one commit and run together, and the whole thing was driven through the
product's own flow. This is the check the rest of the evidence is structurally blind to: components
verified separately at different revisions say nothing about whether the product works.

| Process | Built from | How it ran |
|---|---|---|
| Desktop + supervised Client Runtime | `7b7c7a2` (`make desktop`) | The published `dist/forge-desktop/ForgeMission.Desktop`, launched with **zero arguments**. It started its own Kind bridge and its own Client Runtime child — no endpoint override anywhere. |
| ConversationHost | `forge-conversation-host:7b7c7a2` | Built from this commit and rolled into Kind. |
| Worker | `forge-conversation-worker:7b7c7a2` | Same. |

Both flows were driven end to end and produced durable child runs of the Project's Mission
container (`missionRef: null`, `purpose: projectMission`, `projectId` set):

- **Janus** — run `cc043d1e`, 15 events: `userMessage → runStatus(queued) → proposer → approver →
  approval(revisionRequested) → proposer → approver → approval(approved) → implementer →
  runStatus(completed)`. **ToolRequested 0, ToolResult 0.**
- **Naive** — run `05902c7b`, 4 events: `userMessage → runStatus(queued) →
  participantMessage(naive) → runStatus(completed)`. **ToolRequested 0, ToolResult 0.**

The surface driven was the packaged Desktop's *own* supervised Client Runtime, rendered in the
browser; the packaged WebView's rendering of the same page is proved separately below. Kind was
restored to the clean-`main` images (`767c2891…`) afterwards and the rollout verified.

Still a controlled test, by construction: the images were built from a branch rather than through
the sanctioned clean-`main` route, so this cannot close default-path acceptance — which remains the
post-merge observation.

**Packaged-app parity — PASS (2026-09-05).** The published `dist/forge-desktop/ForgeMission.Desktop`
was launched with zero arguments and its normal configuration; the operator opened a Project in it,
and the window alone was captured by window id, so none of the operator's screen is in the image.
Its outer window is 800×600 with a 32px title bar, corroborating the 43.20 Task 1 measurement of a
**800×568** usable viewport — the exact viewport every browser state was verified at.

The packaged WebView renders the Missions page as the references bind it: the three-entry rail with
`Project Explorer` wrapped and unclipped at the rail's lower bound, `Missions` selected and marked,
the header naming the Project, the empty-activity line, and the composer's picker / instruction /
`Run` on one row with the error row above it. No clipping, no overlap, no document scrolling.

**The parity check earned its place: it found a defect nothing else could.** The run failed against
the clean-`main` Host (which predates Task 1's endpoints, so a pre-merge failure is expected there)
— but it rendered as `net_http_message_not_success_statuscode_reason, 500, Internal Server Error`.
AOT trims the framework's resource strings, so raw exception text is *unreadable specifically in
the packaged build*, which is the one a person uses. The browser never showed this. Unexpected
faults on this surface now read as a sentence — "Forge could not start this run. The runtime did not
answer." — with the cause left to the Client Runtime's log, where it is complete. Every failure a
Project can actually have was already a typed `ProjectOperationError` with its own wording; this
covers only the fault path.

Known and out of scope: the connection-lost banner still appends its raw reason and would read the
same way in the packaged build. It belongs to the 43.17 stream-supervision work, not to this task.

**Done when:** browser and packaged Desktop show Janus visibly preselected, expose only Janus and Naive, persist deliberate selection, and submit both through the same named run action; both runs visibly have zero tool activity; TUI-equivalent contract tests pass and no Presentation code references provider, model, expert, or ProjectControl endpoint. After the combined replacement PR merges, the clean-main Kind rebuild plus zero-argument packaged Desktop prove default Janus and explicit Naive runs before the correction is marked complete or release-ready.

### Task 3 — Legacy route removal

Remove the obsolete ProjectControl public endpoints, Client Runtime session, fixed `MissionControl` resolver/executor, participant type, and user-facing strings. Retain only Task 1’s read-only legacy-history migration path.

**Done when:** source and contract tests prove no user-invocable `MissionControl` or ProjectControl execution path remains; a migrated Project starts Janus/Naive runs and retains prior history without treating it as a current mission; full suite and default-path packaged run pass.

## Completion condition

The correction is complete when a new or migrated Project makes the selected mission obvious, starts a durable child Mission Run for every submitted instruction, and shows Janus’s multi-expert exchange or Naive’s one-expert result without a direct-model or hidden-control path.
