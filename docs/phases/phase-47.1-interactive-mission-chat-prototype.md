# Phase 47.1 — Interactive Mission Chat prototype

> **Status:** build-ready UI-only scope. Claude plan review is next; no implementation has started.

## Outcome and component fit

Build the three-frame Mission Chat journey as an interactive local prototype in
ForgeMission.Presentation. It advances that component's purpose—rendering, navigation, focus,
and local view state—without creating a second owner of Project, mission, conversation, turn,
approval, capability, or transport rules.

## Exact scope

| State | Required UI behavior | Binding reference |
|---|---|---|
| Startup | Render the first-use choice. Create a mission returns to the unchanged existing launcher; Chat with a mission opens the fresh Janus preview without sending a request; Open an existing workspace retains the existing launcher behavior. | [01-start.png](../design/assets/mission-chat-experience-v1/01-start.png) at 1440 × 960 |
| Fresh Janus chat | Render Janus v1.4, its local chat list, You/Proposer/Approver, read-only fixed access, an enabled composer, and exactly the three-entry rail. New chat returns to this fresh local state. | [02-new-janus-chat.png](../design/assets/mission-chat-experience-v1/02-new-janus-chat.png) at 1440 × 960 |
| Active chat | Select the Launch plan row and render the shared chronological transcript. A non-empty composer entry adds a local You message and the fixed stand-in group response; message selection and New chat continue to work without network or durable state. | [03-active-chat.png](../design/assets/mission-chat-experience-v1/03-active-chat.png) at 1440 × 960 |

## Required implementation shape

| Path | Responsibility |
|---|---|
| src/ForgeMission.Presentation/Pages/Home.razor | Add only local routing between the launcher and the static preview. Existing Project, session, transport, durable-Missions, and authoring paths retain their behavior. |
| src/ForgeMission.Presentation/Components/MissionStartChoice.razor and scoped CSS | Render the first frame and raise presentation-only callbacks. It contains no project form, Application Transport type, or decision logic. |
| src/ForgeMission.Presentation/Components/MissionChatPreview.razor and scoped CSS | Own the fixed stand-in chat list, selected local transcript, ephemeral composer text, focus, and deterministic stand-in response sequence. All styling resolves through the existing Workbench tokens. |
| src/ForgeMission.Tests/Presentation/MissionChatPreviewTests.cs | Prove the local state transitions, keyboard/composer behavior, rail count, and that no unowned control is introduced. |
| src/ForgeMission.Tests/Presentation/HomeSessionOperationTests.cs | Extend the zero-authority launcher proof: choosing the static chat route makes no IApplicationChannel request. |

Do not modify Application, Application Transport, Application Host, Conversation Host, Worker,
Client Runtime, Desktop, contracts, persistence, routes, configuration, or the existing durable
MissionsLandingView / NewMissionConversationPanel behavior.

## UI rules

- Workbench is the named theme selector. The page preview must not inherit or apply
  forge-desktop-dark. Components use named tokens only; no sampled colour, radius, spacing,
  font size, or shadow literal belongs in scoped CSS.
- Keep the global rail's exact count, labels, order, and settings footer. Do not create Chat, Run,
  agent, or drawer navigation.
- The local list groups Janus and Naive chats under their versions. Only selected Chat behavior
  exists in the prototype; a real durable list is unchanged and remains deliberately inert.
- Normal composer text is the only steering affordance. It may show a user message and fixed
  stand-in replies; it must not claim a live model, durable run, trace, or approval outcome.
- Empty/whitespace-only text does nothing. Enter sends; Shift+Enter retains a newline. After send,
  focus remains in the composer and an accessible live region announces the local update.
- On reload, return to the startup choice and discard the preview state. That reset is part of the
  prototype contract, not an error or recovery path.

## Failure and containment

| Condition | Required result | Proof |
|---|---|---|
| No Application channel / unavailable backend | The static Chat route still renders and changes only local state; it does not make a request or show a fake connection failure. | Component and Home tests with a recording/no-call channel. |
| Empty composer submit | No transcript or selected-chat mutation; focus remains usable. | Focused component test. |
| Reload / fresh launch | No prior stand-in message or selected active chat survives. | Browser refresh observation and fresh-component test. |
| Deferred rail destination | It remains visibly present but cannot imply an implemented static screen or call the backend. | Markup and keyboard-access review. |

## Default-path and visual acceptance

| Fact | Required observation |
|---|---|
| Artifact | dist/forge-desktop/ForgeMission.Desktop, launched with zero arguments. |
| Defaults | MissionRuntime:Mode, MissionRuntime:BaseUrl, FORGE_API_ENDPOINT, and ConversationRuntime:BaseUrl are absent. |
| Starting state | Fresh launch with no open Project; no existing Project or durable conversation is changed to demonstrate the prototype. |
| Action | Choose Chat with a mission; create a fresh local Janus chat; select Launch plan; add a normal steering message; reload. |
| Outcome | Each local transition works, no durable/backend action occurs, and reload returns to startup. Record Codex's browser observation. |
| Visual | Codex inspects the running UI against all three reference images at 1440 × 960, then checks 800 × 568, 800 × 1024, 1536 × 568, 1536 × 1024, 200% zoom, keyboard focus, long text, and no clipping/overlap/unintended document scroll. |

## Claude handoff

Claude receives a link to this spoke and the three image files on the branch. The first request is
**plan only; do not edit**. It must name the exact paths, local-state sequence, test plan,
reference-image mapping, token use, and every assumption. Codex must explicitly approve that plan
before Claude edits. After each implementation pass, Claude returns actual test results and
screenshots; Codex independently inspects the diff and live UI, then accepts or issues a narrower
revision scope.

## Done when

The startup, fresh Janus, and active-chat frames are interactive as specified; all new state is
local and reset-on-reload; focused Presentation tests and the full solution checks pass; no
Application/Host/runtime/contract/persistence path changed; the zero-argument Desktop observation
passes; and Codex records a visual PASS against every owned reference state. The user may then
review the static UI before any integration phase is designed.
