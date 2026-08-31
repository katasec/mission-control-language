# Phase 43.20 — Project Workbench MVP

> **Status: Task 1 default-window visual acceptance failed (2026-08-31).** The six 1536×1024
> references pass, but the packaged Desktop's default usable viewport opens with its primary action
> below the fold. Measure and bind the compact viewport before remediation or approval. Part of
> [Phase 43 — Forge Desktop](phase-43-forge-desktop.md).

## Outcome

A person starts at a zero-authority Project home, opens an existing local **Project**, or supplies
one goal to create a named Project. The Project owns a local manifest, mission assets, attached context, one durable
Mission Control conversation, and its named runs. It is not a Git repository and may refer to
zero, one, or many repositories.

Mission Control is the project-scoped human ↔ Forge conversation. A run is a separately named,
immutable execution launched from that context. Selecting a run opens a small Trace document that
reads the original durable expert messages in order. A live Trace can request a stop or queue one
piece of guidance for its next safe boundary. The UI never maintains a second transcript
or claims that an external effect was rolled back.

This is the deliberately small first workbench. Docking, source/diff panes, search, transcript
filters, compact timelines, inline artifact previews, pause-after-step, notifications, project
membership, and a registry browser are later work—not unfinished MVP requirements.

## Read boundary

Read this spoke first. Then read only:

1. [Forge Architecture](../design/forge-architecture.md), [Durable
   Conversations](../design/durable-conversations.md), [Security
   Architecture](../design/security-architecture.md), and [Engineering
   Philosophy](../design/engineering-philosophy.md);
2. `src/ForgeMission.ClientRuntime/Services/DefaultWorkspace.cs`,
   `Transport/ClientRuntimeSessionStore.cs`, and
   `src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor` for the anonymous-workspace
   behaviour this replaces;
3. `src/ForgeMission.Conversations.Contracts/ConversationContracts.cs`,
   `src/ForgeMission.ConversationHost/Grains/ConversationGrain.cs`,
   `src/ForgeMission.ConversationWorker/Messaging/MissionCommandProcessor.cs`, and
   `src/ForgeMission.ClientRuntime/Services/ConversationRuntimeSession.cs` for the current
   durable path; and
4. [43.3 — Mission-as-attach-point](phase-43.3-mission-attach-point.md) only when a task touches
   mission discovery or OCI dependencies. Its registry catalog is not silently included here.

Do not change the Desktop Supervisor/Host contract, create a hosted Project database, give the UI
direct filesystem or Conversation-store access, add an OCI search/install workflow, or make
`Pause after step` a disguised Stop action.

## Locked MVP model

### Project home and local manifest

`forge.project.json` is a Forge-owned local manifest at the Project home. On first use the only
required input is the goal. Forge derives a title and a collision-safe slug, then creates
`<user-profile>/Forge/Projects/<slug>`; the location/name are visible and editable, but no chooser
is required. Opening an existing directory uses that directory as the home and creates or discovers
the same manifest there.

The manifest records a schema version, stable Project ID, title, goal, home-relative Forge assets,
selected mission reference, attached context descriptors, Mission Control conversation ID, and
local run metadata. It contains no credentials, secret-derived values, raw transcript, or remote
data-plane connection string. The Project home is the first run's sole local execution root. A
later multi-root capability is not inferred by this task.

`~/source/repos/0001` and successors are removed. Desktop does not create a directory, Client
Runtime session, or tool authority merely because the app opened.

### Manifest v1 schema and launcher boundary

Task 1 writes the complete v1 shape so later tasks add facts to empty collections instead of
silently changing the local schema:

| Field | Type / initial value | Ownership rule |
|---|---|---|
| `schemaVersion` | `1` | Reject a newer version; migrations are explicit. |
| `projectId`, `title`, `goal` | `Guid`, non-empty strings | Stable local Project identity and user intent. |
| `assets` | `ProjectAssetDescriptor[]`, initially empty | Every descriptor is `{ kind: Mission \| Expert \| LockFile, relativePath, contentHash? }`; paths are normalized, home-relative, and never escape the Project home. |
| `selectedMission` | `ProjectMissionReference`, initially `{ origin: BuiltIn, reference: Janus, digest: null }` | `origin` is `BuiltIn`, `Local`, or `Oci`; a local reference is home-relative, an OCI reference has a pinned digest, and the local content hash belongs to a run snapshot rather than the mutable selection. |
| `attachedContext` | `ProjectContextDescriptor[]`, initially empty | Each descriptor is `{ id, kind: SourceRoot \| File \| Artifact, displayName, reference, contentHash? }`. `reference` is an absolute local path only for `SourceRoot`/`File`; for `Artifact` it is an opaque artifact ID. These values never cross the Conversation boundary. |
| `missionControlConversationId` | nullable `Guid`, initially `null` | Task 2 writes the server-issued ID after durable acceptance. |
| `runs` | `ProjectRunMetadata[]`, initially empty | Local projection only; Conversation remains canonical for events and status. |

