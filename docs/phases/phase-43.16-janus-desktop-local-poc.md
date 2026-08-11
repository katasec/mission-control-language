# Phase 43.16 — Janus Desktop local PoC

**Status: Design approved 2026-08-11 — ready to hand off for implementation.** Part of
[Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Builds on the completed Desktop shell
([43.11](phase-43.11-wasm-photino-shell.md)) and Janus negotiation proof
([43.15](phase-43.15-janus-inter-agent-mission.md)). It deliberately does **not** depend on
`sdlc-agent` publishing, OCI, the hosted catalog, or the full IDE workbench in 43.4.

## The proof

Attach **Janus** in Forge Desktop, submit a local task, then watch the real multi-provider mission
as a chronological group conversation:

```text
You                  task
Proposer             implementation plan (or a clarifying question)
Approver             approved / requested changes / answer to the question
Proposer             revised plan, if needed
Implementer          tool activity and completion — only after approval
```

This is the first product proof of “missions attach instead of models.” A user does not select a
model; they select the collaboration shape that will handle their task. Janus happens to use
OpenAI for `Proposer`/`Implementer` and Anthropic for `Approver` today, but those profiles are
deliberately swappable in `missions/janus/forge.toml`; proving the interaction matters, not which
vendor occupies a role.

The session is **observational** after the user submits a task. It has no mid-negotiation reply,
approve/deny, suspend, or escalation control. Those are the later mission-level human-in-the-loop
work in [43.5](phase-43.5-human-in-the-loop.md), not a shortcut in this PoC.

## Locked decisions

| Decision | Resolution |
|---|---|
| Runtime target | Local only. Start Janus through a local `forge serve` Mission Runtime; do not publish an OCI artifact, edit `StaticMissionCatalog`, or call the hosted API. |
| Picker | Retain the familiar composer-adjacent trigger from [43.3](phase-43.3-mission-attach-point.md), but it is a **Mission** picker. The local PoC exposes Janus as the real selectable mission; it must not show an entry that this local runtime cannot run. |
| Input | The composer text is Janus’s single incoming `goal`, following the existing `forge serve` convention. The token-bucket text remains only the CLI/demo default, not the Desktop task. |
| Conversation | Render the existing pipeline chronology as a group chat, not as a raw console log and not as the full dockable debugger workbench. Every completed expert step is a named participant message; a running step is a transient typing/status row. |
| Approval | `Approver` pass renders **Approved**. Fail renders **Requested changes** plus its reason, then the next `Proposer` attempt. Exhaustion renders **Not approved** plus the final reason and never renders an `Implementer` action. |
| Tool calls | Tool call/status rows belong beneath the active `Implementer` message. The current Client Runtime authorization and dispatch path stays authoritative; Janus only decides to request the capability. |
| Session lifetime | Keep the in-memory, current-window transcript only. A new composer submission is a new Janus run; no cross-run conversation semantics or persistence are added. |

## UX baseline

The existing [mission-picker mockup](../images/phase-43.3/mission-picker-open.png) is the right
composer affordance: a compact pill beside **Send**, a checkmark for the selected mission, and no
model/provider controls. It needs a Janus entry, not a fresh control design.

The richer [trace-workbench mockup](../brainstorm/images/web-console-concept.png) is explicitly
**not** this deliverable. It remains the later 43.4 direction. This PoC uses the existing chat
surface and makes one complete Janus exchange legible before adding code panes, docking, source
anchors, or human gates.

Each run displays, in order:

1. The user’s task as their own bubble.
2. A labelled `Proposer` message when its step completes.
3. A labelled `Approver` message, with its approval/revision state visually explicit.
4. Further attempt groups in the same chronological thread if the approver rejects.
5. An `Implementer` message and its existing Read/Edit/Write/Bash activity only after approval.
6. A terminal success, failure, or **Not approved** state.

Showing the Approver’s passing text even when it repeats the plan is intentional: the user must be
able to inspect the actual approval trail rather than merely trust an inferred green indicator.

## Transport design

`forge run … --steps` already has the correct semantic information, and `PipelineRunner` already
emits `OnStepStart`/`OnStepComplete`. The gap is transport: Desktop’s current standard
`/v1/messages` client receives text deltas and tool calls, but no expert identity or verdict. The
UI must never parse console text to recover those facts.

Add an additive, Forge-native **local mission-trace stream** for the Client Runtime. It is separate
from the spec-bound Anthropic/OpenAI `/v1/*` doors: those doors keep their current compatibility
contract, and external coding agents do not receive Forge-specific event types.

The stream’s contract carries only facts the UI needs to render the session:

```text
MissionTraceEvent
  kind: step_started | step_completed | tool_use | tool_result | completed | failed
  expert_name: Proposer | Approver | Implementer (when applicable)
  attempt: positive integer
  text / status / reason: copied from the completed StepEnvelope
  tool metadata: existing tool id, name, arguments/result (when applicable)
```

The Mission Runtime produces `step_started` and `step_completed` from the existing
`PipelineRunOptions` callbacks; the Client Runtime relays them as its own typed SSE events to the
WASM UI. Tool execution remains the established Client Runtime round trip, so the runtime never
gets filesystem or terminal access. All JSON at the new boundary uses source-generated contexts;
no reflection-based serializer options are introduced into AOT code.

## Build tasks

1. **Make Janus serveable as the local chat mission.** Align its entry parameter with the `goal`
   convention while preserving the current CLI demo default, add the local agent configuration,
   and prove a Desktop composer prompt becomes that mission’s goal. Do not change provider profiles
   or add a provider-selection UI.

2. **Expose the local mission-trace stream.** Add the Forge-native local route and its
   source-generated `MissionTraceEvent` contract. Wire the existing `PipelineRunner` callbacks so
   each Janus expert start/completion, retry attempt, terminal failure, and tool boundary is emitted
   in order. Keep `/v1/messages` and `/v1/chat/completions` byte-for-byte compatible.

3. **Consume and relay typed trace events in the Client Runtime.** Extend the local
   `MissionRuntimeSession` path and `ClientRuntimeEvent` contract without allowing the Presentation
   layer to call the Mission Runtime directly. Reuse the existing capability dispatcher for every
   tool request and result.

4. **Render the Janus group session.** Replace the single anonymous assistant response for a Janus
   run with named participant bubbles, attempt markers, approval/revision state, transient running
   rows, and Implementer-owned tool rows. Keep the normal current chat rendering for missions that
   do not supply trace events.

5. **Verify the proof.** Add unit/transport tests for event ordering, rejection-without-
   Implementer, and client rendering state. Then live-run a local Janus session through the browser
   Desktop surface using real Anthropic and OpenAI providers; observe the named conversation and a
   real approved Implementer tool call against the opened workspace. Re-check once in the packaged
   Photino desktop build.

## Done when

- Janus is selectable as a **local mission** in the Desktop composer; no cloud catalog or OCI
  publish was needed.
- A real composer task maps to Janus’s `goal` and produces a live, chronological Proposer/
  Approver/Implementer session in the UI.
- A rejection/retry is visibly distinguishable from approval, and an exhausted negotiation shows
  **Not approved** with the final reason and no tool execution.
- On an approved run, real tool calls appear beneath Implementer and execute only through the
  authorized Client Runtime path.
- The existing external `/v1` wire contracts remain compatible, the full test suite passes, and
  the browser proof is visually confirmed once more in packaged Photino.

## Explicitly deferred

- Cloud Janus, OCI publishing, hosted catalog registration, and mission metadata/curation.
- Switching providers from the Desktop UI; edit `forge.toml` to run a comparison instead.
- Human mid-conversation intervention, suspend/resume, or escalation (43.5).
- The trace workbench’s code pane, source anchors, docking, and persistent session store (43.4).
- Cross-run conversational memory and multi-mission conversation transfer.
