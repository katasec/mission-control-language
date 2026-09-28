# Phase 52.1 — Cloud conversations

> **Status: build-ready (2026-09-29).** Hub: [Phase 52](phase-52-desktop-simplification.md).
> Builds toward the [command-bus architecture](../design/command-bus-architecture.md). Selected
> from the backlog item "Cloud as the default local target (replacing Kind)".

**Goal:** the Desktop talks to the cloud. Every durable turn runs in the one forge-runner image
and is metered exactly once, and no new point-to-point command path is built.

## Locked decisions

| Area | Decision |
|---|---|
| Client setting | The Desktop's runtime URLs choose cloud or local; default cloud. ForgeAPI never chooses. |
| API style | Message-based ([Phase 42.6](phase-42.6-hosted-endpoint-ttfa.md#api-design--message-based-decided-2026-07-18)). One message per purpose: every call the Desktop makes today in `ConversationHostClient` (26 calls + the event stream) becomes a ForgeAPI message, `POST /api/{MessageName}`, reusing the existing DTOs from `ForgeMission.Conversations.Contracts`. |
| Commands vs queries | ForgeAPI publishes write messages to the conversation **ingress** queue and awaits the Host's result on the **reply** queue (Service Bus request/reply); the existing response DTO is returned unchanged. Read messages (run history, conversation, event stream) are direct queries to the Host. |
| Identity | Azure CIAM via `forge login` → platform key in `~/.forge` → `Bearer`. Only the platform resolves it to a `MemberId`; no client-supplied identity is trusted. |
| Ownership | The Host records the creating `MemberId`; every lookup is scoped by `(MemberId, id)`; a non-owner gets not found. |
| One runner image | forge-runner absorbs the Conversation Worker by **moving** its existing queue consumer, command processor, and executor (~755 lines) into the runner — not rebuilding them. HTTP `/run` and the moved queue consumer are two thin entry points into one engine. The Worker project and image are deleted. The Host keeps publishing to the existing work queues, so turns survive restarts. |
| Usage | Both entry points build their LLM client through the runner's existing `BuildRunner` (`UsageTrackingChatClient` + `UsageAccumulator`). The Worker's deployment-default provider becomes a runner setting. |
| Continuation | The saved opaque continuation is the only mechanism. `ExecuteMission` moves to it; transcript replay (`RunRequest.History` + `ToolContinuationGate`) and the enrichment cache are removed. |
| Billing service | Extract Billing into its own tier-2 container hosting `ForgeMission.Billing` unchanged. It owns `authbilling_db`, consumes the financial queue, and answers queries (balance, platform-key resolution). ForgeAPI loses its `authbilling_db` credentials. |
| Metering | forge-runner sends `{MemberId, RunId, Segment, MissionRef, Usage}` to the financial `private-run-settlement` queue after every run segment. Billing calls `SettleRunAsync` with `RunId`+`Segment` as the idempotency token. ForgeUI and ForgeAPI stop settling inline. |
| Identity hop | ForgeAPI resolves `MemberId` (query to Billing) → ingress message → Host stores owner → work-queue command → forge-runner → settlement message. |
| Transitional exception | ForgeUI keeps `authbilling_db` write access for exactly two sign-in writes: key issuance (`PlatformKeyEndpoints.IssueAsync`) and `GrantStartingCreditAsync` (`MemberProvisioningService`). Removal: backlog item "Move `forge login` key issuance". Verification: no other ForgeUI code references a `BillingService` write. |
| Out of scope | Rooms and `forge exec` moving to the bus; deleting HTTP `/run`; Rooms extraction; unauthenticated local mode; `forge dev start`; local ForgeAPI ([backlog](../backlog.md)). |

## Queues

Classes and enforcement: [Security Architecture](../design/security-architecture.md#service-bus-queue-classes).

| Queue | Class | Namespace | Sender | Listener |
|---|---|---|---|---|
| `conversation-ingress` | Ingress | edge-facing | ForgeAPI | Conversation Host |
| `conversation-reply` (sessions) | Reply | edge-facing | Conversation Host | ForgeAPI |
| `private-mission-command` (renamed from `mission-command`) | Internal work | `sb-forge-conversation-dev` | Conversation Host | forge-runner |
| `private-conversation-progress` (renamed from `conversation-progress`) | Internal work | `sb-forge-conversation-dev` | forge-runner | Conversation Host |
| `private-run-settlement` | Financial | financial | forge-runner | Billing |

## State ownership

Each store has exactly one owner. Do not add a cache, table, or database to hold anything below.

| Store | Owner | Holds | Change |
|---|---|---|---|
| Azure Table (events, checkpoint incl. continuation, run index, Orleans) | Conversation Host | Durable conversations | None |
| Azure Blob `forgeconversationartifacts` | Conversation Host | Artifacts (wired, no writer yet) | None |
| Service Bus session state on `private-mission-command` | forge-runner | Working copy of a paused continuation | Owner moves from Worker to runner |
| Postgres `authbilling_db` | Billing service | Keys, ledger, balances | Owner moves from ForgeAPI; ForgeUI keeps the transitional exception |
| Postgres `rooms_db` | ForgeUI | Rooms | None |
| Enrichment cache (Postgres/in-memory) | forge-runner | Replay context | **Deleted** with replay |
| `~/.forge` | `forge login` | Platform key | None |

## Architecture-security review

| Question | Answer |
|---|---|
| Contexts and data owners | Conversations → Host (Table/Blob). Billing → Billing service (`authbilling_db`). Rooms → ForgeUI (unchanged, deviation recorded). |
| Public entry point | ForgeAPI: platform-key auth; publishes to ingress, awaits reply, queries tier-2 owners. |
| Tier 2 | Conversation Host, forge-runner, Billing service. |
| Tier 3 | Table/Blob, `authbilling_db`, internal-work and financial queues. |
| Cross-context store access | None. ForgeAPI holds no store credentials after this phase. |
| Secrets | Managed identities with queue-scoped roles per the Queues table; Billing: `authbilling_db`; Host: 350 identity; runner: provider keys. Worker identity and `Mcl-ApiKey` binding removed. |
| Type 1 / Type 2 | Type 1: public conversation messages; Billing extraction; settlement path; one runner. Type 2: queue names; Standard-tier namespaces (Security Architecture exception). |
| Proof | A second member's conversation returns not found; ForgeAPI has no `authbilling_db` setting; no edge identity has a right on internal-work or financial queues; each run segment debits once. |

## Default path (new)

| Fact | Value |
|---|---|
| Artifact | Published Desktop bundle, zero arguments, no `MissionRuntime:*`, `ConversationRuntime:*`, or `FORGE_*` overrides, after `forge login`. |
| Mission Runtime URL | `https://api.forge.katasec.com`; `/health` checked. |
| Conversation Runtime URL | ForgeAPI; `/health` checked. |
| Action / result | Through the published bundle's Application Host `/transport/*` actions (the calls the UI will make), submit a Project turn; its streamed events include the reply; the run appears in run history; the member is debited once per run segment. The Desktop UI is a mock-up today, so clicking through the UI is acceptance for the task that brings the UI to life, not for this phase. |

## Tasks

| # | Task | Done when |
|---|---|---|
| 1 | ✅ **Done 2026-09-29** — Worker moved into forge-runner; see [completion record](phase-52.1-cloud-conversations_completed.md#task-1--forge-runner-absorbs-the-worker). | Moved Worker tests pass inside forge-runner; Host production code unchanged; no Worker project remains. |
| 2 | Saved continuation only: `ExecuteMission` moves to it; remove transcript replay and the enrichment cache. | `ExecuteMission` tool continuation passes with a continuation; no `History` replay or `ToolContinuationGate` remains. |
| 3 | Billing service: new container hosting `ForgeMission.Billing`; queries for balance and platform-key resolution; ForgeAPI resolves keys and balances through it and drops `authbilling_db`. | ForgeAPI auth and `GetAccount` pass with no `authbilling_db` setting. |
| 4 | Metering: financial namespace + `private-run-settlement`; `MemberId` and segment on runs; runner settles per segment; Billing consumes; ForgeUI and ForgeAPI stop inline settlement; ForgeUI balance reads query Billing. | A Rooms run, a `forge exec` run, and a paused-then-resumed durable turn each debit once per segment; redelivery adds nothing. |
| 5 | Host: consume `conversation-ingress`, reply on `conversation-reply`; owner link. Queries stay direct. | Each write message round-trips over the bus; a second member gets not found. |
| 6 | Infra: edge-facing and financial namespaces (Standard tier), `private-` renames of the internal-work queues, rename the runner's `ConversationWorker:Default*` settings to runner names, an alert on the runner's queue-consumer fault log, queue-scoped roles, Billing and Host images, runner queue rights, 525 without the Worker (`what-if` first). | All containers healthy; role assignments match the Queues table; local auth disabled. |
| 7 | ForgeAPI: 26 messages + event stream — writes via ingress/reply, reads via Host queries, `MemberId` attached. | Each Desktop call succeeds through ForgeAPI with a platform key. |
| 8 | Desktop: message route strings, `Bearer` on the conversation client, default URL → ForgeAPI; update [Default-Path Acceptance](../design/default-path-acceptance.md). | The default-path action above (via `/transport/*`) passes on the published bundle. |

## Known limitations

| Limitation | Handling |
|---|---|
| If forge-runner's queue consumer faults at runtime, it stays down until the process restarts; `/health` still reports OK because `/run` is unaffected (by design, so a queue fault cannot take down Rooms or `forge exec`). | The fault is logged at error level; Task 6 adds an alert on it. |
