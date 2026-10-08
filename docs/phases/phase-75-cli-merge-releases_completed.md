# Phase 75 — Verified observations

Accepted 2026-10-08. Both automatic releases are published; the patch release passed on its
first attempt, and clean-main installation plus the downloaded published macOS archive passed
the distribution default path. Locked policy remains in the [design](phase-75-cli-merge-releases.md).

## Implementation checks

| Observation | Evidence |
|---|---|
| Initial/final managed build | `make build` exit 0; zero warnings/errors. Final source `124db512b6297f46d4f46be18b3356752f6a3eb2`, 10.21 seconds, log `/tmp/forge-cli-shared-managed-124db51.log`. Earlier logs `/tmp/forge-cli-shared-managed.log` and `/tmp/forge-cli-shared-managed-final.log`. |
| Focused boundaries | `make cli-script-test`: 29 checks PASS. Real disposable Git histories/files/hashes; GitHub transport is explicitly fake. Covers development/tag/dirty identities, shallow/missing history, minor/patch/major policy, reservation reuse/conflict, exact merge source, partial draft recovery, completed rerun, checksum/missing assets, old-source latest protection, managed build/test identity and compiler failure propagation. |
| ZIP portability | A real PowerShell/.NET ZIP probe showed Unix `100755` on the archived executable; no chmod workaround needed. |
| Actual PR macOS archive | Downloaded the full successful PR ZIP, independently verified SHA256 `1608e48b1e73e5b0c16a6e6218e3b3bfa4ce66d8501d6ceba49b74f25227335d`, extracted without a chmod repair and ran the native binary on the normal laptop dependencies. Identity matched the PR test-merge SHA. Supporting evidence; final published-archive acceptance is recorded below. |
| Independent simplicity/style review | `simplicity_review` PASS after correcting managed-test identity propagation and flattening release lookup. Hand McCabe: dispatcher 8, reservation <=10, no function >15. Reviewer independently reran all 29 checks and inspected the final install-only exception. Canonical native zero-warning checks subsequently passed on all four hosts below. |
| Independent ownership review | `ownership_review` PASS on actual product diff after searching the nine mapped repositories, including the final install-only exception; no source/package/workflow ownership moves. |
| Relevant laptop overrides | RID, CLI_OUTPUT and RELEASE_TAG absent during the initial local build checks. No injected version or runtime setting. |
| Initial laptop native check | `make cli-package` compiled but failed the new raw-output scanner on six already-known Apple linker diagnostics. `/tmp/forge-cli-shared-native.log`; the install-only baseline repair is documented in the active design. Final normal installation passed below. |

## Installed default artifact

`make install` passed from clean forge-mcl `main` at patch merge `8d28dc1ff8f127facfd708b1c89369179e98cf18`,
with RID, CLI_OUTPUT and RELEASE_TAG absent. Normal .NET 10.0.401 and existing Homebrew
OpenSSL/Brotli prerequisites; no custom SDK, library swap or injected version. Full payload copied
to `/Users/ameerdeen/.local/bin`; default `Get-Command forge` resolves that installed binary.

| Observation | Result |
|---|---|
| Installed help | Exit 0; `/tmp/forge-cli-main-patch-installed-help.txt`. |
| Installed/default command version | `0.10.1-dev.0+8d28dc1ff8f127facfd708b1c89369179e98cf18`; exact tag remains a development build locally. |
| Binary SHA256 | `18a18adde627dd49ff37aae6d6a7d1a9910b82c0a90e635eb933e47a15a24340`. |
| Native compile/sign/install | Exit 0; `/tmp/forge-cli-main-patch-install.log`. The same six known linker warnings remain displayed/logged under the reviewed install-only exception; this is not a local zero-warning claim. |
| Source state | `HEAD` and `v0.10.1` resolve the same merge SHA; `git status --short` empty after installation. |

The initial clean-main installation at `cae994cd71fc6e4b640a0f7a0f00a271c7bffc06` also passed
with `0.10.0-dev.0+cae994cd71fc6e4b640a0f7a0f00a271c7bffc06`; log
`/tmp/forge-cli-main-install.log`, binary SHA256
`092a71afb895d6fabd9bcd8309211ff85c2ac7249490391118454991a34950f7`.

## Remote work

