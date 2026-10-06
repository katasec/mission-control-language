# Phase 70.2 — Verified source observations

This records source/controlled observations, not rich-selection design approval, a clean
continuous adapter, product implementation or installed acceptance. The bounded probe below is verified; the full rich contract and route remain open in the
[active spoke](phase-70.2-rich-transcript-selection.md).

## Source boundaries — 2026-10-06

The supervisor inspected the already-pinned UI source at
`6f4e0cde3890d8ce2510ac0451b861863e4aeeaa` and current Forge graphics source while the approved
foundation implementation was running. No native source, package or product code was changed.

| Observation | Consequence to prove before selecting a route |
|---|---|
| [ISelectionOwner](https://github.com/XenoAtom/XenoAtom.Terminal.UI/blob/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src/XenoAtom.Terminal.UI/Input/ISelectionOwner.cs) exposes participation, nonempty-selection state, clearing and text extraction. It exposes no range endpoints or point-to-text map. | A document coordinator cannot assume those capabilities follow from clipboard extraction. |
| [Paragraph's point mapping](https://github.com/XenoAtom/XenoAtom.Terminal.UI/blob/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src/XenoAtom.Terminal.UI/Controls/Paragraph.cs#L919), wrapped-line cache and [range setter](https://github.com/XenoAtom/XenoAtom.Terminal.UI/blob/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src/XenoAtom.Terminal.UI/Controls/Paragraph.cs#L1275) are private. Paragraph is sealed. Visual's routed-event raiser is protected internal. | Neither a Paragraph subclass nor an assumed public synthetic-pointer call is a demonstrated mapping adapter. No private access is authorized. |
| Native mapping accounts for hard lines, word wrapping, discarded whitespace, first/continuation prefixes and indents, alignment, clipping/ellipsis, grapheme widths and absolute-column tab stops. | Counting cells in Text is insufficient by itself. Compare an actual adapter against these cases and native reflow; do not estimate its cost from the word-wrap helper alone. |
| [Native Markdown table cells](https://github.com/XenoAtom/XenoAtom.Terminal.UI/blob/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src/XenoAtom.Terminal.UI.Extensions.Markdown/MarkdownDocumentBuilder.cs#L597) use wrapped Paragraphs with per-column left/center/right alignment. Lists/quotes use native indent and prefix fields. | A left-aligned body-only probe cannot close table/list/quote support. |
| [MarkdownDocumentContent](https://github.com/XenoAtom/XenoAtom.Terminal.UI/blob/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src/XenoAtom.Terminal.UI.Extensions.Markdown/MarkdownDocumentContent.cs) exposes construction, BlockCount and GetBlock; its builder and source model remain private. | Public visual materialization is a candidate semantic seam, not an existing canonical offset map. |
| Forge HeadingImage retains its logical constructor text; TextImages delegates wrapping to TextArt. TextArt splits words, collapses spaces and cuts overlong words with ellipsis. GlyphText already owns font advances, kerning and coverage of a character interval laid out in a complete line. | Reuse the graphics owner. Retain logical source offsets through wrap/cut; searching repeated displayed words cannot establish them. Selected-image appearance and pending terminal-heading mapping still need proof. |
| The pinned Markdown project references native UI; TextMateSharp references both native UI and Markdown, and both use the existing source generator for build-time generation. Foundation's extension pins the unchanged UI package exactly. | A hypothetical fork is a compatible package-family and consumer/provenance/AOT decision, not just two extra methods in Paragraph. No fork, package-ID/version switch or upstream write is authorized. |

Forge sources inspected: `/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/Graphics/HeadingImage.cs`,
`TextImages.cs`, `TextArt.cs`, `GlyphText.cs`, and `Tui/ForgeMarkdown.cs`. The latter routes eligible
plain top-level headings through its existing per-process pseudo-fence marker. That rendering
path must remain authoritative.

The conditional comparison remains unchanged: first prove the bounded public adapter; compare a
fork only for a concrete essential gap or substantial duplicated native mechanism. Source
inspection alone establishes neither a clean adapter PASS nor approval to maintain a fork.

## Bounded public geometry probe — 2026-10-06

The reused implementer used genuine native backend pointer batches and completed-frame
acknowledgements with a controlled clipboard and no-op Kitty transport. The supervisor read the
entire probe and comparison, checked all 55 recorded source/artifact hashes and independently
reran the public consumer: exit 0, 30 rows across light/dark, every non-frame field equal.
Build: exit 0, zero warnings/errors. Negative public API compilation: expected exit 1,
CS1061 for point mapping and range setting, zero warnings/two errors. These are controlled
observations, not installed Ghostty/Retina acceptance or a rich-selection PASS.

| Case | Observed in both themes |
|---|---|
| Cross-Paragraph drag | Copies only first suffix `echo echo echo tail`; second has no selection. |
| Repeated physical rows | Separate first/second-row selections both copy `echo`; no public endpoints distinguish them. |
| Native wrapping/graphemes | Interior two-row drag copies `cho ec`; grapheme drag copies exact `é 👩‍💻 `; selected payload survives width 20→8. |
| Actual image heading | Linked HeadingImage is hit-testable, not a selection owner. Heading/code drags both return NoSelection, zero writes. Width 20→8 wraps its logical text into three image lines. |
| Styled code | Native ranges preserve Link/Success foregrounds over selection tokens; exact `echo echo\n  ec` survives reflow. Explicit StyledRuns, not a TextMate language-tokenizer proof. |
| Public cell overlay | Retains foreground while replacing a known cell background; supplies no logical mapping. |
| Narrow code divergence | Width 20 displays the ZWJ emoji; width 8 omits it from the recorded native frame while whole selection still copies exact `echo echo\n  echo 👩‍💻\n`. Initially unresolved; subsequently isolated [below](#narrow-native-paragraph-isolation--2026-10-06). No native correction authorized. |

The essential public point/range gap triggers the operator's conditional comparison. The
public route must reproduce native wrapped source slices, discarded spaces, prefixes/indents,
alignment, clipping/ellipsis, tab and grapheme mapping plus invalidation. Inspected native
regions total 890 physical lines; the proposed adapter's 500–800 added lines is an estimate,
not a built adapter. A hypothetical native map/read/set facade is estimated at 40–80 lines,
excluding tests, semantics, coordinator, headings, package maintenance and any narrow-render
correction. No patch/fork was made. Neither estimate proves total cost or build readiness.

Both routes still require canonical document ordering/separators and stable identities, source
spans through existing TextArt wrapping/cutting, existing GlyphText full-context font geometry,
selected-image rendering and all promised range/lifecycle/gesture cases. The native facade does
not create a Markdown semantic API. UI/Markdown/TextMate and their SourceGen packaging need compatible-family verification;
required repacking depends on the selected identity, as corrected below. Only Forge CLI, extension and tests currently consume native packages
across the canonical Forge repositories. A changed native identity/version requires a newly
reviewed immutable extension release; foundation 0.1.0's official exact pins remain valid.
No IDs, versions or fork ownership are approved.

| Evidence | Pointer |
|---|---|
| Complete conditional comparison | [comparison](../evidence/phase-70/rich-public-probe/comparison.md) |
| All raw observations/build/negative compile | [manifest](../evidence/phase-70/rich-public-probe/manifest.json), [observations](../evidence/phase-70/rich-public-probe/observations.json), [build](../evidence/phase-70/rich-public-probe/build.log), [negative API](../evidence/phase-70/rich-public-probe/api-negative-build.log) |
| Independent supervisor rerun | [verification](../evidence/phase-70/rich-public-probe/supervisor-verification.json), [full run](../evidence/phase-70/rich-public-probe/supervisor-run.log) |
| Complete implementer result | [final](../evidence/phase-70/rich-public-probe/implementer-final.md) |

Probe source/project, native/product source, fonts and binaries stay outside this mission-control
repository. Their actual paths and digests are retained in the manifest. Raw prior scratch
failures are retained separately; they are not the current build/run result.

| Stage | Start UTC | End UTC | Evidence |
|---|---|---|---|
| `[investigate:implementer:r1] 70.2 public geometry` | 2026-10-06T03:44:01.975Z | 2026-10-06T03:56:48.856Z | Complete final and supervisor verification above. |

## Conditional comparison review — R1 and current R2

Simplicity review returned REVISE for two comparison assumptions, not for the observed public
gap. The supervisor verified the published nuspecs and corrected the current comparison:
native ordered ranges lose anchor/active direction; published Markdown/TextMate dependencies
permit later UI versions, so repacking depends on chosen identity and proven binary/AOT
compatibility. Foundation's own exact UI pin still requires a new extension release for a change.
The frozen original comparison is retained as historical evidence; use [current R2](../evidence/phase-70/rich-public-probe/comparison-current.md).

The reviewer supported native-seam **design exploration** over duplicate wrap/hit code, with no
fork approval. At R1, skipped leading-whitespace measurement versus the retained native slice
was only a hypothesis. The subsequent [isolation below](#narrow-native-paragraph-isolation--2026-10-06)
varied indentation, Unicode and adjacent widths and corroborated that source path. No patch
was tested. Full semantics, heading selection and default-path acceptance remain open.

| Stage | Start UTC | End UTC | Evidence |
|---|---|---|---|
| `[investigate:simplicity:r1] 70.2 conditional comparison` | 2026-10-06T03:58:15.536Z | 2026-10-06T04:04:15.352Z | [All 11 checks](../evidence/phase-70/rich-public-probe/simplicity-r1-final.md), [metadata/proof](../evidence/phase-70/rich-public-probe/comparison-r2-verification.json). |

## Current comparison R2 — both independent reviews PASS

Ownership checked all 18 behaviours and six persona checks; simplicity rechecked all 11
checks against the complete current artifact. Both PASS **investigation comparison only**.
The supervisor accepts the source/controlled facts and the justification for native-seam
design exploration. This approves no fork, public contract, package identity or rich design.
Native geometry/ranges/painting stay with their existing owner; Forge source semantics,
headings and policy stay CLI; transport and truthful results retain their existing owners.
The complete contract and the operator's Type-1 route/identity decisions remain open.

| Stage | Start UTC | End UTC | Full current verdict |
|---|---|---|---|
| `[investigate:ownership:r1] 70.2 comparison` | 2026-10-06T04:05:59.510Z | 2026-10-06T04:08:40.503Z | [ownership-r1](../evidence/phase-70/rich-public-probe/ownership-r1-final.md) |
| `[investigate:simplicity:r2] 70.2 comparison` | 2026-10-06T04:09:29.743Z | 2026-10-06T04:10:40.911Z | [simplicity-r2](../evidence/phase-70/rich-public-probe/simplicity-r2-final.md) |

## Narrow native Paragraph isolation — 2026-10-06

Scratch investigation only: build/run exit0, zero warnings/errors, 20 genuine native
selection→Ctrl+C exact-source single-write cases, seven observed rendering losses. The
supervisor independently verified all 23 frozen source/artifact/assembly hashes, reran the
probe and matched all 20 complete observation objects including raw frames.

At width8, two leading spaces cause a nine-cell ASCII body to lose its final Y and nine-cell
ZWJ/CJK bodies to lose their complete two-cell suffix; the eight-cell combining case fits.
Unindented variants fit. Indented ZWJ clips at7/8 and fits at9/10/20. This occurs with and
without earlier hard lines; wrapping stays enabled. Highlighting does not restore clipped
text, but Copy retains every source character, leading space and trailing newline.

Pinned Paragraph's helper skips leading whitespace while budgeting, but its caller retains
the original slice start. CellBuffer then clips an over-budget span and rejects a two-cell
grapheme crossing the right edge. This is a source-supported diagnosis corroborated by the
matrix, not private-cache inspection or a tested fix. Native Paragraph owns correction;
whitespace/wrap semantics require design. No product patch, fork or package identity is
approved. DefaultLight isolation does not replace both Forge themes or operator Retina
acceptance. Tabs, prefixes, alignment and other clipping boundaries were not investigated.

| Stage | Start UTC | End UTC | Evidence |
|---|---|---|---|
| `[investigate:implementer:r2] 70.2 narrow Paragraph` | 2026-10-06T04:11:32.312Z | 2026-10-06T04:19:31.921Z | [Full findings](../evidence/phase-70/narrow-paragraph-probe/implementer-final.md), [diagnosis](../evidence/phase-70/narrow-paragraph-probe/diagnosis.md) |

[Raw observations](../evidence/phase-70/narrow-paragraph-probe/observations.json),
[build log](../evidence/phase-70/narrow-paragraph-probe/build.log),
[run log](../evidence/phase-70/narrow-paragraph-probe/run.log),
[source/API excerpts](../evidence/phase-70/narrow-paragraph-probe/source-api.log),
[manifest](../evidence/phase-70/narrow-paragraph-probe/manifest.json), and
[supervisor proof](../evidence/phase-70/narrow-paragraph-probe/supervisor-verification.json)
preserve full provenance. Manifest SHA256 `a6d6101b3d261374f4e8917ebb4f57c31e317195749dbb9831bfe5bf7a0d840b`.
Product source, scratch Program/CSProj and binaries remain outside mission control.
