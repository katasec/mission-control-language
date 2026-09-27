# Phase 52.1 — Cloud conversations

> **Status: design (2026-09-28). Not build-ready** — see [Open questions](#open-questions).
> Hub: [Phase 52](phase-52-desktop-simplification.md). Selected from the backlog item
> "Cloud as the default local target (replacing Kind)".

**Goal:** the Desktop's default Conversation Runtime is the hosted Conversation service, reached
through ForgeAPI. Kind stops being the normal path.

## Locked design

| Area | Decision |
|---|---|
| Hosted service | Deploy `forge-infra/dev/525-conversation-app` (Conversation Host + Worker) with real images. The Host keeps internal-only ingress. |
| Public entry | ForgeAPI (Tier 1) adds an authenticated conversation route that proxies to the internal Conversation Host. Reuse ForgeAPI's existing platform-key auth, ownership, billing, and streaming patterns. |
| Contract | The Desktop speaks the same Conversation contract it speaks to Kind today. Only the base URL and the platform-key credential change. Any other change needed is contract drift and is fixed, not worked around. |
| Desktop default | `ConversationRuntime:BaseUrl` absent → ForgeAPI's conversation route. An explicit value still overrides. |
| Kind | Remains available as an explicit override until 52.2 deletes the tunnel. |

## Architecture-security review

| Question | Answer |
|---|---|
| Bounded context / data owner | Durable conversations. Conversation Host owns the 350 Storage (Table/Blob). |
| Public entry point | ForgeAPI (Tier 1): platform-key authentication, then routes to the internal Conversation Host (Tier 2). |
| Tier 2 components | Conversation Host (internal ingress); Worker (no ingress, Service Bus only). |
| Tier 3 stores / transports | 350 Storage and Service Bus; no public ingress; identities as provisioned by 350. |
| Cross-context store access | No. ForgeAPI holds no conversation Storage or Service Bus credentials. |
| Secrets per component | ForgeAPI: none new. Host: 350 host identity. Worker: 350 worker identity + `Mcl-ApiKey`. |
| Type 1 / Type 2 | Type 1: new public route to the conversation context. Type 2: base URL default, route prefix. |
| Enforcement and proof | 525 `external: false` ingress; a direct call to the Host's FQDN from outside the environment fails; an authenticated call through ForgeAPI succeeds. |

## Default path (new)

| Fact | Value |
|---|---|
| Artifact | Published Desktop bundle, zero-argument launch, no `ConversationRuntime:*` or `FORGE_*` overrides. |
| Mission Runtime URL | `https://api.forge.katasec.com` (unchanged); `/health` checked. |
| Conversation Runtime URL | ForgeAPI conversation route (URL fixed in Task 2); `/health` checked. |
| Credential | Platform key from `forge login` (see open question 2). |
| Action / result | Send a message in a Project conversation; the streamed reply appears and survives reopening the Project. |

## Tasks

| # | Task | Done when |
|---|---|---|
| 1 | Build and push real Host and Worker images; replace the `:pending` tags; `make 525-conversation-app-what-if` then `make 525-conversation-app`. | Both Container Apps run the new images; Host `/health` returns 200 from inside the environment. |
| 2 | ForgeAPI conversation route with auth, ownership, billing, and SSE pass-through. | A curl script runs start → command → `events` stream through ForgeAPI with a platform key, and the same script passes against local Kind by changing only URL and key. |
| 3 | Desktop default → ForgeAPI route; update [Default-Path Acceptance](../design/default-path-acceptance.md). | Default-path action above passes on the published bundle. |

## Open questions

1. **Existing patterns.** Name the ForgeAPI code that already implements platform-key ownership,
   billing debit, and streaming pass-through, so Task 2 reuses rather than reinvents.
2. **Desktop credential.** How does the Desktop obtain the platform key today for cloud missions
   (`~/.forge` from `forge login`, or otherwise)? Conversations use the same source.
3. **Network path.** Confirm ForgeAPI and the Conversation Host share the Container Apps
   environment, so internal ingress is reachable.
4. **Route shape.** Prefix under ForgeAPI (e.g. `/conversations/...` mounted at a fixed base).
