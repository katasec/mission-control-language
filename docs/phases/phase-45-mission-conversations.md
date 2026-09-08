# Phase 45 — Forge Desktop mission conversations

> **Status:** version/evaluation foundation and the Phase 46 generic durable-runtime prerequisites
> are accepted as of 2026-09-08. Current Desktop remains Project → selected one-shot Mission →
> instruction → terminal run card → trace until the declared integration and surface spokes are
> delivered in order.

## Why this phase exists

Forge Desktop already has the right foundation: a local Project, a three-entry rail, a native
Supervisor/WebView Host, an Application-owned typed transport, a durable Conversation Host, and
presentation-only trace rendering. Its one-shot Mission flow cannot retain operator context, pin a
version, or give an author a controlled route from edited source to evidence and publication. This
phase changes that experience in place. It adds neither a parallel client, rail destination,
transcript store, runtime, nor presentation-owned product state.

## Product decisions — locked

| Area | Decision |
|---|---|
| Workspace and navigation | One Project workspace serves operators and authors. The persistent rail remains exactly **Project Explorer**, **Missions**, and **Settings**. Opening a Project selects Missions. Authoring is a Project Explorer document, not a fourth rail entry. |
| Version lifecycle | A mission version moves only `Draft → Candidate → Evaluated → Approved → Superseded`. A candidate is editable; editing invalidates prior evaluations. An evaluated version is immutable. Publishing approves it and supersedes the prior approved version for *new* conversations only. |
| Mission hands profile | Every mission version declares exactly one immutable `NoHands`, `ProjectWorkspace`, or `ProjectWorkspaceAndTerminal` profile; changing it creates a new version. The profile is a bounded request, not authority. Creation of a Mission Conversation shows the exact profile and records the operator's acceptance; there are no per-tool checkboxes, profile narrowing controls, runtime escalation, network, credential, publishing/push, arbitrary-path or Full Access profile. |
| Operator selection | Missions lists persistent Mission Conversations and starts a new one. The picker exposes Approved versions only. Starting stores an immutable version launch snapshot and pins it to that conversation. Candidate, Evaluated, Draft, and Superseded versions cannot be selected by an operator. |
| Turns and failure | One submitted user message creates one turn. The transcript is primary. Each answer has compact evidence and a turn-specific trace link. A failed turn preserves its message, failure and partial trace; it does not end the conversation. Retry creates a new attempt of that turn. Cancel is an explicit durable request/outcome, never implied rollback. |
| Trace | Forge Trace is chronological exact evidence for one turn. It has origin `(conversation_id, turn_id, attempt_id)` and returns to that exact transcript anchor, never a generic Missions route. An evaluation trace also retains `(mission_version_id, evaluation_case_id)`. |
| Authoring and evaluation | Authoring starts from **Author a mission** in Missions or a mission asset in Project Explorer. Evaluation cases hold input, expected success, expected failure, observed result, state and exact trace reference. Publish is unavailable unless every current case for the unchanged candidate revision passes. |
| Scope boundary | Human gates, suspend/resume, generic checkpoint resume, switching version within a conversation, OCI/catalog installation, and a new rail entry are excluded. They require a separately accepted runtime contract first. |

The dark [implementation reference](../design/forge-desktop-dark-implementation-reference-v1.md)
is the binding proposed visual outcome. Its visible MCL text is illustration only: editor/help text
must follow [Language design](../design/language.md), including `when(mode: "design")` and the
currently unsupported status of `debate {}`.

## Architecture and ownership — locked

| Concern | Owner and boundary |
|---|---|
| Local authored definitions, lifecycle, cases/results, Project migration | `ForgeMission.Application/Projects`; `ProjectService` stays the single manifest/file transaction owner. |
| Version listing, conversation/turn/evaluation commands and reconciliation | `ForgeMission.Application/Missions`; it coordinates typed owners but never sequences durable facts. |
| Durable order, command idempotency, turn/run lifecycle, trace events and recovery | `ForgeMission.ConversationHost` and `ForgeMission.Conversations.Contracts`; Host remains the only Conversation Table/Blob writer and sequence allocator. |
| MCL execution and progress | `ForgeMission.ConversationWorker`; it receives an immutable launch snapshot and never reads a Project manifest or local path. |
| HTTP/SSE and source-generated JSON | `ForgeMission.Application.Host`; every route remains one concrete typed action. |
| Layout, navigation, form/focus state and responsive rendering | `ForgeMission.Presentation`; it does not calculate lifecycle, mutate Project state, or invent a transcript. |
| Mission-to-hands grant and local capabilities | Application approves and binds a new/reconnected conversation/session to one fresh, ephemeral Bob attachment constrained to its pinned profile. `ForgeMission.ClientRuntime` alone owns capability policy, confirmation, structural containment, audit, execution, cancellation and cleanup. Version declaration and Desktop/TUI display grant nothing; Worker never receives Bob/local authority. |

The Type-1 boundary remains `Desktop Presentation → Application/Conversation service →
Conversation Table/Blob and Service Bus`. Application Host holds no data-plane credential. Project
files are owned by Application/Projects; Conversation Host and Worker receive a value snapshot,
not a Project path or direct read. The local Kind bridge is a Type-2 deployment adapter, not an
authority change. No exception is proposed.

