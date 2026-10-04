# Phase 65 — Release verification record

The [design](phase-65-cli-release-copy.md) owns the release contract. Workflow copying,
native payload checks, upload recovery and publication are verified, 2026-10-04.

## Copied workflow

Copied native release behaviour from original source commit
`1f282f19da6d660e91e8662ea3cf516946b11171`, `.github/workflows/release.yml`, into forge-mcl.
Original workflow history/releases were not moved, deleted or replaced. The CLI owner receives
the native matrix only; Desktop and Docker publication are outside this task.

[forge-mcl PR 56](https://github.com/katasec/forge-mcl/pull/56) merged as
`530a54f5fffe3b4b8d02dd145e4bf6db7bddf944`. Exactly two files changed: workflow and README.
Supervisor approved the bounded plan before edits and independently inspected the resulting diff.
Independent final Ownership/Simplicity review PASS on source `8b99709`: existing owner, matrix
and platform tools; no framework, helper scripts, package or product changes.

Official actionlint 1.7.12 passed with no findings, independently rerun by supervisor. Implementer
observed all twelve run scripts parse successfully; four malformed versions and a non-default
source ref fail before Git access. Controlled temporary stubs exercised existing tag/release,
HTTP 401 refusal and HTTP 404 preparation. Those checks did not mutate repository/release state
and are labelled controlled supporting evidence, not successful GitHub builds.

## GitHub execution

[Release CLI run 37213197522](https://github.com/katasec/forge-mcl/actions/runs/37213197522)
was dispatched on merged main `530a54f` with version `0.9.1` at **19:29:30 Dubai, 2026-10-04**.
Verification passed: Debug/Release builds each zero warnings/errors; Linux suite 740 passed,
10 existing opt-in skips. Prepare and all three native matrix jobs passed, including extracted
payload help/version/source/missing-declaration smoke. The final upload failed at its initial
REST draft tag lookup (HTTP 404), before any asset was attached. Whole-run status remains failed;
successful build jobs are not described as a successful whole workflow.

## Upload-only recovery

User authorized fixing and retrying attachment with existing artifacts, without rebuilding.
The draft existed at source `530a54f`; `gh release view --json isDraft` observed true and zero
assets. Replace only the lookup and JSON field (two lines); no new workflow, job or input.
[forge-mcl PR 57](https://github.com/katasec/forge-mcl/pull/57) merged `7aaeb43` after independent
simplicity review against the original "fix and upload only" request passed. Official actionlint,
whitespace and draft true/false predicate checks passed; supervisor independently reviewed diff.

Supervisor recovered the exact three artifacts from run 37213197522, verified all ZIP checksums,
and used the corrected gh attachment command. No tag, version, build or product change.

| Archive | SHA-256 |
|---|---|
| forge-osx-arm64.zip | fd3e79e4d02bd6fa08da6ceea66cd66c2da696b8cba73eaca216e5e334518017 |
| forge-linux-x64.zip | 7d2b14a77609e107453abc14d843709906b45f8f9536f6b346cb270facfdb37d |
| forge-win-arm64.zip | 09fe48eb0137c5a4a899cbb089e5da7d230a23b504f5c9270ae4925a01b6b27b |

Extracted recovered macOS artifact reports `0.9.1+530a54f5fffe3b4b8d02dd145e4bf6db7bddf944`.
Supervisor ran it in the dedicated declaration-only clone with normal login/default ForgeAPI
and no endpoint override: existing `phase64 prior history` replayed, no stderr, exit 0.
No paid turn or full tests were repeated during upload recovery; Phase 64 already proves the
unchanged product's real turn behaviour. Windows ARM64 execution is proven by native CI smoke;
interactive laptop/terminal testing is not claimed.

## Published release

[forge-mcl v0.9.1](https://github.com/katasec/forge-mcl/releases/tag/v0.9.1) published at
**20:19:53 Dubai, 2026-10-04**. GitHub observed `isDraft: false` and exactly six uploaded assets:
three platform ZIPs and their checksum files. Before publication, supervisor compared every
remote asset's GitHub SHA-256 digest against its recovered local file; all six matched. Tag/source
remains `530a54f`; guard fix `7aaeb43` changes no product code and does not require a rebuild.

The original Actions run remains failed at its old upload step. Recovery used corrected CLI
commands on its existing artifacts, not a rerun of the old workflow definition. No duplicate
release/version, new build job, asset clobber or tag rewrite. Original repository releases remain.
Documentation-only reconciliation records default-path gate N/A; release evidence is above.

## Simplicity control failure

The initial reviewer returned PASS on the implementation, but did not challenge the scope's
repeated full verification and manual release ceremony against the user's simple build/publish
request and existing evidence. Supervisor labelled its own design locked and then treated review
conformance as effective simplicity acceptance. This was false assurance of completion, not a
missing reviewer invocation. The existing persona already requires challenging unnecessary work;
more checklist rules were not the fix. Future reviewers must retain authority to challenge the
supervisor's added scope. No broader pipeline rewrite was authorized during upload recovery.
