# Phase 42.5 — Platform identity & keys

> **Status: Design (2026-07-15).** Give a user a **platform key + free credits** in one command, so they can
> point a coding agent at a hosted forge mission with **no provider account**. The hosted runner calls
> providers with *our* keys server-side, metered against the user's balance. This is the friction-killer
> behind the TTF-awesome demo.
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:** Phase 39 identity/billing
> (Entra External ID `forgeids`, `BillingService`, ledger — **live**) · **Blocks:**
> [42.6](phase-42.6-hosted-endpoint-ttfa.md) · **AOT rules:** [CLAUDE.md](../../CLAUDE.md).
>
> **Done when:** `forge login` opens a browser, signs the user in, stores a **platform key** in `~/.forge`,
> and (first time) grants free credits; that key works as the bearer token (`ANTHROPIC_API_KEY` /
> `OPENAI_API_KEY`) against the hosted `/v1` endpoint and is resolvable server-side to a user + balance.

## Context an implementer needs (verified against the code / memory 2026-07-15)

- **Naming (DECIDED 2026-07-16):** `forge login` = **platform sign-in** (browser OAuth → platform key +
  credits — the thing users mean by "log in"). Today's OCI-registry login
  ([`Program.cs` `BuildLoginCommand`](../../src/ForgeMission.Cli/Program.cs)) moves to **`forge registry
  login`**; keep the old invocation working with a deprecation notice for a release. Both write to the same
  `~/.forge/credentials.json` store (`platform` + registry sections).
- **Identity + billing exist (Phase 39):** Entra External ID tenant `forgeids`; `BillingService` grants
  credits on first sign-in (the live "Granted 5,000,000 µ$"), does balance-check + debit-after-run against
  per-user `ledger_entries` in Rooms Postgres. **We are issuing a CLI-usable key that maps to this same
  user + ledger — not building new billing.**
- **The key must be usable as an opaque bearer token** by Claude Code (`ANTHROPIC_API_KEY`) and Codex
  (`OPENAI_API_KEY` / a `model_providers.*` `env_key`). Both send it as `Authorization: Bearer <key>`. So
  the platform key is just a token the hosted endpoint (42.6) validates → user → balance.

## Design

**Login flow (device/browser OAuth):**
```
forge login
  → open browser to forge.katasec.com auth (Entra External ID), device-code or loopback-redirect flow
  → on success, forge exchanges for a PLATFORM KEY (long-lived, revocable) tied to the user
  → store in ~/.forge/credentials.json  (extend the existing store; add a `platform` section)
  → BillingService grants free credits on first login (idempotent, self-heals existing members — 39.2 already does this)
  → print:  ✓ signed in as you@…  ·  <balance> µ$  ·  key stored
```

**Platform key properties:**
- opaque, long-lived, **revocable**, scoped to the user; not a provider key.
- **Format (decided — external design review, 2026-07-15): `fg_live_<identifier>_<secret>`.** A visible
  prefix (support-friendly, greppable in leaked logs, scannable by secret-scanners), a lookup identifier,
  and a random secret. **Store only a keyed hash of the secret** — never plaintext. This gives safe lookup,
  individual revocation, and a survivable DB compromise.
- **Opaque + table, not JWT.** A JWT avoids a lookup but makes immediate revocation, leak response, rotation,
  scope changes, suspension, and credit-status checks all harder — and we need account state on the request
  path for billing anyway, so the lookup isn't a saving.
- resolvable server-side (42.6) to `(userId, balance)`.

**Abuse bound (DECIDED 2026-07-16 — trigger ladder, see 42.6 task 3).** The credit cap (~$5) plus
stop-at-zero freeze is the accepted bound at F&F scale; check-then-debit is knowingly not a strict ceiling.
Before this spoke's keys reach strangers, 42.6 adds an edge rate limit (Cloudflare, config-only); in-app
cost/concurrency caps and transactional reservation are pre-recorded there with their triggers.

**Sybil note:** the $5 grant is per *identity* — Entra External ID is the choke point. One person creating
many accounts multiplies free credits; acceptable now, revisit if sign-up abuse ever appears (CAPTCHA /
verified-email / per-payment-method grant are the usual rungs).

**Auxiliary commands:**
- `forge whoami` → show signed-in user + balance (reads local key + a `/me` call).
- `forge logout` → clear the local platform key.
- (later, 39.6) `forge topup` → hosted purchase.

## Tasks (chronological)

1. ~~Design-review the naming/key format~~ **DECIDED:** `forge login` = platform; registry → `forge registry
   login` (deprecation shim one release). Key = opaque `fg_live_<id>_<secret>` + keyed hash (already decided
   2026-07-15). First implementation task: the rename + shim.
2. **Auth flow in the CLI:** browser/loopback or device-code OAuth against `forgeids`; token exchange →
   platform key. AOT-safe HTTP + STJ (no bare `JsonSerializerOptions`).
3. **Credential store:** extend `~/.forge/credentials.json` with a `platform` section (key, user, endpoint);
   keep registry creds working.
4. **Server-side issuance + resolution** (in the hosting layer, shared with 42.6): mint the platform key on
   login; a `platform_keys` table (or JWT signer) mapping key → user; a `/me` endpoint returning user +
   balance. Reuse the Phase-39 credit-grant on first login (idempotent).
5. **`forge whoami` / `forge logout`.**
6. **Revocation path** (admin/user can revoke a key) + test: a revoked key is rejected by the hosted endpoint.

## Out of scope

- Key→mission routing and the metered request path — **42.6** (this spoke issues + resolves the identity;
  42.6 spends it).
- Paid top-ups / tiers / Stripe — Phase 39.6.
- Per-key rate limits beyond the credit cap — later hardening.