## Shared model and invariants

All IDs are GUIDs unless stated otherwise. Every persisted/wire record evolves additively and uses
source-generated JSON; serialized enum values append only.

| Identity | Definition |
|---|---|
| `MissionId` | Stable Project-local mission identity, never its display name. |
| `MissionVersionId` | Immutable identity of one numbered lifecycle record; paired with `VersionNumber` and SHA-256 `DefinitionHash`. |
| `MissionConversationId` | Durable Conversation Host identity for one operator conversation, pinned to exactly one `DurableMissionLaunch`. |
| `TurnId` | Durable identity of one user message inside a conversation; stable across attempts. |
| `TurnAttemptId` | Durable identity of one execution attempt; retry creates a new one with a new command ID/status/trace range. |
| `EvaluationCaseId` / `EvaluationResultId` | Stable Project-local case identity and immutable result identity; result names candidate revision/hash and trace origin. |
| `TraceOrigin` | `(conversation_id, turn_id?, turn_attempt_id?, evaluation_case_id?, run_id, first_sequence, last_sequence)`. Exactly one of turn/evaluation case is populated. |

`DurableMissionLaunch` is the only durable execution snapshot: `mission_version_id`,
`version_number`, `definition_hash`, immutable definition content, exactly one immutable
`MissionHandsProfile`, and a bounded `DurableMissionPackage` containing the resolved MCL/expert
content. Application's internal `MissionVersionLaunch` is compatibility provenance only and never
crosses the durable wire. Conversation Host stores the bounded launch artifact under its own
Blob/store ownership before queueing work; Worker receives verified content/value and
profile-limited generic tool declarations only. No command contains a Project path, credential,
provider selection, Bob handle, local root, or local capability.

The durable conversation records the approved profile with the pinned launch. Application alone
can create a live `(conversation, version, profile, session)` attachment; reconnect creates a new
ephemeral Bob attachment under that same durable approval. A missing attachment is an explicit
durable `AwaitingHands` state, not an authority grant, remote fallback, or successful result.

## Dependency-ordered spokes

| Order | Spoke | Deliverable | Gate before next spoke |
|---|---|---|---|
| 45.1 | [Version and evaluation contracts](phase-45.1-version-evaluation-contracts.md) | Project-owned version/evaluation model, migration, typed Application messages and deterministic evaluation rules. | Contract, migration and default-path design accepted. |
| 45.2 | [Version-to-durable conversation integration](phase-45.2-durable-conversation-turns.md) | Bind Project-owned Approved/Candidate versions and evaluation cases to the accepted generic Core, hands, Host, and Worker path. | Approved/Candidate admission, evaluation reconciliation, and failure/recovery proof accepted. |
| 45.3 | [Operator Missions experience](phase-45.3-operator-missions-experience.md) | Conversation list/picker, transcript and trace round-trip in the existing rail/shell. | Browser-first visual PASS and zero-argument Desktop path PASS. |
| 45.4 | [Project Explorer authoring](phase-45.4-project-explorer-authoring.md) | Author/edit/evaluate/publish flow in Explorer, with evidence links and publish block. | Browser-first visual PASS and zero-argument Desktop author/evaluate/publish path PASS. |

## Required gates

| Gate | Phase disposition |
|---|---|
| Default-Path Acceptance | This documentation task is N/A. The first user-facing proof remains 45.3/45.4: zero-argument `dist/forge-desktop/ForgeMission.Desktop`, absent overrides, normal dependencies, a disposable Project, action, and durable observation. 45.2's controlled integration checks do not close that acceptance. |
| Desktop Interaction Principles | Reference at 1440×960 is binding. UI spokes also record packaged default usable viewport, four-corner matrix, continuous resize, long content, text scale and text-fit evidence. |
| UI Design System | Add/select named `forge-desktop-dark` theme maps with light/dark token values; components consume tokens only. The theme is independent of `data-theme`; no sampled component-local values. |
| Security Architecture | No new public endpoint/store owner. Host remains Tier 2/internal state owner; Worker has no Project/Conversation store access; Application Host has no data-plane credential. |
| Engineering Philosophy | Each command has one owner and failure result. No generic dispatcher, UI-local domain state, retry knob, local worker path, or speculative version framework. |
| Native AOT | JSON contexts are source-generated; no reflection, untyped JSON options, runtime type discovery or new warning suppression. |
| Claude/Codex workflow | Codex approves this design and each Claude plan before code. Completion needs Claude’s evidence summary and Codex review against Done when. |

## Build readiness

The product, data, lifecycle, routing, failures, migration, security, default path and visual
contracts are **locked** in the spokes. Phase 45.2 integrates the accepted Phase 46 generic
root-continuation, immutable-hands, and durable-Worker path; it does not reimplement or fork those
owners. A later spoke may need a contract-alignment pass when an already-accepted prerequisite
changes an implementation type or persistence detail; that pass must preserve these decisions,
name the exact replacement, and never recast it as an open Phase 45 design question or request an
operator decision. Escalate only an actual contradiction of a table above or a new Type-1
tier/data/identity boundary. The listed scope exclusions are intentional and do not block this
work. No implementation starts until Codex approves the relevant spoke and its task-specific plan.
