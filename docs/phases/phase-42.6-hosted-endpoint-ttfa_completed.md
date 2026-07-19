# Phase 42.6 — Completed work & resolved history

> Companion to [phase-42.6-hosted-endpoint-ttfa.md](phase-42.6-hosted-endpoint-ttfa.md), which stays a
> lookup table of current status. This file holds the full build narrative, evidence, and resolved
> investigations for anything already done — so a session working on what's *still open* doesn't have
> to load this into context. Nothing here should ever need re-reading to build 5a/5b onward; if it
> does, that content was moved to the wrong file — move it back.

## Task 1 — `ForgeMission.Billing` lib (foundation)

**Goal:** extract `CostMeter` + a ledger-facing `BillingService` (balance-check / debit / grant) from
`ForgeUI/Services` into a new AOT-clean project, depending only on an `ILedgerStore` abstraction +
POCOs — no reflection, no runtime `JsonSerializerOptions`. Referenced by both `ForgeUI` and `ForgeAPI`
(one meter, not two). Done when the room path (`RoomAgentInvoker`) still bills identically through the
lib.

**✅ DONE (2026-07-18).** New [`ForgeMission.Billing`](../../src/ForgeMission.Billing/) project
(`IsAotCompatible=true`, builds 0 warnings) holding the moved `LedgerEntry`/`LedgerEntryKind` POCOs,
the `ILedgerStore` interface, `CostMeter`, and `BillingService`. `IConfiguration` swapped for an
injected `BillingOptions` record so the lib carries no config-binder reflection (host binds it from
config at startup). `Rooms.Data` keeps the EF `LedgerStore : ILedgerStore` impl +
`LedgerEntryConfiguration` and now references Billing; `ForgeUI` + tests re-pointed via
`using ForgeMission.Billing`. Added to `ForgeMission.slnx`.

**Verified:** solution + `ForgeUI` build clean; the 11 `Ledger`/`PlatformKeyResolver` tests pass
against a real Postgres container — the room billing path (append / balance-as-`SUM` / idempotent
grant) is byte-identical through the extracted lib.

## Task 2 — `authbilling_db` split (foundation)

**Goal:** move `platform_keys` + `ledger_entries` into a separate database on the same Postgres
server. `ForgeAPI` accesses them via raw Npgsql (`IPlatformKeyStore` / `ILedgerStore` implementations,
no EF) so it stays an AOT target. Bootstrap schema with idempotent `CREATE TABLE IF NOT EXISTS` at
startup (no EF migrations on this DB). `userId` is the only cross-context link to `rooms_db` — no
cross-DB FK.

**✅ DONE (2026-07-18, code — full split, decided w/ Ameer).** `ForgeMission.Billing` now owns the
whole billing bounded context: `PlatformKey`/`LedgerEntry` POCOs + `IPlatformKeyStore`/`ILedgerStore` +
`PlatformKeyMinting`/`PlatformKeyResolver`, plus raw-Npgsql
[`NpgsqlLedgerStore`](../../src/ForgeMission.Billing/NpgsqlLedgerStore.cs) /
[`NpgsqlPlatformKeyStore`](../../src/ForgeMission.Billing/NpgsqlPlatformKeyStore.cs), idempotent
[`AuthBillingSchema.EnsureCreatedAsync`](../../src/ForgeMission.Billing/AuthBillingSchema.cs), and an
`AddAuthBilling(connString)` DI extension. **rooms_db cut over:** EF `LedgerStore`/`PlatformKeyStore` +
configs + DbSets deleted; a `DropLedgerAndPlatformKeysFromRooms` migration drops both tables (Down
fully recreates → reversible). **ForgeUI re-pointed** to `authbilling_db`
(`ConnectionStrings:AuthBillingConnection`, else derived from `WriteConnection` with
`Database=authbilling_db`); bootstraps the schema on every boot in all envs. **One meter, one ledger**
— the room path and the coming hosted `/v1` both bill against it.

