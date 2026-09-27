# Phase 52.1 — Cloud conversations

> **Status: design (2026-09-28). Not build-ready** — one open question; see
> [Open question](#open-question). Hub: [Phase 52](phase-52-desktop-simplification.md). Selected
> from the backlog item "Cloud as the default local target (replacing Kind)".

**Goal:** the Desktop talks to the cloud. Its default Conversation Runtime is the hosted
Conversation service, reached through ForgeAPI, and every durable turn is metered exactly as
Rooms and `forge exec` runs are.

## Locked decisions

| Area | Decision |
|---|---|
| Client setting | The Desktop's runtime URLs choose cloud or local. ForgeAPI never chooses. Default: cloud. |
| API style | Message-based ([Phase 42.6 decision](phase-42.6-hosted-endpoint-ttfa.md#api-design--message-based-decided-2026-07-18)). One message per purpose; no consolidation. |
| Message set | Every call the Desktop makes today in `ConversationHostClient` (26 calls + the event stream) becomes a ForgeAPI message, `POST /api/{MessageName}`, reusing the existing request/response DTOs from `ForgeMission.Conversations.Contracts`. The event stream is one streaming message. |
| ForgeAPI role | Authenticate with `PlatformKeyAuthFilter`, then forward to the internal Conversation Host (existing routes, unchanged), streaming through `WireProxy`. It adds the platform-resolved `MemberId`. |
| Identity | Azure CIAM via `forge login` → platform key in `~/.forge`. The Desktop sends it as `Bearer`. Only the platform turns it into a `MemberId`; no client-supplied identity is ever trusted. |
| Ownership | The Host records the `MemberId` that created a conversation. Every lookup is scoped by `(MemberId, id)`; a non-owner gets not found. |
| Execution: one runner image | forge-runner is the single isolated compute sandbox for every mission run, one-shot or durable. It absorbs the Conversation Worker's capabilities: (1) accept a mission package (source + inline experts, root mission and input names) as well as a registry label, with a runner-wide default provider for packages without a manifest; (2) pause on a root-scoped tool call and resume from a saved opaque continuation; (3) stream step output as well as step starts. The runner stays stateless: a paused run returns its continuation to the caller, which stores it. The Conversation Worker, its container, and the Host↔Worker Service Bus queues are deleted. The Conversation Host calls forge-runner the way ForgeUI does; durable state stays in the Host's storage. |
| Metering | forge-runner is compute for rent and the source of truth for usage. `RunRequest` carries the `MemberId` from a trusted internal caller; the runner sends `{MemberId, RunId, MissionRef, Usage}` to the `run-settlement` queue; ForgeAPI consumes it and calls `BillingService.SettleRunAsync` unchanged with `RunId` plus segment number as the idempotency token. A turn that pauses for a local tool and resumes runs as several segments; each is measured and settled separately. ForgeUI and ForgeAPI stop settling inline: one settlement point, no double charge, redelivery never double-debits. |
| Settlement transport | Durable message, not an internal HTTP call: ForgeAPI has public ingress and Container Apps cannot make one route internal-only, so a settle endpoint would be internet-reachable. The queue keeps settlement off the public surface and survives ForgeAPI restarts. |
| Billing single writer | ForgeAPI is the only billing writer for runs; ForgeUI is read-only for billing (CQRS). ForgeUI's balance reads (`HasCreditAsync` in `RoomAgentInvoker`, `GetBalanceMicroUsdAsync` in `Account.razor` and `PlatformKeyEndpoints`) go through ForgeAPI's existing `GetAccount`. |
| Identity hop on the durable path | ForgeAPI resolves `MemberId` → Host stores it as owner → Host passes it on its `RunRequest` to forge-runner. Internal calls only. |
| Network | ForgeAPI (`550-api`) and the Conversation Host (`525-conversation-app`) share Container Apps environment `cae-forge-dev`; the Host keeps internal-only ingress. |
| Transitional exception | ForgeUI keeps `authbilling_db` write access for exactly two sign-in writes: platform key issuance (`PlatformKeyEndpoints.IssueAsync`) and `GrantStartingCreditAsync` (`MemberProvisioningService`). Reason: `forge login` exchanges the CIAM token at ForgeUI today and does not affect Desktop-to-cloud. Removal: moving `forge login` key issuance to ForgeAPI ([backlog](../backlog.md)). Verification: no other ForgeUI code references a `BillingService` write. |
| Out of scope | Unauthenticated local mode and `forge dev start` ([backlog](../backlog.md)); local ForgeAPI. |

## Architecture-security review

| Question | Answer |
|---|---|
| Bounded context / data owner | Durable conversations: Conversation Host owns the 350 Storage. Billing: its existing owner, unchanged. |
| Public entry point | ForgeAPI (Tier 1): platform-key auth, then forwards to the internal Host (Tier 2). |
| Tier 2 components | Conversation Host (internal ingress), forge-runner (internal). |
| Tier 3 | 350 Storage and Service Bus (conversations); billing store and new billing Service Bus namespace `sb-forge-billing-dev` with queue `run-settlement`. No public ingress. |
| Cross-context store access | None. forge-runner holds no billing store credentials; it sends settlement over an internal contract. ForgeAPI holds no conversation store credentials. |
| Secrets | forge-runner: Send-only on `run-settlement`; provider keys as today. The Worker identity and its `Mcl-ApiKey` binding are removed with the Worker. ForgeAPI: Listen-only on `run-settlement`. ForgeUI: billing reads via ForgeAPI, write access limited per the transitional exception. Host: 350 host identity. |
| Type 1 / Type 2 | Type 1: public conversation messages; settlement moving to forge-runner; forge-runner becoming the only execution container. Type 2: default URL, message names. |
| Enforcement and proof | Host `external: false`; a call to the Host FQDN from outside the environment fails; an authenticated call through ForgeAPI succeeds; another member's conversation returns not found. |

## Default path (new)

| Fact | Value |
|---|---|
| Artifact | Published Desktop bundle, zero arguments, no `MissionRuntime:*`, `ConversationRuntime:*`, or `FORGE_*` overrides, after `forge login`. |
| Mission Runtime URL | `https://api.forge.katasec.com`; `/health` checked. |
| Conversation Runtime URL | ForgeAPI; `/health` checked. |
| Action / result | In a Project, send a message; the streamed reply appears; reopening the Project shows it in run history; the member's balance is debited once. |

## Tasks

| # | Task | Done when |
|---|---|---|
| 1 | forge-runner: mission-package input (source + inline experts, root mission/input names, package validation) and a runner-wide default provider for packages without a manifest. | The Worker's package tests pass against forge-runner. |
| 2 | forge-runner: root-scoped tool pause and resume from an opaque continuation returned to the caller; tool-result status (Succeeded/Denied/Cancelled/Failed) and mission/expert location on the pause. | The Worker's pause/resume tests pass against forge-runner. |
| 3 | forge-runner: stream step output (text or failure reason, mission name, attempt) as well as step starts. | The Desktop's live participant messages are produced from the runner stream. |
| 4 | Metering: billing Service Bus namespace `sb-forge-billing-dev` + `run-settlement` queue; `MemberId` on `RunRequest`; forge-runner settles per segment; ForgeAPI consumer calls `SettleRunAsync`; ForgeUI and ForgeAPI stop settling inline; ForgeUI balance reads via `GetAccount`. | A Rooms run, a `forge exec` run, and a paused-then-resumed durable turn each debit exactly once per segment; redelivery debits nothing extra; ForgeUI has no billing write outside the transitional exception. |
| 5 | Conversation Host calls forge-runner; delete the Conversation Worker, its image, and the Host↔Worker queues. | Host tests pass with forge-runner as the executor; no Worker project remains. |
| 6 | Host owner link, scoped by `MemberId`. | A second member's lookup returns not found. |
| 7 | Build and push the Host image; deploy 525 without the Worker (`what-if` first). | Host `/health` 200 from inside the environment; Host reaches forge-runner. |
| 8 | ForgeAPI: 26 messages + event stream, auth, `MemberId` forwarding. | Each Desktop call succeeds through ForgeAPI with a platform key. |
| 9 | Desktop: message route strings, `Bearer` on the conversation client, default URL → ForgeAPI; update [Default-Path Acceptance](../design/default-path-acceptance.md). | The default-path action passes on the published bundle. |

## Open question

1. **One continuation mechanism.** forge-runner today continues a tool call by replaying the
   transcript (`RunRequest.History` + `ToolContinuationGate`), used by ForgeAPI `ExecuteMission`
   (`forge-platform/src/ForgeMission.Api/MissionExecutionService.cs:134`). Task 2 adds the opaque
   continuation. Keeping both is two code paths; removing replay changes `ExecuteMission`'s
   tool-continuation contract, and the spec-bound chat wire (API B, on hold) can only replay
   transcripts. Decide which callers keep replay, if any.
