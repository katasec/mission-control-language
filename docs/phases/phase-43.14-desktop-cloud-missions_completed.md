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
