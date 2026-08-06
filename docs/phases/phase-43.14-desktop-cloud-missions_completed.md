# Phase 43.14 — Completed work

> Companion to [phase-43.14-desktop-cloud-missions.md](phase-43.14-desktop-cloud-missions.md), which
> stays a lookup table of current status. This file holds the full build narrative and evidence for
> each task once it's done — so a session working on what's still open doesn't have to load this into
> context.

## Task 1 — ForgeAPI DTOs

**Goal:** add the additive DTO fields for agent-turn support to ForgeAPI's `ExecuteMission`/
`ExecuteMissionResponse` contract in `src/ForgeMission.Api/Messages.cs` — `History`/`Tools` on
`ExecuteMission`, `ToolUse` on `ExecuteMissionResponse`, plus the new `TurnMessage`/`TurnContent`/
`MissionToolDecl`/`ToolUseCall` types (shapes locked in the "Design review" section of the active
spoke). Register the new types in the existing STJ source-gen `JsonSerializerContext`. Purely
additive — the existing `MissionExecutionServiceTests.cs` suite had to pass unmodified.

**✅ DONE (2026-08-06)** — implemented by Codex on `codex/phase-43.14-api-dto-turn-support`
(`26d9550 Add agent turn DTOs to API messages`), following the Claude↔Codex handoff protocol
([docs/design/claude-codex-workflow.md](../design/claude-codex-workflow.md)): task assignment →
implementation plan → Claude review/approval → implementation → completion summary → Claude
verification (below).

**What changed:**
- [`src/ForgeMission.Api/Messages.cs`](../../src/ForgeMission.Api/Messages.cs) — `History`/`Tools`
  added to `ExecuteMission`, `ToolUse` added to `ExecuteMissionResponse`; new `TurnMessage`,
  `TurnContent`, `MissionToolDecl`, `ToolUseCall` sealed classes, shapes matching the spoke's "Wire
  shape" section exactly (`JsonElement` for schema/input/arguments, string-typed `Role`/`Type` for
  the closed-but-additive-friendly enums). All four registered as `[JsonSerializable]` on
  `MessagesJsonContext`.
- [`src/ForgeMission.Api/Properties/AssemblyInfo.cs`](../../src/ForgeMission.Api/Properties/AssemblyInfo.cs)
  (new) — `[assembly: InternalsVisibleTo("ForgeMission.Rooms.Tests")]`, exposing the internal
  `MessagesJsonContext` to the test assembly. Matches existing precedent in this repo
  (`ForgeMission.Runner`, `ForgeMission.Orchestration`, `ForgeMission.ClientRuntime`,
  `ForgeMission.Docker` all use the same pattern for internal-context test access).
- [`src/ForgeMission.Rooms.Tests/Api/MessagesSerializationTests.cs`](../../src/ForgeMission.Rooms.Tests/Api/MessagesSerializationTests.cs)
  (new) — two tests round-tripping `ExecuteMission` (with populated `History`/`Tools`, including
  nested `tool_use`/`tool_result` content and a `JsonElement` tool-input payload) and
  `ExecuteMissionResponse` (with populated `ToolUse`) through `MessagesJsonContext.Default`,
  asserting the camelCase wire property names (`"history"`, `"tools"`, `"toolUse"`) and that nested
  `JsonElement` payloads survive the round trip.

