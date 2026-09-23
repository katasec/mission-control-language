# Phase 46.3 — Runner repository cutover: completion record

## Result

On 2026-09-23, `forge-runner` became the sole source owner of the Runner host, Runner Contracts,
focused Runner tests, Runner image workflow, and eight baked fallback mission trees. The duplicate
monorepo paths were deleted after a byte-level inventory comparison. The intentional differences
are the Runner Contracts package metadata and the direct test dependency on Npgsql.

## Consumer cutover

`ForgeMission.Billing`, `ForgeUI`, and `ForgeMission.Api` directly reference
`Katasec.Forge.Runner.Contracts` `0.1.0`. `ForgeMission.Rooms.Tests` directly references
`Katasec.Forge.Runner` and `Katasec.Forge.Runner.Contracts` `0.1.0` for its existing internal
tool-round-trip test. The API does not receive the contract only through Billing's transitive
reference.

## Verification

| Observation | Result |
|---|---|
| Monorepo Runner source, tests, Dockerfile/workflow, and eight mission paths | All physically absent after the move. |
| Old local Runner project references | None remain. |
| Monorepo build | `dotnet build src/ForgeMission.slnx -c Release` passed with 0 warnings and 0 errors. |
| Monorepo tests | Conversation Host 179 passed; Conversation Worker 18 passed; Rooms 97 passed; ForgeMission 353 passed, with one Docker-dependent test skipped. |
| Runner build/tests | Release build passed with 0 warnings/errors; 13 Runner tests passed. |
| Runner packages | GitHub Actions run [35866511629](https://github.com/katasec/forge-runner/actions/runs/35866511629) published private `Katasec.Forge.Runner` and `Katasec.Forge.Runner.Contracts` `0.1.0`; pack and publish both succeeded. |
| Package-only monorepo restore/build | `dotnet restore src/ForgeMission.slnx --configfile nuget.config --force-evaluate` and `dotnet build src/ForgeMission.slnx -c Release --no-restore -v:q` passed with 0 warnings and 0 errors. |

## Deliberate temporary exception

`ForgeMission.Rooms.Tests` needs Runner internals for its existing tool-round-trip test. This is a
temporary Type-2 test exception: `Katasec.Forge.Runner` grants
`InternalsVisibleTo("ForgeMission.Rooms.Tests")`. Replace that package dependency and friend-assembly
entry with black-box HTTP coverage in the later test-boundary task.
