# Phase 46.2 Task E — retired Desktop.Maui trial removal

> **Status:** design approved; implementation in progress. Finding: F46.1-06. Parent:
> [Phase 46.2](phase-46.2-codex-supervised-remediation.md).

## Scope card

`ForgeMission.Desktop.Maui` is an unconsumed Windows-only in-process MAUI trial. It owns native
window startup and starts the Application Host plus runtime dependencies itself, duplicating the
accepted Desktop process topology. Removing it advances [Desktop Host](../../src/ForgeMission.Desktop.Host/README.md)'s
purpose: it is the sole disposable MAUI native-window process, while `ForgeMission.Desktop` owns
supervision and lifecycle.

The end state deletes only `src/ForgeMission.Desktop.Maui/` and its `ForgeMission.slnx` entry. The
accepted Supervisor -> inherited-pipe Desktop Host -> Application Host topology stays unchanged.

## Boundaries and non-goals

| Area | Decision |
|---|---|
| Source / build boundary | Delete the seven tracked trial files and its sole live solution entry. Do not add a replacement project, compatibility shim, build property, or Rider-specific workaround. |
| Published Desktop graph | Unchanged: Makefile and Desktop-build CI publish Application Host, Supervisor, and `ForgeMission.Desktop.Host` only. |
| Contracts, data, identity, tiers | No public entry point, wire/persistence contract, bounded context, datastore, credential, or tier changes. |
| UI | No supported window, WebView, copy, interaction, or platform target changes; Desktop visual and presentation-parity gates are N/A. |
| Historical record | Retain Phase 48 evidence, changing only its stale present-tense description to record that the trial was removed. |

This is a Type-2 local code/build-graph cleanup. Reversal is restoring the deleted project and its
solution entry from version control; no data migration or release rollback is needed.

## Failure boundary

| Fact | Decision |
|---|---|
| Expected failure | An undiscovered live consumer could fail to resolve the deleted project. |
| Containment owner | The solution/project graph and CI/package commands fail before a supported Desktop artifact is produced. |
| Caller-visible result | A build error names the missing project; no released Desktop runtime changes. |
| Recovery | Restore the project from version control only if a verified consumer exists; otherwise remove the stale consumer. |
| Negative proof | Repository-wide reference audit after deletion finds no live `ForgeMission.Desktop.Maui` reference; macOS solution build succeeds without Windows targeting. |

## Verification and Done when

1. The trial directory and solution entry are absent; source, Makefile, CI, installer, and package
   reference audits find no live consumer.
2. The Phase 48 completion record accurately preserves the trial as historical, removed work.
3. `dotnet build src/ForgeMission.slnx`, `dotnet test src/ForgeMission.slnx`, and a Release Native
   AOT publish of `ForgeMission.Cli` pass.
4. The candidate commit's canonical GitHub Actions **Desktop build** produces the Windows ARM64 and
   macOS ARM64 bundles. Its Mac job is the package regression check; a local `make desktop` is a
   diagnostic only.

Default-path acceptance is N/A: this deletion does not alter the supported package inputs or a
published runtime path. The canonical GitHub Actions Desktop artifact remains the acceptance
source for a future change to that graph; local package output here is a build-graph regression
check, not a replacement for published-artifact acceptance.

## Local diagnostic

`make desktop` was attempted on 2026-09-21 after the solution build, test suite, and CLI Native AOT
publish passed. It stopped while linking the unchanged Native AOT Application Host because the
machine's selected Xcode 26.6 toolchain resolved the incompatible Command Line Tools macOS 27 SDK;
its linker rejected `arm64e.x1` metadata. This is a local Xcode/SDK selection fault, not a project
or trial-project dependency failure. The canonical GitHub Actions Desktop build is the required
cross-platform package check.
