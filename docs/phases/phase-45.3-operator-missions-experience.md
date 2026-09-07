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
| Application Transport/Host | Additive list/start/submit/retry/cancel/trace/profile-approval/hands-status/confirmation transport routes, channel/event tests. |
| Application | Conversation/query interfaces, profile-bound attachment and Application-event projection only; no presentation decision rules. |
| Documentation | Completion evidence and the [mission-hands visual contract](../images/phase-45/mission-hands-profile-states.svg); no new design-system authority. |

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

`CreateMissionConversation` returns the immutable version/profile that Application resolved; the
surface presents that exact read-only profile and sends only an acceptance acknowledgement—not a
profile value, tool selection or grant. `GetMissionHandsStatus` returns the durable profile,
`AwaitingHands`/tool/confirmation state and correlation. `ResolveMissionToolConfirmation` sends a
choice for one correlation to Application; Application returns it to the same Bob attachment.
Presentation never calls Bob, dispatches a tool, makes a policy decision or converts a denial into a
prompt. TUI uses the same actions and outcomes.

### Visual and interaction contract

Binding reference: [4a](../design/assets/forge-desktop-dark-implementation-reference-v1/4a-missions.png),
[4b](../design/assets/forge-desktop-dark-implementation-reference-v1/4b-mission-conversation.png),
and [4c](../design/assets/forge-desktop-dark-implementation-reference-v1/4c-forge-trace.png), at
1440×960, plus the binding [mission-hands profile states](../images/phase-45/mission-hands-profile-states.svg)
at 1440×960. The new wireframe owns the access indicator, fixed-profile approval, required
confirmation and `AwaitingHands` state; it does not add rail destinations, a tool dashboard or
permission controls. Select named `forge-desktop-dark` at workbench boundary. Map warm surfaces to
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
| Mission-hands profile states | Read-only Mission access indicator, creation-time fixed-profile approval, correlated tool status, required confirmation and `AwaitingHands`. | Per-tool checkboxes, profile narrowing, runtime escalation, capability dashboard, arbitrary host access and Full Access. |

At measured packaged usable viewport, selected initial action and composer are visible without
document scroll. Use bounded fluid token values. Test all rectangular corners, continuous resize,
long names/output, 200% text/zoom, keyboard focus, no clipping/overlap/horizontal scroll; inspect in
browser first then one packaged parity run.

The indicator is an informational chip, never a control. The creation approval appears only after
the operator selects an Approved version and repeats the profile in plain language; it has one
start/decline choice, no editable permissions. A tool row identifies the bounded operation and
correlation/status. Only Bob's `ConfirmationRequired` status reveals Allow/Deny; an out-of-profile
denial renders a non-interactive explanation, not an escalation prompt. `AwaitingHands` explains
that reconnecting the same conversation restores only its already-approved profile.

### Failure/accessibility/interaction contract

| State | Required treatment |
|---|---|
| List/query unavailable | Honest retryable availability state; no local fake list. |
| Candidate/stale version chosen | Picker prevents it; typed rejection refreshes list and states Approved-only rule. |
| Submit acceptance uncertain | Preserve draft/message and offer same-command retry; never append synthetic turn. |
| Failed/interrupted/cancelled | Preserve message/partial trace; retry or edit-resend; conversation remains usable. |
| Running/cancelling | `aria-live` status, disabled send, clear Cancel, no focus jump. |
| Fixed profile approval | Show exact version/profile and decline/start controls; no tool selection, narrowing or hidden default. Decline creates neither conversation nor local attachment. |
| AwaitingHands | Persistent, correlated status explains that no live local hands attachment exists; reconnect can resume the existing request, never grant a broader profile. |
| Confirmation required | Announce the bounded operation and its correlation; Allow/Deny calls one typed Application action. It is not a profile change. |
| Out-of-profile/denied | Render the typed denial and preserve trace; do not turn it into a confirmation, retry with broader access or client-side fallback. |
| Trace unavailable | Preserve selected conversation and render retryable trace error; never generic-route fallback. |

Controls are semantic, labelled, keyboard-operable, visibly focused and honestly disabled.
Cooper/Rams/Norman review is PASS: the profile answers the operator's immediate trust question
without exposing implementation detail; the profile chip is read-only and confirmation appears
only when Bob requires it; every control maps to one typed action; invalid/escalating states are
constrained. The named theme uses ordinary semantic tokens only: profile/status surfaces use
`--surface`/`--border`/`--text-muted`; confirmation uses `--warning`/`--warning-bg`; denial uses
`--danger`/`--danger-bg`; focus/action continues to use `--accent`/`--accent-contrast`.

### Default path, verification, and done when

Use zero-argument published Desktop, normal endpoints/bridge and disposable Project whose approved
version came through authoring. Observe the exact profile at creation, its durable indicator, a
bounded tool status, `AwaitingHands` then reconnect, a typed denial, any required confirmation,
two messages, failed/retry, running cancel, trace open/back exact anchor and reopen. Record default
facts/IDs/profile/correlation; fixture endpoints/events remain controlled evidence.

**Done when:** focused Presentation/transport tests pass; browser-first matrix is PASS against
4a–4c and mission-hands profile states; packaged parity is PASS; the default path records the
complete operator journey including profile/attachment states; Codex accepts completion against
this task.
