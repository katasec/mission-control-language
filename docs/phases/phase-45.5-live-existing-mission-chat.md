# Phase 45.5 — Live existing-mission chat

> **Status:** design locked for scope; implementation is blocked until the operator identifies the
> existing Project and Approved mission version used by the startup chat route. This task is about
> **chatting with an existing mission only**. It does not create, author, evaluate, publish, or
> change a mission.

## The task, in one sentence

Make the already-approved Phase 47 **Chat with a mission** window send to, receive from, and reopen
one real version-pinned Mission Conversation—without changing that window's UI.

## Exact user-visible contract

This is the complete owned slice. An implementation may not infer a nearby feature or improve the
screen while doing it.

| Exact action | Required result |
|---|---|
| Start Forge at the existing first-use screen and click the existing **Chat with a mission** button. | The existing Mission Chat window opens with its existing layout, labels, rail, list, participants, fixed-access display, transcript area, composer, send behavior, focus behavior, and styling. |
| Type normal text in the existing composer and send it using the existing control or Enter. | The exact text becomes a real durable user turn in the selected existing Mission Conversation. It is not a local message. |
| Wait for the mission response. | The transcript shows the real durable conversation events and response, not the static Proposer/Approver sequence. |
| Reopen the same chat. | Its durable history is replayed in order, then continues live from that point. Its pinned mission version remains the version originally used by that conversation. |

### Non-negotiable rejection rule

The Phase 47 visual reference is the fixed shell for this task:
[start](../design/assets/mission-chat-experience-v1/01-start.png),
[fresh chat](../design/assets/mission-chat-experience-v1/02-new-janus-chat.png), and
[active chat](../design/assets/mission-chat-experience-v1/03-active-chat.png), all at 1440 × 960.

If a proposed code change alters the existing screen's markup shape, visible words, styling,
spacing, rail, navigation, list/composer/transcript layout, keyboard behavior, focus behavior, or
accessibility behavior, it fails this task and must be discarded. Replacing the stand-in values and
handlers with authoritative values and handlers is allowed; changing the UI is not.

## Scope: what is and is not allowed

| Allowed | Forbidden |
|---|---|
| Replace the fixed local chat rows, selected transcript, fixed replies, and local send handler in `MissionChatPreview.razor` with authoritative data and named Application actions. | Create a mission; author, edit, evaluate, publish, approve, supersede, or select a new version; add an authoring control or authoring route. |
| Open an existing Project-owned Mission Conversation, submit its turn, replay its history, and tail its live events. | Add, remove, rename, move, style, or repurpose any visible UI control, including the rail, fixed-access display, mission/chat list, composer, or transcript. |
| Reattach a previously created conversation to its immutable pinned launch, including after a newer version has become Approved. | Re-resolve an old conversation to today's Approved version, or let Presentation provide a profile, package, definition, Project path, provider, credential, or authority. |
| Add only the concrete Application Transport actions needed by this screen. | Reuse legacy `PromptRequest` / `ConversationService`; that path is not the Project-owned, version-pinned Mission Conversation contract. |

## Required source fact — no invented mission

The Phase 47 `Atlas Beta` / `Janus v1.4` values are presentation stand-ins. They are not a shipped
Project or durable mission. A real Mission Conversation is Project-local and must use an existing
Approved mission version.

Before code starts, the operator must lock one source for the startup route:

1. the exact existing Project and its Approved mission version; **or**
2. use of the already-existing workspace-selection route before entering this exact chat window.

The implementation must not create a hidden default Project, pick an arbitrary local Project,
invent a bundled Janus mission, or silently fall back to static replies. Until this source is
identified, implementation does not begin. This is a missing product input, not an implementation
choice.

## Minimal implementation boundary

Only these four existing code projects are in scope. No new project, runtime, store, queue,
Desktop packaging change, or UI framework is permitted.

| Project | Exact responsibility |
|---|---|
| `ForgeMission.Presentation` | Keep the approved screen unchanged; replace local stand-in state with the named transport actions and authoritative event projection. `Home.razor` owns only screen/session view wiring. |
| `ForgeMission.Application` | Verify the current application session may use the Project-owned conversation; resolve authoritative stored launch facts; open/replay/tail that conversation; submit a turn through the existing durable owner. |
| `ForgeMission.Application.Transport` | Define the small, source-generated typed requests/responses and client calls for opening/replaying a selected Mission Conversation and submitting one message. |
| `ForgeMission.Application.Host` | Expose one concrete typed HTTP action for each transport operation; no generic dispatcher and no durable-store access. |

