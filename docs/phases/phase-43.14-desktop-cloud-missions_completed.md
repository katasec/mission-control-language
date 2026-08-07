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

**Merged:** [PR #27](https://github.com/katasec/mission-control-language/pull/27) into `main`,
approved by the operator 2026-08-06.

## Task 5 — ForgeAPI wiring

**Goal:** thread `msg.History`/`msg.Tools` into the `RunRequest` sent to the runner; when the
runner's `RunResponse.ToolUse` is populated, map it to `ExecuteMissionResponse.ToolUse` and skip
billing settlement; when it's `null`, settle exactly as today.

One real gap surfaced during planning: `msg.History`/`msg.Tools` (`ForgeMission.Api`'s types, Task
1) and `RunRequest.History`/`Tools` (`ForgeMission.Runner.Contracts`'s types, Task 2) are
separately-defined, field-identical DTOs on either side of the ForgeAPI→Runner boundary — not the
same C# type. "Thread into the RunRequest" needed a real field-for-field bridge, not a direct
assignment. Two response-shape questions (what `Usage`/`BalanceMicroUsd` become on a non-terminal
response; whether output-artifact copying and `runStore.SaveAsync` should run for a non-terminal
response) were resolved in the plan before implementation, per AGENTS.md's design-first rule.

**✅ DONE (2026-08-06)** — implemented by Codex on `codex/phase-43.14-forgeapi-tool-wiring`
(`b3ef071 Wire tool continuations through Forge API`).

**What changed:**
- [`src/ForgeMission.Api/MissionToolTurnMapper.cs`](../../src/ForgeMission.Api/MissionToolTurnMapper.cs)
  (new) — field-for-field bridge (`ToRunnerHistory`, `ToRunnerTools`, `ToApiToolUse`) between
  `ForgeMission.Api`'s and `ForgeMission.Runner.Contracts`'s separately-defined `TurnMessage`/
  `TurnContent`/`MissionToolDecl`/`ToolUseCall` types, using a `RunnerContracts` namespace alias to
  disambiguate the identically-named types cleanly. Pure copying, no semantic conversion.
- [`src/ForgeMission.Api/MissionExecutionService.cs`](../../src/ForgeMission.Api/MissionExecutionService.cs) —
  `RunCoreAsync` (shared by both the buffered and streaming entry points) now maps `msg.History`/
  `msg.Tools` into the outbound `RunRequest`. When `result.ToolUse` is populated, returns early: a
  new `DiscardOutputsFromRunnerAsync` helper deletes any unexpected runner output artifacts (rather
  than copying or leaking them), `Usage` reports the runner's real token/compute observations with
  `CostMicroUsd: 0` (nothing settled), `BalanceMicroUsd` is still fetched (unchanged, but visible to
  the client), and `Answer`/`Verified`/`Trace`/`Artifacts`/`runStore.SaveAsync` are all skipped as
  terminal-only. When `result.ToolUse` is `null`, behavior is byte-identical to before this task.
- [`src/ForgeMission.Rooms.Tests/Api/MissionExecutionServiceTests.cs`](../../src/ForgeMission.Rooms.Tests/Api/MissionExecutionServiceTests.cs) —
  new `Execute_forwards_agent_turn_and_returns_tool_use_without_settlement` test captures the
  outbound `RunRequest` via an extended stub handler and asserts the full round trip (nested
  `tool_use`/`tool_result` content, tool schema) maps correctly; asserts zero cost, unchanged
  balance (both in the response and via a direct `Billing.GetBalanceMicroUsdAsync` check), zero
  `runStore` saves (`RecordingRunStore`), zero artifact downloads, and exactly one artifact
  delete — deliberately exercising the unexpected-output-artifact cleanup path by planting an
  `OutputArtifacts` entry on the mocked runner response. The existing plain-request test gained
  `Assert.Null(handler.LastRunRequest.History/Tools)` as the regression guard. The pre-existing
  `Retried_ClientToken_does_not_double_debit_across_two_Execute_calls` test needed no changes —
  still proves terminal idempotency unmodified.

**Verified independently by Claude:**
- `dotnet build src/ForgeMission.slnx --no-restore` — clean, 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.Rooms.Tests --filter "FullyQualifiedName~MissionExecutionServiceTests"` —
  **9/9 pass**.
- `dotnet test src/ForgeMission.slnx --no-build` (full solution) — **457/457 pass, 11 skipped, 0
  failed**. Confirms Codex's reported two live-xAI 403s and one flaky exec-runner broken-pipe
  failure are environment/flakiness issues (skip cleanly here with no `XAI_API_KEY`), not a masked
  regression from this change.
- Diff read directly (`git show b3ef071`) and checked against the approved plan — matches exactly,
  including both response-shape decisions (zero-cost usage + unchanged balance; skip artifacts/
  run-store for non-terminal turns).

**Merged:** [PR #28](https://github.com/katasec/mission-control-language/pull/28) into `main`,
approved by the operator 2026-08-06.

## Task 6 — Desktop-side cloud client

**Goal:** a new `CloudMissionRuntimeSession` in `src/ForgeMission.ClientRuntime/Services/` — same
round-trip shape as `MissionRuntimeSession` (`SendAsync` loop, `onTextDelta`/`onToolCall`
callbacks, executes tool calls via the existing `ToolExecutorRegistry`/`CapabilityRegistry`), but
speaking `ExecuteMission`/`ExecuteMissionResponse` JSON against ForgeAPI instead of Anthropic SSE.
Owns and replays `History` itself (no server-held session, per the locked decision), reuses one
`ClientToken` across the whole logical run. `MissionRuntimeSession` stays untouched.

Three real questions were resolved in planning before implementation, per AGENTS.md's design-first
rule — none silently improvised:
- **Wire DTOs**: `ForgeMission.ClientRuntime` has no project reference to `ForgeMission.Api` or
  `ForgeMission.Runner.Contracts` (confirmed by reading its `.csproj`) — consistent with how every
  tier in this phase has independently defined its own wire-shaped DTOs rather than sharing
  assemblies across deployables. `CloudMissionRuntimeSession` follows the same pattern: private
  `WireExecuteMission`/`WireTurnMessage`/etc. types local to the file.
- **`ClientToken` scope**: one fresh token generated at the start of each `SendAsync` call, reused
  across every `ExecuteMission` request within that call's tool-continuation loop; a later,
  independent `SendAsync` call (a new user prompt) gets its own fresh token — matching what the
  billing idempotency test actually protects against (a retry of the same logical run, not a whole
  conversation).
- **`onTextDelta` granularity**: ForgeAPI's API A has no token-level streaming (only a terminal
  `Answer` string) — `onTextDelta` fires once with the complete answer on the terminal response,
  honest about the wire's real capability rather than faking a typing effect. Tool-use (non-terminal)
  responses fire no text delta.
Also decided: buffered `ExecuteMission` (not the streaming form) — simplest correct implementation
for API A's locked behavior, with progress-NDJSON UI plumbing deferred as separate future work; and
a shared `MissionRuntimeSession`/`CloudMissionRuntimeSession` interface is deferred to Task 8, which
does the actual mode-based selection.

**✅ DONE (2026-08-06)** — implemented by Codex on `codex/phase-43.14-cloud-mission-session`
(`894f451 Add cloud mission runtime session`).

**What changed:**
- [`src/ForgeMission.ClientRuntime/Services/CloudMissionRuntimeSession.cs`](../../src/ForgeMission.ClientRuntime/Services/CloudMissionRuntimeSession.cs)
  (new) — `SendAsync` maintains a per-session `_history` list (`WireTurnMessage`s) that accumulates
  across calls; a `firstTurn` flag (true only for this session instance's very first `SendAsync`
  call) governs exactly when the user's prompt gets appended, avoiding duplicate entries whether the
  first response is immediately terminal or goes through one or more tool round-trips. Every request
  resends `Input` as the original prompt text (required both by ForgeAPI's non-empty-`Input`
  validation and to keep `ConversationHash.Prefix` stable across the continuation). Tool arguments
  convert `JsonElement → IDictionary<string,object?>` via the same reflection-free pattern already
  used in `MissionRuntimeSession`. Errors surface as `HttpRequestException` (transport) or
  `InvalidOperationException` (a `ResponseStatus.ErrorCode` on an HTTP-200 business failure) — both
  already caught identically by the one existing caller, `ClientRuntimeEndpoints.cs`. A private
  `CloudWireJsonContext` (camelCase, matching the real `MessagesJsonContext` wire convention) covers
  the private wire DTOs.
- [`src/ForgeMission.Tests/ClientRuntime/CloudMissionRuntimeSessionTests.cs`](../../src/ForgeMission.Tests/ClientRuntime/CloudMissionRuntimeSessionTests.cs)
  (new) — a genuinely end-to-end test: real `LocalDiskWorkspace`/`WorkspaceFileProvider`/
  `CapabilityDispatcher` infrastructure actually reads a real temp file for the "Read" tool call
  (not mocked), against a scripted ForgeAPI handler. Asserts the first request has no `history`
  field; the second (continuation) request reuses the same `clientToken`/`input` with a 3-item
  replayed history (`user`/`tool_use`/`tool_result`, including the real file content flowing through
  `tool_result`); a third, independent `SendAsync` call gets a different `clientToken`; `onTextDelta`
  fires exactly once per call with the final answer text only; and the tool-call notification log
  shows exactly one Running→Done pair.

**Verified independently by Claude:**
- `dotnet build src/ForgeMission.slnx --no-restore` — clean, 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.Tests --filter "FullyQualifiedName~CloudMissionRuntimeSessionTests"` —
  **1/1 pass**.
- `dotnet test src/ForgeMission.slnx --no-build` (full solution) — **458/458 pass, 11 skipped, 0
  failed**, including the new test. No PostgreSQL SSL errors here at all — confirms Codex's reported
  three Postgres-fixture failures are environment-specific to their machine, not a regression (this
  class never touches Postgres).
- `dotnet publish src/ForgeMission.ClientRuntime -p:PublishAot=true` — reproduces the same
  `NETSDK1203` error Codex reported (`ForgeMission.ClientRuntime.Presentation`, a `browser-wasm`
  project, is incompatible with Native AOT publish). Confirmed **structurally pre-existing, not
  introduced by this task**: `git show 894f451 --stat` shows only 2 new `.cs` files, zero
  `.csproj`/project-reference changes — the WASM project reference this error comes from is
  untouched by this diff.
- Traced `ConversationHash.Prefix`/`ToolContinuationGate`'s actual logic (from Task 4) against this
  client's history-replay behavior by hand, both during plan review and again reading the shipped
  code — the hash stays stable across multiple sequential tool round-trips within one logical turn
  (not just a single tool call), because every subsequent user-role message in that turn carries
  `FunctionResultContent` and is correctly excluded from `LastUserTurnIndex`'s "last real user turn"
  candidacy — confirmed correct, not just plausible.
- Diff read directly (`git show 894f451`) and checked against the approved plan — matches exactly.

**Merged:** [PR #29](https://github.com/katasec/mission-control-language/pull/29) into `main`,
approved by the operator 2026-08-06.

## Task 7 — Credentials

**Goal:** replace the hardcoded `MissionRuntime__Credential = "local"` in
`src/ForgeMission.Desktop/Program.cs`'s `StartClientRuntime` with a read of the platform key
`forge login` writes to `~/.forge`, reusing the CLI's existing helper rather than reimplementing
the read.

**✅ DONE (2026-08-07)** — implemented by Codex on `codex/phase-43.14-desktop-credentials`
(`910b837 Forward the real platform credential to the Client Runtime`), two small revision rounds.

**What changed:**
- [`src/ForgeMission.Desktop/Program.cs`](../../src/ForgeMission.Desktop/Program.cs) — the
  `args.Length == 0` branch (the real double-click launch path; the explicit-URL dev path is
  correctly left untouched, since Desktop doesn't own that external process's environment) now
  calls `CredentialStore.GetPlatform()` before resolving or launching any runtime. A missing/empty
  key prints `"Not signed in. Run `forge login`."` (matching `ForgeExec.cs`'s exact existing
  message) and calls `Environment.Exit(1)` before any child process starts. `StartClientRuntime`
  gained a `missionRuntimeCredential` parameter, passed through as `MissionRuntime__Credential`
  instead of the `"local"` sentinel.
- [`src/ForgeMission.Desktop/ForgeMission.Desktop.csproj`](../../src/ForgeMission.Desktop/ForgeMission.Desktop.csproj) —
  new direct `ForgeMission.Core` project reference (genuinely needed: `Desktop → Orchestration →
  Docker` is a dead end, `Docker` has zero project references of its own, so `Core`'s
  `CredentialStore` was not transitively reachable before this).

**One real revision round, caught by direct testing, not code reading:** Codex's own sandbox
couldn't exercise the missing-credential path (no way to simulate an unauthenticated environment
without interactive sudo). Claude tested it independently by overriding `HOME` to an empty temp
directory (never touching the real `~/.forge/credentials.json`) and running the built native
binary directly — confirmed the message printed correctly, but the exit code was **0**,
inconsistent with this same file's sibling bad-args branch (which throws, yielding a non-zero
exit) and with `ForgeExec.RunAsync`'s exact precedent (`return 1`) for the identical message. Sent
back as a one-line fix (`Environment.Exit(1)`); re-verified directly after the fix landed.

**Verified independently by Claude:**
- `dotnet build src/ForgeMission.slnx --no-restore` — clean, 0 warnings, 0 errors.
- **Missing-credential path, executed directly** (not just read): built the native `dotnet`
  invocation with `HOME` overridden to an empty temp directory — prints `"Not signed in. Run
  `forge login`."` to stderr and exits with code **1**.
- **Credentialed path, executed directly**: with the real `~/.forge/credentials.json` present, the
  app proceeds past the check and launches the full Photino window flow (observed
  `Photino.NET: "Forge".Load(...)` in stdout) — confirmed no orphaned child processes remained
  afterward (`pgrep` found none).
- `dotnet test src/ForgeMission.slnx --no-build` (full solution) — **458/458 pass, 11 skipped, 0
  failed**.
- Diff read directly and checked against the approved (twice-revised) plan — matches exactly.

**Merged:** [PR #30](https://github.com/katasec/mission-control-language/pull/30) into `main`,
approved by the operator 2026-08-06.

## Task 8 — Mode selection + default

**Goal:** default `MissionRuntime:Mode` to the cloud endpoint (mission `"vanilla"`), with `docker`
staying available as an explicit local-dev override.

The spoke's framing ("DI picks CloudMissionRuntimeSession vs. MissionRuntimeSession based on mode")
didn't match the real code — traced during planning, not assumed: `MissionRuntimeSession` was never
actually DI-resolved (a dead typed-`HttpClient` registration existed alongside a direct `new
MissionRuntimeSession(...)` at the real construction site in `ClientRuntimeEndpoints.cs`'s
`/transport/prompt` handler), and no signal existed at all for telling the Client Runtime child
process which wire protocol its `BaseUrl` spoke. Both were resolved before implementation, per
AGENTS.md's design-first rule.

**✅ DONE (2026-08-06)** — implemented by Codex on `codex/phase-43.14-cloud-mode-default`
(`fefffca Default Forge Desktop's mission runtime to the cloud endpoint`), two revision rounds (the
first added the missing mode signal/session-selection precision; the second added test coverage the
first plan omitted entirely, including fixing an existing test that the new behavior would
otherwise break).

**What changed:**
- [`src/ForgeMission.Orchestration/MissionRuntimeResolver.cs`](../../src/ForgeMission.Orchestration/MissionRuntimeResolver.cs) —
  `ResolveMode` defaults absent `MissionRuntime:Mode` to `"cloud"`; a new pure
  `ResolveCloudBaseUrl(configuredUrl, apiEndpoint)` helper (deliberately pure — no direct
  `Environment.GetEnvironmentVariable` call inside it — so tests can exercise the fallback/override
  logic deterministically without mutating process-wide environment state) resolves `cloud`'s
  `BaseUrl`: configured `MissionRuntime:BaseUrl` first, then `FORGE_API_ENDPOINT`
  (trailing-slash-trimmed, matching `ForgeExec.cs`'s existing convention), then the
  `DefaultCloudEndpoint` constant (`https://api.forge.katasec.com`). `ResolveAsync` now returns
  `(BaseUrl, Mode, Launcher)` — `docker` unchanged; a genuinely unrecognized mode still requires an
  explicit `BaseUrl` or throws.
- [`src/ForgeMission.Desktop/Program.cs`](../../src/ForgeMission.Desktop/Program.cs) — threads the
  resolved mode through to a new `MissionRuntime__Mode` env var alongside the existing
  `BaseUrl`/`Credential`.
- [`src/ForgeMission.ClientRuntime/Program.cs`](../../src/ForgeMission.ClientRuntime/Program.cs) —
  removed the dead typed `AddHttpClient<MissionRuntimeSession>` registration.
- [`src/ForgeMission.ClientRuntime/Transport/ClientRuntimeEndpoints.cs`](../../src/ForgeMission.ClientRuntime/Transport/ClientRuntimeEndpoints.cs) —
  the `/transport/prompt` handler now reads `MissionRuntime:Mode` via injected `IConfiguration` and
  picks between `CloudMissionRuntimeSession`/`MissionRuntimeSession` directly at the construction
  site via a new `UsesCloudMissionRuntime(mode)` selector (`null`/case-insensitive `"cloud"` → true;
  `"docker"`/anything else → false). Deliberately no shared interface or factory — the two session
  types have identical public signatures by construction (Task 6), so the small duplication between
  the two branches is the direct, sanctioned cost of not building unneeded abstraction.
- [`src/ForgeMission.Tests/Orchestration/MissionRuntimeResolverTests.cs`](../../src/ForgeMission.Tests/Orchestration/MissionRuntimeResolverTests.cs) —
  the pre-existing `ResolveAsync_NonDockerModeWithoutConfiguredUrl_Throws` test (which asserted
  `"cloud"` mode with no `BaseUrl` throws) was replaced — that assertion directly contradicted this
  task's own new behavior. New coverage: no-mode-configured defaults to cloud; cloud without a
  `BaseUrl` uses the endpoint convention; a genuinely unrecognized mode (`"remote"`) without a
  `BaseUrl` still throws (preserving the old catch-all behavior test, now on the right mode value);
  plus direct unit coverage of the pure `ResolveCloudBaseUrl`/`ResolveMode` helpers.
- [`src/ForgeMission.Tests/ClientRuntime/ClientRuntimeEndpointsTests.cs`](../../src/ForgeMission.Tests/ClientRuntime/ClientRuntimeEndpointsTests.cs)
  (new) — narrow theory-based tests on `UsesCloudMissionRuntime` alone (not a full hosted-TestServer
  endpoint test — deliberately scoped down since `MissionRuntimeSession`/`CloudMissionRuntimeSession`
  are already independently tested elsewhere, so a full endpoint test would mostly re-test their
  internals via HTTP indirection without adding confidence in the actual new logic this task adds).

**Verified independently by Claude:**
- `dotnet build src/ForgeMission.slnx --no-restore` — clean, 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.Tests --filter "FullyQualifiedName~MissionRuntimeResolverTests|FullyQualifiedName~ClientRuntimeEndpointsTests"` —
  **13/13 pass**.
- `dotnet test src/ForgeMission.slnx --no-build` (full solution) — **469/469 pass, 11 skipped, 0
  failed**. No xAI 403s in this environment (skip cleanly, no `XAI_API_KEY` configured here) —
  confirms Codex's reported two live-Grok failures are environment-specific, not a regression.
- Confirmed both flagged `InternalsVisibleTo` requirements (`ForgeMission.Orchestration` →
  `ForgeMission.Tests` for the internal pure helpers; `ForgeMission.ClientRuntime` →
  `ForgeMission.Tests` for the internal `UsesCloudMissionRuntime`) already existed — no redundant
  plumbing was added.
- Diff read directly, file by file, and checked against the approved (twice-revised) plan — matches
  exactly, including the deliberately-accepted small duplication in the endpoint's two branches.

**Merged:** [PR #31](https://github.com/katasec/mission-control-language/pull/31) into `main`,
approved by the operator 2026-08-06.

## Task 9 — Tests: tier-2 planted-content round trip

**Goal:** the locked testing strategy's tier 2 — a planted-content mock-host round-trip test, same
rigor as `ForgeMission.Tests/Integration/MockClaudeHostTests.cs`, but driving the `ExecuteMission`
shape (API A) instead of the Anthropic `/v1/messages` wire. Tier 1 was already covered incrementally
across Tasks 1/2/5's own test additions to `MissionExecutionServiceTests.cs`
(`BillingServiceClientTokenTests.cs` confirmed pre-existing and unrelated — pure ledger-level
`SettleRunAsync`/`ClientToken` idempotency, not touched by the agent-turn feature).

**The real architectural question, resolved before implementation, per AGENTS.md's design-first
rule:** there is zero precedent anywhere in this repo for a real hosted `TestServer`/
`WebApplicationFactory` for either `ForgeMission.Api` or `ForgeMission.Runner` (confirmed by a
repo-wide search before writing the task assignment). `MissionExecutionServiceTests.cs`'s existing
`StubRunnerHandler` returns canned `RunStreamEvent`s — fine for testing ForgeAPI's own logic in
isolation, but using it here would mean `MissionRunHandler`/`ToolContinuationGate`/the enrichment
cache never actually run, which is exactly the false-green failure mode this test exists to
prevent (`MissionRunHandler` is the real execution engine, not a thin adapter — unlike
`MissionEndpoints`, which is why the existing precedent's "skip the host" reasoning doesn't
transfer). Resolved architecture: drive `MissionExecutionService` directly (in-process, no real
ForgeAPI host — consistent with existing precedent), backed by a **live** `HttpMessageHandler` that
deserializes the real `RunRequest` and calls a real, directly-constructed
`MissionRunHandler.RunStreamAsync(...)` (reusing Task 4's `MissionRunHandlerTests.cs` construction
pattern), serializing its real `RunStreamEvent`s back into NDJSON — genuinely exercising both tiers
in-process, no real sockets, no new hosting infrastructure invented.

**✅ DONE (2026-08-07)** — implemented by Codex on `codex/phase-43.14-mock-host-integration-test`
(`30e8765 Add tier-2 planted-content round-trip test for API A tool continuations`).

**What changed:**
- [`src/ForgeMission.Rooms.Tests/Api/MissionExecutionToolRoundTripTests.cs`](../../src/ForgeMission.Rooms.Tests/Api/MissionExecutionToolRoundTripTests.cs)
  (new) — a real 3-step mission (`Enrich → Respond(role:agent) → Verify`) built fresh on disk, a
  `ChainedToolRunner : IExpertRunner` that derives each step from actual conversation state (not
  hardcoded), a chained two-file plant (file A's content names file B's path; file B holds the
  magic word — the test can only pass if both hops genuinely execute), a `LiveRunnerHandler :
  HttpMessageHandler` that bridges to a real, directly-constructed `MissionRunHandler`, and a
  `MockExecuteMissionClient` that drives real `ExecuteMission` calls, executes real file reads for
  returned `ToolUse` calls, and replays `History` with the real `TurnMessage`/`TurnContent` DTOs.
  Asserts: exactly two real reads in the correct order; the magic word present only in the
  terminal answer; `Enrich` ran once, the agent ran three times, `Verify` ran once; **zero**
  settlement (verified via a live `Billing.GetBalanceMicroUsdAsync` check, not just response
  fields) on both tool-use turns; **exactly one** settlement on the terminal turn, with the real
  ledger balance matching the response's reported balance.
- [`src/ForgeMission.Rooms.Tests/ForgeMission.Rooms.Tests.csproj`](../../src/ForgeMission.Rooms.Tests/ForgeMission.Rooms.Tests.csproj) —
  new `ForgeMission.Runner`/`ForgeMission.Core` project references + a `Microsoft.Extensions.AI`
  package reference (needed for direct compile-time use of `ChatMessage`/`FunctionCallContent`/etc.
  in the new test file — a real, build-verified necessity, not a guess).
- [`src/ForgeMission.Runner/Properties/AssemblyInfo.cs`](../../src/ForgeMission.Runner/Properties/AssemblyInfo.cs) —
  new `[assembly: InternalsVisibleTo("ForgeMission.Rooms.Tests")]`, verified genuinely required
  (`RunnerRegistry`/`IRunnerArtifactStore` confirmed `internal` by reading the source before
  approving the plan).

**A subtle correctness point verified by hand-tracing the real code, not assumed:** the test
asserts `response.Usage.CostMicroUsd == 0` on tool-use turns — NOT that no usage was recorded. Real
token usage genuinely accumulates on every hop (`ChainedToolRunner.RunAsync` calls
`UsageAccumulator.Add(...)` on every expert step, including the two tool-use hops), but
`MissionExecutionService`'s tool-use branch (Task 5) hardcodes `costMicroUsd: 0` for non-terminal
responses specifically because settlement is skipped — so this assertion is testing the precisely
correct distinction (billed cost vs. recorded usage), not a coincidence that happens to pass.

**Verified independently by Claude:**
- `dotnet build src/ForgeMission.slnx --no-restore` — clean, 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.Rooms.Tests --filter "FullyQualifiedName~MissionExecutionToolRoundTripTests"` —
  **1/1 pass**.
- `dotnet test src/ForgeMission.slnx --no-build` (full solution) — **470/470 pass, 11 skipped, 0
  failed**.
- Full hand-trace of the actual `PipelineRunner`/`MissionRunHandler`/`MissionExecutionService`
  mechanics against every assertion in the test (not just a structural read) — confirmed each one
  tests something that could only be true if the real system genuinely worked: the enrich-once
  count depends on `ToolContinuationGate`'s real `StartAtAgent` cache-hit path; the ordered
  `ReadPaths` depend on the chain genuinely being followed; the cost/balance assertions depend on
  real Postgres state, independently queried, not just trusted from response fields.
