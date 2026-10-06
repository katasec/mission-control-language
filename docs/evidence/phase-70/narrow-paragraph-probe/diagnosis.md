# Narrow Paragraph isolation — scratch findings

20 native in-memory cases completed. Build and run exit 0; build reports 0 warnings and 0 errors. Every genuine native pointer selection and Ctrl+C wrote the complete exact source once, including leading spaces, combining marks, the ZWJ sequence and trailing newlines. Rendering loses content in seven cases; those observations are negative evidence, not a product PASS.

## Matrix

All cases use native Paragraph.Wrap=true, native DefaultLight, native full-loop completed frames, a linked unchanged TerminalInteractionTestHost and native public pointer events. No production source is compiled into the scratch except that existing test host. Hard=false is a single source hard line; it does not disable wrapping. Hard=true source is `echo echo\n` + the identical body + `\n`. Body is zero/two literal spaces + `echo ` + suffix. Width is terminal cells. Full source, bounds, selected text, clipboard text and both 20-line raw styled frames are in observations.json and run.log.

| Width8 body | No leading spaces, both hard variants | Two leading spaces, both hard variants |
|---|---|---|
| ASCII XY (7/9 cells) | Entire suffix visible | Only X visible; Y clipped |
| ZWJ woman-technologist (7/9 cells) | Entire suffix visible | Entire two-cell grapheme absent |
| Combining e+U+0301 (6/8 cells) | Entire suffix visible | Entire suffix visible |
| CJK U+4E2D (7/9 cells) | Entire suffix visible | Entire two-cell character absent |

All width8 single-hard-line cases have bounds (0,0) 8x1, even the nine-cell indented bodies. All width8 hard-line cases have bounds (0,0) 8x4: first hard line wraps, body remains one physical row, final hard line is empty. Selection highlighting preserves the same visible/clipped content; it does not make clipped suffixes appear. Raw selected ASCII row is `[bold #02060b on #dbe7f8]  echo X[/]`; raw selected ZWJ/CJK body row is `[bold #02060b on #dbe7f8]  echo [/][#02060b on #eef5f9] [/]`. These are native diagnostic frames, not an offset parser.

For indented ZWJ hard-line source, width7 and width8 clip the suffix with bounds 7x4 / 8x4; width9,10,20 show it with bounds 9x3 / 10x3 / 20x3. Width7 raw body is `  echo `; width8 raw body is `  echo  `; width9 row is `  echo ` followed by the intact ZWJ grapheme.

## Source-supported diagnosis and limits

Published UI3.10.0 nuspec names source commit 6f4e0cde3890d8ce2510ac0451b861863e4aeeaa. Paragraph constructor line48 enables wrapping. Its private TryGetNextWrapSlice at lines753–805 skips source-leading whitespace at763–766, then budgets cells against text[start..] at773; it returns only endExclusive/nextStart. Its caller at630–641 still constructs the span using the original localStart, thereby including those skipped leading spaces. AppendLayoutLine at681–708 measures the resulting original span without shortening it. RenderOverride at273–294 passes the complete stored span to WriteStyledSpan; the latter delegates to CellBuffer.WriteText at388/447. CellBuffer.WriteText at357 stops at buffer width; at384–387 it refuses an entire grapheme when posX+width exceeds width.

For the exact nine-cell body at viewport8, this source path explains an over-budget single wrapped span: wrap budgeting sees the seven-cell suffix body after two leading spaces, the stored span still contains nine cells, ASCII Y falls beyond the right edge and a two-cell ZWJ/CJK element beginning at column7 is rejected. The eight-cell combining case fits. The single-source-hard-line cases show that an earlier hard line is not necessary. This is a source-supported mechanical diagnosis corroborated by the matrix, not inspection of private runtime cached fields or a tested patched fix. No wrapping/hit mapping algorithm was copied or patched. It is not established that this is the only narrow-layout defect; tab, prefixes, alignment, other grapheme sequences and alternate wider host clip boundaries were not investigated.

Expected required result is that native wrapped body text remains meaningfully visible within its bounds while selection copies exact source. Here native Copy succeeds while rendering clips source characters. Resolving whitespace budget/slice semantics remains a native-owner design question before a patch; this investigation does not approve a fix or library route. The previous rich-selection public-geometry gaps and family comparison remain unchanged.

## Verification and ownership

- `dotnet build /tmp/phase70-narrow-paragraph-probe/Probe.csproj -warnaserror`: exit0; zero warnings/errors.
- `dotnet run --project /tmp/phase70-narrow-paragraph-probe/Probe.csproj --no-build`: exit0;20 exact source Copy assertions; seven rendering loss observations.
- `dotnet list ... package --include-transitive`: UI3.10.0, Terminal2.2.0; transitive Ansi1.7.1 and Wcwidth4.0.1.
- Existing reliable public host acknowledges preceding relay enqueues with InputBatchEnd, posts acknowledgement, then waits through the native pending-input drain and completed render before the next phase. No larger timing sleeps, private APIs or synthetic routed event raising.
- UI Paragraph owns wrap/slice/render/hit/range machinery; Terminal transport and memory backend are reused. CLI has no role in this isolated native defect. No product source, package, default, graphics or theme changed.
- forge-mcl main d23387492de4563abe9a57d5a51dfb538745f725 and Desktop main39e3ba2eec498d09048cc79bcee2ffcb7cdf2cd0 remain clean. Earlier rich probe manifest and native source hashes remain unchanged.
- Native AOT, installed default-path, physical Ghostty/Retina, full rich semantic contract and product acceptance are N/A to this scratch investigation. DefaultLight here is isolation only; eventual UI principles/design-system/TUI-graphics and operator both-theme gates still apply.

manifest.json records raw log, source/API/helper/package/output hashes. commands.json preserves the actual checks. Source excerpt diagnostics are source-api.log; package provenance is package-provenance.json. No secrets or non-test clipboard payloads were used.