`ForgeMission.ConversationHost`, `ForgeMission.ConversationWorker`, `ForgeMission.ClientRuntime`,
mission authoring, evaluation, persistence schema, Desktop, and deployment are out of scope. The
existing Host owns durable order, event sequence, command idempotency, and launch storage; this
task must use those facts, not duplicate them.

## Required contracts and ordering

1. **Open/replay:** Presentation asks Application to open a specified Project-owned conversation.
   Application verifies ownership and returns safe view facts plus a transcript projection and its
   exact durable cursor. Application starts/takes ownership of the live tail without a replay race.
2. **Live events:** Presentation applies only events for that opened conversation, in durable
   sequence after the returned cursor. It never fabricates a participant reply.
3. **Send:** Presentation submits only `(conversation identity, command identity, text)` through
   the named Application Transport action. Application derives the Project, pinned launch,
   version, hands profile, package, and provider facts from owned state; no caller supplies them.
4. **Reopen:** Application attaches to the stored immutable launch for the conversation, even when
   that version is no longer the one currently Approved for new conversations.

The existing durable `SubmitMissionTurn` contract and Host event stream are the lower-level owners.
The missing work is the narrow Application-owned surface contract, session/Project check, safe
replay handoff, and Presentation binding. A subscription begun after sending is insufficient: it
can lose replayed or in-flight events.

## Failure rules

| Condition | Required outcome |
|---|---|
| No operator-approved existing Project/mission source | Do not begin implementation and do not invent data, a default mission, or a fallback route. |
| Conversation does not belong to the current Project/session | Application rejects it before any Host call; Presentation receives the existing application-operation failure path, not a fake transcript. |
| Send is repeated with the same command identity | Existing durable idempotency determines the single turn outcome. |
| Host or transport fails | Preserve the existing transcript; report the explicit existing application-operation failure. Do not append fixed success replies or silently retry. |
| A newer version is approved | Existing conversations retain and reopen their original pinned version; only a separately scoped new-conversation path may select the newer version. |

No new error visual treatment is part of this task. If the existing application-operation failure
path cannot represent a real failure without a UI change, reject the implementation plan rather
than inventing one.

## Gates before implementation

| Gate | Locked answer |
|---|---|
| Component purpose | PASS: Presentation renders the fixed screen; Application owns Project/session and conversation use; Host owns HTTP binding; Conversation Host remains durable owner. |
| Security Architecture | PASS: no new public tier, datastore, credential, or cross-context data access. Application Host remains a typed Tier-1/2 boundary with no Conversation data-plane credential. |
| Engineering Philosophy | PASS: one concrete action per operation; no new knob, generic dispatcher, fallback, UI-local durable state, or duplicate transcript owner. |
| Presentation-surface parity | PASS when the open and send operations are named `Application.Transport` actions with the same authorization/outcome semantics available to another surface. |
| Desktop UI gate | PASS only when the browser inspection matches the three binding references and confirms the existing keyboard/focus behavior remains intact. |
| Default-path acceptance | Applies. The proof uses the published zero-argument Desktop artifact, normal dependencies and absent overrides, plus a dedicated existing Project with an already Approved mission. Setup of that Project is not authoring work in this task. |
| Native AOT | Source-generated JSON contexts only; no reflection, runtime type discovery, untyped JSON options, new package, or new warning suppression. |

## Required tests and acceptance

| Layer | Required proof |
|---|---|
| Presentation | The fixed screen sends through the named action, renders authoritative replay/live events, retains existing Enter/Shift+Enter/focus/live-region behavior, and contains no fixed reply sequence. Visual/DOM comparison rejects UI drift. |
| Application and transport | Project/session ownership, source-generated DTOs, safe replay-to-tail cursor handoff, immutable pinned-version reattach, and idempotent send are covered by focused tests. |
| Failure containment | Cross-Project/session open is rejected before Host access; a transport/Host failure preserves the transcript and returns the explicit operation failure. |
| Browser | Codex opens the exact existing **Chat with a mission** route, sends a message in its existing composer, observes the real response, and compares all owned states at 1440 × 960 and the required responsive/focus matrix. |
| Default path | From the canonical published Desktop artifact, launch with zero arguments and normal defaults; use the named safe existing Project/Approved mission; send, receive, close/reopen, and observe the persisted version-pinned history. |

## Done when

The only visible change is that the already-approved Mission Chat screen now displays a real
existing-mission conversation instead of its local stand-ins. The exact existing button and
composer send a durable turn; authoritative replies arrive and replay after reopen; the pinned
version does not drift; all tests and default-path/browser evidence pass; and any UI drift rejects
the change.
