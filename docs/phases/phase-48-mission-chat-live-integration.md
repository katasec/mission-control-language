# Phase 48 — Mission Chat live integration

> **Status:** design locked 2026-09-13. Await Claude's revised plan-only relay; no implementation has
> started.

## Why this phase exists

Phase 47 proved the Mission Chat journey before it touched product state. This phase makes that
same journey real. It replaces only its local stand-ins with the existing Project, Mission,
Conversation Host, Client Runtime, Transport, and Presentation owners. It does not add a screen,
rail item, picker, workflow, or chat capability.

## Product decisions — locked

| Visible UI fact | Live behaviour |
|---|---|
| **Chat with a mission** opens a fresh Janus chat immediately. | On first use, Forge creates one managed chat Project; on later uses it reopens that same Project and creates one new empty Janus chat. The person never picks a folder, Project, mission, or version for this flow. |
| The rail has a Project name. | Projects generates a friendly two-word title once. It is ordinary Project display data and can be renamed by a later, separately designed action. No name field appears in this flow. |
| Chats group below a mission version. | The managed Project ships **Janus v1.4**, an immutable Approved mission. Each chat pins that version. A group or row appears only when real durable data exists; the Phase 47 Naive and sample-chat rows are not fabricated. |
| **New chat** returns to the fresh Janus state. | It creates one further empty Janus conversation in the same managed Project. |
| The fixed-access panel is read-only. | It is information, not a question. Creating or sending a chat message does not ask for or record profile consent. Client Runtime still asks before a bounded operation when its own policy requires it. |
| The startup screen remains the startup screen. | Forge does not silently reopen the last chat. **Chat with a mission** is the direct route to a fresh chat in the managed Project; **Open an existing workspace...** keeps its existing meaning. |

`Janus v1.4` is a shipped Forge mission, comparable to a shipped model choice. Its definition is
the small `Proposer -> Approver` mission the UI names. It is release-reviewed code, not an
operator-authored candidate; Forge records no invented evaluation result.

## Exact scope

| UI action | Product action and owner |
|---|---|
| Chat with a mission | Application/Missions asks Application/Projects to find or provision the managed Project and its shipped Janus version, then creates and attaches one Host-owned Mission Conversation. Presentation enters the real chat view. |
| New chat | The same named Application action creates and attaches one further Janus conversation in the current managed Project. |
| Select a real chat row | Presentation asks Application for that Project's Host-owned conversation projection and bounded history page, then follows its ordered event stream from that page's final sequence. |
| Send a non-empty message | Presentation sends one typed submit request. Conversation Host allocates the turn and attempt; Worker progress arrives through the existing relayed event stream; `ConversationTranscript` renders it. |
| Author a mission / Open an existing workspace | Existing launcher routes remain unchanged. |

The live view retains the three binding frames in
[Mission Chat experience v1](../design/mission-chat-experience-v1.md). Their content becomes
data-driven: the UI must not seed `Atlas Beta`, `Launch plan`, Janus/Naive rows, Proposer replies,
Approver replies, approvals, tools, or activity. The view shows the same empty/fresh structure
while it waits for a real first message.

Not included: a Project switcher, Project rename UI, folder picker in the chat route, version
picker, manual profile confirmation, chat rename, search, sorting, last-chat restore, retry,
cancel, trace links, a right drawer, more participants, Naive provisioning, or any new rail
destination.

## Ownership and stored facts

