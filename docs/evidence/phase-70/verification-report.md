# Phase 70 — Foundation delivery and selection investigation report

Recorded 2026-10-06. **Foundation and Forge-only Mac shortcut CLI v0.9.5 installed; package accepted public; operator acceptance
and continuous rich-selection design remain open.** This report replaces raw Phase70 evidence, at the operator's request. The first cleanup removed71 recent artifacts; the follow-up removed91 older foundation files, leaving this single report. Earlier foundation design,
plan and code-review records remain in the [foundation archive](../../phases/phase-70.1-text-interaction-foundation_completed.md).
All times below are UTC.

## Foundation delivery

| Check | Named observation / outcome |
|---|---|
| Merged product | [forge-mcl PR62](https://github.com/katasec/forge-mcl/pull/62), commit `d23387492de4563abe9a57d5a51dfb538745f725`; reviewed tree preserved. |
| Component inventory | [forge-desktop PR9](https://github.com/katasec/forge-desktop/pull/9), commit `39e3ba2eec498d09048cc79bcee2ffcb7cdf2cd0`; documentation only. |
| Merged-source verification | [Package workflow 37410290293](https://github.com/katasec/forge-mcl/actions/runs/37410290293): 873 tests passed, zero failed, 10 existing prerequisite skips/883 total; package tests 26/26. Complete managed/native compiler and linker output had zero actual warnings. Verification job PASS; overall publication FAIL below. |
| CLI release | [Release workflow 37410293172](https://github.com/katasec/forge-mcl/actions/runs/37410293172) SUCCESS at 04:12:05; [v0.9.4](https://github.com/katasec/forge-mcl/releases/tag/v0.9.4). All four native hosts passed publish/help/version; supervisor inspected complete compiler/linker output, zero actual warnings. All eight ZIP/checksum assets present; four checksums matched GitHub asset digests. Other platform ZIPs were not independently downloaded or executed locally. |
| Mac installation | Authenticated whole ZIP downloaded and checksum verified. All seven payload files, including dylibs and dSYM, installed at 04:13:46.786 under `/Users/ameerdeen/.local/bin`; every installed hash matched the extracted payload, unrelated files preserved. |
| Installed identity | Normal PowerShell PATH resolved `/Users/ameerdeen/.local/bin/forge`; help/version exited0; version `0.9.4+d23387492de4563abe9a57d5a51dfb538745f725`. Installed ad-hoc signature valid on disk and satisfied its designated requirement. |
| Live acceptance | Pending operator on normal installed Ghostty/laptop Retina, both themes and normal/narrow windows. No agent UI, physical clipboard, network-chat or default-path PASS claimed. Continuous rich selection remains unimplemented. |

Mac ZIP SHA256: `58e89226b4d5e8e805182c7963507f87c30a902602150cb94e99ea3848cc5115`.
Installed binary SHA256: `1061317ab01994170cb76811abfb7e25e2047b051cec4b6a9620cf272d7e12af`.
Prior matching installation files were retained at `/tmp/phase70-release-v0.9.4/previous-installed`;
this temporary path is a local reversal aid, not durable evidence.

## Package visibility failure

The workflow pushed `Katasec.Forge.Terminal.Extensions` **0.1.0** at 04:03:51, then its
post-push private-visibility guard failed at 04:04:50. Supervisor API observations showed
package ID **15632832**, version ID **1342072708**, one version, linked to `katasec/forge-mcl`,
**public** while the locked design requires **private**. The repository was also public;
no repository/organization ACL or visibility was changed. The cause of initial package
visibility was not established. The guard detected the mismatch after publication.

A fresh isolated authenticated consumer restored from the normal GitHub NuGet source,
with only the extension reference and no CLI/project references. All public APIs and five
result values passed, zero warnings. Actual nuspec source commit matched the merged product;
exact dependencies resolved UI3.10.0 and Terminal2.2.0, with Ansi1.7.1/Wcwidth4.0.1.
Packed/cache/output DLL hashes matched. Usable publication does not accept its visibility.

Package SHA256: `22504559336333dc0f97f921c293e7a854ef7372b295832829e1ece9f2712095`.
Packed/cache/output DLL SHA256: `77c731ba514b419a5633c16b587349d91bfac0d8b1af0aa0347f199ca0fba685`.

GitHub [documents that public packages cannot become private again](https://docs.github.com/en/packages/learn-github-packages/configuring-a-packages-access-control-and-visibility).
**Resolved 2026-10-07: operator accepted public visibility.** Re-checked with `gh api` that day:
package 15632832 public, one version 0.1.0 (1342072708), linked to `katasec/forge-mcl`, which is
public. No retry, deletion or overwrite was made. Package delivery is closed.

## Rich-selection public probe and comparison

The controlled public-only probe used genuine native backend pointer batches and completed
frames, a memory clipboard and no-op Kitty transport. Build/run exited0, zero warnings/errors.
Thirty observations covered both Forge themes; supervisor independently checked 55 source/artifact
hashes and reran all30, matching every non-frame field. Kitty placeholder IDs/colours can vary
between processes; raw frames were diagnostic, not a logical-offset map or Retina acceptance.
Negative consumer compilation exited1 as expected: two CS1061 errors for unavailable public
point-mapping/range-setting methods, zero warnings.

| Case | Observed result |
|---|---|
| Cross-Paragraph drag | Only the first Paragraph suffix copied; no continuous second-source range. |
| Repeated physical rows | Distinct selections both extracted `echo`; no public directional endpoints identified them. |
| Wrapped/grapheme ranges | Interior two-row drag copied `cho ec`; combining/ZWJ selection copied exact text; local selected payload survived width20→8. |
| Actual image heading | Hit-testable, not a selection owner; heading/code drags returned NoSelection with zero writes. Its logical text wrapped into three image lines at width8. Selected-image rendering was not proved. |
| Styled code and cell overlay | Native selection retained explicit Link/Success foregrounds; public cell overlay retained foreground while changing background. Neither proved TextMate language coverage or supplied logical hit mapping. |

**Finding:** an essential public point/range gap is demonstrated; no clean continuous adapter
was built. This does not prove every possible public adapter impossible or authorize a fork.
Inspected native regions total890 physical lines; estimated public-adapter work500–800 added
lines versus a hypothetical native facade40–80. Estimates exclude coordinator, semantics,
headings, tests, package maintenance and the narrow correction below; neither is total cost.

Current comparison preserves these boundaries: native Paragraph owns wrap/hit/range/painting;
Forge CLI owns source ordering, selection policy and heading geometry; existing extensions
own clipboard results and native Terminal owns transport. Both routes still require meaningful
text separators, stable streaming/reflow identities, heading source spans/full-context font
geometry, scrolling/collapse/retirement, gesture arbitration and selected-heading appearance.

R1 simplicity review required two corrections: sorted native ranges lose anchor/active direction;
Markdown/TextMate published nuspecs permit later UI versions, so full-family repacking depends
on identity and proved binary/runtime/AOT compatibility. UI SourceGen analyzer packaging still
needs verification. Foundation's exact UI pin requires a newly reviewed immutable extension
release for any dependency change. Only CLI, extension and tests currently consume this native
family across the eight canonical Forge repositories; Desktop's atlas is not a product consumer.

Current R2 passed **all11 simplicity checks**, **all18 ownership behaviours** and **all6 ownership
persona checks**. Supervisor accepted investigation facts and native-seam design exploration
only. No fork, library route, maintainer, public API, package/assembly identity or rich design
was approved. Current comparison SHA256: `d327b01d2103d8495f4327a4c641ec26e91a30ac1cbddba193193620a2a8e4be`.

## Narrow native Paragraph diagnosis

Twenty native DefaultLight cases varied ASCII/ZWJ/combining/CJK suffixes, zero/two leading
spaces, single/multiple hard lines, and widths7/8/9/10/20. Wrapping remained enabled.
Build/run exited0, zero warnings/errors. All20 genuine drag→Ctrl+C cases copied the full exact
source once, including leading spaces and trailing newlines. **Seven cases clipped rendered
characters.** Supervisor verified all23 frozen hashes and independently reran all20 complete
observation objects, including styled frames, with exact matches.

At width8, two leading spaces make a nine-cell ASCII body lose its final Y; nine-cell ZWJ/CJK
bodies lose the whole two-cell suffix. The eight-cell combining body fits; unindented variants
fit. Indented ZWJ clips at7/8 and fits at9/10/20. The defect occurs with or without an earlier
hard line. Highlighting does not restore clipped text; Copy remains exact.

Pinned [Paragraph](https://github.com/XenoAtom/XenoAtom.Terminal.UI/blob/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src/XenoAtom.Terminal.UI/Controls/Paragraph.cs#L753)
skips leading whitespace while budgeting the wrap slice, but its caller retains the original
start. [CellBuffer](https://github.com/XenoAtom/XenoAtom.Terminal.UI/blob/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src/XenoAtom.Terminal.UI/Rendering/CellBuffer.cs#L384)
clips the resulting over-budget span and rejects a two-cell grapheme crossing the right edge.
This source-supported diagnosis is corroborated by the matrix, not private-cache inspection
or a tested fix. Native whitespace/wrap correction needs design. Tabs, prefixes, alignment,
other sequences and alternate clip boundaries were not investigated. No patch was approved.

## Investigation timing and limits

| Stage | Start UTC | End UTC | Verdict |
|---|---|---|---|
| Public probe, implementer R1 | 03:44:01.975 | 03:56:48.856 | Controlled observations verified |
| Comparison, simplicity R1 | 03:58:15.536 | 04:04:15.352 | REVISE; two precision corrections above |
| Current R2, ownership | 04:05:59.510 | 04:08:40.503 | PASS investigation only |
| Current R2, simplicity | 04:09:29.743 | 04:10:40.911 | PASS investigation only |
| Narrow probe, implementer R2 | 04:11:32.312 | 04:19:31.921 | Exact Copy PASS; seven rendering losses |

Native source pins: UI `6f4e0cde3890d8ce2510ac0451b861863e4aeeaa`,
Terminal `5517cb3d8cdf0532ecc89260067064f98cde6137`. Hosted tiers/stores/identity N/A to
these local investigations. No private APIs, product patches, copied wrap/font algorithms,
new backend or default overrides were introduced. Controlled probes are not product AOT,
installed default-path or physical acceptance. Existing UI principles, design system, TUI
graphics and both-theme/operator gates remain binding.

The [original checkpoint PR351](https://github.com/katasec/mission-control-language/pull/351)
retains historical audit provenance. The current repository uses this report instead of raw artifacts, intermediate runs and duplicate summaries. This consolidation changes
documentation only: runtime/default-path/security/UI implementation gates N/A.

## Mac shortcut scope checkpoint

Operator screenshots identify the chat input and `/edit` file editor. Installed Ghostty1.3.1
static source handles host bindings before PTY input; native Terminal2.2.0/UI3.10.0 already
decode and execute Ctrl+A Select All. A one-line Cmd+A forwarding candidate is documented in
[the shortcut spoke](../../phases/phase-70.3-mac-select-all.md). That initial checkpoint preceded the operator's Forge-only choice and dedicated-window approval below; it is historical, not the current next action.

| Documentation stage | Start UTC | Validation UTC | Evidence |
|---|---|---|---|
| Scope checkpoint | 07:40:51 | 07:46:45 | Nine Markdown files / 428 local links and anchors PASS; whitespace PASS. |

Product design/plan/code approvals, product tests/AOT and physical/default-path acceptance
were N/A to that documentation checkpoint. Current implementation status is below.

## Forge-only shortcut feasibility

Operator selected **Forge-only**, with normal terminal behaviour restored after exit. Static
supervisor/implementer investigation found no supported automatic current-window route in
Ghostty1.3.1, pinned `332b2aefc6e72d363aa93ab6ecfc86eeeeb5ed28`.

| Observation | Primary source / implication |
|---|---|
| Host binding precedes encoding; Select All has no Forge/alternate-screen condition | [Surface.zig](https://github.com/ghostty-org/ghostty/blob/332b2aefc6e72d363aa93ab6ecfc86eeeeb5ed28/src/Surface.zig#L2646), Select All at5785. A Forge alias alone cannot receive the key. |
| Conditions cover OS/theme, not foreground application | [conditional.zig](https://github.com/ghostty-org/ghostty/blob/332b2aefc6e72d363aa93ab6ecfc86eeeeb5ed28/src/config/conditional.zig#L9). No per-process binding condition. |
| Tables activate/pop explicitly; VT/OSC offers no binding push/pop | Surface5815–5893 and native stream/OSC handlers. No application-exit/crash lifetime. |
| AppleScript can target known UUIDs but cannot identify this child surface reliably | Installed/source scripting dictionaries match; [Exec.zig](https://github.com/ghostty-org/ghostty/blob/332b2aefc6e72d363aa93ab6ecfc86eeeeb5ed28/src/termio/Exec.zig#L626) exports no own-surface UUID. Frontmost/title/cwd matching can target another tab. Explicit cleanup and Automation permissions would still be required. No calls made. |
| Dedicated process is a candidate, not an approved design | Scoped CLI override/direct Forge command leaves ordinary instances unchanged. Normal exit closes its surface; very early abnormal exit may retain an error surface (Surface1203–1279). Launch/config/exit contracts and manual acceptance remain unproved. |

Tar/raw Surface hashes match `320f36fba48cdcd9ed3add5c0e2e294137732f5d779ff064c0c7b62f7b129f5c`.
Raw-source lines2646/5785 correct earlier web-page indexing2424/5301. No product/config changes,
app launches, automation, input, captures or clipboard calls occurred. Global config rewriting,
OS hooks and unconsumed host selection were rejected for violating isolation/containment.

| Stage | Start UTC | Result observed UTC | Limit |
|---|---|---|---|
| Scope, supervisor | 09:11:26 | 09:17:30 | Static feasibility only; no design/plan approval. |
| `[investigate:implementer:r1]` shortcut | Not recorded | 09:17:30 | Duration unavailable; reused thread lifetime is not stage timing. |

At that checkpoint the dedicated launch choice was pending; it is now approved below. Product tests/AOT and physical/default-path acceptance were
N/A to that investigation; implementation followed the later dedicated-window approval.

## Dedicated Mac launch design

Operator approved normal `forge chat` opening a dedicated Forge window on2026-10-06.
The complete [R2 design](../../phases/phase-70.3-mac-select-all.md) passed both independent reviews and was supervisor-locked at10:50:22 UTC. The complete R2 plan passed both reviewers and was approved at11:14:38 UTC. Implementation R1 returned at11:23:21 UTC; current code review and canonical verification are pending.
Static pinned Ghostty1.3.1/open(1) evidence supports new-instance argv/environment inheritance,
identical initial/global Forge commands and native last-window quitting. A controlled shell probe
passed4/4 exact argument cases including spaces, quotes, backslashes, shell metacharacters and Unicode;
no actual Ghostty/LaunchServices operation was performed.

R1 full simplicity and ownership reviews required removing a Linux-only delay and preserving
cwd/endpoint values through native trimming. R2 uses one Base64 cwd/endpoint context with explicit
endpoint absence, literal outer quotes for host cwd, and child-held restoration failures.
Recursive includes are cleared only in the dedicated profile; themes replay the protected CLI values.
No shared config or restoration preference is written. Native restored window count/activation,
physical key delivery and actual LaunchServices inheritance remain manual acceptance observations.
Existing PR-event verification supplies full tests and warning-enforced Mac Native AOT; no new workflow.

| Stage | Start UTC | Result available UTC | Verdict |
|---|---|---|---|
| Scope, supervisor |10:15:44|10:16:37| Approved dedicated-window requirement |
| Investigation, implementer R2 |10:16:37|10:26:25| Static contracts + controlled argv4/4; no physical proof |
| Design, supervisor R1 |10:17:44|10:28:29| Candidate; docs links/whitespace PASS |
| Design review, simplicity R1 |10:28:29|Unavailable| REVISE; verdict available by10:35:19; no inferred duration |
| Design review, ownership R1 |Unavailable|10:40:51| REVISE; complete owner/checklist tables |
| Design, supervisor R2 |10:42:04|10:42:36| Combined corrections;9 Markdown/427 local links PASS |
| Design review, simplicity R2 |10:42:36|10:44:08| All11 checks PASS |
| Design review, ownership R2 |10:44:44|10:47:21| All23 behaviours/all6 checks PASS |
| Supervisor design lock |10:50:22|10:50:22| Complete R2 approved; no implementation approval |
| Implementation plan, R1 |10:50:29|10:55:59| Six bounded product-repo files; no writes |
| Plan review, simplicity R1 |10:59:32|11:02:03| REVISE; name source versus executable production-wiring evidence |
| Plan review, ownership R1 |11:02:53|11:05:39| Same evidence-layer correction; placement PASS |
| Implementation plan R2 |11:06:21|11:07:13| Same files/APIs; explicit source/controlled/default evidence layers |
| Plan review, simplicity R2 |11:08:56|11:11:30| Full11 checks PASS |
| Plan review, ownership R2 |11:12:12|11:13:34| All28 behaviours/all6 checks PASS |
| Supervisor plan approval / branch |11:14:38|11:14:38| Approved six-file R2; branch isolated from main |
| Implementation R1 |11:14:40|11:23:21| Frozen six files; managed checks PASS; physical/default pending |
| Code review, simplicity/style R1 |11:28:21|11:30:05| All11 simplicity checks PASS; all style source checks PASS, native-warning gate pending |
| Code review, ownership R1 |11:31:28|11:33:33| All28 behaviours/all6 checks PASS; same manifest/source |

## Foundation controlled evidence

Older foundation JSON/log artifacts are consolidated here. Historical full audit remains in
[checkpoint PR351](https://github.com/katasec/mission-control-language/pull/351) and the prior Git commits;
current raw artifacts are removed rather than duplicated. Current final delivery observations are
[in the delivery table](#foundation-delivery).

| Controlled observation | Recorded outcome / limit |
|---|---|
| Public native feasibility | R10 public-control probe64/64 and independent rerun PASS, zero managed build warnings. Uses existing native geometry/menu/Paste; does not prove continuous rich selection. |
| Mutation retirement | R12 requires synchronous snippet/source retirement before native content replacement or detach; reviewed controlled regression proves stale actions are not kept alive until layout. |
| Light/dark reference | Existing SVG galleries remain binding. Csharp selected syntax minimum contrast4.89 light/4.26 dark; broader language coverage and physical Retina acceptance are separate. |
| Final controlled foundation | Focused181/181, package26/26 and full canonical873 passed/10 existing prerequisite skips; earlier failing intermediate tests were corrected, not accepted. |
| Pre-merge canonical source | [Run37407903290](https://github.com/katasec/forge-mcl/actions/runs/37407903290), merge candidate `c4536e416e89931873ed6ad9171f308c7d8c66ca`, exact reviewed tree `90e0f26f2adc1ffb9ed79e96379bc9f01b709d47`. Managed/native output zero compiler/linker warnings; native SHA256 `eee92831abced7a7f97210ed9bae7bde6b430df135a97302c35e3c523ea3f9b8`. |
| Local AOT limit | Local publish exited0 with six linker warnings: zero-warning FAIL. Canonical PASS did not relabel or waive this result. |
| Physical/default acceptance | Pending operator; controlled backends and fixtures never prove installed Ghostty delivery or Retina appearance. |

Normal top-level Ghostty config baseline SHA256:
`e2a910c754718b31d2e2a8e86d976d446d2bf57968cff3e322652b10486fdd49`.
It contains only font-size16; static reading/hashing does not prove physical shortcut behaviour.
Report consolidation is documentation-only:91 older files removed, all old evidence links redirected,
exactly one tracked/physical file remains in this evidence directory;9 Markdown/428 local links and
whitespace checks PASS. This cleanup adds no product/default-path behaviour.


## Mac patch verification and pre-code elapsed allocation

[Product PR63](https://github.com/katasec/forge-mcl/pull/63) is draft, head
`742e5ed`, six files/483 insertions/4 deletions. Both source code reviews found no required source changes; the style reviewer's native-warning gate remains open. [Canonical run37456671786](https://github.com/katasec/forge-mcl/actions/runs/37456671786) started11:28:48 UTC and is in progress; no native PASS yet. Managed build:0 warnings/0 errors,11.70s. Focused81 passed/0 failed/0 skipped.
Full process-scoped key-unset suite:924 passed/0 failed/6 existing prerequisite skips,930 total,
2m12s. Controlled process and shell tests did not invoke open/Ghostty. Root independently read
full command entry and RunAsync: all preflights precede direct launch return before HTTP/client
composition. This is source evidence only. Existing RunAsync complexity16 versus15 has a
scoped supervisor exception in the active spoke; no score-only refactor broadened the patch.

| Elapsed activity before coding | Duration | Contents |
|---|---|---|
| Scope, investigation and design corrections |14m30s| First candidate and correction; root work partly overlapped investigation |
| Implementation plan and correction |6m22s| First plan5m30s; revised verification wording52s |
| Four review rounds including handoffs |27m52s| Design12m22s +4m45s; plan6m07s +4m38s |
| Supervisor documentation, approval and handoffs |10m12s| Recording, cleanup, assignments, branch and approval |
| Total scope start→coding assignment |58m56s|10:15:44→11:14:40 UTC |

These are elapsed allocations, not exclusive person-hours or measured active reviewer thinking.
Review sequence: simplicity→ownership for initial design, repeated after the design correction;
then simplicity→ownership for the initial plan, repeated after evidence-layer clarification.
Eight individual assignments ran sequentially. Initial design changes fixed unsupported Mac
options and exact context transport; the first plan correction changed evidence wording, not
product files/APIs/scope. Initial review boundaries are partly unavailable above; no fabricated
individual duration is supplied. The largest measured pre-code block is review cycles27m52s.


Canonical CI product/package step completed successfully and Native AOT started11:34:01 UTC.
Root fetched PR63 merge candidate `32af6c2b8a15d0b86502232a382cff16d0921582` and independently
matched its tree to the reviewed head: `415174ec3f572ae88d9f059db0c7e34bbe1f5057`.
Full artifact/log inspection and Native AOT completion remain pending; no native PASS recorded.

At operator request, the event table was exported outside the repository to
`/Users/ameerdeen/tmp/phase70-event-timing.md`, with independently checked elapsed durations,
unknown timing marked and wall-clock totals58m56s before coding /1h07m37s through implementation
handoff. The export is a convenience copy, not a second evidence archive or product file.

## Future assessment — development checks and release compilation

Operator clarified2026-10-06: the requested assessment is **fast development cycles without
AOT**, not chiefly eliminating duplicate delivery builds. Assess managed build/run/test feedback
so development iterations do not require or wait for AOT compilation. Reserve AOT for final
verification and publishing after development is finished. Current local checks already use
managed builds; assess the complete development feedback path and when native CI runs. See
[backlog](../../backlog.md). No workflow or gate change is approved or implemented by this
note; the current patch continues through its locked delivery route.
Static development fact: the managed Debug build produces a124696-byte `forge` apphost
without running Native AOT. Assess the interactive Mac chat feedback path as well as tests;
no agent launch or physical managed-chat acceptance was performed.

Canonical PR63 run37456671786 completed SUCCESS at12:00:41 UTC; verify job31m44s,
11:28:56→12:00:40. Product/package step4m42s; full920 passed/10 existing prerequisite skips,
930 total; package26/26. Native publish11:34:01→12:00:18 =26m17s. Root read complete workflow
and artifact logs: zero actual compiler/linker warnings. GitHub's Node20 deprecation annotation
is workflow tooling, not a product compiler warning. Package publication was SKIPPED as required.

Artifact source.txt is `32af6c2b8a15d0b86502232a382cff16d0921582`; version is
`1.0.0+32af6c2b8a15d0b86502232a382cff16d0921582`. Root independently matched its tree to
reviewed HEAD and matched downloaded binary SHA256 to its recorded digest:
`4cb027520febfad558902755118f7d37fa35fc7f89cde4f6220acf917ced5db5`.
Downloaded native help/version checks are in progress; current final reviewer verdict and merge
remain pending. A seven-line Cartesian test fixture's third nested foreach received a scoped
supervisor test-only exception/removal condition in the active spoke, with no source change.

Final full simplicity/style review R2 ran12:07:36→12:10:05 UTC: all11 simplicity and all9 style
checks PASS with the two recorded scoped exceptions. Ownership remains current on the unchanged
six-file source. Root actual downloaded native --help/--version both exited0, exact CI source
identity matched. No source corrections were requested. Supervisor readiness gate passes:
approved scope/owners/identity/failure contracts retained; focused/full/package/current zero-warning
AOT evidence and identical reviewed/CI tree independently checked; compiler warnings not waived;
UI live readiness uses the operator-approved Phase70 exception and remains physical acceptance
pending. Product merge/release/install follows the normal route.

Product [PR63](https://github.com/katasec/forge-mcl/pull/63) merged at12:10:58 UTC,
commit `b11e6e3ba1f18b13e28d7c10dd3c12937c2e2059`. Product scope→merge span:
10:15:44→12:10:58 =1h55m14s; operator acceptance is later and still open. Root rechecked
remote main and unused v0.9.5 tag before dispatching the unchanged normal release workflow.
[Release run37461528621](https://github.com/katasec/forge-mcl/actions/runs/37461528621)
created12:11:43 UTC from that exact merged main commit, all four native hosts. Release,
whole-ZIP installation and physical acceptance are pending; no release/install PASS yet.


## Mac release and installation — verified; manual acceptance open

| Check | Named observation |
|---|---|
| Product merge | PR63 merged12:10:58 UTC; `b11e6e3ba1f18b13e28d7c10dd3c12937c2e2059`, same reviewed/CI tree. |
| Release | [Run37461528621](https://github.com/katasec/forge-mcl/actions/runs/37461528621) SUCCESS; created12:11:43, completed12:42:40 =30m57s. [v0.9.5](https://github.com/katasec/forge-mcl/releases/tag/v0.9.5) published12:42:37. |
| Native hosts | All four native build/sign/archive/help/version jobs PASS; root inspected complete compiler/linker output, zero actual warnings. Mac publish12:12:20→12:41:35 =29m15s; Linux x649m45s, Linux ARM13m36s, Windows ARM15m51s. |
| Assets | Eight assets; all four ZIP sidecar hashes match GitHub asset digests. Mac ZIP independently downloaded and hashed; other platforms not downloaded/executed locally. |
| Installation | All seven unchanged payload files atomically installed12:46:54.745 UTC under `/Users/ameerdeen/.local/bin`; every installed SHA256 matches the extracted payload. Prior files backed up in temporary storage; unrelated files preserved. |
| Installed identity | Normal PowerShell PATH resolved `/Users/ameerdeen/.local/bin/forge`; actual `forge --help` and `forge --version` exited0. Version `0.9.5+b11e6e3ba1f18b13e28d7c10dd3c12937c2e2059`; extracted and installed strict ad-hoc signature checks PASS. |
| Shared config bytes | `/Users/ameerdeen/Library/Application Support/com.mitchellh.ghostty/config.ghostty` SHA256 still baseline `e2a910c754718b31d2e2a8e86d976d446d2bf57968cff3e322652b10486fdd49`. Static bytes only; native preferences/key behaviour require operator observation. |
| Manual/default acceptance | Pending operator. Initial window/composer/editor/quit/ordinary-terminal check requested after installation. Both themes, normal/narrow Retina, mouse focus/modal, graceful errors/additional dedicated surfaces and retained Ctrl+A/Copy remain in Done when; no physical PASS inferred. |

Mac ZIP SHA256: `306e2715bee2b51731ebcbad3c85e19b4fc723ff125c255a3d6a23493f4e05f5`.
Installed binary SHA256: `18da97d27d34264a5818108a03693cc051484eb0a7c86afd11e773be8ed8ef4a`.

Timing: estimate given11:18:39 UTC was80–90 minutes to installation, around16:40–16:50 Dubai.
Actual installation12:46:54.745 UTC /16:46:54.745 Dubai =88m15.745s after that estimate.
Scope→installation10:15:44→12:46:54.745 =2h31m10.745s; product merge remains the timing-span
end for workflow accounting, and installation/operator acceptance are recorded separately.
The Native AOT stages alone took26m17s pre-merge plus29m15s release =55m32s; their elapsed
sum is not a claim of exclusive critical-path cost. Current delivery is implemented/installed,
awaiting manual acceptance, with no package-visibility or rich-selection closure implied.

## Icon-only Copy follow-up

Requirement: remove the persistent Copy code words beside the snippet icon. The operator selected
exactly `return glyph;` in the existing CodeCopyButton.Label. Idle/success/failure remain
`⧉`/`✓`/`!`; full TooltipText, dimensions, padding, styles, activation, immutable payload and native
clipboard/lifecycle remain unchanged. Owner is the existing Forge CLI snippet presentation.
Security tier/store/identity/credential/contracts are N/A for this presentation-only change;
Engineering Philosophy reuses one owner/path without new side effects, dependencies or knobs.
Desktop Interaction Principles/UI Design System bind the existing light/dark icon-only state
fixtures within unchanged native button bounds. The former width/layout proposal was withdrawn.
No new solution design is needed: operator explicitly locked the exact line. Existing assertions
are aligned with that line; no new tests/components/public APIs or workflow changes.

Operator evidence on 2026-10-06: first reported Cmd+A works in v0.9.5, later edited the label and
ran `make install`, reported the local AOT build compiled/worked, then stated “Everything works
now. both changes. commit andmerge plesae”. This confirms both requested fixes by operator manual
observation. Exact local build flags, terminal dimensions, theme matrix and warning output were
not supplied; no additional cases or canonical zero-warning result are inferred. No agent-operated
Ghostty, capture, input or clipboard occurred. The earlier fast managed development iteration was
stopped before any delivered dev executable; normal final checks resume for requested merge.

Scope/merge continuation observed at 2026-10-06 14:35:24 UTC. Operator implementation happened
before that boundary; its exact start/end are unavailable. Existing tests aligned in commit `716ec7b0dbe6af9c7bbc84282a95c9ed0cb0e176` after
operator source commit `8b13747`. [PR64](https://github.com/katasec/forge-mcl/pull/64) opened
14:44:04 UTC. Four files, 45 insertions/19 deletions; executable behavior changes only the
existing label return. No new tests or production mechanisms. Cmd+A PR63 remains already merged.

Managed `make build` completed in11.63s with0 warnings/errors. Existing focused button/tile/motion
run passed69/69, zero failures/skips,17s; logs remain temporary at `/tmp/phase70-icon-only/`.
Supervisor read the complete source/test diff and focused/build logs. Current full simplicity/style
source review14:45:25→14:46:24 UTC PASS; all11 simplicity and8 source-style checks pass. The
remaining zero-warning style condition was satisfied by current canonical Native AOT without
source changes or another design/review round. Manual classic
complexity Label3, TooltipText3, AssertLabel1, Find3, changed tests/callbacks≤11. No source correction
or new exception. The operator's commented-out return is not an executable legacy path.

GitHub initially exposed no PR checks; the supervisor reopened the same PR14:46:30 UTC, without
source changes. Two same-head runs appeared; the older duplicate37481790069 was canceled to avoid
redundant work. Current [run37481849696](https://github.com/katasec/forge-mcl/actions/runs/37481849696)
created14:46:58 UTC; verify job started14:47:07 and product/package checks14:47:28.
Product/package step passed14:51:33 UTC (4m05s); Native AOT began14:51:33 UTC.
The workflow is unchanged, PR-event verification only; package publication is not requested.

Current ownership review14:47:07→14:49:24 UTC PASS: all5 behaviors and6 persona checks, actual
four-file head716ec7b. Both reviewers independently read the diff; no source correction requested.
The supervisor separately checked the existing native glyph mapping, retained TooltipText,
unchanged geometry/transport/launch and the complete assertions. Native/full verification passed,
and no new CLI release is part of the operator's commit/merge request.

Local installed observation at14:47:07 UTC: normal executable version is
`1.0.0+b11e6e3ba1f18b13e28d7c10dd3c12937c2e2059`, SHA256
`d1aa1210a2a02bb5cd52020328d8a5396430ae56c4166ce9e77cc660c7df5b2d`.
The version identifies the base commit, not the uncommitted label change at operator build time;
this is the operator's local make-install artifact, not a newly published CLI version.

Canonical run37481849696 completed successfully: managed build0 warnings/errors; full suite920
passed,10 prerequisite skips,0 failures; Terminal Extensions26/26,0 skips; package metadata,
contents and exact native dependencies PASS. Native publish passed with0 actual compiler/linker
warnings; help/version identity PASS. Package publication correctly skipped on the PR event.
Supervisor read the actual workflow output and warning gate. Verified candidate
`98a220ca8c6b073f1fa399f6ace116771497bfcd` and reviewed head716ec7b both have tree
`9944cebdfea93c04a06604cbebe679cce3ce6eac`; merged main has the same tree.

| Event | Start UTC | End UTC | Elapsed |
|---|---|---|---|
| Test alignment and local checks | 14:41:20 | 14:43:44 | 2m24s |
| Simplicity/style source review | 14:45:25 | 14:46:24 | 59s |
| Ownership review | 14:47:07 | 14:49:24 | 2m17s |
| Canonical verification job | 14:47:07 | 15:20:40 | 33m33s |
| Native publish within that job | 14:51:33 | 15:20:14 | 28m41s |
| Product PR64 merged | — | 15:22:36 | Timestamp only |
| **Resumed scope through product merge** | **14:35:24** | **15:22:36** | **47m12s** |

Rows overlap and must not be summed. These are elapsed stage durations, not measured agent idle
or provider queue time; no queue-delay claim is supported. PR64 merged as
`d212e8149b022479bc4ad3f138c8b0ac07302e61`; forge-mcl is clean on main, matching origin/main.
Raw workflow logs remain outside the repository in `/tmp/phase70-icon-only-ci/`.
The single report records this delivery; package visibility and unreported broader manual cases
remain open. Documentation-only reconciliation has default-path acceptance N/A.
