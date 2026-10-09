# Phase 76.3 — OCI integrity/authentication evidence

**Accepted 2026-10-09 20:27:58 UTC.** The bounded OCI prerequisite is complete. This does not
close unified cloud execution or settle its two pending Type-1 choices.
[Contract](phase-76.3-oci-integrity-auth.md) · [approved plan](phase-76.3-oci-integrity-auth-plan_completed.md).

## Product and publication

| Fact | Observation |
|---|---|
| Product PR | [oci-client-dotnet #5](https://github.com/katasec/oci-client-dotnet/pull/5), opened 20:26:06 UTC, merged 20:26:20 UTC |
| Reviewed branch commit | `37367cd9225d767cd1f9c74031767b584e6eceba` |
| Merged main / immutable release tag | `5fa8ae7cc8a722f120e2350fb4e16ddc7c63f12a`; `v0.5.0` resolves to this exact commit |
| Normal publication | [Publish run 37987035091](https://github.com/katasec/oci-client-dotnet/actions/runs/37987035091), main/head `5fa8ae7`, SUCCESS; test, pack, GitHub Packages push and release steps passed |
| Published artifact | [Katasec.OciClient 0.5.0](https://github.com/katasec/oci-client-dotnet/releases/tag/v0.5.0), 42,448-byte nupkg |
| Restored source identity | Nuspec version `0.5.0`, repository commit `5fa8ae7cc8a722f120e2350fb4e16ddc7c63f12a`; `.nupkg.metadata` source `https://nuget.pkg.github.com/katasec/index.json` |
| Package SHA-256 | `9e3b80ca5d9c0f8fbda9b0e4bd93e2d44b19d51fd9658513409d787b4391934d`; independently restored bytes match GitHub release asset digest |
| Checks before merge | Full local managed/live/AOT evidence and both current code reviews passed; PR merge state CLEAN/MERGEABLE, no checks reported; no failing required check bypassed |
| Final OCI state | Clean `main`, equal to `origin/main`, `git rev-list --left-right --count main...origin/main` returned `0 0`; no task worktree |

The exact baseline was `565d3e2a7a87d95b05c7dd8261834c843f467f78`. Cached 0.4.0 contained
a mission API from an earlier merge reverted by PR 4; source/header/layer/auth inspection showed
that upgrading the cache alone could not supply the required integrity/authority behavior.
The implementation adds the missing API to actual main and repairs existing owners. CLI and
MissionRegistry still pin 0.2.1; consumer adoption is future work, not delivered by this library PR.

## Managed and branch checks

All eight changed/new files matched the source hashes recorded with final checks. Only existing
OCI client/auth/models/tests/README changed; no workflow, project version, extraction, CLI or
cloud code changed. The supervisor independently read the full product diff and actual logs.

| Command / layer | Final observation |
|---|---|
| `dotnet build Katasec.OciClient.slnx -c Release --no-restore -warnaserror` | PASS, 0 warnings, 0 errors |
| Focused digest/mission/auth filter, Release `--no-build` | 96/96 PASS |
| `dotnet test ... --filter 'Category!=Integration'` | 102/102 PASS; normal publication CI independently also passed 102/102 |
| `dotnet test ... --filter 'Category=Integration' --logger 'console;verbosity=normal'` | 14/14 PASS, 27.1799 seconds; existing private expert fixtures, immutable expert re-resolution and live push/pull roundtrip |
| Four malformed-bearer cases | CRLF/NUL × verification/retrieval: typed refusal before retry/cache; no disclosure; next fresh exchange succeeds on same client |
| Distinct branch package / fresh isolated AOT consumer | `0.5.0-phase76.2`, no stale normal-version cache; packaged/restored hash `d9f86be747e4375241932397ab2306bfe1579cc4903e229cb4d88886c354d866`; native publish/run PASS |
| Diff validation | `git diff --check` and staged equivalent PASS |

Controlled handlers prove independently wrong request/header/layer digests, same-size corruption,
schema/descriptors, bounded unknown/dishonest streams, cancellation/interruption/disposal,
unsafe authority/query/challenges, expiry/scope changes, redirect secrecy/bounds, and push-body
replay. They remain lower-layer evidence. Branch-package AOT also remains lower-layer evidence.

Local final branch evidence: `/var/folders/kl/zcltgz9s1hv4c2p8tlh_13d40000gn/T/phase76-oci-r2-201fd360`
(`verify-branch.ps1`, complete patches/source hashes, `logs/`). Earlier r1 evidence is preserved
in sibling `phase76-oci-63528568`.

## Full independent code reviews

| Simplicity check | Round 1 | Round 2, full current artifact |
|---|---|---|
| New apps or libraries | PASS | PASS: existing HTTP/STJ, no dependency/project changes |
| Reuse | PASS | PASS: shared manifest/auth/blob paths |
| Multiple code paths | PASS | PASS: old mission API delegates; verification shares auth |
| Legacy paths | PASS | PASS: no insecure mission fallback |
| Knobs | PASS | PASS: approved caller budget only |
| Speculative abstractions | PASS | PASS: reader owns three actual response boundaries |
| Library choice | PASS | PASS: existing source generation, real native probe |
| Copy-paste | PASS | PASS: shared bounds/exchange/retry |
| Redundant definitions | REVISE: unused cached Challenge | PASS: unused field removed |
| Size versus requirement | PASS | PASS: approved OCI scope only |
| Test volume | PASS | PASS: distinct integrity/authority/failure observations |

| Code-style check | Round 1 | Round 2, full current artifact |
|---|---|---|
| Progressive disclosure / outline first | PASS | PASS |
| Small functions | PASS | PASS |
| Top-down order | PASS | PASS |
| Explicit errors | REVISE: malformed token escaped as FormatException | PASS: platform header validation inside auth boundary before cache/retry |
| Shallow nesting | PASS | PASS |
| Separate side effects | PASS | PASS |
| Zero warnings | PASS | PASS: actual current build/native logs read |
| Extract for a real reason | PASS | PASS |
| Complexity | PASS | PASS: manual McCabe, MissionLayer 14, SendCoreAsync 13, FetchTokenAsync 11; cohesive validation/protocol boundaries, others ≤10 |

| Ownership behavior / existing owner | Round 1 | Round 2, full current artifact |
|---|---|---|
| Verified mission identities / OCI client | PASS | PASS |
| Old API delegates / OCI client | PASS | PASS |
| Manifest identity / shared manifest retrieval | PASS | PASS |
| Schema/descriptors/hash / OCI client | PASS | PASS |
| Bounded reads / internal OCI HTTP support | PASS | PASS |
| Credential verification without persistence / auth owner | PASS | PASS |
| Authority/challenge/query / auth owner | PASS | PASS |
| Scoped lifetime/retry / auth owner | PASS | PASS |
| Bounded HTTPS redirects / HTTP/auth owner | PASS | PASS |
| Existing expert/push behavior / OCI client | PASS | PASS |
| Named malformed-auth failure / auth owner | REVISE | PASS: rejected before cache/retry, regression recovery proven |

Both reviewers independently reproduced the token error. Supervisor accepted both findings and
sent one correction to the same implementer. No reviewer edited code. Both then rechecked the
complete current artifact; no PASS was inherited. No duplicate owner/new component/second job.
The supervisor checked scope, public/credential/failure contracts, actual logs and source hashes
and approved readiness at 20:25:44 UTC. UI N/A. No findings dismissed or waived.

## Published default-path acceptance

| Required fact | Supervisor observation |
|---|---|
| Artifact | Exact remote `Katasec.OciClient` 0.5.0, restored into fresh `/private/tmp/phase76-oci-published-acceptance-20261009/packages`; no project reference or substituted DLL |
| Defaults / dependency | Public `new OciClient()` / constructor credential APIs, real `ghcr.io`, normal GitHub Packages feed; no internal handler, fake registry or alternate service endpoint |
| Starting state | Disposable consumer/cache/log directories; existing credentials used without printing or persistence; no unrelated Project/data mutation |
| Build | `dotnet publish ... -c Release -r osx-arm64 -p:PublishAot=true -p:AppleMinOSVersion=27.0 -warnaserror --configfile .../NuGet.Config`; native code generated without warnings |
| Action | Anonymous mission tag → returned immutable digest; independently hash/measure/compare returned bytes; deliberately invalid and existing valid credentials; private expert pull |
| Public mission outcome | `katasec/forge-mission-assistant:0.1.0`, identical tag/digest bytes, 17,408 bytes; manifest `sha256:4ba3278af7b9400e28ff20c559a4274b6546c03571e3e248a67ec03eabcddbf9`; bundle `sha256:30dba78b8bbf3e13b6aea645a969de8eab3664de8e65d3ddb14b2f108eb17c78` |
| Credential outcome | Invalid credential refused with OciAuthException; existing supplied credential verified through real GHCR |
| Private outcome | `katasec/kubernetes-architect:0.1.0` succeeded, 829 characters; manifest `sha256:bc4d494d09f74f5fad29d515e9199db48fe29f293afbe20264a3169ab3ab13e3`; GitHub package API independently confirmed visibility `private` (mission fixture `public`) |
| Result | Native executable exit 0, `Public API Native AOT probe PASS`; supervisor accepted at 20:27:58 UTC |

Reusable supervisor script/source/logs: `/private/tmp/phase76-oci-published-acceptance-20261009`
(`accept-published.ps1`, `consumer/`, `logs/published-*`, publication workflow log).
Nuspec/source/hash inspection matched the immutable release and merge commit above.

Toolchain scope: SDK 10.0.401/runtime 10.0.12, current macOS 27.0.1 host; existing Homebrew
OpenSSL/Brotli supplied through LIBRARY_PATH. First branch link lacked those library search paths;
the next macOS12-target link emitted five deployment-version warnings because installed libraries
target macOS26/27. A clean final host-target publish had no warnings; vtool confirmed minos27.0.
No OCI configuration or public setting changed; no macOS12 or authenticated Docker Hub
compatibility claim. There is no missing private-fixture permission limitation.

## Timing

The shared Phase 76 scope/design preceded this independently bounded prerequisite. The table
records actual observed boundaries; unavailable times are not inferred. Tokens N/A. Product
end-to-end from shared scope 19:24:51 to OCI merge 20:26:20 is **1 h 1 m 29 s**; this is not
Phase 76 completion time. Publication and acceptance are separate from product merge timing.


| Stage / role / round | Start (UTC) | End (UTC) | Wall | Evidence |
|---|---|---|---|---|
| Scope / supervisor | 2026-10-09 19:24:51 | 2026-10-09 19:24:57 | 6 s | Clean repository state, requirements and workflow |
| Investigate / implementer / r1 | Unavailable | Unavailable | Unavailable | Read-only source investigation; boundaries not recorded |
| Investigate / implementer / r2 | Unavailable | Unavailable | Unavailable | Read-only continuation/dependency probe; boundaries not recorded |
| Design / supervisor / r1 | 2026-10-09 19:24:57 | Unavailable | Unavailable | Initial contract draft; exact end boundary not recorded |
| Review-design / simplicity / r1 | Unavailable | 2026-10-09 19:33:24 | Unavailable | REVISE; first review returned before observed end boundary |
| Review-design / ownership / r1 | 2026-10-09 19:33:24 | Unavailable | Unavailable | FAIL; exact result boundary not recorded |
| Design / supervisor / r2 | Unavailable | 2026-10-09 19:40:56 | Unavailable | Combined first-round corrections |
| Review-design / simplicity / r2 | 2026-10-09 19:40:56 | 2026-10-09 19:42:35 | 1 m 39 s | Full artifact REVISE: launch mapping |
| Review-design / ownership / r2 | 2026-10-09 19:42:35 | Unavailable | Unavailable | Full artifact REVISE; exact end boundary not recorded |
| Design / supervisor / r3 | Unavailable | 2026-10-09 19:46:13 | Unavailable | Launch/context/produced-file corrections |
| Review-design / simplicity / r3 | 2026-10-09 19:46:13 | 2026-10-09 19:47:19 | 1 m 6 s | Full technical PASS |
| Review-design / ownership / r3 | 2026-10-09 19:47:19 | Unavailable | Unavailable | Ownership PASS; OCR filename correction |
| Design / supervisor / r4 | Unavailable | 2026-10-09 19:50:09 | Unavailable | OCR correction and bounded OCI prerequisite |
| Review-design / simplicity / r4 | 2026-10-09 19:50:09 | 2026-10-09 19:51:33 | 1 m 24 s | Both designs technical PASS; source-description correction |
| Design / supervisor / source wording | Unavailable | 2026-10-09 19:51:33 | Unavailable | Add missing API against actual main; no second retrieval path |
| Review-design / ownership / r4 | 2026-10-09 19:51:33 | 2026-10-09 19:52:51 | 1 m 18 s | Both designs technical PASS |
| Plan / implementer / r1 / OCI prerequisite | 2026-10-09 19:52:51 | 2026-10-09 19:57:04 | 4 m 13 s | Read-only Plan returned and recorded; no code approval |
| Review-plan / simplicity / r1 / OCI prerequisite | 2026-10-09 19:57:04 | 2026-10-09 19:57:48 | 44 s | Full checklist PASS |
| Review-plan / ownership / r1 / OCI prerequisite | 2026-10-09 19:57:48 | 2026-10-09 19:58:50 | 1 m 2 s | Full Plan PASS |
| Implement / implementer / r1 / OCI prerequisite | 2026-10-09 19:58:50 | 2026-10-09 20:13:46 | 14 m 56 s | Bounded code returned; build zero warnings, deterministic 98, real integration 14, branch-package Native AOT PASS |
| Review-code / simplicity and style / r1 / OCI prerequisite | 2026-10-09 20:13:46 | 2026-10-09 20:16:22 | 2 m 36 s | Full REVISE: malformed token error contract and unused cache metadata |
| Review-code / ownership / r1 / OCI prerequisite | 2026-10-09 20:16:22 | 2026-10-09 20:19:12 | 2 m 50 s | Placement PASS; malformed token error contract REVISE |
| Implement / implementer / r2 / OCI prerequisite | 2026-10-09 20:19:12 | 2026-10-09 20:22:14 | 3 m 2 s | Combined correction returned; deterministic 102, integration 14, current-source Native AOT PASS |
| Review-code / simplicity and style / r2 / OCI prerequisite | 2026-10-09 20:22:14 | 2026-10-09 20:24:30 | 2 m 16 s | Full current artifact PASS for all 11 simplicity and 9 style checks |
| Review-code / ownership / r2 / OCI prerequisite | 2026-10-09 20:24:30 | 2026-10-09 20:25:44 | 1 m 14 s | Full current artifact PASS; all 11 behavior-owner rows |
| Product PR / OCI #5 | 2026-10-09 20:26:06 | 2026-10-09 20:26:20 | 14 s | PR opened/merged timestamps from gh; merge 5fa8ae7 |
| Publication / normal workflow | 2026-10-09 20:26:47 | 2026-10-09 20:27:21 | 34 s | Publish run 37987035091 SUCCESS |
| Accept / supervisor / published package | 2026-10-09 20:27:28 | 2026-10-09 20:27:58 | 30 s | Fresh remote restore, source/hash identity, native real GHCR PASS |

Supervisor revisions were interleaved with reviews; start boundaries were not recorded.
Earlier orientation/investigation assignment boundaries were not recorded. Missing times are not
reconstructed. Tokens are N/A unless independently measured.

## Documentation handoff

Supervisor documentation validation observed at 2026-10-09 20:30:42 UTC: eight files, 60 local
links/anchors, two JSON examples, balanced fences and the whole top-level-only plan hub PASS;
diff checks PASS. Documentation work was interleaved; its start is unavailable. Its closure PR is
outside the product timing span. Product tests are the actual OCI observations above; cloud
implementation/default acceptance remains open.

Checkpoint found the project memory directory empty, so no durable fact needed folding/deletion.
Live/repository checkpoint skill copies had identical SHA-256. No task worktree was created.
The plan now names the next step: operator settlement of executable trust and Host-owned content,
then supervisor contract lock and dependency-ordered implementation plans. No unfinished cloud
code or substitute acceptance was created.


## Approved completion conditions


- Sequential full design reviews pass; supervisor locks this bounded proposal. The same fixed
  implementer provides a plan; sequential full plan reviews pass before explicit approval.
- Implementation matches the API/authority/integrity/failure contracts; existing managed tests
  and targeted regressions pass with zero warnings; sequential full simplicity/style and
  ownership reviews pass; supervisor checks the actual diff/evidence.
- Normal PR merged into main, normal publish workflow succeeds from that exact main revision,
  new package version restored and inspected. No old cached package substituted.
- Supervisor runs the restored package's public API in a disposable Native AOT consumer against
  real default GHCR: anonymous mission/tag/digest retrieval verifies identity; supplied valid and
  invalid credential verification have the specified result; a private authorized pull succeeds
  if existing credentials permit it. Any missing private permission is reported as an observed
  limitation, not a compatibility claim or reason to publish failing work.
- Completion record names commands/results, package/workflow/commit IDs and timing; every touched
  repo is clean/current main with no unpushed work. Cloud execution/content/CLI acceptance stays
  open in the parent phase.
