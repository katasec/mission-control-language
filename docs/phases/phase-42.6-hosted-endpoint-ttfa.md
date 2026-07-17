# Phase 42.6 — Hosted endpoint + time-to-first-awesome

> **Status: Design (2026-07-15).** The payoff spoke. Expose the converged `/v1` image on
> `forge.katasec.com` as a **multi-tenant hosted endpoint**: a request carrying a platform key is
> authenticated, **routed key→mission**, run, **metered**, and debited. Definition of done **is the demo** —
> a stranger reaches a cited, past-cutoff answer through their own Claude Code in 2–3 commands.
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

## Design

**Request path (one hosted turn):**
```
Claude Code ──/v1/messages + Bearer <platform key>──▶ forge.katasec.com (ACA)
   ├─ AUTH   : resolve platform key → (userId, balance)         [42.5]
   ├─ ROUTE  : URL/handle → mission  (/@websearch or a header)   [OCI catalog]
   ├─ BALANCE: reject 402 if insufficient                        [BillingService]
   ├─ RUN    : converged /v1 image · mission · UsageTrackingChatClient   [42.4 + 39]
   │            └─ Scout live retrieval → 🌐   ·   enrich-once/tool loop  [41 + 42.3]
   ├─ DEBIT  : ledger_entries -= cost                            [BillingService]
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

1. **Auth middleware:** validate the platform key (42.5) on every `/v1/*` request → attach `(userId,
   balance)`; 401 on bad/revoked key.
2. **Routing:** map handle (path segment, recommended) → OCI catalog mission the runner loads; 404 on
   unknown handle.
3. **Wrap the run in Phase-39 billing + the spend-abuse trigger ladder (DECIDED 2026-07-16).** Reuse
   `BillingService` + `UsageTrackingChatClient` as-is. The known hole: `balance-check → run → debit` is not
   a strict ceiling — cost is unknown until after, the check-to-debit window is ~60s (a live run took 71s),
   and concurrent requests all pass `balance > 0` before any debit lands. **Accepted at F&F scale**
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
4. **Shared enrichment cache:** implement the 42.3 content-addressed store (`ISessionStore` seam) over Rooms
   Postgres / a cache — ACA may run >1 replica or scale to zero, so a continuation can land on a different
   replica than the turn that enriched. Cache miss ⇒ re-run pre-agent (never answer ungrounded).
5. **`forge missions`** command → hosted catalog list. **`forge claude`/`forge try` hosted mode** →
   `@handle` resolves to the hosted URL + platform key.
6. **Deploy to `forge.katasec.com`** (ACA), custom domain/HTTPS intact; verify the **headline demo** live:
   `forge login && forge claude @websearch` → cited past-cutoff answer, ledger debited, balance shown.
   Verify the **anti-hallucination guarantee**: a stale-fact question triggers the classify→search path (not
   a confident-wrong answer).
7. **`forge codex @websearch`** against the same endpoint once 42.7 lands (`/v1/responses` door).

## Out of scope

- Authoring/publishing user missions (only built-in catalog here) — Phase 39.5.
- Paid top-ups / subscription tiers — Phase 39.6.
- MCP door — 42.8.
- Per-key rate limiting beyond credit cap; heavy-mission container isolation — later hardening.
