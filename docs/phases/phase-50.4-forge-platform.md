# Phase 50.4 — forge-platform

> **✅ Complete (2026-09-26).** The tasks below are the record of what was done.

> Detail for row 4 of [the repository map](phase-50-repository-extraction.md). Move only: one change at a time,
> no redesign, no new contracts, no new gates. If a task below doesn't say to change a line, don't.

| Item | State |
|---|---|
| `ForgeMission.Billing` + tests | ✅ Moved; `Katasec.Forge.Billing` `0.1.1` published; consumers on `0.1.1` |
| `ForgeMission.Api` + tests | ✅ Moved (forge-platform #7, MCL #194) |
| `Dockerfile.forgeapi` + `forge-api-image.yml` | ✅ Moved (forge-platform #8, MCL #195); `forge-api` `0.3.2` pushed from forge-platform |

Paths: `MCL` = `/Users/ameerdeen/progs/mission-control-language`,
`FP` = `/Users/ameerdeen/progs/forge-platform`.

## Task 1 — Preflight ✅ (2026-09-26, forge-platform #6)

Both `MCL` and `FP` on clean `main`, pushed.

In `FP`, rename `NuGet.config` → `nuget.config` (`git mv NuGet.config tmp && git mv tmp nuget.config`
on macOS) and update its two references in `.github/workflows/publish-billing-package.yml`
(line 10 `paths`, line 52 `--configfile`). This matches `MCL` and `forge-runner`, so the API
Dockerfile moves without edits. Merge this as its own PR before Task 2.

**Done when:** `git status` shows clean and up to date in both, and `FP` CI passes with `nuget.config`.

## Task 2 — Move the API project ✅ (forge-platform #7, MCL #194)

Create branch `codex/move-api` from `main` in both `MCL` and `FP`.

Move `MCL/src/ForgeMission.Api/` (all tracked files) to `FP/src/ForgeMission.Api/`, then delete it
from `MCL`:

```
ApiJson.cs  ArtifactStore.cs  ForgeMission.Api.csproj  Messages.cs  MissionCatalog.cs
MissionEndpoints.cs  MissionExecutionService.cs  MissionToolTurnMapper.cs  PlatformKeyAuth.cs
Program.cs  README.md  RunStore.cs  WireProxy.cs  Properties/AssemblyInfo.cs
Properties/launchSettings.json
```

- No edits to any file. The csproj keeps `PackageReference Katasec.Forge.Billing 0.1.0`.
- Add `<Project Path="ForgeMission.Api/ForgeMission.Api.csproj" />` to `FP/src/ForgePlatform.slnx`.

**Done when:** `dotnet build FP/src/ForgePlatform.slnx` passes, and every moved file is
byte-identical to its original on `MCL` `main`.

## Task 3 — Create the API test project ✅ (forge-platform #7)

Create `FP/src/ForgeMission.Api.Tests/ForgeMission.Api.Tests.csproj`. It's a copy of
`FP/src/ForgeMission.Billing.Tests/ForgeMission.Billing.Tests.csproj` with these changes:

- `ProjectReference` → `..\ForgeMission.Api\ForgeMission.Api.csproj` (instead of Billing).
- Add `PackageReference`s, with versions copied from
  `MCL/src/ForgeMission.Rooms.Tests/ForgeMission.Rooms.Tests.csproj`:
  `Katasec.Forge.Billing`, `Katasec.Forge.Runner`, `Katasec.Forge.Mcl.Core`,
  `Microsoft.Extensions.AI`.

Copy (named exception to move-only) `FP/src/ForgeMission.Billing.Tests/BillingPostgresFixture.cs` into the new project. Change only
its namespace to `ForgeMission.Api.Tests`. This replaces the Rooms-owned `PostgresFixture` the API
tests used.

Add `<Project Path="ForgeMission.Api.Tests/ForgeMission.Api.Tests.csproj" />` to
`ForgePlatform.slnx`.

## Task 4 — Move the API tests ✅ (forge-platform #7)

Move these 7 files from `MCL/src/ForgeMission.Rooms.Tests/Api/` to `FP/src/ForgeMission.Api.Tests/`,
then delete them from `MCL`:

```
FileArtifactStoreTests.cs  MessagesSerializationTests.cs  MissionExecutionServiceTests.cs
MissionExecutionToolRoundTripTests.cs  MissionHandleTests.cs  PlatformKeyAuthFilterTests.cs
StaticMissionCatalogTests.cs
```

The only edits allowed (the same ones the Billing move made):

| File | Edit |
|---|---|
| All 7 | `namespace ForgeMission.Rooms.Tests;` → `namespace ForgeMission.Api.Tests;` |
| `MissionExecutionServiceTests.cs`, `MissionExecutionToolRoundTripTests.cs` | Delete `using ForgeMission.Rooms;` and `using ForgeMission.Rooms.Data;` |
| Same 2 | `PostgresFixture` → `BillingPostgresFixture` |
| Same 2 | Delete the `Writes` property and the `NewMemberAsync()` helper |
| Same 2 | `await NewMemberAsync()` → `Guid.NewGuid()`, then `member.Id` → `member` |

**Done when:** `dotnet test FP/src/ForgePlatform.slnx` passes — Billing's 28 tests plus the moved
API tests — and the diff of each moved test file against its original shows only the edits above.

## Task 5 — Update MCL consumers ✅ (MCL #194)

- Remove `<Project Path="ForgeMission.Api/ForgeMission.Api.csproj" />` from `MCL/src/ForgeMission.slnx`.
- Remove the `ForgeMission.Api` `ProjectReference` (and its comment) from
  `MCL/src/ForgeMission.Rooms.Tests/ForgeMission.Rooms.Tests.csproj`.
- Leave every other line of that csproj unchanged. The `Runner`, `Mcl.Core`, and
  `Microsoft.Extensions.AI` package references become unused; removing them is a separate
  later step.
- `MCL/src/README.md`: point the `ForgeMission.Api` row at `forge-platform`, like the Billing row.
- `MCL/AGENTS.md` repository map: move "Forge API" from the `mission-control-language` line to the
  `forge-platform` line.

**Done when:** `dotnet build MCL/src/ForgeMission.slnx` has 0 errors and `dotnet test` on
`ForgeMission.Rooms.Tests` passes.

## Task 6 — Merge ✅ (merged 2026-09-26)

One PR per repo, `FP` first. Merge once CI passes. Both repos back on clean `main`. Update row 4 of
`readme-updated.md`.

## Task 7 — Move the image build ✅ (forge-infra #13, #14; run 36249520588)

Moving `Dockerfile.forgeapi` and `.github/workflows/forge-api-image.yml` to `FP` is a plain move
(the `nuget.config` casing is fixed in Task 1), except for one change that isn't a file move:

**Azure push access.** The CI identity only trusts GitHub OIDC tokens from
`mission-control-language` (`forge-infra/dev/150-ci/main.bicepparam`, `appRepo`). From
`forge-platform`, the workflow can't push to ACR until `forge-infra` adds a federated credential
for it. `forge-runner`'s image workflow has the same gap today.

After Task 5, `MCL` has no API source, so the API image can't be built from either repo until this
task is done. The running Azure API is unaffected; only new image builds are blocked.

Order: move both files unchanged, then add the
`forge-platform` federated credential in `forge-infra` as its own PR.

## Deviations (all approved)

| Deviation | Why |
|---|---|
| `forge-runner`: `InternalsVisibleTo("ForgeMission.Api.Tests")`, Runner + Contracts `0.1.1` published and used by Api.Tests (forge-runner #5) | The round-trip test uses Runner internals; only the old `ForgeMission.Rooms.Tests` name had access. |
| `ForgeMission.Api/Properties/AssemblyInfo.cs`: `InternalsVisibleTo` now names `ForgeMission.Api.Tests` | Same reason, for API internals. |
| Billing `0.1.1` published by the simplified workflow; consumers bumped (MCL #192) | Proved the new publish workflow. |
| 11 private packages granted Read Actions access for `forge-platform` | CI restore returned 403 without it. |
| `forge-platform`: `forge-ui-image` environment + 5 repo variables copied from MCL | Needed by `forge-api-image.yml`. |
| OIDC subject uses GitHub's ID-qualified form `katasec@87564133/forge-platform@1381261019` (forge-infra #14) | GitHub presents that form for this repo; the plain form failed with AADSTS700213. |
