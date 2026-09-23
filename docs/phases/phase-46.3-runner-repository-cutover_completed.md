# Phase 46.3 — Runner repository cutover: completion record

## Result

On 2026-09-23, `forge-runner` became the sole source owner of the Runner host, Runner Contracts,
focused Runner tests, Runner image workflow, and eight baked fallback mission trees. The duplicate
monorepo paths were deleted after a byte-level inventory comparison. The intentional differences
are the Runner Contracts package metadata and the direct test dependency on Npgsql.

## Consumer cutover

`ForgeMission.Billing`, `ForgeUI`, and `ForgeMission.Api` now directly reference sibling
`forge-runner` Contracts. `ForgeMission.Rooms.Tests` directly references sibling Runner and
Contracts for its existing internal tool-round-trip test. The API no longer reaches the contract
only through Billing's transitive reference.

## Verification

| Observation | Result |
|---|---|
| Monorepo Runner source, tests, Dockerfile/workflow, and eight mission paths | All physically absent after the move. |
| Old local Runner project references | None remain. |
| Monorepo build | `dotnet build src/ForgeMission.slnx -c Release` passed with 0 warnings and 0 errors. |
| Monorepo tests | Conversation Host 179 passed; Conversation Worker 18 passed; Rooms 97 passed; ForgeMission 353 passed, with one Docker-dependent test skipped. |
| Runner build/tests | Release build passed with 0 warnings/errors; 13 Runner tests passed. |

## Deliberate temporary exception

The sibling source links require both repositories under `~/progs`; a standalone monorepo clone and
its Docker contexts cannot resolve them. This is a bounded transition, not the permanent package
boundary. Publish `Katasec.Forge.Runner.Contracts`, replace Billing/ForgeUI/API source links with
package references, and replace the Rooms internal-host test with black-box HTTP coverage before
requiring standalone MCL or its container builds.
