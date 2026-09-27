# Phase 52.1 — Cloud conversations

> **Status: build-ready (2026-09-28).** Hub: [Phase 52](phase-52-desktop-simplification.md). Selected
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
| Execution | The Conversation Worker runs turns through **forge-runner**, as Rooms and `ExecuteMission` do. Its in-process MCL path (`GenericDurableMissionExecutor` → `PipelineRunner`) is deleted. |
| Metering | forge-runner is compute for rent and the source of truth for usage. `RunRequest` carries the `MemberId` from a trusted internal caller; the runner sends `{MemberId, RunId, MissionRef, Usage}` to the `run-settlement` queue; ForgeAPI consumes it and calls `BillingService.SettleRunAsync` unchanged with `RunId` as the idempotency token. ForgeUI and ForgeAPI stop settling inline: one settlement point, no double charge, redelivery never double-debits. |
| Settlement transport | Durable message, not an internal HTTP call: ForgeAPI has public ingress and Container Apps cannot make one route internal-only, so a settle endpoint would be internet-reachable. The queue keeps settlement off the public surface and survives ForgeAPI restarts. |
| Billing single writer | ForgeAPI is the only billing writer for runs; ForgeUI is read-only for billing (CQRS). ForgeUI's balance reads (`HasCreditAsync` in `RoomAgentInvoker`, `GetBalanceMicroUsdAsync` in `Account.razor` and `PlatformKeyEndpoints`) go through ForgeAPI's existing `GetAccount`. |
| Identity hop on the durable path | ForgeAPI resolves `MemberId` → Host stores it as owner → Host puts it in the internal mission-command message → Worker passes it to forge-runner. Internal messages only. |
| Network | ForgeAPI (`550-api`) and the Conversation Host (`525-conversation-app`) share Container Apps environment `cae-forge-dev`; the Host keeps internal-only ingress. |
| Transitional exception | ForgeUI keeps `authbilling_db` write access for exactly two sign-in writes: platform key issuance (`PlatformKeyEndpoints.IssueAsync`) and `GrantStartingCreditAsync` (`MemberProvisioningService`). Reason: `forge login` exchanges the CIAM token at ForgeUI today and does not affect Desktop-to-cloud. Removal: moving `forge login` key issuance to ForgeAPI ([backlog](../backlog.md)). Verification: no other ForgeUI code references a `BillingService` write. |
| Out of scope | Unauthenticated local mode and `forge dev start` ([backlog](../backlog.md)); local ForgeAPI. |

## Architecture-security review

| Question | Answer |
|---|---|
| Bounded context / data owner | Durable conversations: Conversation Host owns the 350 Storage. Billing: its existing owner, unchanged. |
| Public entry point | ForgeAPI (Tier 1): platform-key auth, then forwards to the internal Host (Tier 2). |
| Tier 2 components | Conversation Host (internal ingress), Worker (no ingress), forge-runner (internal). |
| Tier 3 | 350 Storage and Service Bus (conversations); billing store and new billing Service Bus namespace `sb-forge-billing-dev` with queue `run-settlement`. No public ingress. |
| Cross-context store access | None. forge-runner holds no billing store credentials; it sends settlement over an internal contract. ForgeAPI holds no conversation store credentials. |
| Secrets | forge-runner: Send-only on `run-settlement`. ForgeAPI: Listen-only on `run-settlement`. ForgeUI: billing reads via ForgeAPI, write access limited per the transitional exception. Host/Worker: 350 identities. The Worker no longer needs `Mcl-ApiKey` once it runs through forge-runner. |
| Type 1 / Type 2 | Type 1: public conversation messages; settlement moving to forge-runner. Type 2: default URL, message names. |
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
| 1 | Worker runs turns through forge-runner; delete the in-process execution path. | Existing Worker tests pass against forge-runner; no `PipelineRunner` use remains in the Worker. |
| 2 | Metering: billing Service Bus namespace + `run-settlement` queue (forge-infra, data layer); `MemberId` on `RunRequest`; forge-runner sends settlement; ForgeAPI consumer calls `SettleRunAsync`; ForgeUI and ForgeAPI stop settling inline; ForgeUI balance reads via `GetAccount`. | A Rooms run, a `forge exec` run, and a durable turn each debit exactly once; a redelivered settlement message debits nothing extra; ForgeUI has no billing write outside the transitional exception. |
| 3 | Host owner link; `MemberId` carried in the mission-command message. | A second member's lookup of a conversation returns not found. |
| 4 | Build and push Host/Worker images; deploy 525 (`what-if` first). | Both Container Apps run; Host `/health` 200 from inside the environment. |
| 5 | ForgeAPI: 26 messages + event stream, auth, `MemberId` forwarding. | Each Desktop call succeeds through ForgeAPI with a platform key. |
| 6 | Desktop: message route strings in `ConversationHostClient`, `Bearer` on the conversation client, default URL → ForgeAPI; update [Default-Path Acceptance](../design/default-path-acceptance.md). | The default-path action above passes on the published bundle. |
