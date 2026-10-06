**PASS — current R2 investigation comparison.** The evidence supports native-seam design exploration. It does not approve a route, fork, public API, full rich design or implementation.

Reviewed [comparison-current.md](/Users/ameerdeen/progs/mission-control-language/docs/evidence/phase-70/rich-public-probe/comparison-current.md), SHA256 `d327b01d2103d8495f4327a4c641ec26e91a30ac1cbddba193193620a2a8e4be`. Independently checked all 55 manifest source/artifact hashes, read the complete probe, all 30 observation records, negative compilation and actual native source/package metadata.

Owners were derived from the atlas and current component READMEs before inspecting the comparison.

| Behaviour | Derived owner | Comparison placement | Verdict |
|---|---|---|---|
| Convert Paragraph pointer coordinates into logical offsets | Native UI Paragraph layout owner | Proposed facade delegates existing native mapping; public adapter duplication explicitly identified | PASS |
| Read directional anchor/active endpoints and set validated native ranges | Native UI Paragraph range owner | Native-seam exploration; ordered getter correctly rejected as direction-losing | PASS |
| Paint terminal text selection while preserving styled foregrounds | Native UI rendering | Existing native painting retained; public overlay distinguished from logical mapping | PASS |
| Perform wrapping, grapheme/tab measurement, alignment and clipping | Native UI layout, using native Terminal text primitives | Existing machinery retained by native-seam option; reproduction cost exposed for public option | PASS |
| Coordinate continuous selection across Forge’s owned sources | Forge CLI TUI | Proposed Forge coordinator; no global native input redesign | PASS |
| Arbitrate selection, links and snippet actions | Forge CLI presentation policy over native routing | Explicitly unresolved design contract | PASS |
| Preserve Copy failure consumption and no-selection Stop | CLI policy plus existing clipboard extensions | Foundation semantics remain mandatory; unchecked native app Copy identified as a containment hazard | PASS |
| Extract/copy meaningful selected text and report transport facts | Existing terminal extensions; native Terminal transport | No new clipboard backend or parallel transport path proposed | PASS |
| Define transcript ordering, separators, excluded chrome and card boundaries | Forge CLI transcript/presentation semantics | Forge semantic projection remains necessary on both routes | PASS |
| Preserve stable logical identity through streaming and reflow | Forge CLI semantic/source lifetime | Remains open; no assumption that native range methods solve it | PASS |
| Manage unrealized, collapsed, scrolled and retired source ranges | CLI coordinator with native DocumentFlow lifecycle | Remaining contract and regression obligations named | PASS |
| Preserve heading source spans through wrapping/cutting | Existing CLI TextArt/HeadingImage owners | Extend the existing graphics boundary; no displayed-word search or placeholder decoding | PASS |
| Map proportional heading positions using font advances and kerning | Existing CLI GlyphText font-layout owner | Reuse complete-line geometry; no copied font engine | PASS |
| Render selected logical heading text and pending headings | CLI graphics with ForgeTheme/ForgeStyles | Same remaining work on both routes; no claimed selected-image implementation | PASS |
| Provide a general reusable Markdown source map, if required | Native Markdown/library owner | Separate reviewed contract; excluded from the small Paragraph-facade estimate | PASS |
| Build and distribute compatible UI/generator/Markdown/TextMate artifacts | Selected library maintainer/package owners | Identity-dependent compatibility and packaging gates explicitly retained | PASS |
| Change Forge extension dependency pins and integration consumers | Forge MCL package/delivery owner | New immutable extension release required; existing 0.1.0 remains valid | PASS |
| Maintain Desktop inventory without adding a consumer | Desktop atlas | Documentation-only external row; no Desktop product change proposed | PASS |

| Persona check | Verdict | Evidence |
|---|---|---|
| 1. List behaviours from the requirement | PASS | Continuous ranges, meaningful text, heading graphics, syntax, wrapping, streaming and failure/lifetime duties separated above |
| 2. Derive owners blind | PASS | Desktop atlas, Forge MCL/CLI/Core/Extensions READMEs and native contracts establish the owners |
| 3. Classify existing versus new components | PASS | Existing native layout/range and CLI graphics/semantics owners cover the work. No new component is approved |
| 4. Compare placements | PASS | Complete current comparison and scratch probe match those boundaries. No product implementation is claimed |
| 5. Search for duplicates | PASS | Exactly eight README-listed Forge repositories searched. No existing continuous-selection implementation found; only CLI, extension and tests consume the native family |
| 6. Check owners have one job | PASS | Native geometry, Forge semantics/graphics and clipboard results remain separate. Expanding the clipboard component into wrapping/font/document geometry would violate its present admission boundary |

The corrected contract and package claims are supported by actual sources:

- [Comparison:12](/Users/ameerdeen/progs/mission-control-language/docs/evidence/phase-70/rich-public-probe/comparison-current.md) correctly preserves direction. Native `TryGetOrderedSelection` returns sorted minimum/maximum endpoints; it cannot supply anchor/active direction. The private setter clamps endpoints and increments interaction version, but a public lifetime/thread/grapheme contract still requires design.
- [Comparison:22](/Users/ameerdeen/progs/mission-control-language/docs/evidence/phase-70/rich-public-probe/comparison-current.md) correctly makes repacking conditional. Published Markdown/TextMate nuspecs specify minimum UI `3.10.0`, while Forge’s extension specifies exact `[3.10.0]`. Permitted resolution does not prove binary/runtime/AOT compatibility.
- UI packages the netstandard SourceGen analyzer; Markdown and TextMate use it during their builds. Reuse does not remove analyzer packaging and generated-code compatibility obligations. The generator itself is a build-time component, not the product Native AOT executable.
- [Comparison:14](/Users/ameerdeen/progs/mission-control-language/docs/evidence/phase-70/rich-public-probe/comparison-current.md) labels 500–800 adapter lines and 40–80 facade lines as estimates. Neither establishes total delivery cost.
- [Comparison:36](/Users/ameerdeen/progs/mission-control-language/docs/evidence/phase-70/rich-public-probe/comparison-current.md) accurately retains the narrow emoji divergence. Raw frames omit the emoji at width eight while extraction includes it. Source shows whitespace measurement and retained-slice handling differ; that supports investigation, not a general bug diagnosis or authorized correction.

**Security Architecture and Engineering Philosophy:** PASS for this bounded investigation. Hosted tiers/stores/identity are N/A. Controlled clipboard and no-op Kitty transport are labelled accurately; no private access, product patch, fork or external write occurred.

**Desktop Interaction Principles, UI Design System and TUI graphics:** correctly remain governing gates. Existing theme and graphics owners are preserved. The probe proves native styled foreground retention and heading layout observations, not selected-heading appearance, TextMate language coverage, physical Retina fidelity or installed Ghostty behaviour.

Next design exploration should lock the complete semantic scope, then specify the smallest native point/directional-range contract alongside the existing CLI heading/source-span contract. Investigate the narrow rendering case before treating that facade as sufficient. A proposed public adapter must demonstrate its mapping without duplicating native layout; a native seam must prove its validated contract and compatible family cost.

Operator Type-1 decisions remain required for any changed library route, public API/ownership boundary, maintainer, package/assembly/namespace identity and distribution authority. Forge publication permission supplies no upstream or fork authority.

No current owner has two unrelated jobs. **Move nothing; keep native geometry with its native owner and Forge transcript/heading semantics with the CLI.**