**Verified independently by Claude (not just Codex's summary):**
- `dotnet build src/ForgeMission.slnx --no-restore` — clean, 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.Rooms.Tests --no-build --filter "FullyQualifiedName~MessagesSerializationTests|FullyQualifiedName~MissionExecutionServiceTests"` —
  **10/10 pass** (2 new `MessagesSerializationTests` + 8 existing `MissionExecutionServiceTests`,
  confirming the existing suite is unmodified and still green).
- Diff read directly (`git show 26d9550`) and checked field-for-field against the spoke's locked
  "Wire shape" DTO definitions — exact match.

**Merged:** [PR #24](https://github.com/katasec/mission-control-language/pull/24) into `main`,
approved by the operator 2026-08-06.

## Task 2 — Runner contract DTOs

**Goal:** mirror Task 1's additive fields one tier down, in
`src/ForgeMission.Runner.Contracts/RunContracts.cs` — `RunRequest` gains `History`/`Tools`,
`RunResponse` gains `ToolUse`, same shapes as `ExecuteMission`/`ExecuteMissionResponse`. No new
`RunStreamEvent.Type` — a non-terminal turn rides the existing `"result"` event with
`RunResponse.ToolUse` populated instead of `AgentText`. Purely additive — existing
`ForgeMission.Runner.Tests` and `ForgeMission.Rooms.Tests` (the ForgeAPI-side consumer of
`RunRequest`/`RunResponse`) had to pass unmodified.

**✅ DONE (2026-08-06)** — implemented by Codex on `codex/phase-43.14-runner-contract-dtos`
(`546d967 Add agent turn fields to runner contracts`), following the same Claude↔Codex handoff loop
as Task 1.

**What changed:**
- [`src/ForgeMission.Runner.Contracts/RunContracts.cs`](../../src/ForgeMission.Runner.Contracts/RunContracts.cs) —
  `History`/`Tools` appended as optional trailing params on the `RunRequest` record (after
  `InputArtifacts`), `ToolUse` appended on `RunResponse` (after `OutputArtifacts`) — preserves every
  existing named-arg call site. New `TurnMessage`/`TurnContent`/`MissionToolDecl`/`ToolUseCall`
  classes, field-for-field mirrors of Task 1's API-side types. All four registered as
  `[JsonSerializable]` on `RunContractsContext`. Deliberately **PascalCase on the wire** (no
  `PropertyNamingPolicy` override) — this context has never used camelCase, unlike
  `MessagesJsonContext`; mirroring the shape doesn't mean mirroring that unrelated policy.
- [`src/ForgeMission.Runner.Tests/RunContractsSerializationTests.cs`](../../src/ForgeMission.Runner.Tests/RunContractsSerializationTests.cs)
  (new) — two tests, same structure as Task 1's `MessagesSerializationTests`: round-trip a
  `RunRequest` with nested `History`/`Tools` (incl. a `JsonElement` tool-input payload) and a
  `RunResponse` with `ToolUse`, through `RunContractsContext.Default`.

**Verified independently by Claude:**
- `dotnet build src/ForgeMission.slnx --no-restore` — clean, 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.Runner.Tests --no-build` — **3/3 pass** (2 new + 1 pre-existing
  `RunnerRegistryTests`).
- `dotnet test src/ForgeMission.Rooms.Tests --no-build` — **95/95 pass**, confirming the ForgeAPI-side
  consumer of `RunRequest`/`RunResponse` is unaffected.
- Diff read directly (`git show 546d967`) and checked against the spoke's Task 2 description and
  Task 1's DTO shapes — exact mirror, record-append pattern preserves existing constructors as
  claimed.

**Merged:** [PR #25](https://github.com/katasec/mission-control-language/pull/25) into `main`,
approved by the operator 2026-08-06.

## Task 3 — Enrichment cache (code)

**Goal:** a real, shared, multi-replica-safe `IEnrichmentCache` for the runner —
`PostgresEnrichmentCache` (raw Npgsql, no EF), own database/connection string (never
`authbilling_db`'s), idempotent schema bootstrap mirroring `AuthBillingSchema`'s pattern, DI-wired
in `src/ForgeMission.Runner/Program.cs` with a fallback to `InMemoryEnrichmentCache` when
unconfigured. Additionally scoped in during planning: thread the DI-resolved cache into
`MissionDoorClient` (the `/v1` doors), since today it silently defaults to a fresh
`InMemoryEnrichmentCache()` per request — a gap Codex's plan surfaced and confirmed independently
before implementing, per AGENTS.md's design-first rule (an unresolved wiring question gets flagged,
not silently built around).

**✅ DONE (2026-08-06)** — implemented by Codex on `codex/phase-43.14-enrichment-cache`
(`3077b51 Add runner Postgres enrichment cache`).

**What changed:**
- [`src/ForgeMission.Runner/PostgresEnrichmentCache.cs`](../../src/ForgeMission.Runner/PostgresEnrichmentCache.cs)
  (new) — `GetAsync` filters `expires_at > NOW()`; `SetAsync` upserts via
  `ON CONFLICT (prefix_hash) DO UPDATE`, storing the snapshot as `jsonb`
  (`NpgsqlDbType.Jsonb`), serialized through a new source-generated
  [`EnrichmentCacheJsonContext`](../../src/ForgeMission.Runner/EnrichmentCacheJsonContext.cs)
  (`Dictionary<string,string>`, AOT-safe, no runtime `JsonSerializerOptions`). 30-minute default TTL,
  matching `InMemoryEnrichmentCache`.
- [`src/ForgeMission.Runner/EnrichmentCacheSchema.cs`](../../src/ForgeMission.Runner/EnrichmentCacheSchema.cs)
  (new) — idempotent `CREATE TABLE IF NOT EXISTS enrichment_cache (prefix_hash PK, snapshot jsonb,
  expires_at)` + an index on `expires_at`, mirroring `AuthBillingSchema.EnsureCreatedAsync`'s pattern.
- [`src/ForgeMission.Runner/Program.cs`](../../src/ForgeMission.Runner/Program.cs) — resolves
  `ConnectionStrings:EnrichmentCacheConnection` only (never derives from or reads
  `AuthBillingConnection`); registers `InMemoryEnrichmentCache` when unset, or a singleton
  `NpgsqlDataSource` + `PostgresEnrichmentCache` when set, running the schema bootstrap once at
  startup. Both `MissionDoorClient` constructions (the Anthropic and OpenAI `/v1` doors) now receive
  the one shared `IEnrichmentCache` singleton instead of each building their own default.
- [`src/ForgeMission.Runner/MissionDoorClient.cs`](../../src/ForgeMission.Runner/MissionDoorClient.cs) —
  new constructor parameter `IEnrichmentCache enrichmentCache`, passed through to
  `MissionChatClient`'s `enrichmentCache:` argument (previously omitted, defaulting to a fresh
  `InMemoryEnrichmentCache()` every request — the gap Codex's plan flagged and fixed).
- [`src/ForgeMission.Runner.Tests/PostgresFixture.cs`](../../src/ForgeMission.Runner.Tests/PostgresFixture.cs)
  (new) — `Testcontainers.PostgreSql`-backed, mirroring `ForgeMission.Rooms.Tests`' fixture pattern
  (previously absent from this test project).
- [`src/ForgeMission.Runner.Tests/PostgresEnrichmentCacheTests.cs`](../../src/ForgeMission.Runner.Tests/PostgresEnrichmentCacheTests.cs)
  (new) — round-trips a snapshot through two independent `NpgsqlDataSource` instances (a writer and a
  reader), proving a real durable store rather than same-process caching; also calls
  `EnsureCreatedAsync` twice to prove the bootstrap stays safe on every boot.

**Verified independently by Claude:**
- `dotnet build src/ForgeMission.slnx --no-restore` — clean, 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.Runner.Tests --no-build` — **4/4 pass** (1 new Postgres round-trip +
  3 pre-existing).
- `grep -rn "AuthBillingConnection|authbilling" src/ForgeMission.Runner/` — zero config-path
  references; the only hits are doc comments explaining the isolation, confirming the runner never
  reads `authbilling_db` credentials in any code path.
- Started the runner directly (`dotnet run --project src/ForgeMission.Runner`) with
  `EnrichmentCacheConnection` unset — `GET /health` returned `{"status":"ok"}` with no Postgres
  connection attempted (confirmed from startup log: no schema-bootstrap step ran), proving the
  no-Postgres local fallback.
- Diff read directly (`git show 3077b51`) and checked against the approved plan — matches, including
  the `MissionDoorClient` wiring fix.

**Explicitly not yet done — tracked separately as its own claim, not implied by the above:**
actual deployment/verification against real Azure Postgres. That's
[Task 3b](phase-43.14-desktop-cloud-missions.md#3b-enrichment-cache-infra--provision-enrichmentcacheconnection-in-forge-infra)
in the active spoke — a separate `forge-infra` change, still open, with the exact wiring chain
locked in against the real current Bicep (not assumptions) specifically to avoid repeating a past
"built/tested locally, nothing worked in Azure" failure.

**Merged:** [PR #TBD] into `main`, pending operator approval.
