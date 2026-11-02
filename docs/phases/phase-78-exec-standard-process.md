# Phase 78 — `exec` steps on standard `Process`

> **Status: decided B (operator, 2026-11-02): use standard `Process`; delete `ExecProcess` and the probe.** Not started.

## Question

Should forge-mcl Core run `exec` steps with .NET's standard `Process` instead of the custom
`ExecProcess`, and delete the custom code and the Native AOT probe that exists only to prove it?

## Evidence (code, 2026-11-02)

| Fact | Where |
|---|---|
| Every `exec` step runs `PipelineRunner` → `ExecExpertRunner` → `ExecProcess.Start`. | forge-mcl `PipelineRunner.cs:714`, `ExecExpertRunner.cs:37` |
| `ExecProcess` is about 540 lines over six files. On macOS and Linux it calls `posix_spawn` through P/Invoke, puts the child in its own process group, kills the group with `kill(-pid, SIGKILL)`, and reaps with `waitpid`. On Windows it uses a kill-on-close job object. It owns anonymous pipes for stdin, stdout and stderr, and refuses to run as Linux PID 1. | `src/ForgeMission.Core/Adapters/ExecProcess*.cs`; commits `dc03b7b`, `1d96270` |
| `tests/ForgeMission.Exec.Probe` (about 560 lines) is published as Native AOT on every release and proves that lifecycle. Its other checks (arguments, working directory, command lookup, cancellation) are ordinary process behaviour. | `scripts/build.ps1` `Test-NativeExec`; `release.yml:110-113` |
| `exec` runs in the hosted runner: a long-lived Linux container with `tini` as PID 1. Shipped missions use it for short Python scripts (`missions/ocr`, `summarize`, `hallucination-guard`). | forge-runner `Dockerfile.runner`, `missions/` |
| A mission package may contain arbitrary code (Phase 76 decision 5). | [Phase 76](phase-76-unified-cloud-run.md#locked-decisions) |

## Options

| | A — keep `ExecProcess` | B — standard `Process` |
|---|---|---|
| Code | As today | `ProcessStartInfo` with redirected stdio; `Kill(entireProcessTree: true)` on timeout or cancel; the bounded reads in `ExecExpertRunner` stay. Delete `ExecProcess*` and the probe, `Test-NativeExec`, and the release-artifact lines. Roughly −1,100 lines. |
| Descendant still in the process tree | Killed (process group) | Killed (tree walk) |
| Descendant that left the tree but kept its process group (parent exited first) | Killed | **Survives** until the container restarts. If it holds stdout, the step's read waits until the step timeout. |
| Descendant that calls `setsid`, or any hostile code | Survives | Survives |
| Linux PID 1 | Refused | Not checked; `tini` in the runner image owns reaping |

Neither option contains hostile code; real isolation needs a per-run sandbox, which is out of scope.
B gives up only the middle case: a well-behaved descendant that outlives its parent.

## Decision

B (operator: this belongs to the base framework, not a 500-line custom assembly). The shipped `exec` steps are short scripts that do not leave descendants behind. The custom code
and its probe are about 1,100 lines and a release gate, maintained for one edge case that standard
`Process` turns into a step timeout instead of a leak-free kill.

## Done when (if B)

- `ExecProcess*` files, `tests/ForgeMission.Exec.Probe`, `Test-NativeExec` and its release-artifact
  lines are deleted; `ExecExpertRunner` uses standard `Process`.
- `ExecExpertRunnerTests` pass, including timeout and a child that spawns a grandchild; the full suite
  passes with 0 warnings; the CLI Native AOT publish has 0 warnings.
- Default path: after runner deploy, the hosted `ocr` mission runs through `forge run` from the
  installed CLI and returns its summary.
