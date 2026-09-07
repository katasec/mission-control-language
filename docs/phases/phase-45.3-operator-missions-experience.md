# Phase 45.3 — Operator Missions experience

> **Status:** implementation-ready design; depends on [45.2](phase-45.2-durable-conversation-turns.md).

## Task 3 — make Missions a persistent conversation experience

### Why and component fit

This advances Presentation’s rendering/navigation/focus purpose and Application/Host’s concrete
shared-action boundary. It reuses the existing Project workspace, rail, Application channel, SSE
subscription and trace renderer. It creates no domain state in Blazor, second shell, generic route
dispatcher, or new rail entry.

### Affected components and files

| Component | Planned files |
|---|---|
| Presentation | `Pages/Home.razor`, `Components/WorkbenchView.cs`, `WorkbenchRail.razor`, reshape `MissionsView.razor`, `MissionPicker.razor`, `MissionComposer.razor`, `RunTraceView.razor`, `ConversationTranscriptView.razor`, relevant CSS/tests. |
| Application Transport/Host | Additive list/start/submit/retry/cancel/trace transport routes, channel/event tests. |
| Application | Conversation/query interfaces and Application-event projection only; no presentation decision rules. |
| Documentation | Completion evidence in this spoke; no new design-system authority. |

### Routes, state, and shared actions

`WorkbenchView` retains rail states Explorer, Missions, Settings; it replaces legacy RunTrace with
`MissionConversation` and `TurnTrace` document states. Neither is a rail entry. Opening Project
sets Missions. Presentation-only state is:

```text
MissionsList
MissionConversation(conversationId, selectedTurnId?)
TurnTrace(conversationId, turnId, turnAttemptId, traceOrigin)
ExplorerAuthoring(missionId, versionId?)  // reached through Explorer, defined in 45.4
```

Every action calls typed transport: ListMissionConversations, ListApprovedMissionVersions,
CreateMissionConversation, SubmitMissionTurn, RetryMissionTurn, CancelMissionTurn,
GetMissionConversation, GetMissionTurnTrace. Responses supply IDs/status/evidence; Presentation
only renders. A TUI can invoke each with identical authorization/outcome/failure, so
Presentation-surface parity is **PASS by design**.

### Visual and interaction contract

Binding reference: [4a](../design/assets/forge-desktop-dark-implementation-reference-v1/4a-missions.png),
[4b](../design/assets/forge-desktop-dark-implementation-reference-v1/4b-mission-conversation.png),
and [4c](../design/assets/forge-desktop-dark-implementation-reference-v1/4c-forge-trace.png), at
1440×960. Select named `forge-desktop-dark` at workbench boundary. Map warm surfaces to
`--bg`/`--surface-sunken`/`--surface`/`--surface-active`, cream ink to
`--text`/`--text-muted`, ember action/selection to `--accent`/`--accent-soft`, and state to
success/danger/warning tokens; define light/dark maps in `forge.css`. Required pairs: text/bg,
muted/surface, accent/accent-contrast, success/success-bg, danger/danger-bg, disabled/surface.
No component-local sampled values.

| Reference slice | Owned | Deferred / omitted |
|---|---|---|
| 4a | Conversation list, Approved-only picker, creation, pin explanation, Author a mission link. | Search/filter sophistication, OCI catalog, version change in an existing conversation. |
| 4b | Transcript, evidence row, failed/retry/edit-resend, running/cancel, disabled/re-enabled composer. | Human gate/suspend-resume, rich artifact preview, token-delta transcript. |
| 4c | Read-only turn trace, origin message/answer anchors, chronological events, exact Back. | Export/copy tools, trace filtering/search. |

At measured packaged usable viewport, selected initial action and composer are visible without
document scroll. Use bounded fluid token values. Test all rectangular corners, continuous resize,
long names/output, 200% text/zoom, keyboard focus, no clipping/overlap/horizontal scroll; inspect in
browser first then one packaged parity run.

### Failure/accessibility/interaction contract

| State | Required treatment |
|---|---|
| List/query unavailable | Honest retryable availability state; no local fake list. |
| Candidate/stale version chosen | Picker prevents it; typed rejection refreshes list and states Approved-only rule. |
| Submit acceptance uncertain | Preserve draft/message and offer same-command retry; never append synthetic turn. |
| Failed/interrupted/cancelled | Preserve message/partial trace; retry or edit-resend; conversation remains usable. |
| Running/cancelling | `aria-live` status, disabled send, clear Cancel, no focus jump. |
| Trace unavailable | Preserve selected conversation and render retryable trace error; never generic-route fallback. |

Controls are semantic, labelled, keyboard-operable, visibly focused and honestly disabled.
Cooper/Rams/Norman review is PASS: transcript serves operator goal; creation is contextual; every
control maps to a real action; invalid states are constrained.

### Default path, verification, and done when

Use zero-argument published Desktop, normal endpoints/bridge and disposable Project whose approved
version came through authoring. Observe picker, pin, two messages, failed/retry, running cancel,
trace open/back exact anchor and reopen. Record default facts/IDs; fixture endpoints/events remain
controlled evidence.

**Done when:** focused Presentation/transport tests pass; browser-first matrix is PASS against
4a–4c; packaged parity is PASS; default path records the complete operator journey; Codex accepts
completion against this task.