`ProjectRunMetadata` is `{ runId, title, status, predecessorRunId?, launchSnapshot }`.
`launchSnapshot` is immutable and contains `{ mission, localMissionContentHash?, resolvedExperts[],
context[] , gitRevision?, artifacts[] }`, where a resolved expert is `{ reference, digest }`, a
context snapshot is `{ contextId, contentHash? }`, and an artifact snapshot is `{ artifactId,
contentHash? }`. Task 4 writes this once; it must not crawl a workspace to populate optional
hashes or revisions. The `status` value is the durable `ConversationRunStatus` projection; Task 6
extends that shared lifecycle with `Stopping` and `StoppedByUser` rather than creating a local
parallel enum.

The Task 1 launcher deliberately has no profile-level recents index or automatic resume: boot
performs no Project open, session creation, or capability authorization. It offers create-from-goal
and explicit existing-directory open. A later recent-project experience requires its own bounded
design; it is not inferred by scanning directories or adding a hidden catalog here.

`goal` is never empty in a persisted manifest. Choosing an existing directory first discovers its
manifest. If it is absent, the launcher keeps that directory as the proposed home and uses the same
goal-confirmation flow to create the manifest there; it does not manufacture an empty-goal Project.

Project create/open share one surface-neutral `ProjectOperationResponse`: `Created`, `Opened`,
`GoalRequired`, or `Failed`. A successful response carries `ProjectSession`; `GoalRequired`
carries `ProjectHomeProposal`; and `Failed` carries the named `{ code, message }`
`ProjectOperationError`. Expected domain failures (invalid goal/home, missing home, invalid/newer
manifest, path validation, and exhausted collision attempts) use this response rather than
surface-specific exception handling. Unexpected transport/process failures may still fail the
transport normally.

`ProjectDraftRequest` is a separate, side-effect-free Client Runtime contract used after the user
enters a goal to show the derived title and home before confirmation. It accepts the goal plus
optional title/home overrides and returns the values to display; it creates no directory, manifest,
session, capability authority, or collision reservation. Create recomputes the draft and performs
the authoritative collision-safe write, so a concurrent Project creation may produce a different
final suffix. Desktop and TUI use this same draft contract; neither derives a Project value itself.

### Project assets and expert dependencies

Project Explorer distinguishes editable local assets from resolved dependencies:

```text
Project
├── Mission
│   ├── mission.mcl
│   ├── local experts
│   └── Expert dependencies (mcl.lock: OCI reference + immutable digest)
├── Source context
└── Runs
```

An OCI expert or mission is read-only dependency evidence, like an installed NuGet package. The
MVP lists only references already resolved by the project lock/manifest; it neither searches a
registry nor pulls/updates a package. A bundled OCI mission remains a selectable mission with its
own pinned bundle digest, not a synthetic collection of editable project experts.

### Mission Control and run identity

Project metadata is local; ordered messages, run events, stop/guidance requests, and artifacts
remain owned by the existing durable Conversation bounded context. Create one Mission Control
conversation per Project and store only its server-issued ID in the manifest. Conversation events
with no `run_id` belong to Mission Control; events with a `run_id` belong to exactly one run.

The current Janus-only `MissionCommandProcessor` is an implementation proof, not the Project
model. Generalise mission selection through a named Worker resolver before Mission Control is
shown. The built-in `MissionControl` mission is a zero-tool, project-refinement mission; it must
not launch implementation work. A project's selected launch mission—initially Janus—is a separate
choice. This replaces the current assumption that every durable user message starts a Janus run.

### Launch snapshot and local-path boundary

Starting a run is explicit, but normally one click. The Project's selected mission and current
brief/context prefill a small expandable summary; Forge derives a meaningful title. The immutable
launch snapshot includes the mission reference and local content hash or OCI digest, resolved
expert references, selected source descriptors, Git revision when cheaply available, and IDs/hashes
for explicitly attached files/artifacts when already available. It never crawls a workspace to make
a snapshot.

Absolute local paths remain in the local manifest. The Conversation API/Worker receives only the
mission reference, goal, declared local capabilities, and opaque stable context references needed
for durable trace meaning—never a workspace path, credential, or secret-derived value.

### Trace and extension seam

One run opens one main-content Trace. Its header names the run and current durable status; its body
is a chronological list of the original durable messages and facts for that `run_id`. A completed
message is rendered exactly as stored—never concatenated, summarized, or rewritten. Explicit
redaction renders an explicit marker. An artifact reference is a link to its own document surface;
the Conversation service remains the sole Blob reader.

The extension seam is the existing ordered, versioned `ConversationEvent`: stable `event_id`,
`sequence`, `kind`, timestamp, optional artifact reference, and immutable run snapshot. The Trace
renders an unknown future event as a plainly labelled activity card. Do not introduce a renderer
plugin framework, a UI transcript store, or a generic layout system.

### Run controls

`Stop run` and `Add guidance` are distinct durable commands.

| Control | Contract |
|---|---|
| Stop run | A confirmed, break-glass request. It blocks future work, requests cancellation of the active provider/tool work, enters `Stopping`, and becomes `Stopped by user` only after the Worker observes cancellation. It never claims rollback. |
| Add guidance | One pending text instruction. It is recorded as queued and delivered once, after the current completed safe boundary and before the next expert action. It cannot interrupt work, override capability authorization, or alter a terminal run. |

