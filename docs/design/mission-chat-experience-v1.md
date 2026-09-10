# Mission Chat experience v1 — visual reference

> **Status:** approved visual reference for Phase 47's static UI prototype, not shipped behaviour.
> **Exported:** 2026-09-10 from Claude Design (`Mission Chat Experience.dc.html`).
> **Default-path acceptance:** N/A — static design artifact; no runtime, contract, or user-path change.

## Purpose and authority

Three frames covering the Mission Chat journey: the pre-workspace startup choice, a fresh empty
chat on an approved mission version, and the active shared chat. They are a visual reference for
review, not an implementation plan and not evidence that any capability ships.

Use with [Desktop Interaction Principles](desktop-interaction-principles.md) and the
[UI Design System](ui-design-system.md). A planning agent must turn this into a phase hub/spoke
before code starts.

## Frames

| File | Screen | Rail |
|---|---|---|
| [01-start.png](assets/mission-chat-experience-v1/01-start.png) | Pre-workspace startup choice: `Create a mission`, `Chat with a mission`, and `Open an existing workspace…` as quiet secondary navigation. No Project rail is drawn, because no Project is open. | none |
| [02-new-janus-chat.png](assets/mission-chat-experience-v1/02-new-janus-chat.png) | Destination of `Chat with a mission`: a fresh, empty chat pinned to Janus v1.4. Makes `You`, `Proposer` and `Approver` explicit before the first message, repeats the fixed access read-only, composer active. | Missions |
| [03-active-chat.png](assets/mission-chat-experience-v1/03-active-chat.png) | Active state: one chronological shared stream where You, Proposer and Approver are peers. Steering happens through an ordinary message in the same composer. | Missions |

Viewport: 1440 × 960, exported at 2×.

## Product model the frames assert

- A **Mission** is reusable configuration/version; a **Chat** is one durable context under it, and a
  Mission can carry several chats.
- The chat list is **local navigation inside Missions**, grouped beneath each Mission version — not a
  fourth global rail entry. The rail stays exactly `Project Explorer`, `Missions`, `Settings`.
- Each existing chat displays its pinned Mission version **read-only**. Version selection belongs to
  creating a new chat; nothing in these frames implies an existing chat can switch versions.
- There is no per-turn steering box, no hidden internal thread, and no right-side discussion drawer.
  The active composer is the only way work is steered.

## Visual-consistency review against the verified Presentation surfaces

Checked against `forge.css`, `Pages/Home.razor`, `WorkbenchRail.razor(.css)`,
`MissionsLandingView.razor(.css)`, `NewMissionConversationPanel.razor(.css)`,
`ProjectLauncher.razor` and `ConversationTranscriptView.razor`.

| Mismatch found | What the frames now do |
|---|---|
| The earlier mock set for this journey read as its own product: dark surfaces and ember accent, taken from the `forge-desktop-dark` reference rather than the blue Workbench theme. | Every value in the frames is a literal from the `data-surface-theme="workbench"` **light** token map: `--bg #f7faff`, `--surface #ffffff`, `--surface-sunken #f6f8fd`, `--border #e7ecf4`, `--border-strong #d5dae5`, `--text #101d33`, `--text-muted #5b6b83`, `--text-subtle #62748c`, `--accent`/`--ink #0f6eeb`, `--accent-soft #eff5fe`. |
| Rail drawn as generic app navigation. | Rail reproduced as implemented: 192px, `--wb-rail-surface #08294a`, selected row `#124e82` with a 3px `#24d5ee` marker, `PROJECT` mono eyebrow + swatch + wrapping title, the same `□ ◎ ⚙` glyphs, Settings bottom-aligned. |
| Accent used decoratively / green used for emphasis. | Forge blue is the only interactive accent. Green (`--success #4d7c0f` on `--success-bg #f2fbe6`) appears only on `APPROVED` and the approval line; the completed-tool check reuses `--success` exactly as `ConversationTranscriptView` does. |
| Chat surfaces invented from scratch. | Composed from shipped primitives: the header band (`.wb-header` geometry, `--surface-sunken`, `--border-strong` hairline, accent primary + bordered secondary action), card shells (`--radius-lg`, 1px `--border`, `--shadow-sm`), the transcript's own row vocabulary (`.convo-user-bubble` accent-soft right-aligned bubble, `.convo-participant-bubble` full-width card with a muted name label, `.convo-approval.approved`, `.convo-tool-row`, `.convo-activity` italic + pulse dot + trailing dots), the `.np-access` read-only fixed-access block, and the mono uppercase column labels used by `.ml-columns` / `.np-group-label`. |
| Startup screen fabricated a Project rail and product chrome. | Frame 1 uses the launcher shell instead: `Forge` in accent + 1px divider + `AI Workbench`, one `min(906px)` card, `Open an existing workspace…` as the quiet `.pl-open-link`-style text button with the same folder glyph. |

### Visual adjustments made in this revision

1. Re-themed all three frames from ember/dark to the Workbench light map; no ember and no dark
   surface remains outside the rail, which is dark in both modes by design.
2. Rebuilt the rail to the implemented component (order, marker, brand block, glyphs, foot).
3. Replaced invented chat chrome with the transcript row kinds and card/bubble treatments that
   already exist in `ConversationTranscriptView` and `forge.css`.
4. Moved the chat list into the Missions content region as a local, mission-grouped column, so no
   frame implies a new global destination.
5. Reduced status colour to two claims: green for Approved only, blue for everything interactive.
6. Type: system sans throughout, mono restricted to versions, identifiers and column labels — 13px
   metadata floor at 1440 × 960.

### Implementation reconciliations — locked for the static prototype

- **Theme:** Phase 47 uses the blue `workbench` theme already selected on the document root. Its
  static Mission Chat shell must not apply `forge-desktop-dark`; a later integration task may
  reconcile the existing durable-Missions shell separately.
- **Pinned provenance:** `PINNED` is neutral in the new static components. Green is reserved for
  `APPROVED`; Phase 47 does not change the accepted durable-Missions `.ml-pinned` component.
- **Opening a chat:** the local chat list is openable only inside the prototype. It never changes the
  deliberately inert durable rows in `MissionsLandingView`.
- **Activity:** `Proposer is revising the plan` is fixed stand-in copy for the prototype. It is not
  a projection claim, transport event, or new domain field.

## Guardrails

- Presentation stays rendering/navigation only. Every create/send/steer/open action visible here
  needs an explicit Application Transport contract first.
- No new colour, radius, spacing or type value was introduced; if implementation needs one, it goes
  in a named theme map in `forge.css`, never in a component.
