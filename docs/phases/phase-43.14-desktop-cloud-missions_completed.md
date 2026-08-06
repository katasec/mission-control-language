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

**Merged:** [PR #26](https://github.com/katasec/mission-control-language/pull/26) into `main`,
approved by the operator 2026-08-06.

## Task 4 — Runner execution: thread `History`/`Tools` into `MissionRunHandler`

**Goal:** `MissionRunHandler.ExecuteAsync` calls `PipelineRunner.RunAsync` directly and does not go
through `MissionChatClient` — so this task extracts `MissionChatClient.BuildOptionsAsync`'s
three-segment gate (`ConversationHash.Prefix`, `IsToolContinuation`, enrichment-cache get/set,
`StartAtAgent`) into a shared helper both `MissionRunHandler` and `MissionChatClient` call, without
losing `MissionRunHandler`'s existing trace/progress/artifact wiring. Populate `RunResponse.ToolUse`.

Two real gaps surfaced during planning, both resolved before implementation (design-first, per
AGENTS.md — neither was silently built around):
- **Type mismatch**: `RunRequest.History` is `List<TurnMessage>` (Runner.Contracts POCOs from Task
  2), but `ConversationHash`/`IsToolContinuation` are hard-typed to `IReadOnlyList<ChatMessage>`
  (Microsoft.Extensions.AI). Resolved by converting `TurnMessage → ChatMessage` in the Runner tier
  before calling the shared gate, keeping `ConversationHash` itself untouched and single-sourced.
- **Role-mapping correctness**: an early plan draft proposed representing a `tool_result` turn under
  a fabricated `ChatRole.Tool`. Caught in review — `TurnMessage.Role` is locked to `"user"|"assistant"`
  only (Task 1/2's own DTO), and `MissionChatClient.cs`'s `ExtractGoal` comment documents that
  tool-result hand-backs arrive with role **"user"** on the real wire. Corrected to a straight 1:1
  role mapping, with `tool_result` becoming a `FunctionResultContent` *within* that user-role message.

**✅ DONE (2026-08-06)** — implemented by Codex on `codex/phase-43.14-runner-tool-continuation`
(`ca880bc Add runner tool continuation support`).

**What changed:**
- [`src/ForgeMission.Core/Runtime/ToolContinuationGate.cs`](../../src/ForgeMission.Core/Runtime/ToolContinuationGate.cs)
  (new) — the extracted three-segment gate as a static helper (`ApplyAsync`), reusing
  `ConversationHash.Prefix` unchanged; returns a `ToolContinuationState(StartAtAgent,
  OnPreAgentComplete)`.
- [`src/ForgeMission.Core/Adapters/MissionChatClient.cs`](../../src/ForgeMission.Core/Adapters/MissionChatClient.cs) —
  its inline gate replaced with a call to `ToolContinuationGate.ApplyAsync`; duplicate-continuation
  observation stays at its existing call site, unmoved. Pure extraction — behavior unchanged
  (confirmed by the existing `ThreeSegmentExecutorTests` passing without modification).
- [`src/ForgeMission.Runner/RunnerToolTurnMapper.cs`](../../src/ForgeMission.Runner/RunnerToolTurnMapper.cs)
  (new, Runner-only, reflection-free) — `ToChatMessages` (maps `TurnMessage.Role` 1:1, synthesizes a
  single user turn from `Goal` when `History` is empty, per the spoke's "forge exec is a degenerate
  case" design); `ToTools` (each `MissionToolDecl` → `new DeclaredTool(...)`, reusing the
  `Katasec.AITools` declaration-only-`AIFunction` shape already used in
  `AgentToolDeclarations.cs`); `ToToolUse` (`FunctionCallContent → ToolUseCall`, converting
  `Arguments` to `JsonElement` via a hand-rolled `Utf8JsonWriter` walk — mirroring
  `MissionRuntimeSession`'s existing `JsonElement→IDictionary` conversion in reverse — that throws
  explicitly on any unsupported value type rather than falling back to a reflection-based serializer.
- [`src/ForgeMission.Runner/MissionRunHandler.cs`](../../src/ForgeMission.Runner/MissionRunHandler.cs) —
  new constructor params `IEnrichmentCache enrichmentCache` and an optional
  `Func<RunnerMission, UsageAccumulator, IExpertRunner>? runnerFactory` (defaulting to the existing
  `BuildRunner`, enabling direct construction in tests with no DI/`Program.cs` changes — a simpler
  seam than an earlier plan draft's dedicated factory class, trimmed in review per AGENTS.md's
  no-speculative-abstractions rule). `ExecuteAsync` now builds `ChatMessage`s from `request.History`,
  runs the shared gate, and maps `MissionResult.ToolCalls → RunResponse.ToolUse`. When `ToolUse` is
  populated: `AgentText` is empty, `Verified` is false; `Trace`/`Usage`/`OutputArtifacts` still
  reflect the actual partial run (Task 5 decides what to do with those fields when shaping the API
  response).
- [`src/ForgeMission.Runner.Tests/MissionRunHandlerTests.cs`](../../src/ForgeMission.Runner.Tests/MissionRunHandlerTests.cs)
  (new) — builds a real 3-step mission (`Enrich → Respond(role:agent) → Verify`) with a scripted
  `IExpertRunner`, and proves end-to-end: the first call stops after `Respond` emits a tool call
  (`Trace.Count == 2`, `Verify` never reached, `ToolUse` populated, `AgentText` empty, `Verified`
  false); the replayed continuation resumes at `Respond` without re-running `Enrich` (enrich-once,
  proven via a call counter against a real `InMemoryEnrichmentCache`), and asserts directly that the
  tool-result message the agent sees has `Role == ChatRole.User` — a functional, not just
  code-reading, confirmation of the role-mapping fix — then reaches `Verify` and returns a terminal,
  verified result.

**Verified independently by Claude:**
- `dotnet build src/ForgeMission.slnx --no-restore` — clean, 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.Runner.Tests --no-build` — **5/5 pass**.
- `dotnet test src/ForgeMission.Tests --filter "FullyQualifiedName~ThreeSegmentExecutorTests"` —
  **6/6 pass**, confirming the `MissionChatClient` extraction is behavior-neutral.
- `dotnet test src/ForgeMission.slnx --no-build` (full solution) — **456/456 pass, 11 skipped, 0
  failed** across all three test projects. The two live xAI integration tests Codex's environment
  reported as 403 failures (credit/spend-limit exhaustion) skip cleanly in this environment (no
  `XAI_API_KEY` configured here) — confirming they're a live-external-API dependency unrelated to
  this change, not a masked regression.
- `dotnet publish src/ForgeMission.Cli -c Release -r osx-arm64 --self-contained -p:PublishAot=true` —
  succeeds; only pre-existing macOS SDK linker-version warnings, no ILC/trim warnings — confirms the
  new code (including the hand-rolled `Utf8JsonWriter` JSON handling) stays AOT-safe.
- Diff read directly (`git show ca880bc`) and checked against the twice-revised, approved plan —
  matches exactly, including both review corrections (the constructor-delegate seam instead of a
  factory class, and the 1:1 role mapping).
