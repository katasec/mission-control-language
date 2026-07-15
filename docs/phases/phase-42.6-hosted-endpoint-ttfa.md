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
id the client legitimately sends. The CLI keeps exposing the friendly `@websearch`.

`forge claude @websearch` (42.2, hosted mode) sets `ANTHROPIC_BASE_URL=https://forge.katasec.com/@websearch`
and the platform key as the token.

**Multi-tenancy.** One converged image, **routing by key+handle**, not a container per user (cold-starts +
cost). Per-user isolation is at the ledger/auth layer; the mission run is stateless per request (plus the
shared re-entrancy store). Heavy/custom-exec missions that need hard isolation are a later concern (ties to
Phase 39.5/39.7).

**`forge missions`** — list the on-tap OCI catalog (name + one-line value), so the user can discover
`@websearch` etc. A `/missions` probe already exists on the runner.

## Tasks (chronological)

1. **Auth middleware:** validate the platform key (42.5) on every `/v1/*` request → attach `(userId,
   balance)`; 401 on bad/revoked key.
2. **Routing:** map handle (path segment, recommended) → OCI catalog mission the runner loads; 404 on
   unknown handle.
3. **Wrap the run in Phase-39 billing — and close the spend hole.** Reuse `BillingService` +
   `UsageTrackingChatClient`, but **balance-check → run → debit is not a strict ceiling**: cost is unknown
   until after, and **concurrent requests all pass the check before any debit lands**. Add
   **transactional credit reservation** (reserve an estimated max pre-run, settle actual after) or hard
   per-request spend/token caps + free-tier concurrency limits. **This also fixes a live Phase 39 hole** —
   the current check-then-debit path is already in production. Return balance in a response header; 402 when
   short.
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
