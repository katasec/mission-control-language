# Phase 46.2 Task E — retired Desktop.Maui trial removal — completed

> **Completed 2026-09-21.** Finding F46.1-06. Parent:
> [Phase 46.2](phase-46.2-codex-supervised-remediation.md).

## Scope and outcome

`ForgeMission.Desktop.Maui` was an unconsumed Windows-only in-process MAUI trial. It owned native
window startup plus Application Host and runtime startup, duplicating the accepted Desktop process
topology. The cleanup deleted its seven tracked files and sole `ForgeMission.slnx` entry.
`ForgeMission.Desktop.Host` remains the sole supported MAUI native-window owner; the accepted
Supervisor -> inherited-pipe Desktop Host -> Application Host topology is unchanged.

No public entry point, UI, wire/persistence contract, bounded context, datastore, credential,
tier, Makefile, CI, installer, or published Desktop input changed. This was a Type-2 local
code/build-graph cleanup; its reversal is restoring the deleted project and solution entry from
version control.

## Failure containment

The only plausible regression was an undiscovered build/package consumer. The project graph and CI
would fail before producing a Desktop artifact; no released runtime could be partially changed.
A repository-wide audit found no live consumer after removal. Recovery, if a verified consumer had
appeared, would have been restoration from version control; none did.

## Verification

| Check | Observation |
|---|---|
| Structure | Trial directory and solution entry absent; no live source, Makefile, CI, installer, or package reference remains. Phase 48 retains the historical record. |
| macOS solution build | `dotnet build src/ForgeMission.slnx` passed with 0 warnings and 0 errors. This is the Rider regression check. |
| Full tests | `dotnet test src/ForgeMission.slnx --no-build --no-restore` passed: 1,032 passed, 5 expected integration tests skipped. Docker-backed ConversationHost tests passed after Docker Desktop started. |
| CLI Native AOT | `dotnet publish src/ForgeMission.Cli/ForgeMission.Cli.csproj -c Release -r osx-arm64 --self-contained` passed and produced `forge`. |
| Canonical Desktop build | [GitHub Actions run 35601851690](https://github.com/katasec/mission-control-language/actions/runs/35601851690) passed on commit `48d603a`: macOS ARM64 bundle in 3m11s and Windows ARM64 bundle in 7m01s. |

Default-path and visual acceptance are N/A: no supported package input or user-visible runtime
behavior changed. A local `make desktop` diagnostic did fail while linking the unchanged Native AOT
Application Host because Xcode 26.6 resolved an incompatible Command Line Tools macOS 27 SDK whose
linker rejected `arm64e.x1` metadata. The passing canonical GitHub Actions Desktop build supplies
the cross-platform package evidence; the local toolchain fault is not a project regression.
