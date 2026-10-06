# Phase 70 — Foundation delivery and selection investigation report

Recorded 2026-10-06. **Foundation CLI installed; package visibility, operator acceptance
and continuous rich-selection design remain open.** This report replaces the 71 individual
artifacts from the latest checkpoint, at the operator's request. Earlier foundation design,
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
retains historical audit provenance. The current repository uses this report instead of the
71 raw artifacts, intermediate runs and duplicate summaries. This consolidation changes
documentation only: runtime/default-path/security/UI implementation gates N/A.

## Mac shortcut scope checkpoint

Operator screenshots identify the chat input and `/edit` file editor. Installed Ghostty1.3.1
static source handles host bindings before PTY input; native Terminal2.2.0/UI3.10.0 already
decode and execute Ctrl+A Select All. A one-line Cmd+A forwarding candidate is documented in
[the shortcut spoke](../../phases/phase-70.3-mac-select-all.md). Its effect on other Ghostty
tabs is awaiting the operator's answer. No host setting or product code was changed.

| Documentation stage | Start UTC | Validation UTC | Evidence |
|---|---|---|---|
| Scope checkpoint | 07:40:51 | 07:46:45 | Nine Markdown files / 428 local links and anchors PASS; whitespace PASS. |

Product design/plan/code approvals, product tests/AOT and physical/default-path acceptance
are N/A to this documentation checkpoint; the shortcut remains unimplemented.
