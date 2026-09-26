# Phase 50.5 — forge-rooms

> Detail for row 5 of [the repository map](phase-50-repository-extraction.md). Move only: one change
> at a time, no redesign, no new contracts, no new gates. If a task below doesn't say to change a
> line, don't. Build or test fails → stop and report the exact error.

| Item | State |
|---|---|
| `ForgeMission.Rooms`, `ForgeMission.Rooms.Data`, `ForgeUI`, `ForgeMission.Rooms.Tests` | ⬜ Tasks 1–7 |
| ForgeUI dev tooling (compose, dev scripts) | ⬜ Task 5 |
| `Dockerfile.forgeui` + `forge-ui-image.yml` | ⬜ Task 8 |

Paths: `MCL` = `/Users/ameerdeen/progs/mission-control-language`,
`FR` = `/Users/ameerdeen/progs/forge-rooms` (GitHub `katasec/forge-rooms`, repo id `1381261126`).

Decisions (approved 2026-09-26): **D1** Desktop gets its own copy of `forge.css`. **D2** the ForgeUI
image drops its dead `missions/` copy. **D3** the image build moves in this phase.

## Task 1 — Preflight

`MCL`, `FR`, `forge-infra` on clean `main`, pushed. `FR` contains only `README.md`. Create branch
`codex/move-rooms` from `main` in `MCL` and `FR`.

## Task 2 — Scaffold forge-rooms

Named copies (not moves — `MCL` keeps its own):

- `MCL/nuget.config` → `FR/nuget.config`
- `MCL/.gitignore` → `FR/.gitignore`
- `MCL/src/Directory.Build.props` → `FR/src/Directory.Build.props`

Create `FR/src/ForgeRooms.slnx` listing `ForgeMission.Rooms`, `ForgeMission.Rooms.Data`, `ForgeUI`,
`ForgeMission.Rooms.Tests` (same format as `MCL/src/ForgeMission.slnx`).

## Task 3 — Move the four projects

Move all tracked files (`git ls-files`) of these folders from `MCL/src/` to `FR/src/`, then
`git rm` them in `MCL`: `ForgeMission.Rooms/`, `ForgeMission.Rooms.Data/`, `ForgeUI/`,
`ForgeMission.Rooms.Tests/`. No edits.

**Done when:** every moved file is byte-identical to `MCL` `main`.

## Task 4 — Desktop keeps `forge.css` (D1)

In `MCL`:

- Copy `MCL` `main`'s `src/ForgeUI/wwwroot/css/forge.css` to
  `src/ForgeMission.Presentation/wwwroot/css/forge.css` (named copy).
- `src/ForgeMission.Presentation/ForgeMission.Presentation.csproj`: delete the
  `<Content Include="..\ForgeUI\wwwroot\css\forge.css" … />` element (4 lines).
- `src/ForgeMission.Tests/Architecture/ForgeCssThemeScopingTests.cs` line 247: path
  `"src", "ForgeUI", "wwwroot", "css", "forge.css"` → `"src", "ForgeMission.Presentation", "wwwroot", "css", "forge.css"`.
  No other edit (`ForgeUiHost_SelectsNoSurfaceTheme` already skips when the host file is absent).
- `src/ForgeMission.Tests/Architecture/MclPackageConsumptionTests.cs`: delete the line
  `("ForgeUI", new[] { presentation }),`.

## Task 5 — Move ForgeUI dev tooling

Move from `MCL` to the same path in `FR`, no edits: `docker-compose.yml`, `.dockerignore`,
`scripts/dev-up.sh`, `scripts/dev-down.sh`, `scripts/dev-reset.sh`, `scripts/db/init/01-init.sql`,
`scripts/gen-pwa-icons.py`.

In `MCL`:

- `Makefile`: delete the `dev-up`, `dev-down`, `dev-reset` targets (6 lines) and remove those three
  names from the `.PHONY` line.
- `.claude/launch.json`: move the `forge-ui` configuration object to a new `FR/.claude/launch.json`
  (same `version` wrapper). `MCL` keeps `mcl-site`.

## Task 6 — Update MCL consumers and docs

- `MCL/src/ForgeMission.slnx`: remove the four moved projects.
- `MCL/src/README.md`: point the four rows at `forge-rooms`, like the Billing/Api rows.
- `MCL/AGENTS.md` repository map: move Rooms and ForgeUI from the `mission-control-language` line
  to a `forge-rooms` line.

## Task 7 — Verify and merge

- `FR`: `dotnet build src/ForgeRooms.slnx` and `dotnet test src/ForgeRooms.slnx` pass.
- `MCL`: `dotnet build src/ForgeMission.slnx` has 0 errors and `dotnet test src/ForgeMission.Tests` passes.
- Pre-approved: a CS0122 visibility error in tests may be fixed only by an `InternalsVisibleTo` grant.
- One PR per repo, `FR` first; merge; both on clean `main`.

## Task 8 — Move the image build (D3)

1. Move `MCL/Dockerfile.forgeui` and `MCL/.github/workflows/forge-ui-image.yml` to `FR`. In the
   Dockerfile, delete the 4-line `# Mission files MUST ship…` comment, the `COPY missions/ /app/missions/`
   line, and the `MissionDir=/app/missions \` line (D2). No other edits. ForgeUI has no reference to
   `MissionDir` or `MCL_API_KEY`.
2. Verify locally: `docker buildx build --secret id=nuget_token,env=NUGET_AUTH_TOKEN -f Dockerfile.forgeui .` in `FR`.
3. GitHub setup in `katasec/forge-rooms`: create environment `forge-ui-image` (no protection rules)
   and copy the 5 repo variables from `katasec/mission-control-language`
   (`AZURE_CI_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `ACR_NAME`, `ACR_LOGIN_SERVER`).
4. Package access: list every `Katasec.*` package in `FR`'s restored `project.assets.json` files.
   Each private one needs `katasec/forge-rooms` added with Read under Manage Actions access. No API
   exists for this — the user relays it to Codex.
5. `forge-infra/dev/150-ci`: add credential `gh-rooms-image`, same shape as `gh-platform-image`,
   subject `repo:katasec@87564133/forge-rooms@1381261126:environment:forge-ui-image`. Serialize with
   `dependsOn`. PR, merge, `make 150-ci-what-if` (only that create expected), `make 150-ci`.
6. Run `forge-ui-image.yml` in `FR` with the next unused `forge-ui` version. The live ForgeUI
   container app stays on its pinned tag.

**Done when:** the image workflow succeeds from `FR` and the new tag is in ACR.
