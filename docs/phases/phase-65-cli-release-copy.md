# Phase 65 — Copy the CLI release workflow

Status: complete, 2026-10-04; copied workflow, native builds and published release verified.

## Scope and source

Copy the native CLI release behaviour from the historical
`125e8abe^:.github/workflows/release.yml` in mission-control-language into forge-mcl. Do not
delete, move or modify original workflow history or existing releases. User requested a remote
build/release, including Windows, and explicitly required copying rather than moving.

The original workflow was removed by `125e8abe` on 2026-09-22. GitHub rejected its attempted
dispatch on current main with HTTP 422 (no workflow_dispatch trigger). Latest published original
CLI release is v0.9.0. The first copied release is v0.9.1; future runs take an explicit version.

| Decision | Locked contract |
|---|---|
| Owner | forge-mcl owns native CLI builds, tags and release assets; copy to `.github/workflows/release.yml`, document in its README. |
| Targets | Preserve original osx-arm64 / macos-14, linux-x64 / ubuntu-latest, win-arm64 / windows-11-arm matrix and .NET 10. Windows output is ARM64. |
| Payload | Current AOT output includes native sidecars (TextMateSharp/ONNX). Each `forge-<rid>.zip` contains the complete isolated publish output, with a SHA-256 checksum file. Bare executable uploads are insufficient. macOS retains existing Homebrew OpenSSL/Brotli runtime prerequisites, documented as `brew install openssl@3 brotli`; no claim of independence from system libraries or dylib-rewrite redesign. |
| Source | Dispatch from the default branch only. Verify the exact dispatched SHA is merged into its fetched default branch; every checkout, tag and binary metadata uses that SHA. |
| Version | Required major.minor.patch input, passed as a quoted environment value; reject invalid input and existing tags/releases before mutation. Set publish Version and SourceRevisionId. No overwrite, force or asset clobber. |
| Release | Preserve draft boundary. All three native builds, extracted-payload smoke checks and uploads must pass; supervisor downloads/verifies the draft before publishing it with gh. No automatic publication of incomplete assets. |
| Permissions | GITHUB_TOKEN only; verify/native build jobs have contents/package read, prepare/upload jobs alone have contents write. Separate artifact upload job consumes native build run artifacts. No provider/datastore credentials, PAT fallback, new secret or package republish. |
| Exclusions | No Desktop jobs (different owner), Docker/GHCR release, new installer, product code, terminal redesign or unrelated CI changes. User manages their Windows terminal. |

## Gates and failures

Ownership/simplicity review derives native release ownership from forge-mcl; the existing matrix,
manual version and prepare/build structure are sufficient. Complete payload packaging and exact
source/quoted-input checks repair concrete copy hazards, without a new framework or knobs.

Security gate: hosted tiers, account data, datastores and cross-context contracts are N/A; this
task only builds existing source and publishes GitHub release assets. Repository token scopes
follow their jobs; platform/provider credentials are never shipped or used by CI. Source and
release identity are fixed before implementation. No architecture exception is introduced.

Engineering gate: build/version/source failures are explicit. Input/source/test/verification-restore
failures stop before tag/draft creation. Native build or upload failure leaves an unpublished draft;
operator owns safe failed-job rerun or a new version. Prepare can leave a tag if draft creation fails
after its push; report that state, never force/delete it. Release API errors other than not-found
stop before mutation. Duplicate version stops without replacing state. A ZIP
contains native dependencies structurally; no warning tells users to reconstruct missing files.
Missing macOS Homebrew libraries remain an explicit native-loader error, recovered by installing
the documented existing prerequisites; this copy does not introduce or hide that baseline.

UI gate is N/A: no user interface changes. Existing product defaults and Phase 64 behaviour remain.

## Verification and default path

| Layer | Required observation |
|---|---|
| Premerge | Independent final workflow/README review, workflow lint and syntax checks; invalid version/source/duplicate refusal checked proportionately. No mirrored YAML test framework. |
| Release checks | Linux controlled Debug/Release builds and full Release suite before mutation; unset optional provider keys and NO_COLOR, build Debug CLI required by existing reflection tests. |
| Native targets | Fresh private restore, Native AOT publish on each actual matrix host; extract each ZIP and run --help, --version, and missing-current-folder chat (exact error / exit 1). |
| Release identity | Tag resolves to dispatched merged-main SHA; ZIP and checksum for all three RIDs; draft remains unpublished until complete. |
| Published-artifact default path | Supervisor recovers/checksums the GitHub-built macOS ZIP and runs it with normal forge login/default ForgeAPI and no endpoint override. Existing declaration-only safe fixture replays hosted history. Reuse Phase 64's real-turn evidence for unchanged product code; do not add a paid turn or repeat full tests to upload-only recovery. |
| Windows | Actual Windows ARM64 runner proves native build and extracted executable smoke. Interactive laptop/terminal testing remains the operator's action, not claimed by CI. |

New supported distribution path: authenticated GitHub Release download from katasec/forge-mcl,
extract the complete platform ZIP, run its forge executable. Existing make install remains supported.
Package versions remain Client 0.9.1 / Client.Contracts 0.2.0 from Phase 64; no dependency swaps.

## Work

| Task | State / Done when |
|---|---|
| Design / plan | Approved and reviewed; [evidence](phase-65-cli-release-copy_completed.md#copied-workflow). |
| Copy / review / merge | Complete: [PR, final personas and static evidence](phase-65-cli-release-copy_completed.md#copied-workflow). |
| Run / publish / accept | Complete: [successful native builds, upload-only recovery and published v0.9.1](phase-65-cli-release-copy_completed.md#published-release). Original run remains failed; recovery used its artifacts without rebuild. |

Done when the copied workflow is merged and the native CLI release is published with all three
complete platform payloads, verified against the conditions above, with original history untouched.

Upload recovery complete: [two-line guard fix and artifact evidence](phase-65-cli-release-copy_completed.md#upload-only-recovery).
The [simplicity control failure](phase-65-cli-release-copy_completed.md#simplicity-control-failure)
is recorded honestly; no broader pipeline rewrite was authorized during recovery.
