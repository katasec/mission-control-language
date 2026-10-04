# Phase 66 — Completion evidence

## Design and review

User requested workflow simplification, automatic publication of a new version without
overwriting v0.9.1, and correction/retry until green. Scope is forge-mcl's release workflow and
README only; no product code, dependency, Desktop or package publishing changes.

Design was recorded before implementation, using the supervisor plan/approval loop. Existing
macOS ARM64, Linux x64 and Windows ARM64 targets build in parallel; one dependent publication
job downloads three complete native ZIPs and checksums and publishes automatically.

Independent simplicity/ownership review challenged the supervisor's draft/edit proposal:
`gh release create` already drafts, uploads assets and publishes internally. Installed CLI help
and official cli/cli source confirmed that behaviour. Use that single command. Keep only an
in-job existing-tag refusal because `--target` cannot replace an already-existing tag's source.
Remove full-suite verification, prepare/tag-push jobs, archive extraction/missing-project checks
and manual publication. Final actual-diff persona review PASS.

Security: hosted tiers/datastores/identity/cross-context contracts and UI are N/A for existing
CLI distribution. Read-only contents/packages build token; contents write only in publication;
no provider key, PAT or new secret. Reversible workflow change, no Type-1 architecture change.
Engineering: matrix failure blocks publication; GitHub CLI owns upload-before-publication and
draft cleanup. Actual failures stop the job; supervisor owns necessary correction/rerun, with
no speculative retry framework. No overwrite, force, clobber or deletion of existing releases.

## Implementation

[forge-mcl PR 58](https://github.com/katasec/forge-mcl/pull/58) merged at
`95c19934cb1a978e52f649a9940405f5155a5b18`. Two files changed, 44 additions / 126 deletions.
Workflow reduced from 229 to 149 lines and four logical jobs to two.

Official actionlint 1.7.12, all nine Bash/PowerShell run-script syntax checks, whitespace checks
and independent diff review PASS. Supervisor independently reran actionlint and inspected the
diff. Version/default-branch guards, exact SHA metadata, private NuGet access, native prerequisites,
macOS ad-hoc signing and complete payload packaging remain. No local product rebuild or repeated
test suite was run for this executable-configuration change.

## Release acceptance

[Run 37217325742](https://github.com/katasec/forge-mcl/actions/runs/37217325742) dispatched normal
main at `95c19934cb1a978e52f649a9940405f5155a5b18` with version 0.9.2. Started 16:35:25 UTC;
publication job completed 16:57:23 UTC. All four executed jobs concluded `success`:

| Job | GitHub job ID | Completed UTC |
|---|---|---|
| macOS ARM64 | 111480383826 | 16:44:55 |
| Linux x64 | 111480384005 | 16:50:37 |
| Windows ARM64 | 111480383905 | 16:56:54 |
| Automatic publication | 111484270263 | 16:57:23 |

All native jobs passed help and exact version/source checks on their real hosts.
[v0.9.2](https://github.com/katasec/forge-mcl/releases/tag/v0.9.2), release ID 403116538,
was published automatically at 16:57:20 UTC (**8:57:20 PM Dubai**), `draft=false`, six assets
in `uploaded` state. Release target and tag both resolve to the dispatched SHA.

Supervisor downloaded the three checksum files and compared them against GitHub's asset digests:

| ZIP | SHA-256 |
|---|---|
| forge-osx-arm64.zip | `888a5f79ee10d117c5241480e59c56e3ea496f9403747f5e0ca50607492981df` |
| forge-linux-x64.zip | `21de07751f6270563625f399586e421183209b20319e70638d6cf3646b71f0df` |
| forge-win-arm64.zip | `55920ecb19bebbd4e91c14c188d6e409785e42fb1d3b6278f22e65664ed7fec6` |

All three matched. v0.9.1 release ID 403085253, publication timestamp, all six asset IDs and
digests match the pre-run snapshot; no overwrite. No retry, draft edit or manual upload needed.

Default-path distribution PASS: normal main workflow, real native hosts, complete platform
ZIPs/checksums and automatic GitHub publication. `git diff v0.9.1 HEAD --name-only` shows only
release.yml and README, so product/API defaults are unchanged. Existing [Phase 64 chat acceptance](phase-64.1-portable-chat-contracts_completed.md#installed-default-path-acceptance)
and [Phase 65 distribution observations](phase-65-cli-release-copy_completed.md#published-release)
are reused explicitly; no new hosted turn or interactive Windows laptop acceptance is claimed.
