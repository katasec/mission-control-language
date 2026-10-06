# Phase 70 — Foundation delivery and selection investigation report

Recorded 2026-10-06. **Foundation CLI installed; package visibility, operator acceptance
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
Operator decision pending: accept public visibility and review the corrected publication
contract, or retain private visibility and design a new identity/route. No retry, deletion,
overwrite or visibility-contract change was made. Delivery closure remains open.

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
