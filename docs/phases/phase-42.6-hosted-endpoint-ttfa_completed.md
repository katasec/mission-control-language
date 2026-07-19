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
