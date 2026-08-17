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

### Task 3 — Adopt it in Forge Desktop — done 2026-08-17

`Home.CurrentActivity()` drives the ordinary turn's shared activity (completed-only tool rows), and the
durable view renders `Typing` / unfinished `ToolCall` through the same component. Packaged macOS
Desktop captured Thinking → Working → Streaming → cleared; `forge.css` is now the sole `pulse`
keyframe. Verified — see
[completed record → Task 3](phase-43.18-shared-conversation-activity_completed.md#task-3--adopt-it-in-forge-desktop-done-2026-08-17).

### Task 4 — Verify the narrow boundary

Run renderer and adapter tests, then verify both Rooms and the packaged macOS Desktop against the
states above. Review the final dependency graph and diff for the following negatives: no new
`IClientRuntimeChannel` event, no Desktop reference to `ForgeMission.Rooms`, no new server route,
and no modification to 43.17's deferred event-delivery work.

**Done when:** `dotnet build src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx` pass
with zero failures, and the manual observation records a visible chat-surface activity state before
the first answer text.

**Status (2026-08-17): verification run, pending review.** Ran on
`codex/phase-43.18-verify-narrow-boundary` from `main` (`76c71b9`). Results below are the evidence
under review; no completed record is written and the phase is not marked complete until approved.

| Check | Result |
|---|---|
| `dotnet build src/ForgeMission.slnx` | Build succeeded. 0 Warning(s), 0 Error(s); `ForgeUI -> ForgeUI.dll` present. |
| `dotnet test src/ForgeMission.slnx` | 0 failed, 749 passed, 11 pre-existing live-provider skips. ConversationHost 2m26s and Runner 2m7s (machine load from the packaged-app work; passing, just slow). |
| Rooms live | `convo-activity-thinking` "@assistant is thinking…" → `convo-activity-working` "@assistant Thinking" (existing progress label) → cleared on answer; both `role="status"` + `aria-live="polite"`; `.agent-thinking`/`.thinking-dots` count 0 throughout; `show thinking` expanded one `.trace-panel` with its Answerer/Verifier PASS rows. |
| Packaged Desktop | Thinking → Working (`Running sleep 8; echo verified…`, no running `.tool-row`) → Thinking (tool done, no text yet) → Streaming → cleared, with `✓ Ran sleep 8; echo verified` retained; every state `role="status"` + `aria-live="polite"`. |
| Negatives | 43.18 touches 16 source files (`8a5dd7c..HEAD`). Transport diff empty (no new channel event); no `ForgeMission.Rooms` reference from any Desktop/Presentation project; no route additions; `ClientRuntimeEventHub.cs`, `DesktopLifecycleTests.cs`, `DesktopSupervisorHostBoundaryTests.cs` absent from the diff, and `Home.razor`'s hunks touch only activity rendering plus dead CSS. |
| Shared animation | `@keyframes pulse` has exactly one source definition (`forge.css:345`); `thinking-bounce` and `convo-activity-blink` intact. |

Not claimed: live durable Janus. No `ConversationHost` was started and no infrastructure was added,
per the Task 3 decision that the durable adapter stays test-proven. The local `authbilling_db`
workaround was not needed — the dev container already held the database from Task 2.

## Completion condition

Rooms and Desktop use one renderer for active conversation work. Desktop visibly communicates
thinking, tool work, and response generation within the transcript; Rooms retains its existing
full-fidelity trace. No new protocol, trace abstraction, lifecycle work, or speculative framework
is introduced.
