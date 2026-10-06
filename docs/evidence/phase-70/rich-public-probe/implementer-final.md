**The pinned public APIs do not provide the point/range boundary needed for a clean continuous-selection adapter.** The native controls preserve local ranges and highlighting, but the probe did not demonstrate continuous selection across Paragraphs or through image headings.

| Case | Actual observation, both themes |
|---|---|
| Two Paragraphs, repeated words | Drag from first Paragraph `(0,0)20×2` into second `(0,3)20×2` copied only `echo echo echo tail` from the first; second had no selection. |
| Reflow 20→8 columns | First Paragraph became `8×5`; its existing selected text remained exact. Native local reflow works. |
| Repeated wrapped rows | Separate drags across the first and second `echo` rows both extracted `echo`. Public extraction exposes neither occurrence’s endpoints. |
| Interior wrapped range | A genuine two-row drag extracted `cho ec`, demonstrating native logical mapping beyond flat cell counting. |
| Graphemes | Native drag copied exact `é 👩‍💻 `, preserving the combining mark and ZWJ sequence. |
| Image heading→code | Actual linked `HeadingImage` is hit-testable but is not an `ISelectionOwner`. Heading→code and code-start→heading drags produced no selection/write. |
| Heading reflow | Logical `echo echo heading` rendered as one image line at width20, then `echo` / `echo` / `heading` in six rows at width8. Existing graphics/font ownership was reused with no-op Kitty transport. |
| Code highlighting | Selected native StyledRuns retained distinct foregrounds over theme selection backgrounds: light `#dbe7fb`, dark `#16345a`. Code range `echo echo\n  ec` survived reflow. |
| Public drawing seam | `OverlayCellStyle` changed a known terminal cell’s background while retaining its foreground. It supplies no logical range geometry. |
| Narrow code divergence | Width20 renders the emoji in `echo echo\n  echo 👩‍💻\n`; width8’s native frame omits it. Whole native selection still copies that exact payload with one write. Cause remains unresolved. |

The point/range gap is established by source inspection **and an actual public consumer compile rejection**:

| Seam | Visibility/evidence |
|---|---|
| [ISelectionOwner](/tmp/phase70-public-sources/XenoAtom.Terminal.UI/src/XenoAtom.Terminal.UI/Input/ISelectionOwner.cs:15) | Public participation, selection state, clearing and extraction only. |
| [Paragraph mapping](/tmp/phase70-public-sources/XenoAtom.Terminal.UI/src/XenoAtom.Terminal.UI/Controls/Paragraph.cs:924) | Private; consumes the private wrapped-line cache, alignment and segment/grapheme/tab mapping. |
| [Paragraph range setter](/tmp/phase70-public-sources/XenoAtom.Terminal.UI/src/XenoAtom.Terminal.UI/Controls/Paragraph.cs:1275) | Private; directional endpoints also remain private. |
| [TerminalTextUtility](/tmp/phase70-public-sources/XenoAtom.Terminal/src/XenoAtom.Terminal/TerminalTextUtility.cs:221) | Public flat-slice cell indexing exists; it does not expose Paragraph’s physical-line start or layout mapping. |
| [MarkdownDocumentContent](/tmp/phase70-public-sources/XenoAtom.Terminal.UI/src/XenoAtom.Terminal.UI.Extensions.Markdown/MarkdownDocumentContent.cs:26) | Public block materialization, without canonical text/source-range metadata. |
| [CellBuffer overlay](/tmp/phase70-public-sources/XenoAtom.Terminal.UI/src/XenoAtom.Terminal.UI/Rendering/CellBuffer.cs:268) | Public styling seam, without hit/range mapping. |

`ApiNegative.csproj` rejects `GetTextIndexFromPosition` and `SetSelection` with CS1061. No private call, reflection or synthetic routed-event raiser executed.

The conditional comparison is recorded in [comparison.md](/tmp/phase70-rich-public-probe/comparison.md):

| Route | Concrete cost and boundary |
|---|---|
| Unchanged-package adapter | Must reproduce wrapped source slices, skipped whitespace, prefixes/indents, alignment, clipping/ellipsis, absolute-column tabs, grapheme mapping and invalidation before it can place ranges. Inspected mechanisms span 890 physical source lines; **500–800 added adapter lines is an estimate**, not an implemented result. |
| Minimal native seam / hypothetical bounded fork | Public point mapping and directional range read/set could delegate the existing machinery and retain native selection painting. Estimated **40–80 facade lines**, excluding tests, coordinator, semantics, graphics, package maintenance and any narrow-render correction. No patch or fork was created. |
| Shared Forge work | Either route still needs logical heading spans through existing TextArt wrapping/cutting, full-context GlyphText positions, selected-image rendering, document semantics and identity/lifetime rules. Native Paragraph access alone cannot close these. |

UI ships its SourceGen analyzer; Markdown depends on UI/SourceGen; TextMate depends on UI/Markdown/SourceGen. A fork therefore requires a compatible three-package family and generator packaging, rather than a lone UI DLL.

Foundation currently pins official UI exactly `[3.10.0]`. A changed native package identity/version requires a newly reviewed immutable extension release and coordinated updates to **CLI, extension and tests**—the only native consumers found across the canonical Forge repositories. Desktop has an atlas row only. Package IDs/versions and ownership remain an operator Type-1 decision. Authenticated restore, provenance, family compatibility, full regressions and warning-free product AOT are all unperformed for that hypothetical route.

Owner/reuse facts:

| Requirement | Existing owner |
|---|---|
| Native text geometry, local ranges and styled rendering | XenoAtom Paragraph |
| Transcript ordering, claims and Copy-versus-Stop | Forge CLI TUI, [README](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/README.md:20) |
| Heading logical text, wrapping, font metrics and rasterization | Existing HeadingImage/TextImages/TextArt/GlyphText, [graphics inventory](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/README.md:80) |
| Truthful clipboard results/fixed menus | Foundation extension |
| Clipboard transport | Unchanged XenoAtom.Terminal |

Artifacts are under [/tmp/phase70-rich-public-probe](/tmp/phase70-rich-public-probe). Current commands:

```text
dotnet build /tmp/phase70-rich-public-probe/Probe.csproj -warnaserror
dotnet run --project /tmp/phase70-rich-public-probe/Probe.csproj --no-build
dotnet build /tmp/phase70-rich-public-probe/ApiNegative.csproj -warnaserror
```

Positive build/run exit0, **zero warnings/errors**, 30 raw observation rows. Negative API compile exits1 with the expected two errors and zero warnings. Initial scratch harness failures remain separately retained. [Full logs](/tmp/phase70-rich-public-probe/run.log), [observations](/tmp/phase70-rich-public-probe/observations.json), [manifest](/tmp/phase70-rich-public-probe/manifest.json).

Manifest SHA256: `5ED52E3E7779D16889CA5455C4B24DA9CCB6D473B5FCC156E48DA0D3B00D2FA4`. Program SHA256: `5F23DFA990D2C37959693FD0C135BE42EF5FDDE0FB19F12829DA63955826754B`. Pinned Paragraph SHA256: `9FEC3A3B922B7B2A7F7D41D653DF0215DC69D78FC2A15400CF25F8292E48687D`.

Open: library route and complete public contract; all promised content ordering/separators, headings, aligned tables/lists/quotes, card/user/chrome boundaries, streaming/collapse/scroll identity, gestures/actions, menus/lifetime and selected-heading visuals. The narrow emoji rendering divergence also needs explanation. No product files changed; no rich design approval, AOT, installed default-path or Retina acceptance is claimed.
