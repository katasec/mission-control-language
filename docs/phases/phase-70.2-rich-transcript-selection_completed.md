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

Thirty controlled observations/root rerun verified; essential public point/range gap found.
See [probe and comparison report](../evidence/phase-70/verification-report.md#rich-selection-public-probe-and-comparison).

## Conditional comparison review — R1 and current R2

R1 REVISE corrected direction-losing ordered ranges and conditional family repacking.
See [corrections and review results](../evidence/phase-70/verification-report.md#rich-selection-public-probe-and-comparison).

## Current comparison R2 — both independent reviews PASS

All simplicity/ownership checks PASS for investigation only; rich design and library route
remain open. See [review results](../evidence/phase-70/verification-report.md#rich-selection-public-probe-and-comparison)
and [stage timing](../evidence/phase-70/verification-report.md#investigation-timing-and-limits).

## Narrow native Paragraph isolation — 2026-10-06

Twenty exact Copy cases passed; seven rendering losses isolated to leading-space wrap budgeting.
No fix approved; see [diagnosis and limits](../evidence/phase-70/verification-report.md#narrow-native-paragraph-diagnosis).
