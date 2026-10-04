# Phase 68 — Verification record

The [locked design](phase-68-cli-project-creation.md) owns the product and failure contracts.

## Shared client baseline

[forge-client PR 12](https://github.com/katasec/forge-client/pull/12) merged as `fbbfcb8` after
independent Ownership/Simplicity review and green CI. Supervisor independently observed Release
build with zero warnings/errors, 32/32 focused cases and 283/283 full tests, zero skipped.
Both Client 0.9.2 and Client.Contracts 0.2.1 package-content verifiers passed.

Both packages published from merged `fbbfcb8be38cbb7a682f7eb7a2c741ee1dffbf98`:
[Contracts run](https://github.com/katasec/forge-client/actions/runs/37239657662) and
[Client run](https://github.com/katasec/forge-client/actions/runs/37239659864) passed build/tests,
provenance packing, immutable-version guard and NuGet push. Their final ownership/visibility
assertion failed with the existing [backlog defect](../backlog.md). Independent authenticated
Packages reads confirmed private visibility, repository `katasec/forge-client`, and versions
Contracts 0.2.1 (`1335780469`) / Client 0.9.2 (`1335780611`). Neither workflow is described as
wholly green; publication was independently verified without republishing.

Controlled Native AOT consumer create/retry and generated DTO JSON passed. Its temporary project
initially omitted the established CLI's macOS linker/AOT settings; copying that existing baseline
fixed the check. Six existing macOS linker warnings remained; no new product suppression was added.
This mocked-adapter check is not installed default-path acceptance.

The implementer authored the changes and test scripts; the supervisor executed their reviewed
scripts to surface sibling-repository sandbox approvals after two child approval attempts timed
out. Independent source and evidence review remained separate from implementation.

## API shape check

The existing public creation command is message-based: `POST /api/CreateMissionConversation`
(`forge-platform/src/ForgeMission.Api/Conversations/ConversationEndpoints.cs`). The edge sends it
through the command bus; Host dispatches it in `ConversationCommandMessages.cs`. The Host removed
its old HTTP creation routes; remaining resource-style routes are internal reads. Contract comments
which name old creation URLs are stale documentation, not the active public wire contract.
No server `CreateProject` command exists: local Projects owns the declaration/identity, and the
hosted command receives that Project ID.

## CLI source verification

The CLI adds one command file and one registration, consumes Client 0.9.2 / Contracts 0.2.1,
and documents the new flow. It constructs no manifest, package or wire request. Independent
Ownership/Simplicity source review passed; PowerShell literal-folder guidance was corrected
and tested for dollar signs, backticks and apostrophes.

Supervisor observed Debug and Release builds with zero warnings/errors, 11 initial focused
passes, and the final full suite including the added quoting case: 756 passed, 6 existing opt-in
live cases skipped, zero failed. The isolated full-test command clears `MCL_API_KEY` and
`NO_COLOR`; initial inherited values exposed the existing missing-key and editor-color test
assumptions. No runtime/editor implementation was changed to satisfy those test prerequisites.

The restored NuGet package `.nuspec` repository commits both equal
`fbbfcb8be38cbb7a682f7eb7a2c741ee1dffbf98`, proving consumption of the published merged baseline.

[forge-mcl PR 60](https://github.com/katasec/forge-mcl/pull/60) carries source commit `6c1cb39`:
seven files, 256 additions and two deletions, including tests and documentation. The only new
production command is 84 lines plus one registration. Branch Native AOT publish and
`forge project create --help` exited zero. Its six macOS linker warnings match the established
baseline; this branch artifact is supporting evidence, not the installed acceptance artifact.
PR 60 merged as `4d232d66bf3aef83e5d249b178b17fe95b0f67b5` after clean merge readiness
and no pending/failing checks. `make install` passed from that merged main.

## Installed default-path acceptance

Supervisor ran the installed native CLI on 2026-10-05, using the ordinary saved platform
login and no `FORGE_API_ENDPOINT`. Every operation exited zero except the expected existing
Project refusal, which exited one and preserved both the declaration and `keep.txt`.

| Fact | Observation |
|---|---|
| Artifact | `make install` on merged forge-mcl main `4d232d66bf3aef83e5d249b178b17fe95b0f67b5`; `/Users/ameerdeen/.local/bin/forge`, SHA-256 `575e18d960136ea788d45f7c8d737b11384d57c83acff95ecc7e770b6a5ae1c1`. Native publish exited zero with the six established macOS linker warnings. |
| Defaults | Saved `forge login` platform key; absent endpoint override; normal `https://api.forge.katasec.com`. No injected adapter or endpoint. |
| Dependency provenance | Read-only `az containerapp show` observed API `forge-api:0.7.0` / revision `ca-forge-api-dev--0000013`, Host `forge-conversation-host:0.9.0` / `ca-forge-conversation-host-dev--0000014`, Runner `forge-runner:0.20.4` / `ca-forge-runner-dev--0000041`, all in `rg-forge-dev`. No deployment changed. |
| Starting state | Dedicated existing empty `/private/tmp/forge-phase68-38c9bdf2b03e47e3b4e5a326d130d281`; clone `/private/tmp/forge-phase68-38c9bdf2b03e47e3b4e5a326d130d281-clone`. No unrelated Project was changed. |
| Create | `forge project create` produced only `forge.project.json`, Project `2e71dad2-feba-4232-9b87-a59025709b14`, declaration `Chat@1` / empty folders, and one hosted NoHands Chat v1 conversation `db35a3df-85ee-5863-9b25-362d52dc5d76`. |
| Retry | Explicit create again preserved identical file bytes, Project/conversation IDs and one hosted pin. |
| Real turn | Normal `forge chat` completed; assistant emitted exact standalone marker `phase68-ready-a4691329a16a4c1bb08f14f1429a2ec7`. Authenticated snapshot observed `Completed`, sequence 6. |
| Portable clone | Copying only the declaration and opening ordinary `forge chat` replayed the same assistant marker; same conversation and unchanged sequence 6, no new turn. |
| Existing declaration | Installed create refused an unrelated `Other@1` declaration with exit one; declaration bytes and unrelated file unchanged. |
| Controlled boundaries | Missing-login cases proved zero owner calls/mutation; client tests covered collision, linked paths/private state, lost reply, refusal, mismatched launch and concurrent edit. These are supporting tests, not default-path substitutions. |

Local raw process/API evidence is `/private/tmp/phase68-default-evidence.json` and
`/private/tmp/phase68-existing-refusal.json`; all durable observations needed to repeat acceptance
are captured above. No credential value is recorded.

The first acceptance harness read `GetConversationResponse` as a bare snapshot; its status check
failed after successful create/retry/live reply. Correcting the harness to read `.snapshot` and
rerunning the complete installed path passed. Product source was unchanged by that correction.
