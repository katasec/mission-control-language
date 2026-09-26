# forge-platform — row 4

> Detail for row 4 of [the repository map](repository-extraction.md). Move only: one change at a time,
> no redesign, no new contracts, no new gates. If a task below doesn't say to change a line, don't.

| Item | State |
|---|---|
| `ForgeMission.Billing` + tests | ✅ Moved; `Katasec.Forge.Billing` `0.1.0` published |
| `ForgeMission.Api` + tests | ⬜ Tasks 1–6 below |
| `Dockerfile.forgeapi` + `forge-api-image.yml` | ⬜ Task 7 |

Paths: `MCL` = `/Users/ameerdeen/progs/mission-control-language`,
`FP` = `/Users/ameerdeen/progs/forge-platform`.

## Task 1 — Preflight

Both `MCL` and `FP` on clean `main`, pushed. Create branch `codex/move-api` in each.

In `FP`, rename `NuGet.config` → `nuget.config` (`git mv NuGet.config tmp && git mv tmp nuget.config`
on macOS) and update its two references in `.github/workflows/publish-billing-package.yml`
(line 10 `paths`, line 52 `--configfile`). This matches `MCL` and `forge-runner`, so the API
Dockerfile moves without edits. Merge this as its own PR before Task 2.

**Done when:** `git status` shows clean and up to date in both, and `FP` CI passes with `nuget.config`.

## Task 2 — Move the API project

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

## Task 3 — Create the API test project

Create `FP/src/ForgeMission.Api.Tests/ForgeMission.Api.Tests.csproj`. It's a copy of
`FP/src/ForgeMission.Billing.Tests/ForgeMission.Billing.Tests.csproj` with these changes:

- `ProjectReference` → `..\ForgeMission.Api\ForgeMission.Api.csproj` (instead of Billing).
- Add `PackageReference`s, with versions copied from
  `MCL/src/ForgeMission.Rooms.Tests/ForgeMission.Rooms.Tests.csproj`:
  `Katasec.Forge.Billing`, `Katasec.Forge.Runner`, `Katasec.Forge.Mcl.Core`,
  `Microsoft.Extensions.AI`.

Copy `FP/src/ForgeMission.Billing.Tests/BillingPostgresFixture.cs` into the new project. Change only
its namespace to `ForgeMission.Api.Tests`. This replaces the Rooms-owned `PostgresFixture` the API
tests used.

Add `<Project Path="ForgeMission.Api.Tests/ForgeMission.Api.Tests.csproj" />` to
`ForgePlatform.slnx`.

## Task 4 — Move the API tests

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

## Task 5 — Update MCL consumers

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

## Task 6 — Merge

One PR per repo, `FP` first. Merge once CI passes. Both repos back on clean `main`. Update row 4 of
`readme-updated.md`.

## Task 7 — Move the image build

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
