# Phase 49.6 — Forge MCL bootstrap completion record

> **Status:** Accepted on 2026-09-22. Active package publication and consumer-proof work remains
> in [Phase 49.5](phase-49.5-private-package-foundation.md).

## Accepted outcome

Private `katasec/forge-mcl` imported the bounded MCL/toolchain source set without changing any
current product path. The bootstrap PR was independently accepted and squash-merged to `main` as
[`e7fc180`](https://github.com/katasec/forge-mcl/commit/e7fc180).

| Evidence | Observation |
|---|---|
| Import provenance | `eng/import/verify.ps1` accepted all 186 records from mono commit `d2c0c121bbe4180b60ddd44fa5f18872e2402771`: 173 byte-preserved records and 13 declared bounded transforms. |
| Boundary proof | `eng/verify-boundaries.ps1` passed: every project reference is internal to `forge-mcl`; no excluded product source is present. |
| Package shape | Seven private package identities have exact internal `1.0.0` dependencies; Docker remains `0.1.0`; the CLI is non-packable. No package was published. |
| Clean verification | The isolated-cache locked restore, Release build and deterministic test suite passed; cross-platform lock sections passed. |
| CI/AOT | GitHub Actions run [35721064441](https://github.com/katasec/forge-mcl/actions/runs/35721064441) passed for the reviewed head, including provenance, boundaries, locked restore, build/test, pack validation, and Linux Native AOT publish. |

The existing shared local NuGet cache contains Homebrew fallback copies of ILLink/ILCompiler whose
hashes differ from the committed package locks and can produce `NU1403`. The clean isolated cache
and CI both passed; this is recorded as local-cache contamination, not a bootstrap-content defect.

## Rollback and next gate

The public monorepo remains the current product and rollback/coordination source at the program
anchor `checkpoint-pre-repo-split-2026-09-22` (`06e11af30b6eb17d2a3c32e1e5b883bbe42c0d51`). No
consumer, deployment, route, datastore, identity, credential, or package feed has changed. The
next card publishes immutable private packages from `forge-mcl` and proves a clean, explicit-grant
restore in private `forge-runner` before any source-edge cutover.
