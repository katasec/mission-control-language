# Phase 50.6 — forge-desktop

> **✅ Complete (2026-09-26).** The tasks below are the record of what was done.

> Detail for row 6 of [the repository map](phase-50-repository-extraction.md). Move only: one change
> at a time, no redesign, no new contracts, no new gates. If a task below doesn't say to change a
> line, don't. Build or test fails → stop and report the exact error.

| Item | State |
|---|---|
| All of `src/` (14 Desktop projects, `ForgeMission.slnx`, `Directory.Build.*`, `README.md`) | ✅ Moved (forge-desktop #1, MCL #200) |
| `Makefile`, AOT scripts, `.vscode/`, `nuget.config` | ✅ Moved (forge-desktop #1, MCL #200) |
| `desktop-build.yml` | ✅ Moved; run 36263803052 built both bundles |

Paths: `MCL` = `/Users/ameerdeen/progs/mission-control-language`,
`FD` = `/Users/ameerdeen/progs/forge-desktop` (GitHub `katasec/forge-desktop`).

Facts checked 2026-09-26:

- Everything tracked under `MCL/src/` is Desktop. No project references anything outside `src/`
  except `ForgeMission.Tests` → `..\..\..\..\progs\oai-server-dotnet\…\Katasec.AnthropicServer.csproj`.
  That path resolves identically from `FD`, so it moves unchanged.
- The `Makefile` is Desktop-only (`desktop-publish`, `build`, `test`, `clean`).
- `desktop-build.yml` restores private packages (`packages: read`); it uses no Azure.

## Task 1 — Preflight ✅ (2026-09-26)

`MCL` and `FD` on clean `main`, pushed. `FD` contains only `README.md`. Create branch
`codex/move-desktop` from `main` in both.

## Task 2 — Named copy ✅ (forge-desktop #1)

`MCL/.gitignore` → `FD/.gitignore` (copy; `MCL` keeps its own).

## Task 3 — Move ✅ (215/215 byte-identical)

Move all tracked files (`git ls-files`) from `MCL` to the same path in `FD`, then `git rm` them in
`MCL`. No edits.

- `src/` (everything tracked under it)
- `Makefile`
- `nuget.config`
- `scripts/Normalize-AotPeTimestamps.ps1`, `scripts/Verify-AotPeIdentity.ps1`
- `.vscode/launch.json`, `.vscode/settings.json`, `.vscode/tasks.json`

**Done when:** every moved file is byte-identical to `MCL` `main`, and `MCL` has no tracked file
under `src/`.

## Task 4 — Verify ✅ (build 0 errors; 353 pass / 1 skip; `make desktop-publish` ok)

- `FD`: `dotnet build src/ForgeMission.slnx` has 0 errors; `dotnet test src/ForgeMission.Tests` passes.
- `FD`: `make desktop-publish` succeeds (macOS).
- Pre-approved: a CS0122 visibility error in tests may be fixed only by an `InternalsVisibleTo` grant.

## Task 5 — Move the Desktop build workflow ✅ (run 36263803052; both artifacts uploaded)

1. Move `MCL/.github/workflows/desktop-build.yml` to `FD`, no edits.
2. Package access: list every `Katasec.*` package in `FD`'s restored `project.assets.json` files.
   Each private one needs `katasec/forge-desktop` added with Read under Manage Actions access. No API
   exists for this — the user relays it to Codex.
3. After the grants, run `desktop-build.yml` in `FD` (`gh workflow run`).

**Done when:** the workflow succeeds and uploads both artifacts (`forge-desktop-win-arm64`,
`forge-desktop-osx-arm64`).

## Task 6 — MCL docs ✅ (MCL #200)

- `MCL/AGENTS.md` repository map: move Desktop and Application from the
  `mission-control-language` line to a `forge-desktop` line.
- Nothing else. The rest of `AGENTS.md` (build commands, `src/README.md` pointer, AOT rules) is
  placed in row 7.

## Task 7 — Merge ✅ (merged 2026-09-26)

One PR per repo, `FD` first; merge; both on clean `main`.

## Notes

- Private packages granted Read for `forge-desktop`: ConversationPresentation, Conversations.Contracts,
  Mcl.Core, Mcl.Docker, Mcl.Parser, Mcl.Scout.
- `ForgeMission.Tests` still source-links `~/progs/oai-server-dotnet` (moved unchanged). The
  Desktop build workflow doesn't build tests, so CI is unaffected.
