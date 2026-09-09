# Phase 45.3 — Operator Missions experience

> **Status:** controlled implementation accepted, 2026-09-09. Task 3A passed Codex's live controlled visual review against its allocated reference slice. Default-path acceptance remains **open**: a normal Desktop user still has no author → evaluate → publish route to create an Approved schema-5 mission version. Do not merge or mark this task complete until that prerequisite is delivered and the zero-argument journey is proven.

## Why this slice exists

This is the smallest useful replacement for the one-shot Missions control thread: an operator can see the Project's durable, version-pinned Mission Conversations and deliberately create one from a current Approved version. Transcript, turns, trace, authoring, and hands-status follow only after this visible selection/pinning seam is accepted.

It advances Presentation rendering/navigation/focus, Application/Missions' Project-to-Host coordination, Application Transport's shared vocabulary, and Application Host's concrete route boundary. Projects remains manifest/Approved-version owner; Conversation Host owns directory/checkpoints; Client Runtime alone owns local-capability authority. Presentation owns no Project, lifecycle, durable, or capability decision.

## Task 3A — Missions landing and approved-version start

### Scope

| Included now | Absent from this slice |
|---|---|
| The existing **Missions** rail destination is a landing view on Project open. It lists Host-indexed conversations with locally resolved mission name, pinned version, and updated time. | Row opening, transcript, title inference, last message, evidence, composer, submit/retry/cancel, trace, running state, hands status, confirmation, reconnect, and turn events. Rows are informational text, never buttons or links. |
| **New conversation** reveals one right-side setup panel with only each mission's current Approved version. | Filter/search, paging, sorting, candidate/superseded rows, catalog versions, version switching, generic browser, or a new rail item. |
| A read-only **Fixed access** summary repeats the selected profile. **Start on <mission> v<version>** creates the durable conversation, then acknowledges that exact resolved profile to Application. | Per-tool choices, profile narrowing/editor, escalation, arbitrary host access, Full Access, capability dashboard, or browser-to-Bob/Host calls. |
| Empty, loading, unavailable, stale-selection, create-uncertain, and attachment-unconfirmed states. | **Author a mission** is omitted, not disabled, until 45.4 has a real Explorer destination. |

Success creates an empty durable conversation and, when acknowledgement succeeds, a fresh same-profile attachment. It submits no turn. The panel closes, the new pinned item appears, and a live announcement confirms creation.

### Binding visual and component specification

Binding reference: [4a — Missions and mission picker](../design/assets/forge-desktop-dark-implementation-reference-v1/4a-missions.png), **1440 × 960**. Task 3A owns rail, header, list container, Approved-version panel, fixed-profile explanation, and one Start action. It explicitly omits 4a's filter, row navigation, last-message column, author link, candidate row, and secondary author action.

![Before — one-shot Missions](/Users/ameerdeen/progs/mission-control-language/docs/images/phase-45/task-3a-missions-landing-before.svg)

![After — Missions landing and approved-version start](/Users/ameerdeen/progs/mission-control-language/docs/images/phase-45/task-3a-missions-landing-after.svg)

The after artifact at **1440 × 960** is the binding composition; 4a supplies dark visual language and panel hierarchy. The existing composer is removed from this state. At measured packaged usable viewport the header, list, selected version, fixed-access summary, and Start action are visible without document scroll.

| Element | Exact behaviour |
|---|---|
| Header | **Missions**; **Review pinned mission versions and start a new conversation.** One primary button: **New conversation**. |
| List | **Mission conversations**; **Each conversation retains its approved mission version.** Columns: **MISSION · VERSION**, **UPDATED**. Empty: **No mission conversations yet. Start one from an approved version.** Semantic ordered list; no click handler, pointer cursor, or link styling. |
| Panel | **New mission conversation**; **Choose the approved version this conversation will pin.** Escape and **Cancel** close without request; focus moves to first option and returns to opener. |
| Options | Radio group **Approved versions**. Each option has mission, v<number>, **Approved**, and profile label. Candidate, Evaluated, Draft, and Superseded are not rendered. No-option: **No approved mission version is available for this Project.** |
| Fixed access | Read-only label: **No local access**, **Project workspace access**, or **Project workspace and terminal access**. Copy: **This exact profile is fixed for this conversation. Forge asks before any bounded operation. It cannot be changed here.** |
| Start | **Start on <mission> v<version>** after selection only. During request: **Starting conversation…**; Start, Cancel, and radios disable. |

Cooper/Rams/Norman is **PASS**: this serves the real pre-turn choice of an approved pinned mission; the panel is disclosed only for it; every control performs one real action; stale/unavailable versions cannot look selected; rows make no false promise.

### Theme and responsive evidence

Select named forge-desktop-dark at Workbench boundary, separate from data-theme. Add both colour-mode maps in source stylesheet src/ForgeUI/wwwroot/css/forge.css, which Presentation links; component styles consume tokens only. Warm background/rail/panel use surface tokens; cream ink uses text tokens; ember start/selection/focus uses accent tokens; pinned uses success tokens; availability/error uses warning/danger tokens. Record text/bg, muted/surface, accent/accent-contrast, success/success-bg, warning/warning-bg, danger/danger-bg, and disabled/surface contrast pairs plus sampled/derived/accessibility provenance.

Use bounded fluid geometry: compact width puts panel below list, wide width uses the after artifact's right column. Browser-first evidence covers four measured usable-viewport corners, continuous resize, long names, empty/error state, 200% zoom, keyboard focus, and no clipping, overlap, or unintended horizontal/document scroll; then one packaged parity check.

