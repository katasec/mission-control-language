# Phase 76 — Revert failed implementation

**Status:** OCI rollback merged; MCL and Desktop exact rollback PRs await an operator override
of the validation gates below. Independent design, plan, and source reviews passed.
The failed implementation documentation is reverted by the documentation PR below.
Operator requested reverting the failed session's merges while preserving its branches.
No Phase 76 implementation is resumed.

## Scope and design

Use ordinary revert commits, not history rewriting, in four repositories. Revert MCL #74
before #73; revert Desktop #10, OCI client #3, and mission-control documentation #368.
Keep the operator's requirements from documentation #367. Make no replacement implementation,
workflow changes, editor repairs, package publication, or deployment.

| Repository | Merge to revert | Expected baseline |
|---|---|---|
| `/Users/ameerdeen/progs/forge-mcl` | #74 `3628cd2`, then #73 `c9a146a` | First parent of #73 |
| `/Users/ameerdeen/progs/forge-desktop` | #10 `40e2932` | First parent of #10 |
| `/Users/ameerdeen/progs/oci-client-dotnet` | #3 `bfc88d8` | First parent of #3 |
| `/Users/ameerdeen/progs/mission-control-language` | #368 `22d73b3` | First parent of #368, plus rollback evidence and paused status |

The four original merge branches had already been deleted remotely; they were restored to
their recorded original heads before rollback. Preserve them. The uncommitted admission amendment is
saved at `adeen/reference-phase-76-admission-amendment` (`80bd16c`). MCL #75 and its branch
remain paused and unchanged. Published OCI 0.4.0 remains immutable; reverting source does not
withdraw it. MCL's existing automatic CLI release may run on merge; do not dispatch releases.
After the documentation revert, update only the phase hub and plan status to identify the
rollback and keep future implementation paused; retain the original requirements unchanged.

## Review and verification

- Security/ownership: restore the previous local component boundaries; no new hosted entry,
  identity, data, or permission decision. Each repository owns its own revert.
- Simplicity: exact inverse changes only; no bug repairs or design improvements.
- UI: no visual or interaction changes; Desktop rollback restores credential composition.
- Run managed builds/tests first; run applicable Native AOT only after managed verification.
  Do not investigate imported editor tests or expand a failing baseline into repair work.
- Default-path gate: verify rollback source and normal package dependencies against the named
  baselines; record native/local execution observations separately. This rollback does not
  claim hosted Phase 76 acceptance or alter deployed services or installed applications.
- Done when: all five merge effects are reverted on reviewed `main`, exact baseline tree checks
  pass, required checks pass, references remain available, and actual validation is recorded.

## Implementation plan

1. Use new rollback branches from current `origin/main`; leave PR #75's branch unchanged.
2. Commit ordinary first-parent merge reversions in the order above. Report conflicts rather
   than inventing replacement changes. Verify exact baseline tree equality immediately.
3. Run existing managed checks: MCL warning-as-error Release build and existing managed suite
   filter; Desktop Release test suite; OCI Release non-integration suite. No new exclusions.
4. Once managed testing finishes, run one final native MCL publish/help/version verification
   and one Desktop supervisor AOT publish. Failed managed checks remain explicit merge blockers;
   native checks do not waive them. OCI is a library; retain existing AOT compatibility checks.
5. Obtain independent code reviews, then create and merge revert PRs after their checks pass.
   Supervisor records evidence and paused status; no reference branch is deleted.

## Evidence

Supervisor independently checked each exact baseline with `git diff --exit-code <baseline>
HEAD -- .` before documentation status edits. All four comparisons and whitespace checks passed.
The original requirements spoke remains byte-for-byte unchanged.

| Repository | Revert commits | Managed result | Final native result |
|---|---|---|---|
| MCL | `fbde8d2`, `e239e97` | Release build: zero warnings/errors; affected tests 28 passed. Key-free full suite: 972 passed, 12 failed, 8 skipped. | Binary produced; canonical publish script exit 1 on six raw linker warnings. Same binary help/version exit 0. |
| Desktop | `079850b` | 188 passed, 1 existing skip. | Binary produced, exit 0; five raw linker warnings. |
| OCI | `985144a` | Existing non-integration Release suite: 18 passed. | Library; existing AOT compatibility analysis. |
| Documentation | `2d08d3b` | Baseline and unchanged-requirements comparisons, relative links, balanced fences, and whitespace checks passed. | N/A |

The 12 MCL failures are `ForgeProjectTests` initialization failures: the `forge` assembly is
already loaded. They repeat in a fresh key-free process on the exact restored tree. The initial
provider-bearing suite also failed a missing-key assertion; that failure disappeared when
`MCL_API_KEY` and `XAI_API_KEY` were absent. No test or exclusion was changed.

Desktop's five linker warnings concern existing Homebrew OpenSSL/Brotli dylibs targeting newer
macOS versions than the executable's deployment target. MCL emitted those warnings plus
unsupported `-ld_classic`. No suppression or repair was added. Native MCL version was
`0.12.0-dev.2+e239e9740dc7c58b5768a3344eedb93f67fcee02`.
Logs are in `/tmp/phase76-rollback-validation/`; source restoration does not claim a passing
full MCL suite, zero-warning native acceptance, or installed/cloud acceptance.

Implementation began 2026-10-09T12:44:14+04:00. Exact earlier review boundaries were not captured;
do not infer them. OCI merged at 2026-10-09T12:57:37+04:00; merged source matches its baseline.
MCL/Desktop merge and post-merge verification remain pending explicit operator disposition.

| Revert PR | State |
|---|---|
| [OCI #4](https://github.com/katasec/oci-client-dotnet/pull/4) | Merged as `0f9d498`; source baseline verified on current `main`. |
| [Desktop #11](https://github.com/katasec/forge-desktop/pull/11) | Reviewed exact restoration; zero-warning native gate blocked. |
| [MCL #76](https://github.com/katasec/forge-mcl/pull/76) | Reviewed exact restoration; full managed and zero-warning native gates blocked. |
| [Documentation #369](https://github.com/katasec/mission-control-language/pull/369) | This documentation rollback; source review and documentation validation passed. |
