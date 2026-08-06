# Plan — Completed / Resolved Archive

Hub-level items (open issues, "under discussion" entries) that are resolved and no longer need to
occupy space in [plan.md](plan.md)'s active tables. Same rule as phase spokes' `_completed.md`
siblings ([AGENTS.md](../AGENTS.md#spoke-shape--lookup-table-not-narrative-completed-work-moves-out)):
full narrative lives here, `plan.md` keeps a one-line pointer back.

## Resolved open issues

### 1. OaiServer — full OAI spec compliance + Responses API

`katasec/oaiserver` was built against an informal reading of the spec rather than the authoritative
OpenAPI definition; the openai Python SDK 2.x could not parse the streaming response. Root cause:
first SSE chunk emitted `role: null` instead of `"role": "assistant"` — the SDK aborted
deserialization.

**What was done:** (a) pinned the official OpenAI OpenAPI spec in
`oai-server-dotnet/spec/openai-openapi.yaml` with a `make update-spec` target; (b) wrote
spec-compliance tests using the official `OpenAI` NuGet SDK (v2.11.0) as the test client pointed at
an in-process server — Kiota was considered and rejected because `ResponsesClient` / all needed
types are already bundled in the `OpenAI` package and Kiota would add a large generated-file
maintenance burden with no benefit; (c) tests committed with 17 passing (non-streaming chat +
models already compliant) and 5 failing — `Streaming_FirstUpdate_HasRoleAssistant` (the streaming
bug) plus all 4 `/v1/responses` tests (endpoint not yet implemented). Fixed the streaming role
emission bug and implemented `POST /v1/responses` (OpenAI's 2025 Responses API — the forward-looking
surface; `chat/completions` is on a deprecation path). All 28 spec tests pass (non-streaming +
streaming for all three endpoints).

**Status:** Done — pending Cli bump (`katasec/oaiserver` package version bump in
`ForgeMission.Cli` still outstanding as of the last check).

### 2. Python integration example

`clients/python/client.py` now uses the official `openai` SDK targeting `/v1/responses`.
`pip install openai` + `forge serve` is all that's needed.

**Status:** Done.

### 3. Provider-key IaC drift (38.5 task 7) — dev

The dev ForgeUI app was wired for `@claude`/`@grok` by adding KV secrets `Anthropic-ApiKey` +
`Xai-ApiKey` and container env vars `ANTHROPIC_API_KEY` / `XAI_API_KEY` **manually via `az`
(2026-07-08)** — not in the `forge-infra` `dev/500-app` Bicep, so a `dev/500-app` redeploy would
revert them. Also the `forge-ui-image` workflow builds+pushes but does not roll the Container App
(rollout is a separate step).

**Fix applied:** forge-infra `6fd0fd3` wires both secrets + env vars into `dev/500-app` Bicep (on
the **runner**, where 39.1 execution lives) and applied live; vault name reconciled
`kv-forge-dev`→`kv-forgerooms-dev` (`5e859ec`). This was causing the `@claude` "No mission is bound"
regression; also needed a core structured-output fix + Anthropic account funding — see
[38.7 §9](phases/phase-38.7-hosting-deployment.md).

**Status:** Done 2026-07-09.

### 4. `ExecExpertRunnerTests` Windows hygiene (8 pre-existing failures)

Known since Phase 43.1, carried unresolved through 43.7 and 43.2 Tasks 1–3, confirmed each time as
zero-diff/pre-existing. Root cause confirmed 2026-07-26: the test helper hardcoded
`Command: "python3"`; on this Windows box both `python` and `python3` (and `py`) resolve to nothing
working — either a non-functional Windows App Execution Alias stub or, for `py`, no launcher
installed at all.

**Fix applied** (implemented by Codex, independently re-verified by Claude): `ExecExpertRunnerTests.cs`
now probes `python3`/`python`/`py -3` by actually **executing**
`import json,sys; print(json.dumps({'major': sys.version_info[0]}))` (not just checking PATH — a
`File.Exists`-only check would wrongly accept the non-functional stub, which is a real file), cached
in a `static readonly Lazy<>` so the probe runs once per test run, not once per test. All 8 tests
converted `[Fact]` → `[SkippableFact]` (verified: 8/8, zero remaining `[Fact]`), skipping with a
clear reason when no working interpreter is found rather than failing red. Reproduced independently:
focused filter → 8/8 skip cleanly (no working interpreter on this box); full suite → same
established zero-regression bar. Scope correctly stayed test-infra-only, no production
`ExecExpertRunner.cs` change.

**Status:** Done 2026-07-26.

### 5. WDAC intermittently blocking locally-built DLLs (this Windows dev box only)

First seen by Codex on `ForgeMission.Rooms.Tests.dll`; independently hit by Claude on
`ForgeMission.Desktop.dll`, breaking a Desktop test (`FileLoadException`, `0x800711C7`) that had
passed cleanly earlier the same session. Did not clear after `dotnet clean` + rebuild. Likely
triggered by directly launching the unsigned `ForgeMission.Desktop.exe` a few times that session
(AOT-publish verification).

**Root cause pinned down 2026-07-26** via `Get-WinEvent -LogName
Microsoft-Windows-CodeIntegrity/Operational` (events 3033/3077): a real **WDAC (Windows Defender
Application Control) Code Integrity policy**, not Smart App Control — `Add-MpPreference` exclusions
(Defender AV) do not affect it. Policy ID `{0283AC0F-FFF1-49AE-ADA1-8A933130CAD6}.cip` exists
locally, dated 01/04/2024 — machine confirmed **not** domain/Azure-AD/Enterprise-joined
(`dsregcmd /status`), so this is a default Windows-shipped baseline policy, not an org-managed one
nobody local can touch.

**Decision:** not a code defect, not caused by any change, intermittent — logged so a future agent
hitting a sudden unrelated `FileLoadException` on this machine doesn't mistake it for a regression
or waste time on a Defender-exclusion fix that won't work. Deliberately not pursuing a WDAC policy
edit — disproportionate to "one flaky test." No action item unless it starts actually blocking work.

**Status:** Environment-only, root-caused, closed — no fix planned unless it starts blocking real work.

## Resolved "under discussion" items

### Mission Composition

Missions usable as steps in other missions — explicit parameter binding, isolated child context,
failure propagation, arbitrary depth. `PipelineRunner` recursively dispatches when a step name
matches a `MissionDeclaration`. Reference example: `missions/sdlc-agent/` — Classifier routes to
`DesignMode` (loop+judge) or `TaskMode` sub-missions. 10 new tests.

**Status:** Done.

### Skills and Tools

Superseded 2026-07-25 — scoped and committed as
[Phase 43.1 — Tool-execution engine](phases/phase-43.1-tool-execution-engine.md).

**Status:** Done (scoped into 43.1).