### Typed actions and rendering projection

Add only facts required by these states. MissionConversationService becomes the named Application owner behind IMissionConversationService, validates live session, uses MissionVersionService and ConversationHostClient, and returns presentation projections. Presentation never receives Project home, manifest, DurableMissionLaunch, Host client, Bob handle, tool declaration, or attachment implementation.

| Action | Request | Typed result |
|---|---|---|
| ListMissionConversations | session_id | MissionConversationListItem[]: conversation_id, locally resolved mission_name, version_number, updated_at_utc. Application reads Host directory and joins pinned mission_version_id to manifest. Missing local definition displays **Mission version is no longer available locally** and stored version; it does not invent a name/delete Host data. |
| ListApprovedMissionVersions | session_id | ApprovedMissionVersionOption[]: mission_id, mission_name, mission_version_id, version_number, definition_hash, exact MissionHandsProfile. Projects alone determines current active Approved; no package/definition content. |
| CreateMissionConversation | session_id, mission_id, command_id | CreatedMissionConversation: conversation_id and immutable MissionAccessApproval (mission_version_id, version_number, definition_hash, profile), or ProjectOperationError. Application resolves current Approved immediately before Host create; caller cannot name version, profile, package, or Project path. |
| AcknowledgeMissionHands (existing) | session_id, conversation_id, exact returned approval, profile_accepted true | Existing owner re-resolves launch and creates at most fresh bounded attachment it owns. Presentation cannot select access. |

The first three are additive in ApplicationContracts, ApplicationJsonContext, IApplicationChannel, HttpApplicationChannel, and concrete /transport/mission-conversations/* endpoints. DTOs use PascalCase members and source-generated camel-case JSON. Raw durable Contracts records stay Host-internal.

Home owns MissionsLanding and NewMissionConversation view state, loads both lists on Project entry, refreshes after definitive create, replaces one-shot history, and removes MissionComposer. No new Workbench rail state, SSE requirement, transcript, or trace navigation is added.

### Failure boundaries

| Failure | Containment and result | Proof |
|---|---|---|
| Stale session, options unavailable, or Host directory unavailable. | Application/Missions returns typed failure; Presentation has no fake cache/list. Show availability state and **Retry**, which repeats only query. | Service/route plus rendered retry test. |
| Version loses Approved state before create. | Projects re-resolves before Host admission; no Host command/attachment. Keep panel, state Approved-only rule, refresh options. | No-create/no-attachment stale test plus browser feedback. |
| Create response lost after dispatch. | Existing Host command_id idempotency; Presentation retains same ID/selection. **We could not confirm whether the conversation was created. Retry** repeats same request, never new command. | Lost-response test proves one conversation. |
| Create succeeds but acknowledgement fails/uncertain. | Created Host conversation remains valid; Application/Client Runtime owns attachment. Do not retry acknowledgement without command identity. Refresh list and announce **Conversation created. Local access could not be confirmed. Open it later to reconnect its fixed access.** No broader profile/fallback. | Controlled failure proves one pinned conversation and no unbounded retry. |

### Gates and done when

Security Architecture is **PASS**: additive Type-2 presentation/transport integration; no public ingress, datastore, credential, cross-store access, or Tier-1 data-plane permission. Presentation calls loopback Host; Application owns Project files and its named Host adapter; Host alone owns Table/Blob; Client Runtime alone owns local capability authority. No exception.

Engineering Philosophy is **PASS**: Projects resolves version/display identity, Missions coordinates, Host orders durable facts, Client Runtime owns attachment, Transport owns wire, Presentation renders/focuses. No dispatcher, durable UI cache, profile knob, alternate runtime, filter framework, or automatic acknowledgement retry.

Desktop design quality is **PASS**: Presentation owns this existing web-client flow; no Host adapter/Supervisor lifecycle change or workaround. Presentation-surface parity is **PASS**: each action is shared typed Application Transport with same authorization/outcome/failure for TUI; layout/focus are surface-specific.

Default-path acceptance applies. Completion uses zero-argument dist/forge-desktop/ForgeMission.Desktop with MissionRuntime:Mode, MissionRuntime:BaseUrl, FORGE_API_ENDPOINT, and ConversationRuntime:BaseUrl absent; normal cloud mission endpoint; Supervisor-owned Kind bridge at 127.0.0.1:18080; clean-main Host/Worker provenance; and disposable Project with Approved authored version. Record list/create action, pinned version/profile, conversation ID, visible result, and PASS/FAIL. Overrides/stubs are controlled evidence only.

1. Add projections/actions, source-generated JSON, Application owner, routes, channel mapping, and surface-neutral/route tests.
2. Replace Missions with landing/panel; add token-only theme/rules; remove one-shot composer.
3. Prove failures, browser matrix, packaged parity, then default path. Codex performs reference comparison.

**Done when:** focused/full tests and AOT publish pass; live 1440×960 matches after artifact and allocated 4a slice; responsive/accessibility pass; no deferred control looks interactive; failure evidence exists; and zero-argument Desktop creates exactly one pinned Approved conversation with selected fixed-profile acknowledgement. Completion includes Default-Path fact table and supervisor visual PASS.

## Required Claude relay format

The user-directed [Claude ↔ Codex workflow](../design/claude-codex-workflow.md) applies. An initial implementation-plan prompt includes its copy/paste relay protocol and required ASCII-only plan labels. Claude must not edit until Codex approves the relayed plan.
