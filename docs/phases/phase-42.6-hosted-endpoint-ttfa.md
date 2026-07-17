# Phase 42.6 — Hosted endpoint + time-to-first-awesome

> **Status: Design (2026-07-15; re-architected 2026-07-18).** The payoff spoke. Expose the hosted `/v1`
> endpoint on `forge.katasec.com` as a **multi-tenant** surface: a request carrying a platform key is
> authenticated, **routed to a mission by path**, run on the internal runner, **metered**, and debited.
> Definition of done **is the demo** — a stranger reaches a cited, past-cutoff answer through their own
> Claude Code in 2–3 commands.
>
> **Re-architected 2026-07-18 (see [Architecture](#architecture-re-architected-2026-07-18) below).** The
> earlier design put the public `/v1` on the runner reading Postgres directly. Rejected: a public,
> mission-executing process (`kind: exec` shells out) must not hold DB creds. New shape follows the
> [Phase 42 north-star tiering](phase-42-forge-cloud.md#3a-deployment-topology--the-north-star-locked-2026-07-18):
> a **dedicated `ForgeAPI`** (tier 1) terminates auth + routing; the **runner stays internal** (`/run`, no
> DB); **auth/billing is its own bounded context** (`ForgeMission.Billing` over a separate `authbilling_db`).
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:**
> [42.3](phase-42.3-tool-capable-enriching-responder.md) (agentic seam), [42.4](phase-42.4-container-convergence.md)
> (one image), [42.5](phase-42.5-platform-identity-keys.md) (platform keys) · **Consumes:** Phase 39
> metering/billing (live), Phase 39.4 OCI catalog (live) · **AOT rules:** [CLAUDE.md](../../CLAUDE.md).
>
> **Done when (== the phase's headline demo):**
> ```
> forge login && forge claude @websearch
>   > "what shipped in the Claude API this week?"
>   → grounded, source-cited answer  ·  debited N µ$ against free credits
> ```
> against `forge.katasec.com`, with **no Anthropic/OpenAI account** on the user's side.

## Context an implementer needs (verified 2026-07-15)

- **Metering + billing are live (Phase 39):** `UsageTrackingChatClient` meters each provider call inside a
  mission; `BillingService` does balance-check (pre) + debit (post) against per-user `ledger_entries`. The
  live login→"Granted 5,000,000 µ$" and `@guard`→"Debited 224 µ$" prove the exact path. **We wrap the hosted
  `/v1` request in this; we do not rebuild it.**
- **OCI catalog is live (39.4):** built-in missions pulled from `ghcr.io/katasec` by pinned digest,
  public/anonymous. `@websearch`, `@guard`, `@debate`, etc. resolve here — hosted routing maps a mission
  handle → the catalog artifact the runner loads.
- **The 42.3 enrichment cache needs a *shared* implementation in cloud.** ACA may run >1 replica or scale to
  zero, so a tool loop's later calls can land on a different replica than the one that ran the pre-agent
  segment. The content-addressed key (hash of the conversation prefix) is replica-independent by design —
  but the **store** behind it must be shared (Rooms Postgres / a cache), swapped in via the `ISessionStore`
  seam. Miss ⇒ re-run pre-agent.
- **Provider keys are ours, server-side.** The hosted runner already has provider keys (`MCL_API_KEY`,
  `XAI_API_KEY`, …) in its ACA config; the user's platform key never carries a provider key. The mapping is:
  platform key → user + balance; provider keys → the runner's own env.

## Architecture (re-architected 2026-07-18)

Follows the [north-star tiering](phase-42-forge-cloud.md#3a-deployment-topology--the-north-star-locked-2026-07-18)
(`CDN → tier 1 → tier 2 → tier 3`, adjacent-only). Two load-bearing calls, both driven by *"assume the
public endpoint is hostile"*:

- **`ForgeAPI` is a dedicated tier-1 service, separate from `ForgeUI`.** Stateless, platform-key auth,
  machine clients (Claude Code / Codex), bursty — a different animal from the stateful OIDC/browser rooms
  app. **42.6 auth + routing land on `ForgeAPI`**, not on `ForgeUI`, not on the runner.
- **The runner never faces the internet and never holds DB creds.** It executes missions (and shells out
  via `kind: exec`) → highest-value compromise target. `ForgeAPI` is its only ingress.

**`ForgeAPI` is, in pattern terms, an API gateway** (edge auth, routing, rate-limit, usage metering +
billing, reverse-proxy — an *API-monetization* gateway specifically). Keep it that: cross-cutting edge
concerns only, **no mission/business logic** (the runner stays the brain). North-star is the textbook shape
— a **stateless** gateway calling a tier-2 auth/billing service (token introspection + metering); the demo
cut's in-proc `authbilling_db` is the reversible compromise. Because it's a standard pattern, swapping in
APIM / Envoy / Kong at scale is a recognized migration, not a rewrite.

**Relay = pass-through `/v1`, not `/run` (decided 2026-07-18).** An agentic turn (`forge claude`) is **N+1
requests**: the runner emits `tool_use`, but the tool runs on the *client's* machine, so control returns to
Claude Code and comes back as a fresh `tool_result` request that resumes the turn (the 42.3 re-entrancy
seam). The internal `/run` contract is a single RPC — it can't hand control back to the client mid-turn, so
it carries `forge exec` (one-shot) but **not** `forge claude`'s tool loop. So `ForgeAPI` **reverse-proxies
the `/v1` wire** (streaming SSE included) to the runner's existing door for **both** verbs — one relay;
provider keys + the tool loop stay on the runner; `ForgeAPI` skims `usage` off the wire response to debit.
`/run` stays the rooms-internal path (`RoomAgentInvoker`), untouched.

**Auth/billing is its own bounded context.** A new AOT-clean `ForgeMission.Billing` lib (`CostMeter` +
ledger-facing `BillingService`, moved out of `ForgeUI/Services`) over a **separate `authbilling_db`** — a
second database on the *same* Postgres server as `rooms_db`, sharing nothing but `userId`. `ForgeAPI`
reaches it with **raw Npgsql, not EF Core** (EF is the one real AOT blocker; the two-table schema —
`platform_keys` + `ledger_entries` — makes Npgsql trivial and keeps `ForgeAPI` an AOT target). `rooms_db`
keeps its EF context on the non-AOT `ForgeUI`.

**The demo cut is a two-way door.** For F&F we collapse the tier-2 auth/billing service *inline*: `ForgeAPI`
calls `ForgeMission.Billing` in-process against the scoped `authbilling_db` (keys + ledger only; the runner
still holds nothing). Every north-star seam is pre-cut, so extraction later is *move the box + swap the
in-proc call for an HTTP client* — **no data migration, no client-visible change.**

![Phase 42.6 demo cut — reversible door](phase-42.6-demo-cut-door.svg)

> **AOT sequencing:** build `ForgeAPI` AOT-*clean* from day one (slim builder + JSON source-gen + Npgsql —
> cheap when greenfield), but flip `PublishAot=true` as a **fast-follow after the endpoint works
> end-to-end** — the macOS OpenSSL/brotli linker dance + linux-x64 cross-compile shouldn't block the demo's
> critical path. Staying AOT-clean keeps the flip a switch, not a rewrite.

## Design

**Request path (one hosted turn):**
```
Claude Code ──/v1/messages + Bearer <platform key>──▶ ForgeAPI (tier 1, forge.katasec.com)
   ├─ AUTH   : resolve platform key → (userId, balance)         [42.5 · ForgeMission.Billing]
   ├─ ROUTE  : path /@handle → mission                          [OCI catalog]
   ├─ BALANCE: reject 402 if insufficient                       [ForgeMission.Billing]
   ├─ RELAY  : reverse-proxy /v1 wire → internal runner's door · mission · UsageTrackingChatClient  [runner, tier 2 + 39]
   │            └─ Scout live retrieval → 🌐   ·   enrich-once / N+1 tool loop  [41 + 42.3]
   ├─ DEBIT  : full-mission usage (runner) on wire tail → authbilling_db ledger_entries -= cost   [ForgeMission.Billing]
   └─ RETURN : grounded, cited answer  (+ balance header)
```

**Routing — the key identifies the *principal*, the path identifies the *mission*.** (Framing corrected in
external design review: it is **not** "key→mission" — that conflated two independent resolutions.)

```
platform key        → principal (user + account + balance)
mission handle/path → mission artifact (OCI)
principal + mission → authorization + billing policy
```

**Path-based, decided:** `forge.katasec.com/m/websearch/v1/messages`. Explicit, cache-friendly, easy to
demo, and it keeps the `model` field free — overloading `model` to select a mission collides with the model
id the client legitimately sends. The CLI keeps exposing the friendly `@websearch`. **Live-confirmed
(wire capture, 2026-07-16):** the real `claude` CLI sends `model: "claude-sonnet-4-6"` on every request —
model-based routing would have asked us for a mission named after the client's model.

`forge claude @websearch` (42.2, hosted mode) sets `ANTHROPIC_BASE_URL=https://forge.katasec.com/@websearch`
and the platform key as the token.

**Multi-tenancy.** One converged image, **routing by key+handle**, not a container per user (cold-starts +
cost). Per-user isolation is at the ledger/auth layer; the mission run is stateless per request (plus the
shared re-entrancy store). Heavy/custom-exec missions that need hard isolation are a later concern (ties to
Phase 39.5/39.7).

**`forge missions`** — list the on-tap OCI catalog (name + one-line value), so the user can discover
`@websearch` etc. A `/missions` probe already exists on the runner.

## UX decisions (2026-07-17)

The client-facing shape of the demo, worked through against prior art (NuGet, PowerShellGet, winget,
Helm-over-OCI).

1. **Verbs — `forge exec` (one-shot) vs `forge claude` (agentic), one endpoint.**
   `forge exec @handle "<prompt>"` = fire-and-forget: run the mission once, stream the answer, exit —
   the top-of-funnel awesome moment. `forge claude @handle` = the full agentic Claude Code session
   pointed at the same hosted mission. Two verbs because the intents differ (one-shot Q&A vs
   Claude-doing-work); same hosted `/v1` underneath. `<prompt>` is the mission's primary/free-text input
   (structured `inputs:` surface later via `--input key=value`). (`exec` overlaps `kind: exec` in name
   only — as a CLI verb "execute this mission" it's unambiguous.)

2. **Output — stream + a trust footer, cost pulled on demand.** Stream the answer token-by-token, then a
   single footer line: **grounded/verified badge · source count** (`--sources` expands to URLs).
   **No cost/balance printed per call** — that's pull-not-push (real services don't shove spend at you
   every request); balance is on demand via `forge whoami`. The footer carries the *trust* signal (MCL's
   thesis), not the receipt.

3. **Discovery — `forge missions` reads a curated index, NOT the raw OCI registry.** Sharp constraint
   from Helm-over-OCI: **OCI registries can't be listed/searched** (`GET /v2/_catalog` is optional and
   ghcr.io/most public registries gate or disable it — this is why `helm search` doesn't work on OCI
   registries). OCI stays the **distribution** layer (pull by digest, already done); **discovery is a
   separate index.** For 42.6 (built-in catalog): a small **curated catalog index** the default source
   serves (`@handle · version · one-line · verified`), which `forge missions` reads. Later (user missions,
   39.5) that index grows into a search service (the forge equivalent of nuget.org search / Artifact Hub).
   **Handles namespace like Helm's `repo/chart`:** `@katasec/websearch` fully-qualified, `@websearch`
   resolves from the default/priority source. **Sources are a registered, extensible concept** (à la
   nuget.config sources / `Register-PSResourceRepository` / `winget source add` / `helm repo add`) with
   per-source **trust + priority** — build the single default public source now, leave the seam (it's the
   marketplace on-ramp). Verified tick reuses the **38.5 identity seal / publisher**.

4. **First-run delight — steer the first `exec` to a past-cutoff question.** The undeniable grounded-vs-
   naked delta comes from a **past-training-cutoff** query (a naked model *can't* know; `@websearch`
   answers with live citations — a reasoning trap is weaker, a good naked model gets it too). So on a
   successful `forge login`, **print one suggested command** — a current-events "what shipped in X this
   week?" style prompt — so the awesome moment is the literal next thing the user can paste.

## Tasks (chronological)

Tasks 1–3 are the **foundation** the re-architecture adds (the split, the DB, the new service); 4–5 are the
original auth + routing; 6+ are billing, cache, CLI, and deploy.

1. **`ForgeMission.Billing` lib (foundation). ✅ DONE (2026-07-18).** Extract `CostMeter` + a ledger-facing
   `BillingService` (balance-check / debit / grant) from [ForgeUI/Services](../../src/ForgeUI/Services/) into a
   new AOT-clean project. Depends only on an `ILedgerStore` abstraction + POCOs — no reflection, no runtime
   `JsonSerializerOptions`. **Referenced by both `ForgeUI` and `ForgeAPI`** (one meter, not two). Done when
   the room path (`RoomAgentInvoker`) still bills identically through the lib.
   - **Shipped:** new [`ForgeMission.Billing`](../../src/ForgeMission.Billing/) project (`IsAotCompatible=true`,
     builds 0 warnings) holding the moved `LedgerEntry`/`LedgerEntryKind` POCOs, the `ILedgerStore` interface,
     `CostMeter`, and `BillingService`. `IConfiguration` swapped for an injected `BillingOptions` record so the
     lib carries no config-binder reflection (host binds it from config at startup). `Rooms.Data` keeps the EF
     `LedgerStore : ILedgerStore` impl + `LedgerEntryConfiguration` and now references Billing; `ForgeUI` +
     tests re-pointed via `using ForgeMission.Billing`. Added to `ForgeMission.slnx`.
   - **Verified:** solution + `ForgeUI` build clean; the 11 `Ledger`/`PlatformKeyResolver` tests pass against a
     real Postgres container — the room billing path (append / balance-as-`SUM` / idempotent grant) is
     byte-identical through the extracted lib. Next: Task 2 (`authbilling_db` split).
2. **`authbilling_db` split (foundation).** Move `platform_keys` + `ledger_entries` into a **separate
   database on the same Postgres server** (see the [infra checklist](#infra-checklist-katasecforge-infra)).
   `ForgeAPI` accesses them via **raw Npgsql** (`IPlatformKeyStore` / `ILedgerStore` implementations, no EF)
   so it stays an AOT target. Bootstrap schema with idempotent `CREATE TABLE IF NOT EXISTS` at startup (no
   EF migrations on this DB). `userId` is the only cross-context link to `rooms_db` — no cross-DB FK. Migrate
   the tiny existing F&F ledger/keys (copy or fresh start).
   - **✅ DONE (2026-07-18, full split — decided w/ Ameer).** `ForgeMission.Billing` now owns the whole
     billing bounded context: `PlatformKey`/`LedgerEntry` POCOs + `IPlatformKeyStore`/`ILedgerStore` +
     `PlatformKeyMinting`/`PlatformKeyResolver`, plus raw-Npgsql [`NpgsqlLedgerStore`](../../src/ForgeMission.Billing/NpgsqlLedgerStore.cs)
     / [`NpgsqlPlatformKeyStore`](../../src/ForgeMission.Billing/NpgsqlPlatformKeyStore.cs), idempotent
     [`AuthBillingSchema.EnsureCreatedAsync`](../../src/ForgeMission.Billing/AuthBillingSchema.cs), and an
     `AddAuthBilling(connString)` DI extension. **rooms_db cut over:** EF `LedgerStore`/`PlatformKeyStore` +
     configs + DbSets deleted; a `DropLedgerAndPlatformKeysFromRooms` migration drops both tables (Down fully
     recreates → reversible). **ForgeUI re-pointed** to `authbilling_db` (`ConnectionStrings:AuthBillingConnection`,
     else derived from `WriteConnection` with `Database=authbilling_db`); bootstraps the schema on every boot
     in all envs. **One meter, one ledger** — the room path and the coming hosted `/v1` both bill against it.
   - **Data migration = fresh start (no copy):** `MemberProvisioningService` calls the idempotent
     `GrantStartingCredit` on every login, so an empty `authbilling_db` self-heals — existing F&F members get
     re-granted 5,000,000µ$ on next sign-in. Acceptable at F&F scale; a copy is optional and unneeded.
   - **⚠️ prerequisite:** the app bootstraps *tables*, not the *database* — `authbilling_db` must exist on the
     server first (infra checklist item 1 / a local `CREATE DATABASE authbilling_db`), else ForgeUI fails to
     start on the missing-DB connection.
   - **Verified:** solution + ForgeUI build clean (Billing is `IsAotCompatible`, 0 warnings incl. Npgsql);
     the full 61-test Rooms suite passes, with the 25 ledger/platform-key/resolver tests now exercising the
     **raw-Npgsql stores against real Postgres** (caught + fixed the `SUM(bigint)→numeric` cast). Next: Task 3
     (`ForgeMission.Api` gateway).
3. **`ForgeMission.Api` service (foundation) — the API-gateway tier.** New tier-1 minimal-API host —
   `WebApplication.CreateSlimBuilder` + JSON source-gen, AOT-clean. Health probe; a **streaming reverse-proxy**
   of the `/v1` wire to the runner's internal door (SSE pass-through; **not** `/run` — see the relay note in
   Architecture). Thin gateway only: auth, route, meter, forward — no mission logic. No DB except the scoped
   `authbilling_db`. This is the public `/v1` edge from here on; the runner's own `/v1` doors stay internal
   (and back local `forge serve` / `--container`).
   - **✅ FOUNDATION DONE (2026-07-18).** New [`ForgeMission.Api`](../../src/ForgeMission.Api/) slim host:
     `/health` + [`WireProxy`](../../src/ForgeMission.Api/WireProxy.cs) — a hand-rolled (no YARP) streaming
     reverse-proxy of `/v1/{**rest}` → the runner (`RunnerBaseUrl`), forwarding both verbs with
     `ResponseHeadersRead` + `DisableBuffering` so SSE relays as it arrives; hop-by-hop headers stripped both
     ways. References only the AOT-clean `ForgeMission.Billing` (auth/billing wraps land in tasks 4–6).
     `PublishAot` stays off for now per the AOT-sequencing note (flip as a fast-follow). Added to the slnx.
   - **Verified locally (two-service):** booted the runner (single-mission mode) behind ForgeAPI and drove
     `POST /v1/messages` through the gateway → runner → real provider. Clean **HTTP 200** with a full
     Anthropic `msg_…` body relayed (elevator-pitch via OpenAI, 3.9s), and the error path also relays (a
     mission-internal 500 flows back verbatim). Gateway forwards + streams; mission outcome is orthogonal.
   - **Deferred to their tasks:** per-handle path prefix `/m/{handle}` selecting the mission (task 5); auth
     middleware (task 4); metering/debit off the wire tail (task 6). The relay itself stays dumb.
4. **Auth middleware (on `ForgeAPI`).** Validate the platform key (42.5, via `ForgeMission.Billing`) on every
   `/v1/*` request → attach `(userId, balance)`; 401 on bad/revoked key.
5. **Routing (on `ForgeAPI`).** Map handle (path segment `/@handle`, recommended) → OCI catalog mission the
   runner loads → reverse-proxy the `/v1` turn to the runner's door with that mission selected; 404 on
   unknown handle.
6. **Wrap the run in billing + the spend-abuse trigger ladder (DECIDED 2026-07-16).** Reuse
   `ForgeMission.Billing` + `UsageTrackingChatClient` as-is. **Metering source (2026-07-18):** debit from the
   runner's `UsageTrackingChatClient`, which sees the **whole** mission (classify + search + synth + judge) —
   surfaced to `ForgeAPI` on the wire tail (an HTTP trailer or a final `forge-usage` SSE event). **Do not**
   debit from the client-facing Anthropic `usage` block — it counts only the terminal segment and under-bills.
   The known hole: `balance-check → run → debit` is
   not a strict ceiling — cost is unknown until after, the check-to-debit window is ~60s (a live run took
   71s), and concurrent requests all pass `balance > 0` before any debit lands. **Accepted at F&F scale**
   (trusted population, stop-at-zero freeze bounds accidents to cents). The ladder — each rung built only
   when its trigger fires:
   1. **Freeze at zero** — already live ([RoomAgentInvoker](../../src/ForgeUI/Services/RoomAgentInvoker.cs)).
   2. **Edge rate limit — LAUNCH REQUIREMENT for this spoke** (strangers + public endpoint = new threat
      model): Cloudflare per-IP rule, pure config, no code. Set generously (e.g. 60/min/IP) — 42.3 tool
      loops legitimately burst N+1 calls per turn.
   3. **In-app caps (pre-recorded, build on evidence of expensive-single-run abuse — rate limits see
      request counts, not dollars):** (a) per-run cost cap — `UsageTrackingChatClient` already accumulates
      cost mid-run; abort past a configured ceiling (~50,000µ$ ≈ 200× an observed `@guard` run); (b)
      per-member concurrency cap (e.g. 2 in flight). ~20 lines total.
   4. **Transactional credit reservation (pre-recorded, trigger = paying users / 39.6):** append
      `Reservation(−cap)` iff `sum(entries) ≥ cap` (atomic via `pg_advisory_xact_lock(memberId)`); on
      completion append `Release(+cap)` + `Debit(−actual)`. Balance stays `sum(entries)`; needs a
      stale-reservation sweeper; also closes the best-effort-debit leak (a swallowed `SettleRunAsync`
      failure currently makes a run free).
   Return balance in a response header; 402 when short.
7. **Shared enrichment cache:** implement the 42.3 content-addressed store (`ISessionStore` seam) over a
   shared cache — ACA may run >1 replica or scale to zero, so a continuation can land on a different replica
   than the turn that enriched. Cache miss ⇒ re-run pre-agent (never answer ungrounded). Lives with the app
   tier, not `authbilling_db` (a run-continuation cache, not billing data).
8. **CLI hosted mode:** **`forge exec @handle "<prompt>"`** (one-shot, stream + trust footer — the
   top-of-funnel awesome moment) and **`forge claude @handle`** (agentic) both resolve `@handle` → the hosted
   `ForgeAPI` URL + platform key. **`forge missions`** → reads the curated catalog index (not the raw OCI
   registry — see [UX decision 3](#ux-decisions-2026-07-17)).
9. **Deploy + verify the headline demo** (details in the checklist below). `forge login && forge exec
   @websearch "<past-cutoff question>"` → cited answer, ledger debited; and `forge claude @websearch` agentic.
   Verify the **anti-hallucination guarantee**: a stale-fact question triggers the classify→search path (not
   a confident-wrong answer).
10. **`forge codex @websearch`** against the same endpoint once 42.7 lands (`/v1/responses` door).

### Infra checklist (`katasec/forge-infra`)

The re-architecture adds infra, sequenced with the tasks above. All in the layered Bicep (`dev/*`); redeploy
the relevant layer (see [deploy.md](../design/deploy.md)).

- [ ] **`authbilling_db`** — one `Microsoft.DBforPostgreSQL/flexibleServers/databases` child on the existing
      `psql-forge-dev` server (same instance → no new server cost, no new firewall/VNet rules). *(Task 2.)*
- [ ] **KV secret + env** — an `AuthBillingConnection` string (same host/creds as rooms, `Database=authbilling_db`)
      → Key Vault → `ForgeAPI` env. Dedicated secret so it can rotate/relocate independently. *(Task 2.)*
- [x] **Table bootstrap** — ✅ handled in-app: `AuthBillingSchema.EnsureCreatedAsync` runs idempotent
      `CREATE TABLE IF NOT EXISTS` at host startup (ForgeUI today, ForgeAPI later). Infra only needs to
      create the **database** (item 1); tables self-provision. *(Task 2.)*
- [ ] **`ca-forge-api-dev` container app** — new tier-1 ACA app for `ForgeAPI`: ingress, custom domain/HTTPS
      on `forge.katasec.com`, network access to `psql-forge-dev` and to the internal runner. *(Task 3 / 9.)*
- [ ] **Cloudflare per-IP rate limit** — pure config, **launch gate** (public endpoint + strangers). *(Task 6.)*
- [ ] **Tier network policy** (north-star direction) — restrict runner ingress to `ForgeAPI`/`ForgeUI` only;
      keep the runner off public ingress. *(Hardening; not a demo blocker.)*

## Out of scope

- Authoring/publishing user missions (only built-in catalog here) — Phase 39.5.
- Paid top-ups / subscription tiers — Phase 39.6.
- MCP door — 42.8.
- Per-key rate limiting beyond credit cap; heavy-mission container isolation — later hardening.