The MVP adds `Stopping` and `StoppedByUser` to the durable run lifecycle. `Paused`, generic
checkpoint/resume, and automatic recovery remain deferred. Terminal runs are append-only: a later
attempt is a new named run linked to its predecessor, never a replay of uncertain work.

Stopping requires a dedicated durable run-control command path, not a message queued behind the
active mission command. ConversationHost owns acceptance/audit and publishes the run-control
command; Worker owns the per-run cancellation source and the observed terminal outcome; Client
Runtime cancels only a locally executing tool for the named stopped run. A provider/tool that does
not observe cancellation is recorded as interrupted/uncertain, never described as stopped.

Guidance uses the same durable run-control path but a separate command kind. Core gains one
awaited safe-boundary callback after a completed trace fact is durable and before the next step
starts. It returns at most one pending instruction, delivered under the reserved `guidance` context
key to that next step. Janus explicitly consumes that key; future missions opt in through their
versioned definition. This is not a hidden, global prompt injection.

### Outcomes

When a run reaches `Completed`, `Failed`, `StoppedByUser`, or `Interrupted`, Mission Control gains
one clearly labelled outcome card with links to the Trace, artifacts, and verification evidence.
Only a deliberately labelled outcome summary may be concise; it is not substituted into the Trace.
The Project manifest/run list records the same terminal state. A stopped/interrupted card names
known partial effects when present and never offers a resume button.

## Architecture, security, and quality gates

| Gate | Decision |
|---|---|
| Bounded context / owner | The local Client Runtime owns `forge.project.json` and local path access. ConversationHost remains the sole owner of ordered Mission Control/run events, run-control audit facts, Table/Blob state, and artifact reads. Worker owns active execution/cancellation. |
| Public entry / tiers | Desktop Presentation calls Client Runtime only. Client Runtime uses the existing Conversation API. In hosted topology, Tier 1 authenticates/routes to the internal Tier-2 Conversation service; no Desktop/UI direct store route exists. |
| Tier 3 / credentials | Conversation Table, Blob, and run-control transport remain private Tier 3. Presentation receives no data-plane credentials; Client Runtime receives neither Conversation-store nor Worker credentials. Local manifests exclude secrets. |
| Cross-context access | No service queries another context's store. Project metadata is local; Conversation state is reached solely through named Conversation commands/queries. |
| Type / reversal | Conversation ownership and run-control audit semantics are Type 1 and remain in the existing Conversation context. Local manifest location, trace layout, and the run-control transport implementation are Type 2 behind named contracts. Removing the local manifest feature removes only Client Runtime files; it does not alter durable ownership. |
| Failure ownership | Manifest store reports invalid/missing local Project data; ConversationHost reports invalid/duplicate control commands; Worker reports observed cancellation; Client Runtime reports local tool cancellation; Presentation renders those facts. |
| Surface portability | Desktop and a future TUI are interchangeable Presentation consumers. Every Project action is a named `IClientRuntimeChannel`/transport contract with the same result and failure semantics; Desktop-only layout/focus details own no business behaviour. |
| Engineering philosophy | One small manifest, one control-conversation owner, one event stream, and two named controls replace anonymous folders and UI-only buttons. No catalog browser, layout engine, or project service is introduced. |
| Proof | Unit/contract tests cover manifest collision and migration, command idempotency, trace replay/deduplication, safe-boundary ordering, and stop outcomes. An isolated Kind/Desktop run proves a named Project opens, a Janus Trace replays exact messages, guidance applies after a safe boundary, and Stop reaches a truthful terminal status. |

### Desktop Design and Implementation Quality Gate

| Required answer | Result |
|---|---|
| What product behaviour is required? | A named Project opens without an anonymous workspace; users can read and safely direct its named runs. |
| Who owns it? | Presentation renders/project-navigates; Client Runtime owns local manifest/filesystem work; ConversationHost owns durable commands/events; Worker owns execution/cancellation. The Desktop Supervisor and Host own none of this behaviour. |
| What has been verified about the adapter? | The current WebView Presentation already reaches Client Runtime through its existing channel; this work needs no Host API, callback, process ownership, or native adapter change. |
| Why is the replacement boundary preserved? | No Host contract, Supervisor lifecycle, credential hand-off, or capability-provider call is added. Replacing Photino leaves the Project/Trace/control contracts unchanged. |
| What proves it? | Presentation/Client Runtime boundary tests plus browser verification of project creation, trace replay, guidance, and stop; packaged Desktop smoke verification confirms the same flow without a Host-specific workaround. |

**PASS.**

## Dependency-ordered work

### Task 1 — Project home and local manifest — default-window visual acceptance failed