| Owner | Change |
|---|---|
| Application/Projects | Owns the managed-Project marker at `~/Forge/Projects/mission-chat-project.json`, containing only the Project GUID; its home is `~/Forge/Projects/<project-guid>`. `ProjectService` remains the sole writer of that Project's directory, assets, and manifest. It creates/validates that Project, its generated title, and the shipped immutable Janus v1.4 definition/package. A missing or invalid marker is repaired only by creating a fresh managed Project; an existing valid Project is never overwritten. |
| Application/Missions | Owns the one named product action that composes managed-Project resolution, Janus admission, Host conversation creation, and the existing Client Runtime attachment. It never writes a manifest, transcript, or Host store. |
| Conversation Host | Remains the sole conversation-store writer and sequence allocator. It adds the durable display title projection: `New chat` before the first turn; a bounded normalized prefix of the first user message thereafter. It does not call a model to make a title. It persists an optional generic expert label unchanged from Worker progress, and exposes one typed bounded event-page read for an existing Mission Conversation. |
| Conversation Worker | Copies the existing `PipelineTraceEvent.ExpertName` into that optional progress label for each started/completed expert step. It does not map mission names, choose a persona, or branch for Janus. |
| Client Runtime | Attaches the version's fixed profile automatically. It alone retains capability policy, bounded-operation confirmation, containment, execution, and cleanup. |
| Application Transport / Host | Adds only concrete typed actions and source-generated DTOs for start, list/open, transcript read/follow, and submit. No generic endpoint or untyped payload is allowed. |
| Presentation | Replaces `MissionChatPreview`'s local arrays and replies with typed application results and the existing durable transcript projection. It owns selection, composer text, focus, loading, and typed-error display only. |

The managed-Project marker is local Project-routing metadata, not a transcript store, account
record, new user setting, or a cross-context database. The Project manifest contains no
Conversation-store credential; Host persists no local Project path.

The shipped mission carries a user-facing release label `1.4` in addition to the existing
immutable numeric version identity. The label is presentation provenance only: admission,
pinning, package validation, and durable correlation continue to use the existing GUID, numeric
version, hash, and immutable package. Existing authored versions retain their existing `v<number>`
labels.

## Contract and flow

`StartMissionChat` is a concrete, zero-input Application action. It returns the created Project
session, the just-created pinned conversation, its read-only mission/version/profile projection,
and the real current chat directory. `CreateMissionChat` uses the active managed session and has
the same output. `OpenMissionChat` returns the selected pinned projection plus an ordered
conversation-event page. Conversation Host supplies that page through one named read contract,
bounded by the snapshot sequence returned before the read; Application starts its existing tail
from that sequence afterwards, so no event can fall between the page and live replay.
`SubmitMissionChatTurn` carries only session ID, conversation ID,
idempotent command ID, and text. Application derives every Project, version, package, profile,
attachment, and Host call.

The Host validates that a submitted conversation belongs to the session's Project and derives the
pinned launch. A caller cannot substitute a mission, version, package, profile, tool, title,
Project path, or attachment. `ConversationProgress` and `ConversationEvent` gain one optional
`ActorName` field. It is the immutable package expert name already present on
`PipelineTraceEvent`; Host stores it unchanged, and Presentation prefers it only for display over
the existing generic `Forge` participant label. This makes real `Proposer` and `Approver` rows
truthful without a Janus mapper or a new participant model. The existing `ConversationTailReader`
relays events through `ApplicationEventKind.ConversationEvent`; Presentation filters by selected
conversation and applies them through `ConversationTranscript`.

For a first managed chat, the sequence is: provision/validate Project -> create empty pinned Host
conversation -> automatically attach the fixed Client Runtime profile -> return the real fresh
view. A failed attachment leaves the durable chat real and visible with its typed status; there is
no profile fallback, silent retry, or fake ready state.

## Failure boundaries

| Failure | Result and recovery |
|---|---|
| Managed marker or Project is missing/corrupt. | Projects either repairs only the missing marker by creating a new managed Project, or returns a typed invalid-Project result for a malformed existing Project. It never overwrites a valid Project. Focused test proves the boundary. |
| Shipped Janus package is invalid or unavailable. | Projects returns a typed unavailable result; no conversation or attachment is created. Presentation shows that result without a substitute mission. |
| Create response is lost. | Host command identity remains idempotent. Presentation repeats the same command only after an explicit retry; it never creates a second chat silently. |
| Attachment fails. | The pinned conversation remains listed; Presentation reports that fixed access is not currently available. Client Runtime owns later recovery; there is no broader profile or remote fallback. |
| Page read or stream fails. | The Host's durable accepted/failed/interrupted fact remains canonical. Presentation shows the typed outcome and never invents a reply, approval, title, or activity row. The page read is a finite typed request; the existing tail reconnects from its last durable sequence. |

