# Phase 47 — Mission Chat static UI

> **Status:** scope locked, 2026-09-10. This is an interactive Presentation-only prototype.
> It creates no product chat, no durable state, and no backend contract.

## Why this phase exists

The Mission Chat experience must be agreed through a complete, usable UI before durable
conversation, submission, or participant orchestration is connected. This phase gives the team a
reviewable Forge-blue prototype of the startup choice, fresh Janus chat, and active group chat.

## Locked decisions

| Area | Decision |
|---|---|
| Visual contract | [Mission Chat experience v1](../design/mission-chat-experience-v1.md) is binding at 1440 × 960: [start](../design/assets/mission-chat-experience-v1/01-start.png), [fresh Janus chat](../design/assets/mission-chat-experience-v1/02-new-janus-chat.png), and [active chat](../design/assets/mission-chat-experience-v1/03-active-chat.png). |
| Theme | Use the existing root Workbench light-token map. Do not apply forge-desktop-dark, ember/orange, or component-local visual literals. Green means Approved only; Pinned is neutral. |
| Prototype boundary | New interactions are local Presentation state only. The static mission/chat/participant content is realistic stand-in content, fixed in the component, and is discarded on reload. It is never a Mission, conversation, turn, run, approval, activity, or trace projection. |
| Startup choice | Create a mission returns to the existing Project launcher unchanged. Chat with a mission opens the static Janus chat. Open an existing workspace retains the existing launcher behavior. |
| Chat behavior | New chat selects a fresh Janus chat. Selecting local rows changes the visible stand-in transcript. A non-empty composer message is appended locally and triggers the fixed Proposer/Approver stand-in sequence; it is normal group-chat steering, including a person's request to stop or redirect work. |
| Navigation | The global rail stays exactly Project Explorer, Missions, Settings. In the isolated chat preview, Missions is active; the other rail entries are visible navigation structure but do not claim a static destination this phase does not design. |
| No false product claim | The prototype contains no transport call, durable command, generated chat identifier, fake retry/cancel/approval result, private subthread, per-turn steering control, or right-side conversation drawer. It does not modify the existing durable Mission rows or authoring workflow. |

## Architecture and gates

| Gate | Disposition |
|---|---|
| Component fit | **PASS.** Only ForgeMission.Presentation changes: it owns rendering, navigation, focus, and local view state. No other owner or contract changes. |
| Security Architecture | **PASS.** Type-2 Presentation-only change: no public entry point, identity, credential, data owner, datastore access, capability authority, or cross-context contract exists. Reversal is deletion of the prototype components; its removal condition is a separately approved integration design. |
| Engineering Philosophy | **PASS.** One small preview component owns its ephemeral state; Home owns only switching between launcher and preview. No preview framework, generic chat model, configuration knob, hidden fallback, or duplicate domain rule is introduced. |
| Desktop design quality | **PASS.** This changes the existing web-rendered Presentation surface only; it neither changes nor relies on a Desktop Host/Supervisor lifecycle callback. |
| Presentation-surface parity | **N/A.** Every new action is explicitly local prototype navigation or simulated display, not a product action. A real create/open/send/stop action requires a shared Application Transport contract before integration. |
| Default-path acceptance | **Applies.** The zero-argument published Desktop path must render and exercise the local prototype with the normal configuration. No external mission or conversation action is part of this slice. |
| Native AOT | **N/A to the intended Presentation-only markup/local-state change.** The implementation must still keep the solution warning-free and add no reflection, JSON, or package dependency. |

## Dependency-ordered spokes

| Order | Spoke | Deliverable | Gate before acceptance |
|---|---|---|---|
| 47.1 | [Interactive Mission Chat prototype](phase-47.1-interactive-mission-chat-prototype.md) | The three binding screens and their local UI transitions, tested and visually reviewed. | Codex browser/reference PASS plus zero-argument Desktop local-prototype PASS. |

## Codex ↔ Claude iteration

This phase uses the user-directed [Claude ↔ Codex workflow](../design/claude-codex-workflow.md).
Each iteration is narrow: Codex issues or revises the scope card; Claude returns a **plan only**;
Codex explicitly approves or rejects it; Claude implements only the approved delta; Codex compares
the running surface with the bound frame and accepts or sends one concrete revision. Claude never
approves its own output, broadens into integration, or treats a screenshot as final acceptance.

The first handoff is 47.1. A later visual adjustment gets a new bounded scope card under the same
spoke; it does not silently add a backend dependency or turn a stand-in into product behavior.