**Data migration = fresh start (no copy):** `MemberProvisioningService` calls the idempotent
`GrantStartingCredit` on every login, so an empty `authbilling_db` self-heals — existing F&F members
get re-granted 5,000,000µ$ on next sign-in. Acceptable at F&F scale; a copy was optional and unneeded.

**Verified (code):** solution + ForgeUI build clean (Billing is `IsAotCompatible`, 0 warnings incl.
Npgsql); the full 61-test Rooms suite passes, with the 25 ledger/platform-key/resolver tests now
exercising the raw-Npgsql stores against real Postgres (caught + fixed the `SUM(bigint)→numeric` cast).

**✅ DEPLOYED + LIVE (2026-07-19) — confirmed by direct check, not inference:**
- `authbilling_db` — a second `flexibleServers/databases` child on `psql-forge-dev` (same instance, no
  new server cost). Confirmed live via direct `psql` through the operator-IP firewall rule
  (`make 300-data-operator-ip` in `forge-infra`).
- KV secret `ConnectionStrings-AuthBillingConnection` — deployed and live, wired into ForgeUI's
  `500-app` env (`ConnectionStrings__AuthBillingConnection` → `connection-authbilling` secret ref via
  `forge-infra`'s `dev/500-app/main.bicep`) — no longer derived from `WriteConnection`. ForgeAPI gets
  the same wiring once `ca-forge-api-dev` is authored.
- Table bootstrap — `AuthBillingSchema.EnsureCreatedAsync` ran on `forge-ui:0.6.0`'s first boot;
  confirmed via direct `psql`: `platform_keys` and `ledger_entries` both exist, owned by `forge_admin`.

## Task 3 — `ForgeMission.Api` service (foundation) — the API-gateway tier

**Goal:** new tier-1 minimal-API host — `WebApplication.CreateSlimBuilder` + JSON source-gen,
AOT-clean. Health probe; a streaming reverse-proxy of the `/v1` wire to the runner's internal door
(SSE pass-through; not `/run`). Thin gateway only: auth, route, meter, forward — no mission logic. No
DB except the scoped `authbilling_db`.

**✅ FOUNDATION DONE (2026-07-18).** New [`ForgeMission.Api`](../../src/ForgeMission.Api/) slim host:
`/health` + [`WireProxy`](../../src/ForgeMission.Api/WireProxy.cs) — a hand-rolled (no YARP) streaming
reverse-proxy of `/v1/{**rest}` → the runner (`RunnerBaseUrl`), forwarding both verbs with
`ResponseHeadersRead` + `DisableBuffering` so SSE relays as it arrives; hop-by-hop headers stripped
both ways. References only the AOT-clean `ForgeMission.Billing` (auth/billing wraps landed in task 4).
`PublishAot` stays off for now per the AOT-sequencing note (flip as a fast-follow). Added to the slnx.

**Verified locally (two-service):** booted the runner (single-mission mode) behind ForgeAPI and drove
`POST /v1/messages` through the gateway → runner → real provider. Clean **HTTP 200** with a full
Anthropic `msg_…` body relayed (elevator-pitch via OpenAI, 3.9s), and the error path also relays (a
mission-internal 500 flows back verbatim). Gateway forwards + streams; mission outcome is orthogonal.

## Task 4 — Auth middleware (on `ForgeAPI`)

**Goal:** validate the platform key (42.5, via `ForgeMission.Billing`) on every `/v1/*` request →
attach `(userId, balance)`; 401 on bad/revoked key.

**✅ DONE (2026-07-19, commit `da977ff`).**
[`PlatformKeyAuthFilter`](../../src/ForgeMission.Api/PlatformKeyAuth.cs) resolves the Bearer token via
the shared `authbilling_db` resolver, stashes `PlatformKeyContext` on `HttpContext.Items` for tasks
5/6 to read, 401 on missing/invalid/revoked. `ApiJsonContext` added for the gateway's own AOT-clean
JSON responses. 4 unit tests pass
([`PlatformKeyAuthFilterTests`](../../src/ForgeMission.Rooms.Tests/Api/PlatformKeyAuthFilterTests.cs)).
Wired onto `/v1/{**rest}` via `.AddEndpointFilter<PlatformKeyAuthFilter>()` in `Program.cs`.

## Task 5a — design gap review (found + resolved 2026-07-19)

**Context:** re-reading the "decided" message-based API design before starting 5a's implementation
surfaced 3 things that read as decided but weren't. All 3 resolved in the same session, reviewed with
Ameer field-by-field across every message DTO — 5a moved from blocked to build-ready.

1. **`GetRunResponse` / `GetAccountResponse`** were proposed shapes, not yet reviewed. Confirmed both,
   plus used the review to align every message DTO on: `ClientToken` (renamed from `RequestId` —
   client-generated idempotency key, the EC2 `ClientToken` pattern, not AWS's server-generated tracking
   `RequestId`, auto-generated by the CLI so the caller never touches it); `ResponseStatus`/
   `ResponseError` now match `ServiceStack.ResponseStatus`/`ResponseError` verbatim, pulled from the
   real source (`github.com/ServiceStack/ServiceStack`, `src/ServiceStack.Interfaces/{ResponseStatus,
   ResponseError}.cs`) rather than assumed — `ErrorCode`/`Message`/`StackTrace`/`Errors`/`Meta`, no
   bespoke `TraceId` field (correlation id lives in `Meta["traceId"]`, matching how ServiceStack does
   it), no orphaned `Warnings` (was never defined, its only use — `MissionDeprecated` — is cut from
   scope); `GetMissionResponse` was referenced by comment but never defined anywhere in the doc — added,
   mirrors `MissionSummary`; closed-set fields (`GetRunResponse.Status`: `running`/`completed`/`failed`)
   documented as a fixed string vocabulary rather than a C# enum, for the same forward-compat reason
   `ErrorCode` is a string — an enum breaks deserialization on an older client when a new value is added.
2. **`MissionSource // NEW — see gap note below`** looked like it pointed at a missing note. Git-blamed
   both the comment and the ["Known gap: sources are not in the runner contract yet"](phase-42.6-hosted-endpoint-ttfa.md#known-gap-sources-are-not-in-the-runner-contract-yet)
   section in the active spoke — both added in the same commit (`da977ff`), same author, same edit. The
   note was never lost; a prior session's audit just didn't scroll far enough to find it before
   flagging it as missing. While closing this out, also checked `MissionSource` field-for-field against
   its actual upstream source, `Scout.SourceRef` (`src/ForgeMission.Scout/IWebSearch.cs`), and fixed two
   real mismatches rather than just confirming the note existed: `Provider` was nullable in the DTO but
   is non-nullable on `SourceRef` (whose own design tenet is "source attribution over source-selection" —
   every source is always attributed); `ImpartialityRating` (`SourceRef`'s existing Phase-41.6 stub) was
   missing from the DTO entirely — added so the shape doesn't need a second breaking change once 41.6
   populates it.
3. **`MissionSummary.Lifecycle`/`SupersededBy`/`SunsetAt`** were still referenced in the DTO and task
   text after mission lifecycle (`deprecated`/`retired` states) was cut from scope — decided 2026-07-19,
   no second consumer exists yet to protect from a breaking mission change. Fields removed from
   `MissionSummary`; the "Mission lifecycle" section in the active spoke rewritten to state the cut
   plainly instead of describing behavior that isn't built. `SearchMissions.IncludeDeprecated` (now dead
   with nothing to filter) dropped too.

## Task 5a — pre-build design lock (second pass, found + resolved 2026-07-19)

**Context:** immediately before writing any 5a code, walked every open server-side question in the
wire design with Ameer — six gaps where the DTOs were locked but *how the server resolves them* was
not. All six closed in one session; decisions + rationale below so a fresh agent never re-asks them.
Concrete interfaces landed in the active spoke's ["Mission
resolution"](phase-42.6-hosted-endpoint-ttfa.md#mission-resolution--handlepublisherversion--catalogrun-storage-decided-2026-07-19-pre-build-design-lock)
section — this is the narrative, that's the reference.

1. **Catalog metadata for `SearchMissions`/`GetMission` (Publisher/Version/Verified)** — the runner's
   `GET /missions` only returns `{MissionRef, Description}`; `ForgeUI.AgentRegistry` has the rest but
   is in-process and unreachable from `ForgeAPI`. **Decided:** a small static catalog behind a new
   `IMissionCatalog` interface, in-memory today, DB-swappable later — same repository-seam shape as
   `ILedgerStore`/`IPlatformKeyStore`. Not a shared lib with `ForgeUI` (more scope than needed today);
   not skipped (the endpoints are in this task's declared scope).
2. **`@websearch` doesn't exist in the runner's catalog** — not in `BuiltinMissions.All`, never
   published to `ghcr.io/katasec`, despite being the headline demo's handle throughout this doc.
   `missions/websearch/mission.mcl` already exists and is near-identical to the published "Grok"
   mission. **Decided:** publish it for real — add to `BuiltinMissions.All`, push to `ghcr.io/katasec`
   by digest, exactly like the other 5 built-ins. This is a build step of 5a, not deferred.
3. **`GetRun` (M6) has no storage** — `authbilling_db` has only `platform_keys` + `ledger_entries`,
   nothing for runs. **Decided:** `IRunStore` interface (opaque `runId` → serializable record,
   key-value shaped rather than SQL-shaped on purpose), in-memory/short-TTL today. Ameer's explicit
   long-term target is **blob storage**, not Postgres — the interface is generic enough to swap to
   disk, DB, or blob without a redesign.
4. **`ClientToken` idempotency (M7) has no schema support** — `LedgerEntry`/`ledger_entries` has no
   idempotency-key column at all. **Decided:** add a nullable `ClientToken` column + unique index via
   the existing idempotent `AuthBillingSchema.EnsureCreatedAsync` bootstrap (no EF migration —
   consistent with how task 2 already bootstraps this DB); `BillingService.SettleRunAsync` checks for
   an existing entry with the same token before appending a new debit.
5. **`GetAccountResponse.Email` has no data source** — email lives only in `rooms_db`; task 2's own
   rule forbids `ForgeAPI`/`authbilling_db` reaching across (`userId` is the only cross-context link,
   no cross-DB FK). **Decided:** `null` for now (`forge whoami` shows `MemberId`); a future internal
   service call (`ForgeUI` exposing an internal member-lookup endpoint) is the documented path to a
   real email later — explicitly not a cross-DB query, which would violate the bounded-context rule.
6. **`Mission` field parsing** — DTO examples showed publisher/version-qualified handles
   (`"katasec/websearch"`, `"websearch@2"`) with no parser, no version axis, and no publisher registry
   anywhere in the code. Resolved in three parts, worked through with Ameer:
   - **Case-insensitivity:** handles are lowercased on parse.
   - **Version:** a *separate* `MissionVersion` field was added to `ExecuteMission` and `GetMission`
     (not a `@version` suffix baked into the `Mission` string) — `null` means latest/currently-pinned.
     Deliberately named `MissionVersion`, not `Version`, because `Version` already means the *protocol*
     version (M3) on the same DTO; conflating the two was caught and avoided before it shipped.
   - **Publisher:** Docker-style default-namespace resolution — `MissionHandle.Parse` splits on `/`;
     an absent publisher defaults to `"forge"` (the brand identity, deliberately **not** `"katasec"`,
     which is the OCI *registry* namespace — a distribution detail this session chose to keep separate
     from the publisher concept, avoiding conflating "where it's hosted" with "who published it").
     Implicit and explicit-default resolve through **one lookup path**, not a fork — proven by a test
     asserting `Resolve("websearch") == Resolve("forge/websearch")`. An unrecognized publisher fails
     closed (`MissionNotFound`); it never falls through to a name-only match. This makes the
     publisher-prefix feature (multiple real publishers) a Bezos two-way door: `MissionHandle`/
     `IMissionCatalog` never change, only `StaticMissionCatalog`'s single hardcoded entry list gets
     replaced by a real registry later.

## Task 5a — build + local verification (2026-07-19)

Built directly from the design-lock above with no further design questions raised, on branch
`phase-42.6-task-5a-mission-invocation`. Two small gaps surfaced during the build that weren't
design decisions so much as omissions in the locked doc — resolved inline rather than re-opening
design review:

- **`Principal` (M5).** No such type existed. Decision: reuse `ForgeMission.Billing.PlatformKeyContext`
  (`MemberId`, `BalanceMicroUsd`) directly as the principal passed into message handlers — it already
  is exactly the HTTP-adapter-resolved value M5 describes; introducing a second, identical type would
  have been pure ceremony.
- **`ErrorCode.RunNotFound`.** The locked authoritative error-code list (six codes) didn't separately
  name a GetRun-not-found code, but `GetRunResponse`'s own doc comment said "e.g. RunNotFound" — an
  evident oversight, not a decision to overload `RunFailed` (a 500) onto a 404 case. Added as an
  eighth code, same additive precedent `MissionNotFound` already set.
- **`ForgeAPI` never called `AuthBillingSchema.EnsureCreatedAsync`** — only `ForgeUI` did. Since
  ForgeAPI now reads/writes `authbilling_db` directly (task 5a), it must not depend on ForgeUI having
  booted first to create the tables. Added the same idempotent bootstrap call ForgeUI's `Program.cs`
  already makes.

**Files added:** `src/ForgeMission.Api/{Messages,MissionCatalog,MissionExecutionService,MissionEndpoints,RunStore}.cs`.
**Files changed:** `src/ForgeMission.Api/Program.cs` (catalog/store wiring + endpoint mapping + schema
bootstrap); `src/ForgeMission.Billing/{AuthBillingSchema,BillingService,ILedgerStore,Ledger,NpgsqlLedgerStore}.cs`
(the `client_token` column + unique index, `LedgerEntry.ClientToken`, `ILedgerStore.FindByClientTokenAsync`,
`BillingService.SettleRunAsync`'s idempotency check); `src/ForgeMission.Cli/BuiltinMissions.cs` (new
`WebSearch` entry, pinned digest below).

**`websearch` published for real:** `ghcr.io/katasec/forge-mission-websearch:0.1.0`, pinned digest
`sha256:dc69d92b53cf0fbb28f0e241568eaa716ab3215f326a7ba72acd62b666d0478d` — pushed via `forge publish`
using a `gh auth token`-bridged registry credential (see
[deploy.md → OCI registry credentials](../design/deploy.md#oci-registry-ghcrio-publish-credentials--gh-cli-is-already-authenticated)).
Runner-verified: `GET /missions` lists `WebSearch` after a real pull from ghcr.io.

**Tests (12 new, `src/ForgeMission.Rooms.Tests/Api/`):** `MissionHandleTests` (parse equivalence,
whitespace, lowercasing), `StaticMissionCatalogTests` (implicit/explicit publisher equivalence,
unrecognized-publisher fails closed, unavailable-mission-ref doesn't resolve, search filtering),
`BillingServiceClientTokenTests` (real Postgres — retried `ClientToken` returns the prior debit
without charging again, distinct tokens both debit, null token preserves original always-debit
behaviour), `MissionExecutionServiceTests` (real Postgres billing + stubbed runner HTTP client —
resolve/run/debit end-to-end, `MissionNotFound`, `InsufficientCredit` without ever calling the
runner, cross-call `ClientToken` idempotency). Also fixed `PostgresFixture` — it never registered
`ILogging`, so any DI-activated type taking an `ILogger<T>` (i.e. `BillingService` itself) failed to
resolve; no existing test had hit this because none resolved `BillingService` directly. Full suite:
**338 passed, 0 failed, 10 skipped** (pre-existing live-integration tests, unrelated).

**Live local smoke test** (real Postgres container, real runner, real `ForgeAPI`, real xAI call —
not mocked): minted a platform key + $5 grant directly against a scratch `authbilling_db`, ran
`POST /api/ExecuteMission` for `{"mission":"websearch","input":"What did Anthropic announce this
week?"}` against the running stack. Result: `Verified: true`, real cited web-search answer,
`Usage.CostMicroUsd: 12390`, balance `5,000,000 → 4,987,610` (exact match), run round-tripped
`GET /api/GetRun` correctly, `GetAccount`/`SearchMissions`/`GetMission` (success + fail-closed) and
the no-auth-token 401 path all verified. Torn down cleanly after (containers removed, processes
killed, scratch files deleted).

**Known gap carried forward, not fixed here (unchanged from the design doc):**
`ExecuteMissionResponse.Sources` stays empty — the runner contract has no structured citations yet
(see "Known gap" in the main spoke). The live smoke test's answer text contains inline `[[1]]`-style
citations from the model, same as today's Grok/Rooms behaviour; `MissionSource[]` plumbing is
additive future work.

**Not exercised by the smoke test:** the streaming (`Stream: true`) response path — implemented
(NDJSON `MissionRunEvent` sequence via the same channel-based pattern as the runner's own
`RunStreamAsync`) and covered by the non-streaming code path sharing `RunCoreAsync`, but not
separately live-tested end-to-end (a second real paid search run wasn't worth the spend once the
buffered path proved the resolve→run→debit chain correct). Worth a quick live check before or during
task 8 (CLI hosted mode), since `forge exec`'s "stream the answer" UX decision depends on it.

## Task 5a/8/9 — deployed live + `forge exec` headline demo verified (2026-07-19)

Same session as the build above, continued through to a live deploy and the actual one-shot half of
the phase's done-when demo — not just local verification.

**Infra stood up (`forge-infra`, mostly authored by Codex, deployed/verified this session):**
- `dev/550-api` — new layer, `ca-forge-api-dev` (external ingress, `RunnerBaseUrl` + KV secret refs
  for `ConnectionStrings-AuthBillingConnection`/`PlatformKeys-HmacKey`). Bootstrapped on a public
  placeholder image, then pointed at the real one.
- `Dockerfile.forgeapi` (mission-control-language) — JIT ASP.NET, same shape as
  `Dockerfile.runner`/`Dockerfile.forgeui`; simpler than the runner's because `ForgeMission.Api`'s
  dependency tree (`Billing` → `Runner.Contracts`) pulls no private GitHub Packages, so no
  `NUGET_AUTH_TOKEN` build secret is needed.
- Built + pushed `crforgeroomsdev.azurecr.io/forge-api:0.1.0` → `0.1.1`, deployed via
  `make 550-api-what-if` → `make 550-api` each time (clean, scoped diffs both times — verified
  before deploying, not assumed).
- Custom domain `api.forge.katasec.com` bound: TXT `asuid.api.forge` added at the registrar,
  `az containerapp hostname add/bind --validation-method CNAME` (same pattern as `forge.katasec.com`
  in `dev/500-app`), managed cert issued in a few minutes (faster than Azure's own "up to 20 min"
  warning). Recorded `customDomain`/`customDomainCertificateId` in `main.bicepparam` and redeployed
  so a future `make 550-api` reapplies the binding instead of dropping it.
- Live-verified at every step via direct `curl`/`az containerapp show`/log inspection, not inferred
  from deploy success — `/health` 200, `/api/GetAccount`+`/api/SearchMissions` correctly 401 without
  a key, container env vars confirmed as KV `secretRef`s not plaintext.

**Bug caught while building the CLI client, fixed before it shipped:** `ExecuteMissionResponse`/
`MissionRunEvent` were serialized via a manual `JsonSerializer.SerializeToUtf8Bytes` call that
bypassed `ConfigureHttpJsonOptions`' camelCase default — so `ExecuteMission` came back PascalCase
while `GetAccount`/`SearchMissions`/`GetMission`/`GetRun` came back camelCase (framework-serialized).
Added `PropertyNamingPolicy = CamelCase` to `MessagesJsonContext`, verified locally (scratch Postgres
+ ForgeAPI, no runner needed — triggered the `MissionNotFound` path to inspect casing without a real
search call), then rebuilt/pushed/redeployed as `forge-api:0.1.1`.

**`forge exec` (task 8, one-shot half) built and live-verified:** `src/ForgeMission.Cli/ForgeExec.cs`
— sends `ExecuteMission` to `ForgeAPI` with the stored platform key (`CredentialStore.GetPlatform()`),
auto-generates `ClientToken` per call (M7), prints the answer + a verified/unverified trust footer
(no cost/balance inline, per the UX decision — `forge whoami` is the pull). Client-side DTOs are
local to the CLI, not a shared reference to `ForgeMission.Api` — deliberate: `ForgeAPI` is a non-AOT
server project and the `forge` CLI is AOT, so the dependency direction must not exist.
`FORGE_API_ENDPOINT` env var override added alongside the existing `FORGE_PLATFORM_ENDPOINT`
convention (`PlatformLogin.cs`) — different hosts (issuer vs. gateway), so one var can't cover both.

**Deploy gap found and closed:** `forge exec @websearch` initially failed `MissionNotFound` even
though the code was correct — the live `ca-forge-runner-dev` was still running an older image built
*before* `websearch` was added to `BuiltinMissions.All` on this branch, so it had never advertised
the mission at all. Not a code bug — a missed deploy step (the runner is a separate image/layer
`dev/500-app` shares with `ca-forge-ui-dev`, never touched by the `550-api` work above). Fixed:
built + pushed `forge-runner:0.9.0` with the `BuiltinMissions.cs` change, bumped `runnerImage` in
`dev/500-app/main.bicepparam`, `make 500-app-what-if` → `make 500-app` (clean 2-resource diff:
runner image bump + a benign `RunnerBaseUrl` env-var expression refresh on `ca-forge-ui-dev`, same
Bicep template). Confirmed via live boot log: `"loaded 6 mission(s): ChatGPT, Forge, Assistant,
Claude, Grok, WebSearch."` `ca-forge-api-dev`'s in-memory catalog is built once at boot from a
runner probe, so it also needed a restart (`az containerapp revision restart`) to pick up the
now-available mission — confirmed via `SearchMissions` returning the `websearch` entry.

**Second gap found, not yet closed (doesn't block the demo):** the runner log showed
`"pull failed for 'WebSearch' (... 401 Unauthorized) — falling back to baked-in
/app/missions/websearch"`. `forge-mission-websearch` defaulted to a **private** GHCR package on
publish, unlike its 5 public siblings (confirmed: `gh api .../packages/container/forge-mission-grok`
→ `public`; `.../forge-mission-websearch` → `private`). The mission still loaded (via the image's
baked-in fallback copy — functionally fine), but the intended anonymous-OCI-pull path is broken for
this one built-in. Attempted to fix via `gh api --method PATCH .../packages/container/...
-f visibility=public` — **404, current token scope (`write:packages`) is insufficient** for an
org-owned package's visibility; needs a manual fix via the GitHub web UI
(`github.com/orgs/katasec/packages/container/forge-mission-websearch/settings` → Danger Zone →
Change visibility → Public) or a token with org package-admin rights.

**Headline demo, one-shot half — verified live, real spend:**
```
$ forge exec @websearch "what shipped in the Claude API this week?"
<dated, source-attributed answer citing July 2026 Claude API release notes>
✓ verified
```
Ledger debited for real: `4,993,713 → 4,981,843 µ$` on the signed-in user's actual account (the
`~/.forge` cached platform key from an earlier `forge login` turned out to still be valid — an
earlier claim in this session that it was "stale" was wrong, based on one 401 that was actually a
transient blip right after a `550-api` redeploy, not a genuinely invalid key; corrected once
re-tested). Full test suite still green after all changes: 338 passed, 0 failed.

**What's still not done for the *full* phase done-when** (both verbs required, not either/or):
task 5b (API B chat-wire adapter — the aux-call classifier problem, not started), task 7 (shared
enrichment cache, needed for 5b's multi-replica correctness), and the `forge claude @handle`/
`forge missions` halves of task 8.

## What the message-based redesign (2026-07-18) supersedes

- **The "mission-selection mechanism" decision is void.** The header-vs-rewrite-`model` problem was
  manufactured by putting the handle in a URL; `RunRequest.MissionRef` was always a field. The runner
  needed no change — only the gateway framing did.
- **Task 5 split** into 5a (mission invocation, API A) and 5b (chat-wire adapter, API B + aux-call
  policy).
- **Task 6's metering source is simpler on 5a:** the terminal `result` message already carries
  full-mission `RunUsage`. The HTTP-trailer / `forge-usage`-SSE invention is only needed for 5b.
- **Task 7 (shared enrichment cache) is 5b-only** — one-shot invocation has no tool loop or
  re-entrancy.

## Migration-job DB-wipe — DEFUSED (2026-07-18), structurally fixed (2026-07-19)

**Status: closed.** No live data-drop path remains; not a deploy gate; not something to re-investigate
unless the symptom recurs.

**What happened:** during the `authbilling_db` infra work the dev `forge_rooms` DB was wiped (all
rooms/members/messages gone, confirmed by sign-in). Two mechanisms were live at the time:
- The two `DropTable` calls in
  [`DropLedgerAndPlatformKeysFromRooms`](../../src/ForgeMission.Rooms.Data/Migrations/20260717233324_DropLedgerAndPlatformKeysFromRooms.cs)
  `Up()`.
- The auto-run migrate step in `forge-infra`'s `.github/workflows/infra.yml` (`500-app` → start
  `caj-forge-migrate-dev`).

**Immediate fix (2026-07-18):** both mechanisms commented out — the `authbilling_db` cutover never
needed the `DropTable` calls (stale `forge_rooms` tables are harmless left in place), and migrations
are now run deliberately, not on every deploy.

**Structural fix (2026-07-19):** the migration job (`caj-forge-migrate-dev`) was moved out of
`dev/500-app` entirely into its own layer, `dev/450-migrate` — an app deploy can no longer touch the
job at all, deliberate or not. This is the actual close-out; the immediate fix above was a stopgap.
See the [Deploy Runbook](../design/deploy.md).

**Root cause — never fully confirmed, investigation abandoned once structurally moot:** the `500-app`
deploy at the time ran a pre-deploy migration job whose container entrypoint was
`/app/migrate --connection $(CONNECTION_WRITE)` (image `forge-ui:0.5.0`). `500-app` was redeployed
twice to recover from a credential rotation; the data was gone afterward. Ruled out: the password
rotation itself (changes a credential, never drops data); the `DropLedgerAndPlatformKeysFromRooms`
migration (existed only in unbuilt local code, not in `0.5.0`). Prime suspect, never verified: the
`/app/migrate` entrypoint doing `EnsureDeleted()`+`EnsureCreated()` or a drop/recreate instead of an
additive `Database.Migrate()`.

**Standing lesson (this is the part worth keeping, independent of the specific incident):** verify a
migration entrypoint's semantics before running it against any populated DB. A migration job coupled
into an app-deployment manifest is a category error — different lifecycle, different blast radius from
the app itself — which is why the structural fix was to separate the layers, not just disable the
auto-run.