## Gates

| Gate | Disposition |
|---|---|
| Component fit | PASS. Each change advances its named owner; Presentation does not acquire Project, durable, or capability authority. |
| Security Architecture | PASS. Type-2 integration over existing boundaries: no public ingress, datastore owner, credential, or cross-store access changes. Application remains the Project boundary; Conversation Host remains the only Conversation-store writer; Client Runtime remains the only capability authority. |
| Engineering Philosophy | PASS. One named start action, one Project marker owner, one shipped-mission catalogue, one Host title rule, and existing typed command/event seams. The optional expert label is a direct generic trace fact, not a mapper or participant framework. No registry platform, generic dispatcher, profile knob, title model, fallback, or client-side durable cache. |
| Desktop design quality | PASS. This is Presentation and application behaviour only; Supervisor and native Host lifecycle are unchanged. |
| Presentation-surface parity | PASS. Each product action is a typed Application action with the same authorization and result for another surface; only layout/focus stay Presentation-specific. |
| UI Design System and interaction principles | PASS. The Workbench light-token reference remains binding. No local visual literals or new controls are introduced. Each existing interactive element gets its real named action; data that does not exist is omitted rather than styled as a fake control. |
| Native AOT | PASS requirement. New DTOs use existing source-generated contexts; no reflection, runtime serializer options, or warning suppressions. |

## Default-path acceptance

| Fact | Required observation |
|---|---|
| Artifact | `dist/forge-desktop/ForgeMission.Desktop`, launched with zero arguments. |
| Defaults | `MissionRuntime:Mode`, `MissionRuntime:BaseUrl`, `FORGE_API_ENDPOINT`, and `ConversationRuntime:BaseUrl` absent. Managed Project root is `~/Forge/Projects`; no manually selected folder or endpoint. |
| Dependency | Normal cloud Mission route and Supervisor-owned Kind Conversation bridge, with Host/Worker from clean `main` provenance. |
| Starting state | No managed marker or managed Project exists; test creates neither an unrelated Project nor a hand-prepared conversation. |
| Action | Choose Chat with a mission; send one message; create a second chat; select the first chat. |
| Outcome | One managed Project folder keyed by its GUID; Janus v1.4 is pinned; each real chat is listed; durable user/participant events appear in the selected transcript; no fake response is present. Record PASS/FAIL. |

## Claude implementation task

Claude may implement this phase only after returning a plan that keeps the file changes within:

- `ForgeMission.Application/Projects`, `ForgeMission.Application/Missions`, and their focused tests;
- `ForgeMission.Conversations.Contracts` and `ForgeMission.ConversationHost` only for the additive
  conversation-title, generic expert-label, and bounded conversation-event-page projections and
  their persistence/contract tests;
- `ForgeMission.ConversationWorker` only to copy its existing trace expert name into that generic
  label, with focused Worker coverage;
- `ForgeMission.Application.Transport`, `ForgeMission.Application.Host`, and contract/route tests;
- `ForgeMission.Presentation` and its focused tests.

Claude must not alter Desktop supervision, Core execution, the provider selection, the existing
authoring flow, or the binding UI structure. Its plan must name every changed action, DTO,
source-generated context, and failure test before edits begin.

## Done when

The three approved screens show only real Project/conversation data; Chat with a mission and New
chat create pinned Janus v1.4 conversations without setup choices; a sent message produces one
durable turn and its real event stream; fixed access is informational; all stated failures are
contained and tested; focused/full/AOT checks pass; and Codex independently records browser,
zero-argument Desktop, and default-path PASS against the binding references.
