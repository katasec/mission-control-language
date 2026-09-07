# Forge Desktop — dark implementation reference v1

> **Status:** proposed product-design export, not shipped behaviour.
> **Exported:** 2026-09-07 from [Claude Design — MCL UI mockups, section 4](https://claude.ai/design/p/778ff889-2ad8-4f6e-9280-d6d1fa31aa84?file=MCL+Mockups.dc.html).
> **Default-path acceptance:** N/A — this is a static design artifact, with no runtime or user-path change.

## Purpose and authority

This is the durable implementation reference for the proposed Forge Desktop mission-conversation experience. It captures the high-fidelity dark-mode frames created in Claude Design and the interactions they make visible. It is not an implementation plan, a runtime contract, or evidence that any proposed capability is shipped.

Use it with [Desktop Interaction Principles](desktop-interaction-principles.md), [UI Design System](ui-design-system.md), and the durable-conversation design before implementing. A planning agent should turn this reference into the relevant phase hub and spoke before code starts.

## Product model

There is one combined Project workspace, not separate author and operator products. Authoring and operating share Project context, mission versions, evaluation evidence, conversations, turns, and traces.

The persistent rail is exactly:

- Project Explorer
- Missions
- Settings

There is no dashboard, inbox, standalone Runs rail, expert registry, or human-gate workflow in this reference.

## Visual language

- Desktop viewport: 1440 × 960; the publish-ready state is a 1440 × 392 state of the authoring frame.
- One dark theme: warm near-black and charcoal surfaces; restrained cream text; muted slate borders; ember/orange only for primary or meaningful status.
- Dense but calm functional hierarchy; consistent 8px rhythm.
- Sans for UI copy; mono only for versions, timestamps, identifiers, and execution evidence.
- A small `Proposed` ribbon sits outside each app frame. It is not app chrome.

## Frames

### 4a — Missions: conversation selection and mission picker

**Rail:** Missions.

Opening a Project lands here. The main surface lists existing Mission Conversations with their mission/version, latest message, and timestamp. Opening a row continues the existing conversation at its latest turn; it never discards history.

Primary action: **New mission conversation**. Its open picker lists only Approved versions, including Janus v1.4 and Naive v2.1. Candidate Janus v1.5 is visibly not selectable. Starting pins the selected version to that conversation; a later publication affects only new conversations unless a person deliberately changes this conversation's version.

Secondary action: **Author a mission**, which opens the authoring surface in Project Explorer.

### 4b — Mission Conversation: persistent, multi-turn interaction

**Rail:** Missions.

This is the operator's primary surface. The header names the conversation, its mission version, Approved status, and pinned state, with an explicit **Back to Missions** action.

Each user message is one turn. Each completed mission answer is human-readable and has a compact, collapsed evidence row with participant count, outcome, elapsed time, and **View trace**.

The frame makes two non-terminal states explicit:

- A failed turn keeps the user message and explains the failure. It offers **Retry turn**, **Edit message and resend**, and **View partial trace**; the conversation continues.
- A running turn reports participant progress and elapsed time, disables the normal send path, and provides **Cancel turn**. The composer re-enables after the turn resolves.

### 4c — Forge Trace: evidence for one conversation turn

**Rail:** Missions.

The trace is read-only chronological evidence for a specific turn. It presents the originating user message and delivered answer as anchors, followed by timestamped mission/expert events and their state/duration.

Its primary return action is **Back to Launch plan · Turn 3**, not a generic route to Missions. It returns to the exact conversation and turn that opened it.

### 4d — Authoring and evaluation evidence

**Rail:** Project Explorer.

The Project Explorer surface hosts an editable Candidate mission document. The reference shows Janus v1.5 derived from Approved v1.4, with saved state, version history, an editor, **Run evaluation**, and a visibly disabled **Publish v1.5** action when any case does not match.

Evaluation cases show:

- input;
- expected successful behaviour;
- expected failure behaviour;
- observed result;
- pass/fail status; and
- a link to exact trace evidence.

A failing expected-failure case gives a concrete publish-block reason and routes back to editing then re-evaluation.

### 4d-ii — Evaluated and publish-ready state

**Rail:** Project Explorer.

Once all cases pass, the state exposes **Publish v1.5** and a confirmation. The confirmation says that v1.5 becomes selectable for new Mission Conversations; existing conversations remain pinned, so `Launch plan` stays on Janus v1.4. The previous version is superseded only for new conversations.

## Required navigation contract

| From | Action | Destination | Persistent fact |
|---|---|---|---|
| Missions | Open conversation row | Mission Conversation | Conversation and pinned mission version persist. |
| Missions | Start new conversation | Mission Conversation | Selected Approved version is pinned. |
| Missions | Author a mission | Project Explorer authoring | Project remains selected. |
| Mission Conversation | View trace | Forge Trace for that turn | Origin conversation and turn are retained. |
| Forge Trace | Back to conversation/turn | Same Mission Conversation, same turn | No generic trace-to-Missions fallback. |
| Authoring | Evaluation trace | Forge Trace | Candidate version and evaluation case remain identifiable. |
| Authoring | Publish approved version | Missions picker for new conversations | Existing conversations remain pinned. |

## Implementation guardrails

- These frames describe intended, proposed behaviour. Current Forge Desktop remains one-shot Mission → instruction → terminal run card → trace until the new behaviour is implemented and accepted.
- The authoring editor's visible MCL snippet is visual content, not grammar authority. Validate every literal against [Language design](language.md) before implementation; for example, guards use the documented `when(...)` form such as `when(mode: "design")`.
- Presentation remains rendering/navigation only. Any new create/save/evaluate/publish/conversation/turn action needs an explicit shared Application Transport contract; do not implement UI-local behaviour.
- A planning phase must define the lifecycle and data contracts before implementation: mission version (`Draft → Candidate → Evaluated → Approved → Superseded`), conversation version pinning, turns, evaluation cases/results, publishing, and turn-origin trace routing.

## Export inventory

The canonical visual source remains the Claude Design project linked above. Its section 4 contains the full 2 × 2 screen set:

1. 4a — Missions and mission picker
2. 4b — Mission Conversation
3. 4c — Forge Trace
4. 4d — Authoring and evaluation evidence
5. 4d-ii — publish-enabled state and confirmation
