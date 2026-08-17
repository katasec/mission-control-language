# Phase 43.18 — Shared conversation activity surface

> **Status: design ready for implementation (2026-08-17).** Part of
> [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Build one small, shared activity visual
> in the chat surface. It replaces neither product's conversation model, event transport, or rich
> trace view.

## Outcome

After a user sends a prompt, the conversation itself immediately says what is happening: an agent
is thinking, using a tool, or producing a response. The visual is the same component in Forge
Rooms and Forge Desktop. It gives Desktop the missing work-in-progress cue without creating a
second visual design for the same concept.

Rooms keeps its existing `show thinking` trace exactly as it is. That trace is richer, durable
evidence attached to completed answers; this task does not pretend that Desktop currently has the
same information.

## Read boundary

Read this spoke first. Then read only:

1. `src/ForgeUI/Shared/RoomConversation.razor` and its activity styles in
   `src/ForgeUI/wwwroot/css/forge.css` for the Rooms adapter.
2. `src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor` and
   `src/ForgeMission.ClientRuntime.Presentation/Components/ConversationTranscriptView.razor` for the
   Desktop adapters.
3. [Durable conversations](../design/durable-conversations.md) for the event truth each client
   may display.
4. [Security Architecture](../design/security-architecture.md) and
   [Engineering Philosophy](../design/engineering-philosophy.md) for the design-gate review below.

Do **not** read or start the deferred bounded-delivery/progressive-rendering work in 43.17, the
43.4 workbench, or a new streaming/trace design. They are explicitly outside this task.

## Locked design

### One renderer, two thin adapters

Create `ForgeMission.ConversationPresentation`, a small Razor Class Library referenced by both
`ForgeUI` and `ForgeMission.ClientRuntime.Presentation`. It owns only:

- `ConversationActivity.razor` — the in-transcript visual;
- `ConversationActivityKind` with exactly `Thinking`, `Working`, and `Streaming`; and
- `ConversationActivityState(string Actor, ConversationActivityKind Kind, string? Detail)`.

The component selects its own icon/cursor/dot treatment from `Kind`; callers do not receive a bag
of animation, colour, cursor, or layout switches. It uses the existing Forge CSS tokens and has an
accessible `role="status"` / polite live announcement. Its animation honours reduced-motion
preferences.

Each product retains ownership of its own state and maps only the facts it already has to that
small state:

| Surface | Existing fact | Shared activity state |
|---|---|---|
| Rooms | `AgentThinking` | `Thinking` for that agent |
| Rooms | `AgentProgress` | `Working` with the existing progress label |
| Desktop mission turn | prompt sent, before a response delta | `Thinking` for the selected mission |
| Desktop mission turn | running `ToolCallStatus` | `Working` with the existing tool label |
| Desktop mission turn | first and subsequent `MissionTextDelta` | `Streaming` for the selected mission |
| Desktop durable/Janus transcript | existing typing and tool-progress entries | the same states for the emitting participant |

The shared renderer appears in the same transcript flow as the conversation it describes. A disabled
`Send` button remains an input-control safeguard; it is not the work-in-progress indicator.

### Explicit non-goals

This phase does **not**:

- change `IClientRuntimeChannel`, SSE, event queueing, replay, coalescing, or render cadence;
- add reasoning capture, token streaming, a generic activity/event bus, or a common trace schema;
- move Rooms' `PipelineTrace` / `show thinking` evidence into the component, or fabricate that
  evidence in Desktop;
- change durable conversation persistence, authorization, Supervisor/Host ownership, or session
  lifecycle; or
- create a reusable UI framework beyond this one concrete renderer and its three-state input.

If Desktop later receives real trace facts and needs a richer trace surface, that is separately
designed work under [43.4 — IDE trace surface](phase-43.4-ide-trace-surface.md), not an extension
silently absorbed here.

## Design gate

| Gate | Answer |
|---|---|
| Bounded context / data ownership | No new context or datastore. Rooms and Client Runtime retain their own conversation state; the library renders an already-known visual state only. |
| Public entry point / tier change | Not applicable. No route, ingress, service boundary, or cross-context data access changes. |
| Credentials | Not applicable. The renderer receives no credential and makes no request. |
| Type | Type 2 presentation reuse. The Razor Class Library may be removed by inlining the component without changing a conversation contract. |
| Failure ownership | Each existing surface owns missing, stale, and failed activity data. The renderer has no background work, subscription, retry, or fallback claim. |
| Engineering-philosophy result | One concrete shared component eliminates current duplicate activity markup. Its fixed three-state model rejects speculative options, a generic event framework, and false trace parity. |
| Desktop quality gate | This is presentation only. The native adapter, Host, Supervisor, and Client Runtime are untouched; proof is component/adapter tests plus a packaged Desktop observation after Send. |

## Dependency-ordered work

### Task 1 — Build the shared activity renderer — done 2026-08-17

`ForgeMission.ConversationPresentation` (RCL, in `ForgeMission.slnx`) ships `ConversationActivity`,
`ConversationActivityKind`, and `ConversationActivityState`; the tokenized `.convo-activity*` styles
live in `forge.css`. Build + tests verified, gate passed — see
[completed record → Task 1](phase-43.18-shared-conversation-activity_completed.md#task-1--build-the-shared-activity-renderer-approved-2026-08-17).

Contract Tasks 2–3 map onto — fixed and safe to build against:
`ConversationActivityState(string Actor, ConversationActivityKind Kind, string? Detail)`, rendered as
`{Actor} {Detail ?? defaultPhrase(Kind)}` with defaults `is thinking…` / `is working…` /
`is responding…`.

### Task 1.5 — Restore Rooms build coverage — done 2026-08-17

`RoomConversation.razor` declares `@using PipelineTraceEvent = ForgeUI.Models.PipelineTraceEvent`, so
its legacy completed-message trace binds explicitly to the view model despite Core's same-named
runtime type being in scope for `StepEnvelope`. `ForgeUI` is in `ForgeMission.slnx`, so the host
builds with the solution. Verified — see
[completed record → Task 1.5](phase-43.18-shared-conversation-activity_completed.md#task-15--restore-rooms-build-coverage-done-2026-08-17).

Task 2 is unblocked: `RoomConversation.razor` compiles, and any regression in it now fails the
normal solution build.

### Task 2 — Adopt it in Forge Rooms — done 2026-08-17

`RoomConversation.razor` renders `ConversationActivity` from its computed `Activity` mapping; the
legacy `agent-thinking` / `thinking-dots` rules are gone and `.convo-activity*` is Rooms' sole
activity styling. Live DOM observation and build/tests verified — see
[completed record → Task 2](phase-43.18-shared-conversation-activity_completed.md#task-2--adopt-it-in-forge-rooms-done-2026-08-17).

### Open, unrelated to the remaining tasks' code: local `authbilling_db` bootstrap gap

`scripts/db/init/01-init.sql` creates only `forge_rooms`, and `make dev-up` applies only the Rooms EF
migrations — but ForgeUI also bootstraps the separate `authbilling_db` context (42.6) at startup and
aborts with `3D000: database "authbilling_db" does not exist`. A fresh clone hits this on its first
`dotnet run`; Task 2's observation needed the database created by hand in the local container
(`CREATE DATABASE authbilling_db OWNER postgres`, `GRANT CONNECT … TO forge_app`, `GRANT USAGE,
CREATE ON SCHEMA public TO forge_app`). Anyone doing a local Rooms run for Task 4 will hit it too.
Left as a documented development-environment issue, not fixed inside 43.18.

### Task 3 — Adopt it in Forge Desktop — implemented, pending review

**Status (2026-08-17):** implemented on `codex/phase-43.18-desktop-adopt-activity-design`
([PR #64](https://github.com/katasec/mission-control-language/pull/64)), awaiting review. Ordinary
turns render `Home.CurrentActivity()`'s state and only completed tool rows; durable Typing and
unfinished `ToolCall` entries render the shared component, with completed rows unchanged. Nine focused
bUnit cases added. Packaged macOS Desktop captured Thinking → Working → Streaming → cleared, with the
completed tool row retained. Evidence goes into the completed record on approval, not before.

Also removed as dead: `Home.razor`'s `.tool-glyph.running` and its orphaned `@keyframes pulse` — the
latter was a same-name, different-curve duplicate of `forge.css`'s `pulse`, so as global CSS it could
override the shared renderer's animation. `forge.css` is now the single definition. In the durable
view, `.convo-typing`, `.convo-tool-glyph.running`, and their sole-user `@keyframes convo-pulse` are
gone.

Replace Desktop's input-only sending cue with `ConversationActivity` in the active turn/transcript.
Use existing prompt, `ToolCallStatus`, text-delta, typing, and tool-progress state only; do not add
an event or alter event timing. Apply the same renderer to ordinary mission turns and durable Janus
transcript activity.

The `ForgeMission.ClientRuntime.Presentation` project references the shared RCL. The two adapters
are deliberately direct projections, not a common activity store:

| Surface | Existing state | Shared state / rendering rule |
|---|---|---|
| Ordinary mission turn | `activeTurn` exists; no active tool and no response text | `Thinking` for `selectedMission.Name`. |
| Ordinary mission turn | `openToolRow` exists from a running `ToolCallStatus` | `Working` for `selectedMission.Name`, with `openToolRow.Label()` as detail. This shared activity replaces the current running tool row; completed tool rows remain transcript history. |
| Ordinary mission turn | `activeTurn.AssistantText` becomes non-empty from `MissionTextDelta` | `Streaming` for `selectedMission.Name`. A running tool takes precedence if both facts are momentarily present. |
| Durable Janus transcript | `ConversationEntryKind.Typing` | `Thinking` for `ParticipantLabel(entry.Participant)`. |
| Durable Janus transcript | unfinished `ConversationEntryKind.ToolCall` | `Working` for `ParticipantLabel(entry.Participant)`, using the existing tool-running label as detail. A completed tool row remains as it is. |

Durable transcript entries do not expose a distinct live-delta fact, so Task 3 does not fabricate a
`Streaming` state for them. It renders the existing typing and unfinished-tool facts only.

Add focused bUnit coverage by extending `HomeSessionOperationTests`' existing fake
`IClientRuntimeChannel` and `ConversationTranscriptViewTests`: normal mission thinking → working →
streaming state selection, durable typing/unfinished-tool activity, and retention of a completed tool
row. The final user-visible proof is a `make desktop-publish` macOS packaged Desktop observation;
the Host, Supervisor, Client Runtime, transport, and event-delivery cadence remain untouched.

| Desktop quality gate | Answer |
|---|---|
| Product behaviour | The conversation transcript visibly states a selected mission/participant is thinking, working, or responding before a normal answer is complete. |
| Owner / process boundary | Presentation owns this rendering-only projection inside its existing render pass. No Host, Supervisor, Client Runtime, or Mission Runtime process boundary changes. |
| Adapter observation | `Home` already holds `activeTurn`, `openToolRow`, and response text; `ConversationTranscript` already projects typing/tool entries. No new `IClientRuntimeChannel` event is needed. |
| Replacement boundary | The Presentation project consumes the small shared RCL; it gains no runtime, process, credential, transport, or service ownership. |
| Proof | Focused adapter tests plus the named packaged macOS Desktop observation after Send. |

| Security / engineering gate | Answer |
|---|---|
| Tier, data, identity, credentials | Not applicable: no ingress, request, store, identity, or credential path changes. |
| Failure ownership | Existing turn/transcript state owns missing or terminal activity; the renderer has no subscription, retry, or fallback work. |
| Scope containment | No new event bus, trace schema, or streaming mechanism; the adapter uses only facts already rendered or held by Presentation. |

**Done when:** in the packaged Desktop, Send immediately creates a visible in-chat `Thinking`
state; a received tool-running event changes it to `Working`; and a text delta changes it to the
streaming cursor. Normal mission and Janus paths both use the shared component.

### Task 4 — Verify the narrow boundary

Run renderer and adapter tests, then verify both Rooms and the packaged macOS Desktop against the
states above. Review the final dependency graph and diff for the following negatives: no new
`IClientRuntimeChannel` event, no Desktop reference to `ForgeMission.Rooms`, no new server route,
and no modification to 43.17's deferred event-delivery work.

**Done when:** `dotnet build src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx` pass
with zero failures, and the manual observation records a visible chat-surface activity state before
the first answer text.

## Completion condition

Rooms and Desktop use one renderer for active conversation work. Desktop visibly communicates
thinking, tool work, and response generation within the transcript; Rooms retains its existing
full-fidelity trace. No new protocol, trace abstraction, lifecycle work, or speculative framework
is introduced.
