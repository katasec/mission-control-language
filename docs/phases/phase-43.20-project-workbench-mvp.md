# Phase 43.20 — Project Workbench MVP

> **Status: design ready (2026-08-30).** Replace Desktop's proof-era anonymous workspace with a
> project-first workbench: one enduring Mission Control conversation, named runs, a minimal exact
> trace, and honest stop/guidance controls. Part of [Phase 43 — Forge
> Desktop](phase-43-forge-desktop.md).

## Outcome

A person opens Forge into their most recent local **Project**, or supplies one goal to create a
named Project. The Project owns a local manifest, mission assets, attached context, one durable
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

### Task 1 — Project home and local manifest

Create the local Project record and replace `DefaultWorkspace`'s numbered directory bootstrap.

- Add a source-generated, versioned `forge.project.json` model/store in
  `ForgeMission.ClientRuntime`; keep all filesystem access there and expose project open/create
  through `IClientRuntimeChannel`/transport DTOs.
- The first-use Presentation asks only for a goal, derives the default title/home, and creates the
  manifest on confirmation. Existing-folder open creates/discovers the manifest in that folder.
- Use the Project home as the initial `IWorkspace` root only after a Project has been opened; do not
  create a Client Runtime session or directory at Desktop boot.
- Keep `MissionControlConversationId` optional until Task 2; no local transcript is written.

**Done when:** an empty profile has no new directory after opening Desktop; creating `Todos API`
creates one deterministic Project home and manifest, including collision handling; reopening it
uses that home as the sole local execution root; the old numbered-workspace tests are replaced;
Client Runtime/Presentation boundary tests plus the normal solution build/test suite pass.

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