| Item | Observation |
|---|---|
| Product PR | [forge-mcl PR71](https://github.com/katasec/forge-mcl/pull/71) merged after every required check passed, 2026-10-08 12:06:21 UTC. Merge/source `cae994cd71fc6e4b640a0f7a0f00a271c7bffc06`; branch `adeen/cli-merge-release-versioning`. |
| Final four-host verification | [Run 37770343141](https://github.com/katasec/forge-mcl/actions/runs/37770343141) PASS on all four native hosts at final branch source `124db512b6297f46d4f46be18b3356752f6a3eb2`. Every host passed the 29 checks, strict native compile/help/version/package and uploaded the full ZIP/checksum. Native identity `0.9.5-dev.16+0582997dd7b9a17504177be1e4345e0256c8cb4a` uses GitHub's PR test-merge SHA. |
| Shared package verification consumer | [Run 37770343176](https://github.com/katasec/forge-mcl/actions/runs/37770343176) PASS: managed build zero warnings/errors; 982 tests passed, 10 existing skips, no failures; strict native help/version PASS; 27 Terminal tests and immutable 0.1.0 package metadata/content/dependency verification PASS. |
| First automatic merged-PR release | [Run 37774544028](https://github.com/katasec/forge-mcl/actions/runs/37774544028) reserved `v0.10.0` at the actual merge SHA and passed all four native hosts with `0.10.0+cae994cd71fc6e4b640a0f7a0f00a271c7bffc06`. Publish-only recovery reused original assets/tag and verified all eight downloaded remote pairs, then published [v0.10.0](https://github.com/katasec/forge-mcl/releases/tag/v0.10.0) as latest at 12:39:53 UTC. No native rebuild or tag change during recovery. |
| Superseded verification | Runs 37768893552/37768893500 and 37769311949/37769311925 were cancelled after final-source revisions; they cannot supply final-source evidence. |
| Patch selector | The `release:patch` label was created in forge-mcl for future bug-fix PRs. PR71 keeps default minor policy. |

No project-memory files were found in either touched repository's memory directory. Local checks
did not mutate release tags/assets; the merge workflow alone reserved the new real tag.

## Draft discovery repair

The bounded repair design was recorded before implementation. Its only product edits were
forge-mcl `scripts/release.ps1` and `scripts/tests.ps1`: retain the creation response/ID, refresh
that exact ID after mutations, and prove delayed list visibility with a controlled fixture.
Existing `gh` upload/download/edit transport and draft-only replacement stayed unchanged.

The reviewed pre-merge boundary was focused real-Git/fake-transport checks plus both independent
reviews, because the already-accepted native/compiler path was unchanged. The ordinary four-host
merge release and downloaded published archive remained mandatory final-source acceptance.

The first publisher created draft 406808274 then failed on immediate list discovery (`draft`
property absent on null). The draft had zero assets and remained unpublished. The original
publish-only rerun found that same draft and succeeded; first-run log
`/tmp/forge-cli-release-publish-failure.log`, recovery
`/tmp/forge-cli-release-publish-recovery.log`. These are observations of delayed discovery, not
a claim that every GitHub read route or token has identical consistency.

| Repair observation | Evidence |
|---|---|
| Independent design/code reviews | Simplicity/style and ownership PASS on exact two-file repair; simplicity independently ran 30 checks. |
| Regression | 30 focused checks PASS, including new draft hidden from lists before upload; `/tmp/forge-cli-release-draft-repair-tests.log`. |
| Product patch | [PR72](https://github.com/katasec/forge-mcl/pull/72), `release:patch`, merged at `8d28dc1ff8f127facfd708b1c89369179e98cf18`, 12:49:00 UTC. Creation response/ID retained, post-mutation refreshes by ID. No build/version/product/Make/YAML edits. |
| Verification boundary | Focused checks/reviews were the pre-merge gate for this transport-only repair. Unchanged native PR runs 37779561181/37779561172 canceled; they provide no acceptance evidence. Final-source native/automatic release and downloaded default acceptance passed below. |
| Patch release | [Run 37779596999](https://github.com/katasec/forge-mcl/actions/runs/37779596999) PASS on attempt 1 at exact repair merge `8d28dc1ff8f127facfd708b1c89369179e98cf18`. All four hosts passed 30 focused boundaries and strict native build/help/version/archive checks with official identity `0.10.1+8d28dc1ff8f127facfd708b1c89369179e98cf18`. Publisher created a new draft, verified all eight remote ZIP/checksum assets and published [v0.10.1](https://github.com/katasec/forge-mcl/releases/tag/v0.10.1) as latest at 13:25:18 UTC, release ID `406857031`. No retry or tag change. |

Final-source logs: `/tmp/forge-cli-patch-release-linux.log`,
`/tmp/forge-cli-patch-release-linux-arm.log`, `/tmp/forge-cli-patch-release-windows.log`,
`/tmp/forge-cli-patch-release-macos.log`, `/tmp/forge-cli-patch-release-publish.log`.

## Published default artifact acceptance

| Fact | Observation |
|---|---|
| Artifact | Full published [v0.10.1 macOS ARM64 ZIP](https://github.com/katasec/forge-mcl/releases/download/v0.10.1/forge-osx-arm64.zip), downloaded through `gh release download`; immutable source `8d28dc1ff8f127facfd708b1c89369179e98cf18`. |
| Defaults | Normal laptop dependencies; no custom SDK, swapped libraries or version/RID/output override. Entire archive extracted with `unzip`, without executable-bit repair. |
| Dependency | Existing Homebrew OpenSSL/Brotli plus the archive's own `libonigwrap.dylib` and `libonnxruntime.dylib`; native `forge` and all sidecars retained. |
| Starting state | Dedicated `/tmp/forge-cli-release-v0.10.1/payload`; no project/account/data mutation. |
| Action | Published ZIP and checksum downloaded, SHA256 checked, whole archive extracted, extracted native `forge --help` and `--version` executed. |
| Outcome | PASS: help exit 0; identity exactly `0.10.1+8d28dc1ff8f127facfd708b1c89369179e98cf18`. ZIP SHA256 `d917972fa23b7a9d3db7c1bc2ca14182c3e977d85104fefbf2d36be6b49dcde3` matches the published sidecar. Help output `/tmp/forge-cli-release-v0.10.1/help.txt`. |
| Controlled tests | GitHub fixture transport is fake and supplies boundary proof only; publication and downloaded/default execution above use real GitHub and normal laptop dependencies. |

The documentation checkpoint changes no executable behaviour: its own default-path gate is N/A.
Both touched repositories finish on clean, synchronized `main`; product delivery is PR71/PR72,
and this repository commits the design, governing defaults and verified evidence.
