# Phase 67 — Completion evidence

## Design and implementation

User requested Linux ARM alongside existing GH release targets and authorized continuing the
target update. Design was recorded before the supervised implementer plan/approval/edit loop.
forge-mcl owns this distribution; one `ubuntu-24.04-arm` / `linux-arm64` / `forge` matrix entry
reuses existing Linux prerequisites, native publish, help/version checks and packaging. Add only
its ZIP/checksum arguments to the single release command; retain two jobs. README now names
four platforms/eight assets and uses version 0.9.3. PDB removal was discussed, not requested.

[GitHub runner documentation](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
confirmed native ARM64 runner availability. Existing ONNX 1.27.0 and onigwrap 1.0.11 packages
contain Linux ARM64 native libraries; no dependency or product code change was necessary.

Security: no hosted tier, datastore, identity or cross-context change; existing read-only
build token and contents-write publication job remain. UI N/A. Engineering: all four native
builds must pass before publication, with existing tag refusal/upload-before-publication.
No new job, framework, credential, architecture exception or retry mechanism.

Independent simplicity/ownership design and actual-diff reviews PASS. Supervisor approved the
implementer plan and independently inspected the diff/reran actionlint.
[forge-mcl PR 59](https://github.com/katasec/forge-mcl/pull/59) merged at
`19f2e78d9154e5ba2d05bb216b01a910b2c52cf6`. Two files, 9 additions / 5 deletions.
Official actionlint 1.7.12 and whitespace checks PASS; executable scripts unchanged apart from
the two literal asset arguments. No local product rebuild or full test stage added.

## Release acceptance

[Run 37219462386](https://github.com/katasec/forge-mcl/actions/runs/37219462386) dispatched normal
main at the merged SHA with version 0.9.3. Started 17:09:57 UTC, completed 17:31:49 UTC, success.

| Job | GitHub job ID | Completed UTC |
|---|---|---|
| macOS ARM64 | 111486677404 | 17:31:30 |
| Linux x64 | 111486677561 | 17:21:41 |
| Linux ARM64 | 111486677577 | 17:24:51 |
| Windows ARM64 | 111486677586 | 17:25:17 |
| Publication | 111490668272 | 17:31:49 |

All five jobs succeeded. Native ARM64 help and exact version/source checks passed on its actual
Linux host. Its archive log names `libonnxruntime.so`, `libonnxruntime_providers_shared.so` and
`libonigwrap.so`, confirming native sidecars are included.

[v0.9.3](https://github.com/katasec/forge-mcl/releases/tag/v0.9.3), release ID 403129614, was
published automatically at 17:31:47 UTC (**9:31:47 PM Dubai**); `draft=false`, eight uploaded
assets. Release target and git tag both resolve to `19f2e78d9154e5ba2d05bb216b01a910b2c52cf6`.
Supervisor downloaded all four checksum files and matched them against GitHub's ZIP digests:

| ZIP | SHA-256 |
|---|---|
| forge-linux-arm64.zip | `340211a1a19e44103b6d50c6f4d2496e131042fda5706cb16338aaa11d33f74a` |
| forge-linux-x64.zip | `228c7d199e4459d5b336b783b79a259be3330beecb1033a2ebc4691cd6ccb9be` |
| forge-osx-arm64.zip | `d345b9b0e146bd27bc03999120cdd5fb5702a49fc2acb60c3a35af82f7b78881` |
| forge-win-arm64.zip | `b664d0f33776681c1fcdf63ebca2562fb2e475b87b253ec1fdc42ae4b463e973` |

Existing v0.9.1 and v0.9.2 release IDs, publication timestamps, all asset IDs and digests match
their previous snapshots; no overwrite. No retry or manual upload/publication was needed.

Default-path distribution PASS: normal main dispatch, real Linux ARM64 host, native executable
checks, complete ZIP/checksum and automatic publication. Product/API defaults remain unchanged;
reuse [Phase 64 chat acceptance](phase-64.1-portable-chat-contracts_completed.md#installed-default-path-acceptance)
and [Phase 65 distribution observations](phase-65-cli-release-copy_completed.md#published-release).
No new paid turn or interactive terminal acceptance is claimed.