**Not approved for merge.** Claude and Codex passed the internal package, behavior, theme, and
six-state 1536×1024 visual comparisons, but the operator found that the packaged Desktop default
window requires a scroll to reach **Create project**. The earlier rejected implementation remains in
[the implementation review record](phase-43.20-project-workbench-mvp_review.md#task-1--project-home-and-local-manifest-visual-acceptance-rejected);
the current implementation is in PR #78. It needs a compact, default-window visual design and a
new internal review before another operator acceptance request.

**Default-window responsive gate.** Before another implementation handoff, observe the packaged
window's actual `window.innerWidth × window.innerHeight`; do not infer it from native window
chrome. Add an approved compact reference for that viewport. It must show every required input,
**Create project**, and the open-folder entry point without document scrolling in the empty,
drafted, busy, failed, and goal-required states. The compact rule must reskin through Workbench
tokens (including responsive `--wb-*` geometry/type values), not component-local overrides. The
`--wb-*` values must be fluid and bounded (`min()`, `max()`, `clamp()`, percentage/viewport-relative
values) so they retain the approved 1536×1024 geometry at that viewport and contract at the actual
default one; do not replace one fixed card size with a different fixed card size. The
open-folder-expanded state may scroll only if its entry point and its own primary action remain
visible; otherwise it too needs a compact fit.

**Binding reference:** [Create a Project from a Goal](../brainstorm/images/mission-project-flow-02-create-project.png)
at its 1536×1024 composition, with its flow context in
[Mission Conversations](../brainstorm/mission-conversations/README.md#2-create-a-project-from-a-goal).
The running implementation inspected on 2026-08-30 is a small two-card launcher; it lacks the
reference's Workbench header, activity rail, project canvas, recent-project area, and initial
context choices. **Result: FAIL.** Codex review and the operator's independent visual review both
recorded this failure; it is a binding rejection, not a request for cosmetic follow-up.

The reference intentionally spans Project creation, navigation, recents, and later workbench
experiences, while this task deliberately owns no recents index and Task 3 owns the rail/explorer.
Before another implementation handoff, create and approve a Task 1-specific SVG slice under
`docs/images/phase-43.20/` that states exactly what this task renders now and what remains deferred.
Do not infer that scope from the broader journey mockup.

#### Approved-for-review visual specification (drafted 2026-08-30, awaiting Codex sign-off)

**Binding Task 1 reference:** the five state files under `docs/images/phase-43.20/`, each a full
1536×1024 frame, are what the implementation is judged against — not the journey mock directly:

| State | File | What it shows |
|---|---|---|
| Empty | [task1-project-launcher-empty.svg](../images/phase-43.20/task1-project-launcher-empty.svg) | Goal field focused and empty; name and location show derived-value placeholders; **Create project** disabled. |
| Drafted | [task1-project-launcher-drafted.svg](../images/phase-43.20/task1-project-launcher-drafted.svg) | `ProjectDraftResponse` values filled in and editable; Create enabled. |
| Busy | [task1-project-launcher-busy.svg](../images/phase-43.20/task1-project-launcher-busy.svg) | A draft or create in flight; every control disabled; the action reads `Working…`. |
| Failed | [task1-project-launcher-failed.svg](../images/phase-43.20/task1-project-launcher-failed.svg) | A typed `ProjectOperationError` message inside the card; nothing created; entered values preserved. |
| Goal required | [task1-project-launcher-goal-required.svg](../images/phase-43.20/task1-project-launcher-goal-required.svg) | A chosen directory with no manifest: the runtime's proposed name/location plus a notice; Create disabled until a goal exists. |
| Open folder | [task1-project-launcher-open-folder.svg](../images/phase-43.20/task1-project-launcher-open-folder.svg) | The `Open an existing folder…` link expanded: one path row beneath the card, spanning its width, in the same field language, with its `Open` action. Nothing else on the surface changes. |

[task1-project-launcher-before.svg](../images/phase-43.20/task1-project-launcher-before.svg)
records the rejected two-card launcher so the defect stays on file.

**Slice.** Task 1 owns the journey mock's centre "New project" card and the page header, and
nothing else on that screen. The card keeps the reference's internal proportions, field order,
label wording, and type hierarchy, recomposed for a canvas with no rail and no recents column.

**Deferred, named so it is not inferred as missing work:** the navy activity rail (Task 3); the
"Recent local projects" column (no recents index exists by design); "Add context (optional)" and
its two buttons (needs an `attachedContext` contract — Task 4); journey screen 01 "Choose a
project" (depends on the deferred recents index); and everything the workbench shows after a
Project opens (Tasks 2/3).

**Geometry**, measured from the journey mock at 1536×1024 and preserved:

| Element | Reference | Task 1 |
|---|---|---|
| Header band | 0–97, rule `#c9d5e7` | identical; wordmark at x=80 (the reference's 83px canvas margin, with no rail to offset it) |
| Card | x 205–1111 (906 wide), top y=134 | 906 wide, centred → x 315–1221; **top stays y=134** so its relationship to the header is unchanged |
| Card padding | 45–46 | identical |
| Goal textarea | x 303–1065 (762), y +115…+285 from card top | identical offsets |
| Project name | label +347, field +361…+424 (63 tall) | identical |
| Location | — | label +461 (16px), field +475…+521 (46 tall), monospace value |
| Divider → action | rule, then button 271×68 right-aligned to the field edge | identical rhythm |
| Card height | 802 | 718, or 804 when an error/notice band is present |

The card is shorter than the reference's because two blocks are deferred; the freed space accrues
below the card rather than being redistributed.

**Copy strings**, exact: header `Forge` / `AI Workbench`; card title `New project`; goal
placeholder `Describe what you want to build`; labels `Project name` and `Location`; name
placeholder `Derived from your goal`; location placeholder `Derived from your project name`;
primary action `Create project`, and `Working…` while busy; secondary link
`Open an existing folder…`; goal-required notice
`That folder is not a Forge Project yet. Enter a goal to create one there.` A failure renders the
`ProjectOperationError.Message` verbatim — the surface never rewrites it.

**Interaction change from the rejected build:** there is no `Continue` button. The reference has one
primary action, so the draft call fires when the goal is committed (blur or Enter) and fills the
name and location fields in place; `Create project` is the only button. Both fields stay editable
overrides, sent back verbatim.

**Reference values**, sampled from the journey mock (not from the ember theme): canvas `#f7faff`; header
`#f6f8fd` with rule `#c9d5e7`; wordmark and links `#0468ed`; primary action `#0f6eeb`; focused
field border `#0f56d2`; card `#ffffff` with border `#e7ecf4`; field border `#d5dae5`; divider
`#e5eaf2`; ink `#101d33`; secondary ink `#1d2c47`; muted `#5b6b83`; placeholder `#8b99ad`. Type:
wordmark 36/700, subtitle 28/400, card title 36/700, goal 22, name 21, labels 20 and 16, action
22/600. These are reference-artifact values only. The implementation maps them to the named
Workbench theme's semantic tokens; no component rule may copy them as literals. Lime is reserved
for healthy/approved states in the locked visual language and the launcher has none, so it is unused.

**Not sampled — the reference has no failure state.** Every value marked **N** in the token maps
below is chosen to sit in the same saturation register as the sampled blues rather than measured
from the reference. They are the one place this spec invents rather than matches.

**Theme architecture.** The sampled values are reference targets; the implementation reaches them
through the existing semantic tokens, via a named **Workbench** product theme in
`src/ForgeUI/wwwroot/css/forge.css`. `ProjectLauncher.razor.css` (Blazor CSS isolation) and the
page's own rules carry structure only — flex/grid, direction, alignment, and disabled/focus/hover
state. Every colour, radius, spacing, font, type size **and the reference's own geometry** resolves
through `var(--token)`; neither file declares a custom property or a literal value.

The measured geometry is theme-owned, in the same map, as `--wb-*`: header height and inset; card
width, top gap and padding; the sparkle gutter and glyph size; goal, name and location field
heights; the title, field, tight-field, label, rule and action row gaps; notice/error gap and
padding; action width and height; link gap and glyph; and the open-folder row's gap and action
width. These are mode-independent, so the dark maps inherit them unchanged.

Selection is `data-surface-theme="workbench"` on `<html>` in the Client Runtime Presentation's
`index.html`. `data-theme` keeps its existing light/dark meaning; `data-surface-theme` is the
orthogonal product axis. Three blocks compose with the existing three-state mode model, each
outranking its default-theme counterpart on specificity so the cascade does not depend on source
order:

| Selector | Specificity | Applies when |
|---|---|---|
| `:root[data-surface-theme="workbench"]` | 0,2,0 | Workbench light — the default, and under a forced `data-theme="light"` |
| `@media (prefers-color-scheme: dark) { :root[data-surface-theme="workbench"]:not([data-theme="light"]) }` | 0,3,0 | Workbench automatic dark |
| `:root[data-surface-theme="workbench"][data-theme="dark"]` | 0,3,0 | Workbench forced dark |

Nothing without the attribute matches any Workbench selector, so ForgeUI stays on its default theme.
The theme reskins every Desktop client surface through semantic tokens — deliberately, since it
changes no layout, interaction, or product behaviour.

**Workbench token map — light.** Provenance: **S** sampled from the reference, **D** derived from a
sampled value by the relationship the default theme already uses, **N** no reference evidence (the
reference has no failure, success, warning, or seal state), **A** an accessibility override of a
sampled value, carried back into the SVG references so they stay binding.

| Token | Light value | Src | Used for |
|---|---|---|---|
| `color-scheme` | `light` | — | |
| `--bg` | `#f7faff` | S | page canvas |
| `--surface-sunken` | `#f6f8fd` | S | header band |
| `--surface` | `#ffffff` | S | card, fields |
| `--surface-hover` | `#eef3fb` | D | |
| `--surface-active` | `#e4ecf8` | D | |
| `--border` | `#e7ecf4` | S | card border |
| `--border-strong` | `#d5dae5` | S | field border |
| `--text` | `#101d33` | S | card title, field values |
| `--text-muted` | `#5b6b83` | S | `Location` label |
| `--text-subtle` | `#62748c` | A | placeholders — see the accessibility override below |
| `--accent` | `#0f6eeb` | S | primary action, links |
| `--accent-hover` | `#0f56d2` | S | hover, focused field border |
| `--accent-soft` | `#eff5fe` | S | notice band |
| `--accent-contrast` | `#ffffff` | S | text on the primary action |
| `--ink` | `#0f6eeb` | S | solid action — the same blue family as `--accent` |
| `--ink-hover` | `#0f56d2` | S | |
| `--ink-contrast` | `#ffffff` | S | |
| `--success` | `#4d7c0f` | N | |
| `--success-bg` | `#f2fbe6` | N | |
| `--success-border` | `#84cc16` | N | the locked language's lime |
| `--danger` | `#b42318` | N | failure message |
| `--danger-bg` | `#fef3f2` | N | failure band |
| `--danger-border` | `#fda29b` | N | |
| `--warning` | `#8a5a06` | N | |
| `--warning-bg` | `#fef6e7` | N | |
| `--seal-official` | `#8a5a06` | N | |
| `--seal-verified` | `#0f6eeb` | N | |
| `--seal-check` | `#ffffff` | N | |
| `--radius-sm` | `8px` | S | `Location` field |
| `--radius` | `10px` | S | goal, name, button |
| `--radius-lg` | `16px` | S | card |
| `--radius-pill` | `999px` | — | |
| `--space-1` … `--space-6` | `4px` `8px` `12px` `16px` `24px` `32px` | — | |
| `--space-7` | `44px` | S | card padding (measured 45–46, within tolerance) |
| `--font-size-display` | `36px` | S | wordmark, card title |
| `--font-size-title` | `28px` | S | `AI Workbench` |
| `--font-size-lead` | `22px` | S | goal text, button label |
| `--font-size-body` | `21px` | S | `Project name` value |
| `--font-size-label` | `20px` | S | `Project name` label |
| `--font-size-meta` | `16px` | S | `Location` label |
| `--font-size-mono` | `15px` | S | location path |
| `--font-sans` | `system-ui, -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif` | — | |
| `--font-mono` | `ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace` | — | |
| `--transition` | `130ms ease` | — | |
| `--focus-ring` | `0 0 0 3px rgba(15, 110, 235, 0.30)` | D | |
| `--shadow-sm` | `0 1px 2px rgba(11, 36, 71, 0.06)` | D | card |
| `--shadow` | `0 1px 3px rgba(11, 36, 71, 0.08), 0 4px 12px rgba(11, 36, 71, 0.06)` | D | |
| `--shadow-lg` | `0 8px 30px rgba(11, 36, 71, 0.14)` | D | |

The seven `--font-size-*` names are new. They are declared in the default `:root` with the sizes
current components already use in literals (`30px` `22px` `17px` `15px` `14px` `12.5px` `12px`), so
adding them changes nothing that exists, and the Workbench map overrides them as above.

**Workbench token map — dark.** Applies to both the automatic and forced-dark selectors. Radii,
spacing, type sizes, fonts and transition are restated identically to the light map and are not
repeated here; every token that differs is listed.

| Token | Dark value | Used for |
|---|---|---|
| `color-scheme` | `dark` | |
| `--bg` | `#071426` | page canvas |
| `--surface-sunken` | `#0a1a30` | header band |
| `--surface` | `#0f2440` | card, fields |
| `--surface-hover` | `#16304f` | |
| `--surface-active` | `#1d3a5c` | |
| `--border` | `#1d3556` | card border |
| `--border-strong` | `#2b4a72` | field border |
| `--text` | `#e6edf7` | |
| `--text-muted` | `#9fb2ca` | |
| `--text-subtle` | `#8aa3c2` | placeholders |
| `--accent` | `#4d9bff` | primary action, links |
| `--accent-hover` | `#74b2ff` | |
| `--accent-soft` | `#12294a` | notice band |
| `--accent-contrast` | `#06121f` | text on the primary action |
| `--ink` | `#4d9bff` | solid action — the same blue family as `--accent` |
| `--ink-hover` | `#74b2ff` | |
| `--ink-contrast` | `#06121f` | |
| `--success` | `#a3e635` | |
| `--success-bg` | `#16300a` | |
| `--success-border` | `#65a30d` | |
| `--danger` | `#ff9d95` | failure message |
| `--danger-bg` | `#3a1512` | failure band |
| `--danger-border` | `#b42318` | |
| `--warning` | `#e8c06a` | |
| `--warning-bg` | `#3a2c10` | |
| `--seal-official` | `#e8c06a` | |
| `--seal-verified` | `#4d9bff` | |
| `--seal-check` | `#071426` | |
| `--focus-ring` | `0 0 0 3px rgba(77, 155, 255, 0.35)` | |
| `--shadow-sm` | `0 1px 2px rgba(0, 0, 0, 0.45)` | |
| `--shadow` | `0 1px 3px rgba(0, 0, 0, 0.50), 0 4px 12px rgba(0, 0, 0, 0.40)` | |
| `--shadow-lg` | `0 8px 30px rgba(0, 0, 0, 0.55)` | |

**Measured contrast**, computed from the values above rather than asserted. Every text-bearing
pairing meets its applicable AA threshold in both maps:

| Pair | Light | Dark |
|---|---|---|
| `--text` on `--surface` | 16.86 | 13.23 |
| `--text` on `--bg` | 16.12 | 15.67 |
| `--text-muted` on `--surface` | 5.42 | 7.20 |
| `--text-subtle` on `--surface` | 4.78 | 6.01 |
| `--text-subtle` on `--bg` | 4.57 | 5.19 |
| `--accent-contrast` on `--accent` | 4.72 | 6.69 |
| `--accent` on `--surface` | 4.72 | 5.53 |
| `--ink-contrast` on `--ink` | 4.72 | 6.69 |
| `--ink-contrast` on `--ink-hover` | 6.40 | 8.58 |
| `--danger` on `--danger-bg` | 6.05 | 8.11 |
| `--success` on `--success-bg` | 4.69 | 9.53 |
| `--warning` on `--warning-bg` | 5.51 | 7.87 |
| `--seal-check` on `--seal-official` | 5.92 | 10.71 |
| `--seal-check` on `--seal-verified` | 4.72 | 6.55 |

**Accessibility override.** The reference's placeholder colour is `#8b99ad`, which reaches only
2.89 against `--surface`. Accessibility wins over sample fidelity, so Workbench light uses
`#62748c` — 4.78 on `--surface`, 4.57 on `--bg`. The change is carried into
`task1-project-launcher-empty.svg` and `task1-project-launcher-goal-required.svg`, the two frames
that render placeholder text, so the SVGs remain the binding reference and the implementation
matches them exactly. One consequence, recorded rather than hidden: `--text-subtle` now sits closer
to `--text-muted` (4.78 against 5.42), so the visual step between hint text and secondary labels is
smaller than the reference's.

**Dark-mode acceptance.** Dark has no visual mock and is not compared to one. It passes on three
checks: every Workbench token resolves under both the automatic and forced-dark selectors, no
default-theme ember value appears anywhere on the surface, and the contrast pairs above hold.

**Visual acceptance rule.** The comparison is card-internal fidelity — field order, labels, copy,
type scale, control sizes, spacing rhythm, divider and action-row placement — plus the header band.
Page-level side margins differ from the journey mock because two columns are deferred; that
difference alone is not a FAIL. Any difference inside the card is, within one stated tolerance:
geometry may differ by ±2px, because hand-written CSS and a hand-authored SVG differ slightly in
text metrics. Copy strings, field order, hierarchy, type scale, token-resolved colours, and state
behaviour are exact requirements with no tolerance.

**Assessment gate.** *Cooper:* the screen serves the one goal a person has before a Project exists —
state the goal and get a workspace — and shows the system's own derived name and location rather
than making them invent a path. *Rams:* one card, one primary action, no control that could be
merged away; all five states are drawn, not just the happy path; nothing here has to be worked
around when Task 3's rail lands, because the rail composes around this canvas. *Norman:* the only
interactive elements are the three fields, the button, and the link; the sparkle is decorative and
carries no button treatment; the draft's returned values are the immediate feedback for committing a
goal; and Create is disabled — not silently failing — until a goal exists.

After reimplementation, Claude and Codex must first record a visual PASS against that approved
slice in the running app. Only then may the operator be asked for final visual acceptance.

**Theme constraint:** Task 1's visual language must be delivered through a named design-system
theme and its light/dark token maps, not launcher-local sampled values or a light-only override.
The launcher may select the theme at its boundary; its rules must consume the shared semantic
tokens. The implementation plan must name the theme selector, token source, and the dark-mode
verification before build approval.

Built: an empty profile stays empty after opening Desktop; `Todos API` creates one deterministic
home and v1 manifest; a second one takes `todos-api-2`; reopening uses that home as the sole
execution root. `DefaultWorkspace`, `/transport/session/default`, and `Workspace:InitialRoot` are
removed.

Still true for later tasks: `ProjectDraftRequest` (side-effect free), `ProjectCreateRequest`, and
`ProjectOpenRequest` all answer with the surface-neutral `ProjectOperationResponse` /
`ProjectDraftResponse`; a non-empty goal is required by every one of them, whatever overrides are
supplied; `SessionSetupRequest` is replacement-only and enforced in Client Runtime, so only project
create/open establish a session and root; `MissionControlConversationId` is still `null` and Task 2
owns its first write-back.

### Task 2 — Durable Project Mission Control

Make the Project's enduring human ↔ Forge conversation a first-class Conversation contract.

- Add `ProjectControl` conversation purpose and named create/submit messages to
  `ForgeMission.Conversations.Contracts`; control messages/events have no `run_id` and cannot
  carry a local path or capability declaration.
- ConversationHost creates the control conversation idempotently and persists only canonical
  control events. Client Runtime writes the server-issued ID back to the manifest after acceptance.
- Replace `MissionCommandProcessor`'s hard-coded Janus dispatch with a named mission resolver.
  Its built-in zero-tool `MissionControl` mission serves refinement turns; Janus remains a selected
  execution mission and is never started by a control message.
- Extend Client Runtime's relay/session model so reopening a Project replays and follows its
  Mission Control event stream without creating a run.

**Done when:** reopening a Project restores its same durable Mission Control conversation; a
control turn produces durable Forge/user messages but no Run record or local tool request; retries
are idempotent; Contracts retain no Host/Orleans/Azure/provider dependency; fresh-Host replay,
contract round-trip, and full-suite tests pass.

### Task 3 — Project Explorer and resolved dependencies

Build the small navigation surface over the manifest and durable projections.

- Replace the proof chat's startup view with the persistent three-entry rail: Project Explorer,
  Mission Control, and the bottom-fixed Settings placeholder. It is a navigation aid, not a
  docking/layout framework.
- Project Explorer lists local Mission assets, attached context, and named runs. It distinguishes
  editable local experts from read-only `mcl.lock` OCI dependencies and displays each pinned
  reference/digest.
- Opening an asset or dependency uses an ordinary document view; no remote registry browser,
  package pull/update, standalone Runs entry, or Notifications entry is added.

**Done when:** a created Project opens Mission Control by default; the rail switches to Explorer
and Settings without creating a new project/session; Explorer accurately distinguishes local and
pinned OCI expert/mission evidence; and browser/component tests prove the three-entry order and
project-scoped navigation.

### Task 4 — Named run launch and immutable snapshot

Launch a selected mission from Project Mission Control without a configuration wizard.

- Add a named `StartProjectRun` contract addressed to the Project's control conversation. It
  creates one run and records its server-side mission identity; the manifest stores local snapshot
  fields and the returned run ID/title.
- The expandable launch summary defaults from the selected mission/current Project brief. A single
  Start action is explicit; title/location/context edits are optional.
- Capture only the locked lightweight provenance. Local absolute paths stay local. A later asset,
  mission, or context edit never changes an existing run snapshot.

**Done when:** starting a run creates one named run visible in Project Explorer and Mission
Control, pins its mission/expert/context evidence, and begins the selected execution mission;
retries create no duplicate run; editing the Project afterwards leaves the recorded snapshot
unchanged; contract/idempotency tests and an isolated durable-run observation pass.

### Task 5 — Minimal exact-message Forge Trace

Render a selected run as one read-only, chronological document.

- Project `ConversationEvent`s by `run_id`; retain sequence/event-ID replay/deduplication but do
  not merge adjacent durable messages as the group-chat proof renderer currently does.
- Render the original participant text, role, timestamp, outcome/status, explicit redaction, and
  an artifact link when present. Unknown future event kinds render a labelled activity card.
- Add the narrow Conversation-service artifact-read contract needed for a linked document. Blob
  access remains inside ConversationHost; Presentation receives only the resulting document data.
- Do not add a timeline mode, filters, search, threading, inline preview, source pane, or control
  buttons other than Task 6/7's live-run controls.

**Done when:** a Trace reopened after an SSE disconnect shows the exact ordered durable messages
once, includes its run status and artifact links, and has no UI-owned transcript persistence;
projection/contract tests and a real Janus trace prove the original Proposer→Approver→Implementer
exchange is readable end to end.

### Task 6 — Durable Stop run

Implement the break-glass control before adding ordinary guidance.

- Add `Stopping`/`StoppedByUser`, `RequestStopRun`, and a durable run-control command/event to
  Contracts. ConversationHost alone accepts/records the request and rejects terminal/duplicate
  requests idempotently.
- Add a dedicated run-control dispatch path and Worker per-run cancellation registry so a Stop can
  reach an executing provider call rather than wait behind its mission command. The Worker reports
  `StoppedByUser` only after cancellation is observed.
- Propagate the named stop to Client Runtime's active local tool hand-off. It cancels that one
  execution and reports its observed result; neither UI nor Worker claims a rollback.
- Add the red, confirmed Trace action. It changes to `Stopping…` after accepted request and shows
  the terminal fact only when the durable stream supplies it.

**Done when:** tests prove a stop blocks queued/future work, cancels active provider and local-tool
paths when they cooperate, keeps a non-cooperating/unknown path truthful as `Interrupted`, and is
idempotent; a live Kind/Desktop run visibly reaches `Stopped by user` with its prior trace intact.

### Task 7 — One safe-boundary guidance instruction

Add non-emergency correction after Stop is proven.

- Add one `QueueRunGuidance` contract/event and a per-run pending-guidance slot. ConversationHost
  accepts only one live-run instruction and records queued/applied/unapplied outcomes durably.
- Add Core's awaited safe-boundary callback after a completed trace fact is durable and before the
  next expert starts. The Worker consumes at most one queued instruction there and passes it under
  the reserved `guidance` key only to the following opted-in mission step.
- Janus declares the guidance binding in its mission/expert assets. Do not alter unrelated
  missions, provider/system prompts, capability policy, or an already-running call.
- Trace renders queued guidance and its exact application location. A terminal run leaves pending
  guidance visibly unapplied.

**Done when:** a live Janus run accepts one instruction, completes its current safe step, applies
the instruction exactly once before the following opted-in expert, and records the ordered facts;
guidance cannot cancel/interrupt a call or mutate a terminal run; Core ordering tests, durable
replay tests, and a live trace observation pass.

### Task 8 — Return terminal outcomes to Mission Control

Close the project loop without replacing the Trace.

- Project the terminal run fact into one concise, explicitly labelled Mission Control outcome
  card. Link its Trace, artifacts, and available verification evidence.
- Use distinct Completed, Failed, Stopped by user, and Interrupted wording. Stopped/interrupted
  cards retain known partial effects and link a new-run action; they never offer implicit resume.
- Keep the source Trace and terminal event canonical; the card is a projection, not a generated
  substitute for expert messages.

**Done when:** every terminal run yields exactly one durable/project-visible outcome card with the
correct status and Trace link; retry/replay does not duplicate cards; component tests and a full
Project run observation prove the user can return from Trace to Mission Control and understand the
result.

## Completion condition

The MVP is complete when a person can create/open a named Project with no anonymous workspace,
continue its durable Mission Control conversation, launch a reproducible named run, read the exact
expert exchange, stop it truthfully or guide its next safe boundary, and return to a clearly stated
outcome. The named browser/Kind observations and full solution suite must pass before this MVP is
marked complete.
