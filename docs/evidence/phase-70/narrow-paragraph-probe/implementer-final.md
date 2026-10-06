The width8 defect is **leading-space wrap budgeting, not specifically emoji rendering**. All 20 native drag→Ctrl+C cases copied exact source once; seven cases lost rendered characters.

| Case | Actual native observation |
|---|---|
| Width8, no indentation | ASCII, ZWJ, combining and CJK suffixes visible, with/without hardlines |
| Width8, two leading spaces | Nine-cell ASCII body loses `Y`; nine-cell ZWJ/CJK body loses the entire final grapheme |
| Width8, indented combining mark | Eight-cell body remains fully visible |
| Indented ZWJ, widths7/8→9/10/20 | Clipped at7/8; visible at9/10/20 |
| Native selection | Clipping unchanged by highlight; copied source retains every character and newline |

All single-hard-line width8 bounds were `(0,0) 8x1`, including nine-cell bodies. Hardline variants were `8x4`. Wrapping remained enabled in every case.

The pinned [Paragraph.cs](/tmp/phase70-public-sources/XenoAtom.Terminal.UI/src/XenoAtom.Terminal.UI/Controls/Paragraph.cs:753) helper skips leading whitespace before measuring the wrap budget. Its caller retains the original slice start at630–641, thereby restoring those spaces into the rendered span. [CellBuffer.cs](/tmp/phase70-public-sources/XenoAtom.Terminal.UI/src/XenoAtom.Terminal.UI/Rendering/CellBuffer.cs:384) then rejects a two-cell grapheme crossing the right edge. This source path explains the observed matrix; private cached fields were not inspected and no patched fix was tested.

Frozen evidence:

- [Findings and limits](/tmp/phase70-narrow-paragraph-probe/diagnosis.md)
- [Exact sources, bounds, selections and raw frames](/tmp/phase70-narrow-paragraph-probe/observations.json)
- [Full build log](/tmp/phase70-narrow-paragraph-probe/build.log), [run log](/tmp/phase70-narrow-paragraph-probe/run.log), [commands](/tmp/phase70-narrow-paragraph-probe/commands.json)
- [Source excerpts](/tmp/phase70-narrow-paragraph-probe/source-api.log), [package hashes](/tmp/phase70-narrow-paragraph-probe/package-provenance.json), [manifest](/tmp/phase70-narrow-paragraph-probe/manifest.json)

Build and run exited0; **zero warnings/errors**. UI3.10.0 and Terminal2.2.0 resolved unchanged. All four dependency cache/output DLL hashes matched.

Manifest SHA256: `A6D6101B3D261374F4E8917EBB4F57C31E317195749DBB9831BFE5BF7A0D840B`.

Both product repositories remain clean on their merged main commits. Previous rich probe and native source hashes remain unchanged. Native Paragraph owns the required wrap/range correction; whitespace semantics and any library delivery route still require supervisor design. AOT/default-path/Retina acceptance are N/A to this scratch investigation.
