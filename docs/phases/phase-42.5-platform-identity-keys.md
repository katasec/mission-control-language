# Phase 42.5 — Platform identity & keys

> **Status: DEPLOYED + LIVE IN PROD (2026-07-17).** T1–T5 ✅. `ca-forge-ui-dev` on `forge-ui:0.5.0`;
> `forge login` / `whoami` verified end-to-end against **`https://forge.katasec.com`**
> (`✓ signed in · $5.00 credit`, exit 0). The migration created `platform_keys` in prod PG; the shared
> HMAC lives in KV wired to both ForgeUI + the runner; the `forge.katasec.com` SNI binding was preserved.
> **Remaining for 42.5: T6 (revocation trigger) only**, plus one polish item (bearer-path member profile,
> see Known gaps). Give a user a **platform key + free credits** in one command, so they can
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

**Auth flow (DECIDED 2026-07-17): loopback + PKCE.** Auth-code + PKCE public client with a localhost
redirect — browser pops, user signs in, CLI catches the redirect. Hand-rolled over raw HTTP + STJ (no MSAL)
per the AOT-first rule: one localhost listener + two token calls. Device-code flow (headless/SSH) deferred
until someone needs it — Entra External ID (CIAM) device-code support needs verification anyway.

**Key resolution on the request path (DECIDED 2026-07-17): direct PG + cached lookup.** The runner
validates platform keys by querying Rooms Postgres directly (`platform_keys` + balance) through a shared
lookup lib, with a short in-process cache (~30–60 s TTL). Prior art: this is the tier-1 pattern (early
Stripe, single-product SaaS) and we have exactly one data-plane service sitting next to the DB in ACA.
Revocation propagates within the cache TTL — the same guarantee Kong's key-auth gives. Known non-breaking
evolution if data-plane services multiply: the shared lib becomes a resolve endpoint / `ext_authz`-style
filter (Kong, Envoy, AWS API Gateway pattern). Supabase's JWT→opaque-key reversal (2024–25) independently
confirms the opaque+table decision above.

**Identity keying — `oid`, not `sub` (DECIDED 2026-07-17, cross-checked w/ external review).**
The domain already has the right shape: `Member` **is** the "ForgeUser" — `Member.Id` (Guid) is the
domain PK that all business data references (`LedgerEntry.MemberId`, `PlatformKey.MemberId`, rooms,
memberships), and `(Issuer, Subject)` is the external identity mapping. The only change: for the
Entra issuer, the external subject is now **`oid`** (the tenant-stable object id, identical across
every app/client) instead of **`sub`** (which is *pairwise per client* — the web app is a
confidential client, the CLI a public client, so the same human would otherwise resolve to two
Members with two `$5` grants). `ForgeClaims.TryGetIdentity` prefers `oid`, falls back to
`sub`/NameIdentifier for the dev sign-in path (no `oid`). **No new abstraction, no RBAC/claims/policy
engine** — authorization stays ownership-based (`resource.MemberId == currentMemberId`); principals/
roles are deliberately deferred until collaboration scenarios exist. Existing dev accounts
re-provision on next sign-in (accepted, F&F scale). *Not* NIH: we lean fully on Entra for identity
(`oid` is Entra's canonical id); the opaque platform key is a separate compatibility bridge for
static-bearer tools, not a reimplementation of auth.

**Two planes, no RPC hop (DECIDED 2026-07-17).** Issuance (①) + `/me` (④) are handlers on the
existing **control plane** (`ca-forge-ui-dev`, which already owns OIDC + `BillingService` + Rooms PG).
The **data plane** (`ca-forge-runner-dev`) gets ③ as a *library*, not a service — it reads Rooms PG
directly (cached), so there is no per-request `ext_authz` call between the two. Kept split on the
39.1 trust boundary (the runner executes untrusted mission code; identity/billing must not live in
that process). Collapsing them is explicitly rejected.

**Storage seam (DECIDED 2026-07-17 — see [persistence.md](../design/persistence.md)).** `platform_keys`
ships on EF/Postgres now, fronted by `IPlatformKeyStore`, so the long-planned move to Azure Table
Storage is a one-line DI swap. `platform_keys` is the **first** table earmarked for that move (pure
key→value lookup by `key_id`; no joins/aggregation). The ledger's balance stays in Postgres (`SUM`
aggregation), and the request path composes the two stores — so the key half can migrate on its own.

**Auxiliary commands:**
- `forge whoami` → show signed-in user + balance (reads local key + a `/me` call).
- `forge logout` → clear the local platform key.
- (later, 39.6) `forge topup` → hosted purchase.

## Tasks (chronological)

