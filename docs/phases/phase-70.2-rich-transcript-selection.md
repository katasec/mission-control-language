# Phase 70.2 — Rich transcript selection

**Status: scoped; design open. Bounded public geometry probe verified; essential public point/range gap and conditional comparison recorded. Not build-ready.**
Depends on the approved [foundation](phase-70.1-text-interaction-foundation.md);
parent [Phase 70](phase-70-tui-text-interaction.md).

The parent's [manual verification exception](phase-70-tui-text-interaction.md#manual-verification-exception)
applies: agent source/controlled verification supports design and implementation; operator
installed Ghostty/Retina checks close live acceptance. This does not resolve the rich-selection
contract or remove the foundation dependency.

## Requirement

Let the user drag through rich chat content and copy its meaningful text while retaining
Kitty-rendered headings and frames, native terminal body/code text, syntax highlighting,
wrapping and streaming. Single-paragraph selection is already available; it does not meet
continuous rich selection. Code snippet Copy remains a distinct convenience.

## Discovery and boundary

| Fact | Implication |
|---|---|
| `Paragraph` owns native selection and is sealed; MarkdownControl/DocumentFlow has no document selection coordinator in pinned 3.10.0. | Public document selection is a real gap, not an `IsSelectable` toggle. |
| Controlled Forge drag from paragraph one into paragraph two copies only paragraph one, in both themes. | Add a regression observation for the cross-paragraph requirement. [Evidence](phase-70.1-text-interaction-foundation_completed.md#controlled-probes). |
| Image headings are Kitty placeholder cells, not ordinary text selection owners. | Retain logical heading text for selection/copy; do not decode screen pixels/placeholders. |
| Source Markdown, rendered text and visual wraps differ. | Lock text semantics and logical-to-visual mapping before choosing an implementation. |

Owner remains Forge CLI TUI. A general Markdown selection extension belongs at the library
boundary if it is reusable; a Forge-specific rendering adapter belongs in the TUI. Decide which
fits only after defining the smallest required public contract. Do not fork XenoAtom or enlarge
`XenoCells` private access as an assumed solution. Library version upgrades require verified
public capabilities and regression evidence, not extrapolation from CodeAlta.

## Public-extension feasibility and fork comparison

Current [source-boundary observations](phase-70.2-rich-transcript-selection_completed.md#source-boundaries--2026-10-06)
name the native mapping cases and package-family cost. The [verified controlled probe](phase-70.2-rich-transcript-selection_completed.md#bounded-public-geometry-probe--2026-10-06)
found an essential public point/range gap. The [current comparison passed both independent reviews](phase-70.2-rich-transcript-selection_completed.md#current-comparison-r2--both-independent-reviews-pass); no clean adapter or library-route approval is claimed.

The operator selected unchanged public packages plus Forge-owned extensions as the primary route,
and asked to skip fork comparison when that stays clean. Foundation's 22 public-only behavior
cases do not prove continuous rich selection. No fork is authorized or created.

| Required boundary | Pinned capability / remaining cost |
|---|---|
| Semantic document | Public MarkdownDocumentContent.GetBlock and DocumentFlowBlock.CreateVisual materialize visuals; they expose no canonical source-offset/range map. MarkdownControl is sealed and its builder internal. [Content](https://github.com/XenoAtom/XenoAtom.Terminal.UI/blob/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src/XenoAtom.Terminal.UI.Extensions.Markdown/MarkdownDocumentContent.cs), [block](https://github.com/XenoAtom/XenoAtom.Terminal.UI/blob/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src/XenoAtom.Terminal.UI/Controls/DocumentFlowBlock.cs). |
| Hit/range mapping | Paragraph's native wrapped-line selection mapping is private. Public Text/Runs and visual bounds alone do not identify offsets for repeated words, graphemes or proportional heading images. A public adapter would need a verified text/layout map; do not assume screen-text search or copied native layout code is adequate. |
| Drawing | Public CellBuffer.OverlayCellStyle can mark terminal cells; Forge already owns heading logical text/font geometry. This is a candidate rendering seam, not a demonstrated heading/rich-range renderer. [Overlay](https://github.com/XenoAtom/XenoAtom.Terminal.UI/blob/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src/XenoAtom.Terminal.UI/Rendering/CellBuffer.cs#L268). |
| Package ownership | Reusable native clipboard/menu extensions use the foundation package; Forge Markdown/Kitty semantics stay CLI. A second selection package or a larger extension API is not justified until the required map is proved. |

The probe covered two Paragraphs, repeated words, narrow wrapping, graphemes and an actual
image heading beside code. It recorded local highlighting, exact copied text and reflow
without private access, but found no public point/range boundary for continuous selection.
Every promised content case below still needs a decision; the probe does not narrow range scope.

The [narrow native isolation](phase-70.2-rich-transcript-selection_completed.md#narrow-native-paragraph-isolation--2026-10-06) reproduced leading-space wrap budgeting loss for ASCII and two-cell graphemes while exact Copy succeeded. Native whitespace/wrap correction needs a reviewed design; the comparison's facade estimate excludes this correction. No patch or fork is approved.

If that requires reproducing substantial native wrapping/hit mapping or cannot meet an essential
case, compare that concrete adapter with a bounded fork exposing existing native mapping/range
operations. Compare actual code/duplicate mechanisms, compatible UI/Markdown/TextMate package
maintenance and consumer/AOT verification, rather than assuming a tiny patch is a cheap fork.
If the public adapter remains small, skip the fork comparison as requested. Route and full rich
design remain open; library-private methods are never an implementation shortcut.

## Decisions required before design can close

| Decision | Output that must be written in this spoke |
|---|---|
| Range scope | Exact boundary for selection across paragraphs, code, lists, quotes, headings, tables, collapsed content and adjacent participant cards; treatment of user messages, timestamps and tool/status chrome. No undocumented partial support. |
| Copy semantics | Canonical plain-text ordering, paragraph/list/table separators, heading text, links, whitespace and code newlines; decide whether a separately requested Copy message as Markdown is warranted. It is not in the current requested slice. |
| Stable identity | Source offsets/anchors across Markdown reflow and streamed replacement; exact rule for content inserted or removed during selection and while a context menu is open. |
| Geometry and gestures | Hit mapping for proportional image headings, Unicode/graphemes, wrapping, scrolling, drag outside viewport, double/Shift-click and deselection; no collision with links or copy buttons. |
| Ownership and API | Selection owner lifecycle, public interfaces and complete data shapes, clipboard policy integration, disposal and failure handling; bounded public adapter and its package/repository route; conditional fork comparison only on a concrete gap/complexity. Type-1 public-contract/ownership changes need operator decision. |
| Visuals and themes | Binding selected-heading/body/code appearance, both themes' semantic tokens, selected text contrast and focus transitions. Preserve existing Kitty identity. |

## Entry points and gates

| Area | Read / prove |
|---|---|
| Product paths | `/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/ChatScreen.cs`, `Transcript.cs`, `ForgeMarkdown.cs`, `ForgeCodeBlockRenderer.cs`, `Graphics/HeadingImage.cs`, `ForgeTheme.cs`, `ForgeStyles.cs`; CLI component README. |
| Library paths | Pinned `Controls/Paragraph.cs`, `Controls/DocumentFlow.cs`, `Controls/FlowDocument.cs`, Markdown `Controls/MarkdownControl.cs` and rendering models. Confirm public surfaces before proposing types. |
| Security / philosophy | Local text only; hosted data/identity/tier changes N/A unless scope changes. Clipboard read only on explicit action, no logging of copied payload. Named selection/renderer/clipboard owners and structural failure containment required. |
| UI | Explicitly name [Desktop Interaction Principles](../design/desktop-interaction-principles.md), [UI Design System](../design/ui-design-system.md), [TUI graphics](../design/tui-graphics.md), and foundation references/theme map in all assignments. Operator verifies on laptop Retina only; supervisor records attributed evidence under the parent exception. |
| Default path | Foundation's installed Native AOT, normal project/sign-in/endpoint and Ghostty route apply unchanged; use a real rich reply and compare clipboard text to the locked semantics. In-memory selection tests supplement acceptance. |

## Ordered tasks

| Task | State | Done when |
|---|---|---|
| 1. Lock range and semantic scope | Probe/current comparison verified and reviewed; narrow wrap defect isolated; full semantics/route decisions open. | All range/content cases above have explicit decisions; operator resolves any Type-1 change before handoff. |
| 2. Design and public contract review | Pending scope closure. | Supervisor applies designer persona; assigned simplicity and ownership reviewers check the complete current contract sequentially, including actual APIs, types, lifecycle and visual reference, before supervisor approval. |
| 3. Plan and review | Pending design. | Assigned implementer plans; assigned reviewers run their complete checklists sequentially; supervisor approves one bounded task sequence and concrete failure/reflow/streaming tests. |
| 4. Implement and review | Pending plan. | Same implementer after explicit plan approval; reused simplicity/style and ownership reviewers check the complete diff sequentially; supervisor independently reviews evidence. Library work, if needed, precedes consumer integration. |
| 5. Merge, install and accept | Pending verification; operator performs live acceptance. | Native AOT and required checks pass; normal artifact installed; operator proves continuous selection and exact Copy through real mixed content in both themes on Retina, supervisor assesses and records evidence. |

## Done when

The locked continuous range works across all promised rich content, including logical image
heading text; copied text follows the defined semantics and never contains Kitty placeholders,
decorative chrome or soft wraps. Selection survives or is predictably resolved under streaming,
reflow, scrolling and menu focus according to the approved contract. Link and snippet actions
remain usable. Clipboard failure cannot cancel a turn. The operator performs installed
default-path and Retina checks in both themes, the supervisor records attributed evidence,
and every changed repository is merged/clean.
