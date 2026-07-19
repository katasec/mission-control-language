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
> **Done when (== the phase's headline demo). BOTH verbs must work (decided 2026-07-18, Ameer)** — this is
> not an either/or, so **5a and 5b are both in scope**:
> ```
> # one-shot (API A · task 5a) — the top-of-funnel awesome moment
> forge login && forge exec @websearch "what shipped in the Claude API this week?"
>   → grounded, source-cited answer  ·  debited N µ$ against free credits
>
> # agentic (API B · task 5b) — Claude Code with the mission as its brain
> forge login && forge claude @websearch
>   > "what shipped in the Claude API this week?"
>   → grounded, source-cited answer  ·  debited N µ$ against free credits
> ```
> against the hosted endpoint (`api.forge.katasec.com` — see the subdomain decision in the
> [infra checklist](#infra-checklist-katasecforge-infra)), with **no Anthropic/OpenAI account** on the
> user's side.

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

> ⚠️ **Superseded for the forge-native API by [API design — message-based](#api-design--message-based-decided-2026-07-18)
> (2026-07-18).** The paragraph above remains correct **only** for the spec-bound Anthropic wire (API B),
> whose `/v1/messages` suffix the client mandates. For Forge's own API (API A) the mission handle is a
> **field in the message, never a route segment** — a mission in a URL is permanent public API surface that
> can never be retired. See the locked invariants below.

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

## API design — message-based (DECIDED 2026-07-18)

> **This section is authoritative for Forge's own API and supersedes the URL-shaped routing sketch in
> [Design](#design).** It was reached by design review with Ameer against ServiceStack's
> [message-based API design](https://docs.servicestack.net/design-message-based-apis) and
> [versioning](https://docs.servicestack.net/versioning) guidance.

### The discovery: these are two different APIs, not one

The spoke previously said `forge exec` and `forge claude` ride the "same hosted `/v1` underneath." Wire
capture disproves the premise. In one real Claude Code run the CLI issued **three different kinds** of
request to the same endpoint ([Fixtures/anthropic-wire](../../src/ForgeMission.Tests/Fixtures/anthropic-wire/)):
`main-loop-tools.json` (the real agentic turn), `aux-title-gen.json` ("generate a 3–7 word session title"),
and `aux-state-check.json` ("classify which of four agent states"). If `@websearch` simply hangs off
`/m/websearch/v1/messages`, **Claude Code's title-generation call runs the full websearch mission** —
classify → search → synthesize → judge, ~40–60 s and a real debit — to name a session. That is what 42.3's
classifier + enrich-once gate exists to prevent.

So:

| | **API A — mission invocation** | **API B — chat wire** |
|---|---|---|
| Who owns the shape | **we do** | Anthropic/OpenAI spec (we don't) |
| Unit of work | 1 call = 1 run | many calls/turn; only *some* should run a mission |
| Billing | debit per call | debit per **mission run**, not per HTTP call |
| Handle means | *which mission to run* | *which mission backs this "model"* |
| Client | `forge exec`, SDKs, MQ later | Claude Code / Codex |

**Framing: one message, two transports.** API B is not a second API — it is a **spec-bound adapter** that
maps a wire we don't control onto the same underlying operation, with 42.3's classifier deciding whether a
given call invokes a mission at all. API A below is the contract.

### Why message-based (and not URL-shaped)

A mission handle in the URL welds two axes that change at completely different rates: the **API contract**
(how you invoke *any* mission — should be near-immutable) and the **mission catalog** (which missions exist
— the fastest-changing thing here, and after 39.5 it is *users* publishing into it, not us). URL-shaped
identity makes **every mission ever published permanent public API surface**: a route to document, secure,
rate-limit, and never GC because someone's script still calls it. We would not even control the
proliferation.

Message-shaped, `ExecuteMission` is **one operation forever** and the catalog is data flowing through it.

> **An alias can be retired; a contract cannot.** Pretty routes may exist as *non-authoritative sugar*, but
> the moment a client is told the URL **is** the contract, that mission is load-bearing forever.

**We adopt the ServiceStack *philosophy*, not the dependency.** ServiceStack is not an AOT target and would
be a large dep; `ForgeMission.Api` stays a slim minimal-API host with hand-written DTOs + STJ source-gen
(per [CLAUDE.md](../../CLAUDE.md)). `ResponseStatus` below is our own POCO, not `ServiceStack.Interfaces`.

### Locked invariants

| # | Invariant | Why |
|---|---|---|
| **M1** | **The message is the contract; routes are non-authoritative aliases.** | A DTO is populatable from route, query, form, or body in any format — the URL is one input channel, not the contract. |
| **M2** | **Mission identity is data (a field), never a route segment.** | Retiring a mission must never orphan a URL. |
| **M3** | **No versioned URLs.** Every request DTO carries `int Version`. Evolve **additively**: never change an existing property's type (add a new one and infer from the old), never make new properties mandatory. A genuinely breaking change becomes a **new named message**, not `/v2/`. | Parallel per-version types force parallel implementations — a massive DRY violation. One implementation serves all versions by reading whichever fields the client populated. |
| **M4** | **Always a coarse-grained Response DTO carrying `ResponseStatus`.** Errors travel **in the message**, not only as HTTP status. | Adding fields later never breaks older clients; and status codes don't exist over a bus. |
| **M5** | **No transport-derived state in service implementations** — `Execute(ExecuteMission msg, Principal principal)`. | There is no `HttpContext` over a service bus. The task-4 auth filter stays an **HTTP adapter**; it resolves the principal and *passes it in*. |
| **M6** | **`RunId` — every run is addressable independently of the connection that started it.** | The seam that makes async / `ReplyTo` / polling execution possible. Without it a run exists only as an HTTP response body. |
| **M7** | **`RequestId` — client-supplied idempotency key; the debit path is idempotent against it.** | Buses are **at-least-once**; redelivery is normal and a run **debits a real ledger**. Cheap now, breaking to retrofit. |
| **M8** | **Usage + balance are fields in the response message.** HTTP headers are an optional projection. | Headers don't survive a transport change. |
| **M9** | **The server owns `missionRef` and `policy`; never client-supplied.** | `policy: "trusted"` unlocks `kind: exec` + loose egress — a client-set policy is straight privilege escalation. |
| **M10** | **Streaming is a sequence of messages.** NDJSON/SSE is a framing detail. | `RunStreamEvent(progress\|heartbeat\|result\|error)` is already exactly this. |

**Long-term intent (Ameer, 2026-07-18):** these invariants exist so the hosted API can later move onto a
**service bus** as a *different transport over an intact contract*, rather than a rewrite. M5/M6/M7 are the
three that are cheap today and breaking to retrofit.

### The messages

Naming follows the Amazon/ServiceStack convention — verb-named operation + `XResponse`, so a caller can
infer the response type without consulting docs. `Get*` = one by key; `Search*` = filtered many.

```csharp
// ---------- Execute (the core operation) ----------
public sealed class ExecuteMission                      // → ExecuteMissionResponse
{
    public int    Version   { get; set; }               // M3. 0 == pre-versioning client
    public string RequestId { get; set; }               // M7. client-supplied dedupe key
    public string Mission   { get; set; }               // "websearch" | "katasec/websearch" | "websearch@2"
    public string Input     { get; set; }               // primary free-text input (→ runner's Goal)
    public Dictionary<string,string>? Inputs { get; set; }  // structured mission inputs
    public bool   Stream    { get; set; }               // M10. a message property, not an Accept header
}

public sealed class ExecuteMissionResponse
{
    public string RunId          { get; set; }          // M6
    public string Mission        { get; set; }          // RESOLVED handle (echo what actually ran)
    public string MissionVersion { get; set; }          // resolved version/OCI digest
    public string Answer         { get; set; }
    public bool   Verified       { get; set; }
    public List<MissionSource>    Sources { get; set; }
    public List<MissionTraceStep> Trace   { get; set; }
    public MissionUsage Usage           { get; set; }
    public long         BalanceMicroUsd { get; set; }   // M8
    public ResponseStatus ResponseStatus { get; set; }  // M4
}

public sealed class MissionUsage
{
    public long   InputTokens    { get; set; }
    public long   OutputTokens   { get; set; }
    public double ComputeSeconds { get; set; }
    public string Model          { get; set; }
    public long   CostMicroUsd   { get; set; }
}

public sealed class MissionTraceStep                    // mirrors the engine envelope
{
    public string Expert { get; set; } public string Status { get; set; }
    public string? Text  { get; set; } public string? Reason { get; set; }
    public int    Attempt { get; set; }
}

public sealed class MissionSource                       // NEW — see gap note below
{
    public string Url { get; set; } public string? Title { get; set; } public string? Provider { get; set; }
}

// ---------- Streaming form (M10) ----------
public sealed class MissionRunEvent
{
    public string Type  { get; set; }                   // progress | heartbeat | result | error
    public string RunId { get; set; }
    public MissionProgress?         Progress { get; set; }
    public ExecuteMissionResponse?  Result   { get; set; }   // terminal
    public ResponseStatus?          ResponseStatus { get; set; }
}

// ---------- Catalog ----------
public sealed class SearchMissions                      // → SearchMissionsResponse   (`forge missions`)
{
    public int Version { get; set; }
    public string? Query { get; set; } public string? Publisher { get; set; }
    public bool IncludeDeprecated { get; set; }
}
public sealed class SearchMissionsResponse
{
    public List<MissionSummary> Results { get; set; }
    public ResponseStatus ResponseStatus { get; set; }
}
public sealed class MissionSummary
{
    public string Mission { get; set; } public string Description { get; set; }
    public string Publisher { get; set; } public string Version { get; set; }
    public bool   Verified  { get; set; }               // 38.5 identity seal
    public string Lifecycle { get; set; }               // active | deprecated | retired
    public string? SupersededBy { get; set; }
    public DateTimeOffset? SunsetAt { get; set; }
}

public sealed class GetMission { public int Version; public string Mission; }   // → GetMissionResponse

// ---------- Account / run ----------
public sealed class GetAccount { public int Version; }  // → GetAccountResponse  (`forge whoami`)
public sealed class GetRun     { public int Version; public string RunId; }     // → GetRunResponse (M6)

// ---------- Shared ----------
public sealed class ResponseStatus                      // our POCO, not ServiceStack's
{
    public string? ErrorCode { get; set; } public string? Message { get; set; }
    public Dictionary<string,string>? Meta { get; set; }
    public List<ResponseWarning>? Warnings { get; set; }
}
```

### Mission lifecycle — expressed in the message, not the routing table

Because the handle is data (M2), lifecycle is **catalog state surfaced in the response**:

| State | Behaviour |
|---|---|
| `active` | runs; no warning |
| `deprecated` | **runs normally**, plus a `ResponseStatus.Warnings[]` entry `MissionDeprecated` with `meta.supersededBy` / `meta.sunsetAt` |
| `retired` | **does not run, is not debited**; `ResponseStatus.ErrorCode = MissionRetired`, `meta.supersededBy` names the replacement |
| unknown | `MissionNotFound` |

```json
{ "runId": "run_01H…", "answer": "…", "verified": true,
  "responseStatus": { "warnings": [{
      "errorCode": "MissionDeprecated",
      "message": "@websearch is deprecated; use @research.",
      "meta": { "supersededBy": "research", "sunsetAt": "2026-12-01" } }]}}
```

HTTP's `Deprecation`/`Sunset` headers are per-endpoint, weak, and vanish the moment the operation rides a
bus. In-message survives the transport and is actionable programmatically.

**Error codes (transport-independent, authoritative):** `MissionNotFound` · `MissionRetired` ·
`InsufficientCredit` · `Unauthenticated` · `InvalidInput` · `PolicyViolation` · `RunFailed`.
The HTTP adapter *projects* these to 404 / 410 / 402 / 401 / 400 / 403 / 500 as a convenience — the
**message is authoritative**.

### Transport mapping (today: HTTP)

```
POST /api/ExecuteMission     ← the contract (message name is the endpoint)
POST /api/SearchMissions
POST /api/GetAccount
POST /m/{mission}/run        ← OPTIONAL alias, non-authoritative sugar (M1). May 410 freely.
```

API B keeps its spec-mandated shape (`{base}/v1/messages`, where the client appends `/v1/...`), and is
implemented as an adapter over the same service — **not** a parallel implementation.

### Known gap: sources are not in the runner contract yet

[`RunResponse`](../../src/ForgeMission.Runner.Contracts/) carries `AgentText, Verified, StepCount,
RetryCount, Trace, Usage` — **no structured citations**. Today "source-cited" means whatever URLs the model
wrote inline in prose. `MissionSource[]` above is the target shape; plumbing it from Scout's `SourceRef`
through the runner contract is required for a real trust footer (`--sources`) and is **additive** (M4), so
it can land after the demo without breaking clients.

### What this supersedes

- **The "mission-selection mechanism" decision is void.** The header-vs-rewrite-`model` problem was
  manufactured by putting the handle in a URL; `RunRequest.MissionRef` was always a field. The runner
  needed no change — only the gateway framing did.
- **Task 5 splits** into 5a (mission invocation, API A) and 5b (chat-wire adapter, API B + aux-call policy).
- **Task 6's metering source is simpler on 5a**: the terminal `result` message already carries full-mission
  `RunUsage`. The HTTP-trailer / `forge-usage`-SSE invention is only needed for 5b.
- **Task 7 (shared enrichment cache) is 5b-only** — one-shot invocation has no tool loop or re-entrancy.

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
   - **✅ DONE (2026-07-19, commit `da977ff`).** [`PlatformKeyAuthFilter`](../../src/ForgeMission.Api/PlatformKeyAuth.cs)
     resolves the Bearer token via the shared `authbilling_db` resolver, stashes `PlatformKeyContext`
     on `HttpContext.Items` for tasks 5/6 to read, 401 on missing/invalid/revoked. `ApiJsonContext`
     added for the gateway's own AOT-clean JSON responses. 4 unit tests pass
     ([`PlatformKeyAuthFilterTests`](../../src/ForgeMission.Rooms.Tests/Api/PlatformKeyAuthFilterTests.cs)).
     Wired onto `/v1/{**rest}` via `.AddEndpointFilter<PlatformKeyAuthFilter>()` in `Program.cs`.
5. **Routing — SPLIT (2026-07-18, see [API design](#api-design--message-based-decided-2026-07-18)).**
   The single "map handle → mission" task was written assuming one API; it is two.
   - **5a — API A, mission invocation (build first).** Implement `ExecuteMission` / `SearchMissions` /
     `GetAccount` / `GetRun` as message endpoints on `ForgeAPI`, with the service signature
     `Execute(ExecuteMission, Principal)` (M5). Resolve `msg.Mission` → catalog entry → the runner's
     existing `POST /run/stream`; server sets `missionRef` + `policy` (M9). Map the runner's
     `RunStreamEvent` sequence to `MissionRunEvent` and the terminal `ExecuteMissionResponse`. Lifecycle
     (`deprecated` warning / `retired` refusal, no debit) per the table above. **The old
     header-vs-rewrite-`model` problem does not arise** — `RunRequest.MissionRef` is already a field.
   - **5b — API B, chat-wire adapter.** `/m/{handle}/v1/*` → the runner's `/v1` door. Needs (i) a
     mission-selection mechanism for the spec-bound wire (the `model` field carries the client's real model
     id, not a handle) and (ii) an **aux-call policy** — the wire capture shows Claude Code firing
     title-gen and agent-state-check calls at the same endpoint, which must NOT each run a full mission.
     Depends on 42.3's classifier. **Sequenced after 5a.**
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
   2. **Edge rate limit — DEFERRED (decided 2026-07-18, Ameer). No longer a launch gate; not implemented
      for this spoke.** Consistent with the ladder's own rule (build a rung when its trigger fires) — at
      F&F scale, with a trusted population and stop-at-zero freeze, the trigger has not fired.
      ⚠️ **Two corrections to the original premise:** (a) there is **no Cloudflare (or Front Door) in the
      stack** — `forge.katasec.com` is bound *directly* to `ca-forge-ui-dev` via an ACA managed cert, so
      this was never "pure config, no code"; it would require introducing an edge tier first. (b) ACA has
      no built-in per-IP limiting, so the realistic options if/when the trigger fires are: an app-level
      per-IP limiter in `ForgeAPI`, Azure Front Door + WAF, or adding Cloudflare. Set generously when
      built (e.g. 60/min/IP) — 42.3 tool loops legitimately burst N+1 calls per turn.
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
   - **Message-based (2026-07-18):** the CLI sends `ExecuteMission` / `SearchMissions` / `GetAccount`
     messages — `@handle` is the `Mission` **field**, not a URL segment (M1/M2). It must set `RequestId`
     (M7) and surface `ResponseStatus` warnings, so a `MissionDeprecated` notice reaches the user and a
     retried invocation never double-debits. `forge whoami` = `GetAccount`.
9. **Deploy + verify the headline demo** (details in the checklist below). `forge login && forge exec
   @websearch "<past-cutoff question>"` → cited answer, ledger debited; and `forge claude @websearch` agentic.
   Verify the **anti-hallucination guarantee**: a stale-fact question triggers the classify→search path (not
   a confident-wrong answer).
10. **`forge codex @websearch`** against the same endpoint once 42.7 lands (`/v1/responses` door).

### Infra checklist (`katasec/forge-infra`)

The re-architecture adds infra, sequenced with the tasks above. All in the layered Bicep (`dev/*`); redeploy
the relevant layer (see [deploy.md](../design/deploy.md)).

- [x] **`authbilling_db`** — ✅ **deployed and verified live (2026-07-19)**: `authBillingDb`, a second
      `flexibleServers/databases` child on `psql-forge-dev` (same instance → no new server cost, no new
      firewall/VNet rules). Confirmed live via direct `psql` connection through the operator-IP firewall
      rule (`make 300-data-operator-ip` in `forge-infra`). *(Task 2.)*
- [x] **KV secret** — ✅ **deployed and verified live (2026-07-19)**: `ConnectionStrings-AuthBillingConnection`
      (same host/creds as rooms, `Database=authbilling_db`), a dedicated secret so it can rotate/relocate
      independently. Surfaces as `ConnectionStrings:AuthBillingConnection`. **Wired into ForgeUI's `500-app`
      env** (`ConnectionStrings__AuthBillingConnection` → `connection-authbilling` secret ref, applied via
      `forge-infra`'s `dev/500-app/main.bicep`) — no longer derived from `WriteConnection`. ForgeAPI gets the
      same wiring once `ca-forge-api-dev` is authored (below). *(Task 2.)*
- [x] **Table bootstrap** — ✅ **verified live (2026-07-19)**: `AuthBillingSchema.EnsureCreatedAsync`
      (idempotent `CREATE TABLE IF NOT EXISTS` at host startup) ran on `forge-ui:0.6.0`'s first boot.
      Confirmed via direct `psql` against `authbilling_db`: `platform_keys` and `ledger_entries` both
      exist, owned by `forge_admin`. Full loop: [Deploy Runbook](../design/deploy.md) TL;DR (tag → CI
      build → `make 500-app-deploy-image`) → confirmed with `\dt` after. *(Task 2, DONE.)*
- [ ] **`ca-forge-api-dev` container app** — new tier-1 ACA app for `ForgeAPI`: **external** ingress
      (the runner stays internal), network access to `psql-forge-dev` and to the internal runner.
      **Hostname DECIDED 2026-07-18 (Ameer): a dedicated subdomain `api.forge.katasec.com`**, not a
      path-split of `forge.katasec.com` — that host is bound *directly* to `ca-forge-ui-dev`, and splitting
      it would require introducing a front proxy (Front Door/Cloudflare) purely for routing. A subdomain
      keeps the tier separation clean and costs one CNAME.
      **DNS (registrar):** `CNAME api → ca-forge-api-dev.niceground-df7fb252.uaenorth.azurecontainerapps.io`
      — every app in `cae-forge-dev` shares that suffix (verify against `ca-forge-ui-dev`'s live FQDN).
      The CNAME can be created before the app exists. **Then, after the app is created:** read
      `az containerapp show -n ca-forge-api-dev -g rg-forge-dev --query properties.customDomainVerificationId`
      → add `TXT asuid.api = <that id>` → bind the hostname (TLS-disabled) → create the managed cert →
      rebind `SniEnabled`. (Same out-of-band cert dance as `500-app`; see its README.) *(Task 3 / 9.)*
- [~] **Edge per-IP rate limit** — **DEFERRED 2026-07-18 (not a launch gate, not implemented).** No CDN/WAF
      tier exists today (`forge.katasec.com` → `ca-forge-ui-dev` directly), so this is not config-only.
      See [task 6 rung 2](#tasks-chronological). *(Task 6.)*
- [ ] **Tier network policy** (north-star direction) — restrict runner ingress to `ForgeAPI`/`ForgeUI` only;
      keep the runner off public ingress. *(Hardening; not a demo blocker.)*

## Migration-job DB-wipe — DEFUSED (2026-07-18); optional post-dream investigation

**Status: DEFUSED — no live data-drop path remains; NOT a deploy gate.** During the `authbilling_db`
infra work the dev `forge_rooms` DB was wiped (all rooms/members/messages gone, confirmed by sign-in).
Both mechanisms are now disabled:
- the two `DropTable` calls in [`DropLedgerAndPlatformKeysFromRooms`](../../src/ForgeMission.Rooms.Data/Migrations/20260717233324_DropLedgerAndPlatformKeysFromRooms.cs)
  `Up()` are **commented out** (the cutover to `authbilling_db` never needed them — the stale forge_rooms
  tables are harmless if left in place);
- the **auto-run migrate step** in [`forge-infra` `.github/workflows/infra.yml`](https://github.com/katasec/forge-infra/blob/main/.github/workflows/infra.yml)
  (`500-app` → start `caj-forge-migrate-dev`) is **commented out** — migrations are now run deliberately
  (you-triggered), not on every deploy.

The remaining items below are **optional cleanup/hardening for post-dream**, no longer a blocker:

> **Structural fix since applied:** the migration job (`caj-forge-migrate-dev`) has been moved out of
> `dev/500-app` into its own layer, `dev/450-migrate` — an app deploy can no longer touch the job at
> all, deliberate or not. See [Deploy Runbook](../design/deploy.md). The forensic narrative below is
> historical (as of 2026-07-18, when the job still lived in `500-app`) — kept for the investigation
> record, not as a description of the current layer structure.

**What we know (evidence, not yet root-caused):**
- At the time, the `500-app` deploy defined a pre-deploy migration job `caj-forge-migrate-dev` whose
  container entrypoint was **`/app/migrate --connection $(CONNECTION_WRITE)`** (image `forge-ui:0.5.0`).
  We redeployed `500-app` (twice) to recover the site from a credential rotation; the data was gone afterward.
- **Ruled out:** the password rotation (changes a credential, never drops data); the 42.6
  `DropLedgerAndPlatformKeysFromRooms` migration (exists only in unbuilt local code, **not** in `0.5.0`).
- **Prime suspect:** the `/app/migrate` entrypoint itself. If it does `EnsureDeleted()`+`EnsureCreated()`
  (or drops/recreates schema) instead of an additive EF `Database.Migrate()`, a routine deploy wipes the
  DB. **A plain `Migrate()` on an already-applied image is a no-op and would NOT wipe** — so if data is
  gone, the entrypoint is likely destructive (or something else is; that's the investigation).

**Investigation TODO:**
1. Find `/app/migrate`'s source in this repo (the migrate tool/entrypoint + its Dockerfile `CMD`). Determine
   exactly what it runs: additive `Migrate()`, or a destructive `EnsureDeleted`/`EnsureCreated`/drop-recreate.
2. Confirm the wipe mechanism (correlate job-run timestamps with the data loss; read the job execution logs —
   needs `containerApps` read).
3. **Guard it:** migrate must be idempotent + non-destructive; never drop a populated table; gate any
   destructive reset behind an explicit opt-in flag that is **off in stg/prod**; consider a row-count/backup
   pre-check that aborts if the target DB is non-empty and the plan is destructive.

**Learning (why this matters):** verify a migration entrypoint's semantics **before** running it against any
populated DB; the `500-app` migration-job step is a recurring risk on every deploy, not a one-off. Dev data
loss is cheap; the same deploy against prod is not.

## Out of scope

- Authoring/publishing user missions (only built-in catalog here) — Phase 39.5.
- Paid top-ups / subscription tiers — Phase 39.6.
- MCP door — 42.8.
- Per-key rate limiting beyond credit cap; heavy-mission container isolation — later hardening.