1. ~~Design-review the naming/key format~~ **DECIDED:** `forge login` = platform; registry → `forge registry
   login` (deprecation shim one release). Key = opaque `fg_live_<id>_<secret>` + keyed hash (already decided
   2026-07-15). ~~First implementation task: the rename + shim.~~ **✅ DONE 2026-07-17:** `forge registry
   login` live; hint strings + README updated; verified against isolated `$HOME`; suite 256 pass.
   Deprecation shim built then **removed same day** per Ameer — sole user, no external invocations to
   protect; `forge login` is purely platform sign-in.
2. **Auth flow in the CLI:** loopback auth-code + PKCE against `forgeids` (decided above); token exchange →
   Entra tokens. Hand-rolled, AOT-safe HTTP + STJ (no MSAL, no bare `JsonSerializerOptions`).
   **✅ DONE + LIVE 2026-07-17.** `PlatformLogin.cs`: PKCE pair, free-port `HttpListener` loopback,
   browser launch, state check, token exchange, id_token claims display; env overrides
   `FORGE_AUTH_AUTHORITY/CLIENT_ID/SCOPE`. App registration created in `forgeids` (CLI public client
   `33595d97-0296-4868-9217-dfab35faa314`, `create-cli-app-registration.sh` + `create-user-flow.sh`),
   real ClientId + Rooms API scope (`api://4f8a95d6-…/cli.login`) baked in so the access token is
   minted for the Rooms audience the issuance endpoint validates. Live-verified: `forge login` →
   browser Email-OTP → loopback redirect → token exchange → `✓ signed in as writeameer@gmail.com`,
   exit 0. **Still pending:** wire the login flow to POST the access token to `/platform/keys` (T4 ①)
   and persist the returned `fg_live_…` via `CredentialStore` (T3) — the CLI→issuance join.
3. **Credential store:** extend `~/.forge/credentials.json` with a `platform` section (key, user, endpoint);
   keep registry creds working. **✅ DONE 2026-07-17:** `PlatformCredential` + `Get/Save/ClearPlatform` on
   `CredentialStore` (shared read-modify-write); registry creds verified untouched; suite 256 pass.
   Save gets wired into the login flow when task 4's exchange endpoint exists.
4. **Server-side issuance + resolution** (direct PG + cached lookup). Broken into four pieces (①–④):
   - **② `platform_keys` table + `IPlatformKeyStore`. ✅ DONE 2026-07-17.** `PlatformKey` entity
     (`key_id` PK → `secret_hash`, `member_id` FK→members cascade, `created_at`, `revoked_at`) +
     snake_case config + `DbSet` + `AddPlatformKeys` migration. `IPlatformKeyStore`
     (Save/ResolveByKeyId/Revoke) + EF impl behind it (the Table-Storage seam). `PlatformKeyMinting`
     (shared): `fg_live_<keyId>_<secret>` hex format, HMAC-SHA256 keyed hash, constant-time verify,
     TryParse. Tested: 14 pass (pure round-trip/verify/reject + store Save/Resolve/Revoke through real
     Postgres, exercising the migration).
   - **① Issuance endpoint `POST /platform/keys`. ✅ BUILT + LIVE 2026-07-17.**
     On ForgeUI: a second JWT-bearer scheme (`PlatformKeyBearer`) validates the CLI's Entra access
     token (authority = `forgeids`, requires `cli.login` scope), stamps the same `forge_iss` the OIDC
     path stamps → `MemberProvisioningService.ResolveAsync` (provisions on `oid` + reuses the 39.2
     grant, both idempotent) → mint key → `SaveAsync` the hash → return the plaintext token once +
     email + balance. **CIAM validation fixes (found live via the WWW-Authenticate reason):** (a)
     access tokens carry `aud` as the **bare app-id GUID**, not the `api://` URI → `ValidAudiences`
     accepts both; (b) the friendly-host authority issues tokens whose `iss` uses the
     **tenant-GUID host** (`<tenantId>.ciamlogin.com`) → `ValidIssuers` accepts both forms, keyed off
     the authority's tenant id. Live-verified end-to-end (see status header).
   - **③ Shared lookup lib (request path). ✅ BUILT + TESTED 2026-07-17.** `PlatformKeyResolver`
     (Rooms.Data): `TryParse` → `ResolveByKeyId` → `PlatformKeyMinting.Verify` the secret → check
     `revoked_at` → `ILedgerStore` balance → return `PlatformKeyContext(memberId, balance)` (or null
     for malformed/unknown/wrong-secret/revoked), behind a ~30–60 s in-process cache (a cache hit
     still verifies the secret against the cached *hash* — never caches the secret; injectable clock).
     `AddPlatformKeyResolver` DI helper; shared HMAC key via `PlatformKeys:HmacKey`. Tests: 7 pass —
     valid resolve, malformed/unknown/wrong-secret → null, and revocation + balance change both
     propagate exactly at TTL expiry (clock-driven). **Runner request-path wiring** (project ref +
     DB-at-boot + a platform-key auth handler that rejects/meters) lands with **42.6** — the runner is
     deliberately unmetered until then, so wiring auth without enforcement would be dead code.
   - **④ `/me` endpoint. ✅ BUILT + LIVE 2026-07-17.** `GET /me` on ForgeUI, authenticated
     by the **platform key** (via ③), *not* the Entra bearer — reads the Bearer header, resolves →
     `(memberId, balance)`, loads the member, returns `{ email, displayName, balanceMicroUsd }`; 401 on
     missing/invalid/revoked. Live: stored key → 200 balance 5000000; bogus key → 401.

   **CLI→issuance wiring (T2 tail). ✅ DONE + LIVE 2026-07-17.** `forge login` POSTs the access token to
   `<endpoint>/platform/keys`, persists the returned `fg_live_…` via `CredentialStore.SavePlatform`
   (T3), prints `signed in as <email> · $X.XX credit · key stored in ~/.forge`. `FORGE_PLATFORM_ENDPOINT`
   overrides the base (default `forge.katasec.com`); failures surface the WWW-Authenticate reason.
   Local e2e harness: `docker compose up` (Postgres) + ForgeUI with `ASPNETCORE_ENVIRONMENT=Development`,
   `Oidc__Authority`/`Oidc__ClientId` = forgeids + Rooms app, `PlatformKeys__HmacKey`, then
   `FORGE_PLATFORM_ENDPOINT=http://localhost:<port>` on `forge login`.

## Known gaps (2026-07-17)

- **Member profile from the bearer path is bare.** The access token carries no `email`/`name` claims,
  so a member first provisioned via `forge login` has `email=null` / `displayName=<oid>`. Balance +
  identity are correct. Fixes: add optional `email`/`name` claims to the access token in the CLI/Rooms
  app registration (forge-infra), or accept the self-heal — a web sign-in (id_token *has* email) updates
  the same member (keyed on `oid`).
## Deploy follow-ups (backlog — surfaced 2026-07-17 deploying 42.5)

These are infra-hardening tasks in `katasec/forge-infra`, tracked here (not in that repo's README).
Both are low priority — the deploy works today; these make it clean/repeatable.

1. **Codify the KV Secrets Officer role for the deployer in bicep.** `kv-forgerooms-dev` is RBAC-mode;
   a human setting secrets (e.g. `PlatformKeys-HmacKey`) needs **Key Vault Secrets Officer** on the
   vault — currently granted by hand (`az role assignment create`). Add it as a
   `Microsoft.Authorization/roleAssignments` in `dev/100-base` so a fresh clone needs no manual grant.
2. **Add required reviewers to the `500-app`/`300-data` GitHub environments (low priority — doesn't
   matter yet).** They have none, so the `gate` job in `infra.yml` runs straight through and a
   `workflow_dispatch` deploy is *unapproved*. Fine for solo dev; add reviewers when a real approval
   gate is wanted, else the `gate` job is decorative.

*(Done during this deploy, for reference: the CI 500-app path was repaired — OIDC `gate`/`deploy` split
so the branch-scoped fed cred matches, custom-domain vars passed so the SNI binding survives, and a
migration-job start+wait step added. See the forge-infra `infra.yml` history.)*

5. **`forge whoami` / `forge logout`. ✅ DONE + LIVE 2026-07-17.** `whoami` reads the stored key, calls
   `GET <endpoint>/me`, prints `Signed in as <email> · $X.XX credit · <endpoint>` (falls back to the
   stored user label when `/me` email is null — the bearer-path gap), exits 1 when not signed in or the
   key is rejected. `logout` clears the platform section of `~/.forge`. Live: whoami → `$5.00 credit`
   (exit 0) → logout → whoami → `Not signed in` (exit 1).
6. **Revocation path** (admin/user can revoke a key) + test: a revoked key is rejected by the hosted
   endpoint. (`IPlatformKeyStore.RevokeAsync` + the ③ `revoked_at` check already exist; T6 adds the
   admin/user trigger + the hosted-endpoint rejection test.)

## Out of scope

- Key→mission routing and the metered request path — **42.6** (this spoke issues + resolves the identity;
  42.6 spends it).
- Paid top-ups / tiers / Stripe — Phase 39.6.
- Per-key rate limits beyond the credit cap — later hardening.
