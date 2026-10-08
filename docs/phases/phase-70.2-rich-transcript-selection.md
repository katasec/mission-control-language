# Phase 70.2 — Rich transcript selection

**Status: the first merged implementation failed installed Ghostty acceptance at 2026-10-08 02:07 Dubai: dragging into trailing line space cleared the range, blocking Copy; selection contrast was also too weak. The narrow acceptance-correction design below is awaiting sequential review before a replacement implementation.**
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

Owner remains Forge CLI TUI. Reusable terminal selection/clipboard glue belongs in the settled
Forge-owned Terminal Extensions package; Forge-specific Markdown and Kitty rendering stays in the
TUI. Do not fork XenoAtom or enlarge `XenoCells` private access as an assumed solution. Library
version upgrades require verified public capabilities and regression evidence, not extrapolation
from CodeAlta.

## Forge-owned extension feasibility

Current [source-boundary observations](phase-70.2-rich-transcript-selection_completed.md#source-boundaries--2026-10-06)
name the native mapping cases and package-family cost. The [verified controlled probe](phase-70.2-rich-transcript-selection_completed.md#bounded-public-geometry-probe--2026-10-06)
found an essential public point/range gap. It does not prove a clean Forge-owned extension seam.

The operator selected unchanged public packages plus Forge-owned extensions. Foundation's 22
public-only behavior cases do not prove continuous rich selection. No fork, upstream write or
layout-reimplementation route is authorized. The R4 Type-2 exception below is limited to two
version-pinned `UnsafeAccessor` calls in the CLI's existing private-access containment; the settled
Terminal Extensions package remains the public clipboard/menu extension.

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

The [narrow native isolation](phase-70.2-rich-transcript-selection_completed.md#narrow-native-paragraph-isolation--2026-10-06) reproduced leading-space wrap budgeting loss for ASCII and two-cell graphemes while exact Copy succeeded. Native whitespace/wrap correction needs a reviewed design. No patch is approved.

### Feasibility result — 2026-10-07

The bounded investigation returned **NO**. The Forge-owned extension can clear or copy a
Paragraph's already-native range, but cannot create the two endpoints that continuous card
selection needs:

| Required operation | Public result |
|---|---|
| Paragraph-local point → source-text index | Missing. `Paragraph.GetTextIndexFromPosition(string, int, int)` is private. Its wrapped-line cache, indent/prefix budgeting, alignment and grapheme mapping are private too. |
| Apply a native anchor/active range | Missing. `Paragraph.SetSelection(int, int)` is private. |
| Clear/copy an existing native range | Available through public `ISelectionOwner.ClearSelection()` and `Paragraph.TryCopySelection`, but insufficient because no cross-block endpoints can be established. |

The source inspection covered the public Paragraph, selection, TerminalApp, DocumentFlow and
Markdown surfaces, plus the existing Terminal Extensions package. No clean public extension seam
exists on the pinned public contract. Recreating native layout, a package upgrade, upstream write
or fork violate the settled boundary. This is an essential-gap result, not an implementation defect
or default-path acceptance result.

### R4 proposed Type-2 bridge — corrected pending design review

The existing CLI already isolates six AOT-safe `UnsafeAccessor` calls in `XenoCells`, pinned by
contract tests to XenoAtom 3.10.0. Extend that existing private-access containment with exactly
two Paragraph bridges:

```mermaid
flowchart LR
    Card[ParagraphSelection] -->|local pointer| Bridge[CLI XenoCells bridge]
    Bridge -->|GetTextIndexFromPosition| Paragraph[Native Paragraph]
    Card -->|anchor + active| Bridge
    Bridge -->|SetSelection| Paragraph
```

| Fact | Locked proposed boundary |
|---|---|
| Scope | Only `Paragraph.GetTextIndexFromPosition(string, int, int)` and `Paragraph.SetSelection(int, int)`; no field access, synthetic input, selection-owner replacement, reflection or unrelated UI private member. |
| Owner | CLI `XenoCells` owns the Type-2 containment beside the existing six private XenoAtom calls. Terminal Extensions retains its public Paragraph Copy menu. The CLI owns card ordering, copy semantics and feedback. |
| Safety | Exact XenoAtom UI 3.10.0 package pin, compile-time `UnsafeAccessor`, no new bridge assembly, and CLI contract tests that execute both bridges against a wrapped Paragraph, pin both member names/signatures, and enforce the exact `XenoCells` allowlist. |
| Removal | Delete the bridge and use public Paragraph APIs when XenoAtom exposes equivalent point mapping and range application; `XenoCells` and the CLI README record this removal condition. |
| Failure | A missing/changed private member fails build/Native AOT verification; no runtime reflection fallback exists. |

This is a Type-2 implementation exception: it is reversible by deleting one adapter once public
APIs exist, touches only local terminal presentation, and has no tier, datastore, identity,
credential, endpoint or clipboard-payload change. It is not a new package, a fork or an upstream
write. The full R4 design remains subject to the required sequential reviews.

## Supervisor design R3 — proposal awaiting Type-1 decisions

**[design:supervisor] began 2026-10-07 16:18:24 UTC.** `TextInteraction` remains the only
cross-source interaction coordinator: it claims the active source, chooses Copy before Stop, and
reports feedback. The existing `ParagraphSelection` wrapper becomes the card-local rich selection
owner: it retains its realized member order/lifetime, translates an active drag into ranges for its
native Paragraphs and Forge-owned HeadingImages, and composes that card's exact plain text.
`TextInteraction` invokes that card owner; it does not acquire card topology or source anchors.
The CLI does not reproduce wrapping, Unicode cell widths, syntax rendering or clipboard behaviour.
The settled public route is the existing Forge-owned Terminal Extensions package on unchanged
pinned XenoAtom packages, with no upstream or fork write. R4 extends the CLI's existing private
containment with its two named, version-pinned `UnsafeAccessor` calls instead of reproducing native
layout; it has no fallback route.

```mermaid
flowchart LR
    Drag[Root drag event] --> Interaction[CLI TextInteraction]
    Interaction --> Card[CLI ParagraphSelection]
    Card --> Paragraph[Native Paragraph range]
    Card --> Heading[HeadingImage range]
    Card --> Copy[Existing ClipboardText]
    Copy --> Clipboard[Terminal clipboard]
```

### Proposed interaction contract

| Case | Proposed result |
|---|---|
| Drag within one rendered assistant card | Select every eligible source block between the two endpoints in visual/document order. A regular paragraph, list item, quote and code body reuse their native source range; a Forge image heading uses its retained logical text and Forge-owned geometry. |
| Drag across a card boundary, user message, timestamp, tool/status row, avatar, frame, link control or code-copy button | Clear the in-progress rich range and let the existing control own its action. No cross-card selection is silently implied. |
| Ctrl/Cmd+C or the existing Copy command while a rich range exists | `TextInteraction` asks the active card owner for canonical plain text and sends it through `ClipboardText`; a clipboard failure reports the existing failure state and never cancels the run. Without a rich range, retain the current editor/Paragraph/Stop order. |
| Paragraph context-menu Copy while a rich range exists | The existing Terminal Extensions menu keeps its attachment, popup and stale-source eligibility guard. Its fixed Copy command asks the card owner whether that card still has a current rich range, then invokes its exact text callback; it does not compose Forge content. |
| Reflow or scrolling after the drag ends | Preserve source anchors and repaint the same logical range after layout. A new drag derives fresh points from the live layout. |
| Streaming replacement, detached content, or an open stale menu | Clear the rich range structurally before replacement or action. The user can select the settled content again; no stale text is copied. |
| Double-click, Shift-click, and non-left pointer input | Retain native single-Paragraph behaviour. They do not create a second document-selection mode. |

**Scope decision — operator approved 2026-10-07:** selection spans one rendered assistant reply
card only. It includes paragraphs, lists, quotes, code and eligible headings; it excludes user
cards and transcript chrome. Tables and collapsed content remain deferred.

The proposed plain-text copy order is source order with a newline between list items and code
lines, and one blank line between other block boundaries. It copies link labels, not URLs; heading
logical text, never Kitty placeholder cells; and no decorative prefix, frame, soft wrap or syntax
colour. Tables and collapsed content are deliberately out of the first contract until the operator
chooses their semantics; they remain unselectable rather than partially copied.

### Behaviour → owner

| Behaviour | Owner | Why |
|---|---|---|
| Point/range mapping inside a Paragraph | XenoAtom Terminal UI `Paragraph`, reached only through CLI `XenoCells` | Paragraph owns wrapped layout, grapheme widths, indents and selection rendering; `XenoCells` contains the two pinned private calls. |
| Cross-source claims, Copy-versus-Stop and feedback | `TextInteraction` in `/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui` | The CLI README assigns `forge chat` presentation and text-source coordination there. |
| Card-member ordering, range projection, anchors and canonical copy text | `ParagraphSelection` in `/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui` | It already owns the bounded registry and retirement of realized Paragraphs in one rendered card. |
| Heading mapping/overlay | `HeadingImage` in `/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/Graphics` | It alone owns logical heading text and its Kitty-image layout. |
| Paragraph context-menu command, stale eligibility and truthful clipboard result | `Katasec.Forge.Terminal.Extensions` | Its README owns fixed native text menus, invocation guards, selection extraction and clipboard-result vocabulary. |
| Theme values | `ForgeTheme` → `ForgeStyles` | Existing semantic `Selection` is the selected range background in both themes. |

### Reuse and boundaries

| Need | Existing implementation checked | Decision |
|---|---|---|
| Drag target while the first Paragraph has pointer capture | `TerminalApp.DispatchMouseEvent` keeps the capture as event target but root `HitTest(UiX, UiY)` still resolves the live point. | Reuse root hit-testing; add no input backend or synthetic event path. |
| Body, list, quote and code selection mapping | `Paragraph` already maps point, wrapping, prefixes, tab stops and graphemes, but both point mapping and range setter are private. | Use only the R4 `XenoCells` bridge's two pinned methods. Do not copy native layout into Forge, write upstream or create a fork. |
| Heading source and geometry | `HeadingImage` retains logical text and `TextImages.WrapHeading` already defines its layout. | Add its range projection beside HeadingImage; do not introduce a second selection renderer or treat Kitty cells as text. |
| Paragraph context-menu Copy | `ParagraphClipboardExtensions` already owns the fixed native menu and stale eligibility closure. | Add its smallest callback overload: availability plus one exact Copy action/report. The CLI supplies only the active card's availability and text; it cannot alter menu lifetime or transport. |
| Clipboard failure/result | `ClipboardText` and `TextInteraction` from Phase 70.1. | Reuse transport/result semantics; card composition passes one exact string to `CopyText`. |

### Security, UI and default path

This is local presentation and an explicit clipboard write: no hosted tier, datastore, identity,
credential, network endpoint or log payload changes apply. The structural failure boundary is the
coordinator clearing its range before content replacement, detached source or stale menu action.
The selected body/code range retains the existing `ForgeTheme.Selection` token through native
Paragraph rendering; HeadingImage overlays use that same token. Dark and light, normal and
narrow Retina Ghostty remain the binding acceptance states, against the Phase 70 reference images
and [TUI graphics](../design/tui-graphics.md). Default-path acceptance remains the installed
Native AOT `forge chat` on Ghostty with no endpoint override, a real rich reply, and exact paste
comparison.

### Principles that changed a decision

| Rule | Decision |
|---|---|
| Designer 3 — No NIH | Retain the settled Forge-owned extension package and native Paragraph layout instead of a new library, a fork or a Forge text-layout implementation. |
| Designer 4 — One owner | Keep rich semantics and HeadingImage mapping in the CLI; expose only native Paragraph mechanics from the UI library. |
| Designer 7 — Minimum needed only | One drag contract and no new setting, global selection mode, source parser or separate package. |
| Designer 10 — Built-in safety | Clear selection at the existing replacement/detachment boundary, preventing stale-copy recovery paths. |
| Designer 11 — Verified means done | Require controlled range/reflow/streaming tests, Native AOT and installed Ghostty copy/paste observations. |

### Rejected alternatives

- A Forge-only adapter that recreates Paragraph wrapping/hit mapping: it duplicates native Unicode,
  prefix, alignment and reflow logic and has already failed the public-boundary probe.
- Broad private access or a new general helper: R4 permits only the two named `Paragraph` methods
  in the existing CLI `XenoCells` containment, with the stated pin, tests and removal path.
- An upstream XenoAtom contribution or Forge-maintained fork: the settled Phase 70 library decision
  is Forge-owned extensions on unchanged package versions, with no upstream/fork writes.
- Terminal-native selection: it conflicts with the TUI's mouse reporting and cannot include
  logical Kitty headings.

### Locked Type-2 exception

1. **Type-2 bridge — locked 2026-10-07 17:15:42 UTC:** two methods in existing CLI `XenoCells`:
   `Paragraph.GetTextIndexFromPosition(string, int, int)` and `Paragraph.SetSelection(int, int)`.
   The ownership review corrected placement from Terminal Extensions. No broader private access may
   be inferred.
2. **Range scope — approved 2026-10-07:** one assistant reply card, excluding user cards and
   transcript chrome; tables and collapsed content are deferred.

### Workflow timestamps

The recorded design-review boundaries are below. A reviewer result without its own emitted end
timestamp is deliberately marked unavailable rather than reconstructed.

| Stage / role / round | Start (UTC) | End (UTC) | Evidence |
|---|---|---|---|
| `[design:supervisor]` | 2026-10-07 16:18:24 | Open — Type-1 decision pending | This R3 proposal |
| `[review-design:simplicity]` | 2026-10-07 16:21:03 | Unavailable | R1 required the single-owner correction. |
| `[review-design:simplicity:r2]` | 2026-10-07 16:24:18 | Unavailable | Full PASS after `TextInteraction` became the sole interaction owner. |
| `[review-design:ownership]` | 2026-10-07 16:25:28 | 2026-10-07 16:28:39 | R1 FAIL: card lifecycle/extraction and native menu ownership corrected. |
| `[review-design:simplicity:r3]` | 2026-10-07 16:30:13 | Unavailable | Full PASS on the corrected owner split. |
| `[review-design:ownership:r2]` | 2026-10-07 16:30:13 | 2026-10-07 16:32:26 | Full PASS. |
| `[design:supervisor:r2]` | 2026-10-07 16:36:30 | 2026-10-07 16:38:43 | Restored the settled Forge-owned extension route and swept stale fallback language. |
| `[review-design:simplicity:r4]` | 2026-10-07 16:36:30 | Unavailable | Required the full active-spoke sweep of stale fallback language. |
| `[review-design:simplicity:r5]` | 2026-10-07 16:38:43 | Unavailable | Full PASS after the sweep. |
| `[review-design:ownership:r3]` | Unavailable | 2026-10-07 16:40:06 | Full PASS after the sweep. |
| `[scope:supervisor:r2]` | 2026-10-07 16:47:35 | 2026-10-07 16:47:35 | Operator approved the one-assistant-card range scope. |
| `[investigate:implementer]` | 2026-10-07 16:47:35 | 2026-10-07 16:51:21 | NO: the pinned public API lacks both required Paragraph operations. |
| `[design:supervisor:r3]` | 2026-10-07 16:58:39 | 2026-10-07 17:10:43 | Proposed the bridge; ownership review required the containment correction. |
| `[review-design:simplicity:r6]` | 2026-10-07 17:04:54 | 2026-10-07 17:07:18 | Required execution coverage for both bridge calls and an extensions-package accessor allowlist. |
| `[review-design:simplicity:r7]` | 2026-10-07 17:07:34 | 2026-10-07 17:08:39 | Full PASS after the execution/allowlist correction. |
| `[review-design:ownership:r4]` | 2026-10-07 17:08:39 | 2026-10-07 17:09:24 | FAIL: corrected private-access ownership from Terminal Extensions to CLI `XenoCells`. |
| `[design:supervisor:r4]` | 2026-10-07 17:10:43 | 2026-10-07 17:15:42 | Applied the ownership correction; R9 simplicity and R5 ownership PASS; design locked. |
| `[review-design:simplicity:r8]` | 2026-10-07 17:11:45 | 2026-10-07 17:13:27 | Required the removal condition move to `XenoCells`. |
| `[review-design:simplicity:r9]` | 2026-10-07 17:13:27 | 2026-10-07 17:14:29 | Full PASS after the removal-condition correction. |
| `[review-design:ownership:r5]` | 2026-10-07 17:14:29 | 2026-10-07 17:15:04 | Full PASS after the ownership correction. |
| `[implementation:implementer]` | 2026-10-07 17:27:58 | Unavailable | Completed the approved implementation, including replacement/detach lifecycle repair and heading source-span correction. |
| `[review-implementation:simplicity]` | Unavailable | Unavailable | First review found wrapped-heading source-offset loss and a stale TextArt test; both corrections were independently re-reviewed PASS. |
| `[review-implementation:ownership]` | Unavailable | 2026-10-07 20:50:03 | PASS: the bridge, range/policy, heading projection and public menu callback remain with their named owners. |
| `[verification:supervisor]` | Unavailable | 2026-10-07 20:59:28 | Debug CLI build: 0 warnings/errors; focused TextArt, interaction, bridge-contract and menu checks: 84/84; terminal extensions: 27/27; Release Native AOT publish completed successfully for `osx-arm64`. The full suite was not recorded: the ambient `MCL_API_KEY` first invalidated its missing-key case and the runner’s foreground window left the retry incomplete. |

## Locked design decisions

### Acceptance-correction design — 2026-10-08

### Acceptance-correction workflow ledger

This ledger preserves the failed first delivery and every correction event. Times are Dubai
(`UTC+4`); `Not recorded` is never reconstructed as a guessed timestamp.

| Stage/event | Work | Status/result | Started | Finished |
|---:|---|---|---|---|
| 1 | Initial design and reviews | Approved | 2026-10-07 20:18:24 | 2026-10-07 21:15:42 |
| 2 | Initial plan and reviews | Approved | 2026-10-07 21:15:42 | 2026-10-07 21:27:58 |
| 3 | Initial implementation and code reviews | Passed | 2026-10-07 21:27:58 | 2026-10-08 00:50:03 |
| 4 | Initial required CI checks | Passed | 2026-10-08 01:01:52 | 2026-10-08 01:28:06 |
| 5 | Initial merge | Merged, commit `b6ecb52` | 2026-10-08 01:34:37 | 2026-10-08 01:34:37 |
| 6 | Initial install and Ghostty acceptance | **Failed:** trailing line-space cleared selection; selection contrast too weak | 2026-10-08 01:34:37 | 2026-10-08 02:07:31 |
| 6.1 | First local install | **Failed:** MSBuild workers exited during AOT | 2026-10-08 01:34 | 2026-10-08 01:35 |
| 6.2 | Install after build-server reset | Passed: installed `forge` reported `1.0.0+b6ecb52…` | 2026-10-08 01:49 | 2026-10-08 01:54 |
| 7 | Scope correction | Trailing-space endpoint and shared selection contrast locked | 2026-10-08 02:07 | 2026-10-08 02:13 |
| 8 | Correction design and reviews | Passed after one simplicity correction | 2026-10-08 02:13 | 2026-10-08 02:18:48 |
| 9 | Correction plan draft | Produced | 2026-10-08 02:19:38 | 2026-10-08 02:19:58 |
| 9.1 | First plan review | **Failed:** plan had not yet been recorded in the spoke | Not recorded | Not recorded |
| 9.2 | Revised plan and reviews | Passed after adding Light/Dark composer/body/heading coverage | 2026-10-08 02:21 | 2026-10-08 02:25 |
| 10 | Implement correction | Passed: bounded `ParagraphSelection` fallback, shared theme token and focused tests | 2026-10-08 02:25 | 2026-10-08 02:43 |
| 11 | Simplicity code review | Passed: no findings | 2026-10-08 02:37:10 | 2026-10-08 02:38 |
| 11.1 | Ownership code review | **Failed P2:** topology tests had not proved rightmost same-row selection or inside-card fallback rejection | 2026-10-08 02:38 | 2026-10-08 02:39 |
| 11.2 | P2 test correction and repeated reviews | Passed: test-only correction proves rightmost same-row member and card-owned inter-block rejection; both reviewers passed | 2026-10-08 02:39 | 2026-10-08 02:46:15 |
| 12 | Focused release validation | Passed: Debug 0 warnings; focused interaction/selection/contract/art/menu suite 147/147; terminal extensions 27/27 | 2026-10-08 02:46 | 2026-10-08 02:48 |
| 12.1 | Native AOT publish | Passed: `osx-arm64` output produced. macOS linker emitted non-fatal deployment-target warnings for local Homebrew OpenSSL/Brotli libraries. | 2026-10-08 02:48 | 2026-10-08 02:54 |
| 13 | Code commit and PR | Passed: commit `30457e0`; [forge-mcl PR #69](https://github.com/katasec/forge-mcl/pull/69) opened | 2026-10-08 02:54 | 2026-10-08 02:56 |
| 13.1 | First PR CI run | **Failed:** unrelated `StartPageTests.Create_does_nothing_and_a_click_on_Chat_opens_the_chat` expected `[True, False]`, got `[True, True]`; 943 passed, 1 failed, 10 skipped | 2026-10-08 02:55:22 | 2026-10-08 03:00:25 |
| 13.2 | CI rerun | **Failed the same StartPage test, then cancelled at operator direction:** local Ghostty proof precedes remote CI | 2026-10-08 03:04:36 | 2026-10-08 03:09:11 |
| 14 | Local install and Ghostty acceptance rerun | **Failed:** trailing-row case improved, but cross-paragraph/gap drags select only one block and slight vertical drift clears the range | 2026-10-08 03:08 | 2026-10-08 03:16 |
| 14.1 | Scope correction: continuous card text flow | Passed: one resolver and one logical reply-body text box locked after sequential design/plan reviews | 2026-10-08 03:16 | 2026-10-08 03:28:38 |
| 14.2 | Implementation and local verification | Passed: `26f92c6`; Debug 0 warnings; 149 focused tests; 27 extension tests; managed Ghostty acceptance confirmed by operator | 2026-10-08 03:28:55 | 2026-10-08 03:43:27 |
| 15 | Native AOT publish | Passed: `osx-arm64` artifact; non-fatal local Homebrew linker warnings | 2026-10-08 03:43:27 | 2026-10-08 03:49 |
| 15.1 | Accepted-correction CI | **Failed:** changing unrelated failures: ForgeRun broken-pipe and ChatScreenLiveMotion virtual-timing assertion | 2026-10-08 03:44 | 2026-10-08 03:57 |
| 15.2 | CI timing-test containment and release CI | Passed: exact two-class CI-only exclusion; CI run `37708331114` passed package verification and macOS ARM64 CLI in 30m31s | 2026-10-08 04:19 | 2026-10-08 04:55 |
| 16 | Product merge and installed Native AOT | Passed: PR #69 merged as `b53df9c`; accepted `26f92c6` Native AOT installed locally (`forge --version` confirmed) | 2026-10-08 05:01 | 2026-10-08 05:05:47 |

The initial installed default-path observation was decisive: dragging inside an assistant card
showed a range, but reaching trailing space at a line end cleared it. Stage 14.2 records the
operator's managed Ghostty PASS for the corrected one-text-box behaviour; Stage 16 records final
Native AOT installation.
The same observation and supplied visual references show that the shared selected-text background
is too close to its surfaces in both assistant content and the composer.

### First acceptance correction — superseded history

The first correction changed the shared `ForgeTheme.Selection` token to `#A9C9F5` light and
`#24558A` dark, which remains active. Its same-row trailing-space endpoint rule is **superseded**:
installed Ghostty acceptance at ledger stage 14 proved that treating vertical gaps as invalid made
the reply behave as separate graphic blocks. The full evidence and failure remain in the ledger;
the only active selection contract is **Second acceptance correction — locked 2026-10-08** below.
The binding visual references are the [foundation light](../images/phase-70.1/foundation-light.svg)
and [foundation dark](../images/phase-70.1/foundation-dark.svg) galleries, the
[finish-line mockup](../design/forge_tui_finish_line_mockup.html), and the
[operator chat reference](../images/phase-70/chat-user-reference.png). `ForgeTheme.Selection`
must be `#A9C9F5` light and `#24558A` dark: its background contrast is respectively 1.70:1 and
2.16:1 against CardSurface, and 1.60:1 and 2.37:1 against CodeBlockFill; normal text on the
selection is respectively 9.91:1 and 5.11:1. Controlled frames must show that exact token for
composer, body/code and heading overlay. The installed default-path rerun must show, at normal
and narrow Ghostty widths in both themes, a continuous selection rectangle visibly distinct from
the unselected CardSurface/CodeBlockFill for composer, assistant body, code and headings.

This remains local presentation only: no tier, datastore, identity, credential, endpoint or
clipboard payload boundary changes. The default path is unchanged; the failed Ghostty observation
must be replaced by a named operator PASS before closure.

### Second acceptance correction — locked 2026-10-08

**The red-circled assistant reply body is one logical selectable text box.** Paragraphs,
blank lines, headings, code blocks and their graphics are layout inside that one box; they are
never selection boundaries. This supersedes the preceding same-row/gap-invalid fallback design.

1. `ParagraphSelection` has **one** pointer-to-logical-text resolver for every non-control point
   inside its reply-body bounds. There is no direct-member route plus trailing-space fallback, no
   same-row requirement, and no rule that clears a range merely because the pointer crosses a
   rendered gap or drifts vertically.
2. The resolver maps any body coordinate to the nearest valid endpoint in the card's ordered text
   stream. A range includes every text member between anchor and endpoint, in either drag
   direction. Existing member mappers retain their responsibility for source-text indices.
3. Interactive controls, including the code-copy button, stay controls rather than text endpoints.
   Card chrome outside the reply body remains outside the text box.
4. The correction is not accepted on a screenshot or a claim. Automated proof must drag both
   lower-to-upper and upper-to-lower across a real blank paragraph gap and copy both paragraphs;
   it must retain selection across small vertical drift during a horizontal drag; it must cover
   heading and code members in the same continuous range; and it must retain control rejection.
5. Installed Ghostty acceptance must reproduce the red-circled case as one continuous range and
   exact copied text before CI is restarted or any merge is considered.
### One-text-box implementation design — 2026-10-08

`ParagraphSelection.RefreshMembers()` already provides the ordered stream of realized native
`Paragraph` and `HeadingImage` members. `ApplyRange`, `Ordered` and `TryCopy` already make a
cross-member range and copy it in source order. The only implementation change is to replace the
current `TryMemberEndpoint` plus `TryTrailingEndpoint` split with one `TryEndpoint` resolver:

1. Reject only a missing reply body, a descendant `Button`, or a point outside
   `ParagraphSelection.Content.Bounds`.
2. For each realized member rectangle `[X, Right) × [Y, Bottom)`, compute `verticalDistance` as
   `Y - uiY` when `uiY < Y`, `uiY - Bottom + 1` when `uiY >= Bottom`, otherwise `0`; compute
   `horizontalDistance` the same way from `[X, Right)`. Choose the lexicographically smallest
   `(verticalDistance, horizontalDistance, memberSourceIndex)`. A point inside either interval has
   distance zero; an exact geometry tie chooses the earlier source member.
3. Pass the original point to that member's existing `TextIndexAt`; native `Paragraph` and
   `HeadingImage` already clamp it to the appropriate source boundary.
4. Keep the resulting `Endpoint(memberIndex, textIndex)` unchanged. Existing range/copy behavior
   then selects partial endpoint members and all intervening members in source order.

This is one pointer-to-text route, not a direct-hit path plus fallback. It makes a blank
paragraph gap, rendered graphics, trailing space and small vertical drift ordinary positions in
one reply-body text box. `TextInteraction`, XenoCells, renderers, HeadingImage, theme,
Terminal Extensions and public APIs remain unchanged.

Tests must replace the superseded gap-rejection and artificial same-row fixture with routed
lower-to-upper and upper-to-lower gap drags that copy both paragraphs; a horizontal drag with
vertical drift that retains its range; and a real heading/body/code range that copies canonical
text and shows native plus overlay selections. They retain button and outside-body rejection.
### Approved implementation plan — second acceptance correction

1. In `src/ForgeMission.Cli/Tui/ParagraphSelection.cs`, delete the direct-member plus same-row
   trailing fallback split. Implement the one locked resolver over `RefreshMembers()` using the
   specified distance tuple. It rejects only controls and points outside reply-body bounds, then
   calls the chosen member's existing `TextIndexAt` with the original point.
2. Keep `ForgeTheme.Selection` exactly as already corrected: `#A9C9F5` light and `#24558A` dark.
   No theme, renderer, `TextInteraction`, XenoCells, Terminal Extensions, package, or public API
   change is permitted.
3. In `tests/ForgeMission.Mcl.Tests/Cli/TextInteractionTests.cs`, replace superseded same-row and
   gap-rejection tests with routed body tests. Drag lower-to-upper and upper-to-lower over a real
   gap between `first body` and `second body`; both copies must exactly equal
   `"first body\n\nsecond body"`. A one-row vertical drift during a horizontal drag must retain
   the range and copy its expected source text. An equidistant geometry tie must choose the earlier
   source member. A mixed `Heading text`, `body text`, `using System;` range must exactly copy
   `"Heading text\n\nbody text\n\nusing System;"` and show HeadingImage plus native code
   selection. Retain code-copy-button and outside-body rejection.
4. Rebuild Debug CLI before reflection tests, run the focused interaction, code-selection,
   bridge-contract, TextArt and menu suites plus terminal-extension tests, and use a local install
   for Ghostty acceptance **before** restarting CI. Native AOT and CI remain post-acceptance
   release gates.

Done when there is one resolver for any non-control point in the reply body; cross-block and
drift selection copies the exact source-order payload; control/chrome boundaries remain invalid;
and the operator proves one continuous range and exact Copy in installed Ghostty.

| Decision | Locked outcome |
|---|---|
| Range scope | One assistant reply card; paragraphs, lists, quotes, code and eligible headings. User cards, chrome, tables and collapsed content excluded. |
| Copy semantics | Source order; newline between list items/code lines; blank line between other blocks; labels not URLs; logical heading text; no chrome, soft wraps or colour. |
| Stable identity | Preserve source anchors on reflow/scroll; clear structurally before streaming replacement, detach or stale-menu action. |
| Geometry and gestures | `Paragraph` maps live pointer positions through its native layout; `HeadingImage` supplies its retained logical geometry. Double/Shift/non-left retain native single-Paragraph behavior. |
| Ownership and API | CLI `XenoCells` contains exactly two private Paragraph calls; `ParagraphSelection` composes card ranges; `TextInteraction` owns policy; Terminal Extensions owns menu/clipboard behaviour. |
| Visuals and themes | Native Paragraph selection and HeadingImage overlay use existing `ForgeTheme.Selection`/`ForgeStyles` in light/dark normal/narrow Ghostty. |

### CI timing-test containment — locked 2026-10-08

The operator directed the release workflow to exclude only
`ForgeMission.Tests.Cli.StartPageTests` and
`ForgeMission.Tests.Cli.ChatScreenLiveMotionTests`. They are virtual-terminal UI tests whose
fixed-tick and real-clock rendering assertions produced changing failures across identical PR
commits. This is CI-only containment in `.github/workflows/publish-terminal-extensions-package.yml`:
`Makefile`'s local `test` target remains the full suite.

The workflow continues to run all other tests, including Phase 70 `TextInteractionTests` and
non-UI `ForgeRunTests`, plus terminal-extension package verification and Native AOT publish.
The exact replacement in the workflow's `Verify current product and package` step is:
`{ make build; dotnet test ForgeMission.slnx --filter 'FullyQualifiedName!~ForgeMission.Tests.Cli.StartPageTests&FullyQualifiedName!~ForgeMission.Tests.Cli.ChatScreenLiveMotionTests'; make verify-terminal-extensions-package; }`.
No `Makefile` target changes. The evidence is CI run `37699171757` (the named StartPage test
failed twice) and CI run `37703917618` (the named ChatScreenLiveMotion test failed on a later
rerun). This is a Type-2 test-gate exception: scope is exactly the two named classes; reversal is
deleting the workflow filter; removal condition is deterministic virtual-terminal synchronization
in those tests.
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
| 1. Lock range and semantic scope | Done — operator scope plus locked two-method `XenoCells` exception. | Recorded above with R9 simplicity and R5 ownership PASS. |
| 2. Design and public contract review | Done — design locked 2026-10-07 17:15:42 UTC. | Actual APIs, lifecycle, visual reference and Type-2 removal path reviewed sequentially. |
| 3. Plan and review | Done — supervisor approved plan 2026-10-07 17:27:58 UTC. | Two `XenoCells` bridges; card-owned range composition; `TextInteraction` policy; HeadingImage overlay; public menu callback; exact contract/integration/menu coverage. No renderer change. |
| 4. Implement and review | Reopened — acceptance correction awaits design/plan reviews. | Same implementer and reviewers must approve the trailing-space endpoint and shared-token correction before code changes. |
| 5. Merge, install and accept | First revision merged and installed; acceptance failed. | Rerun required checks, merge/install the correction, then operator proves continuous selection, exact Copy and visible selection contrast through real mixed content in both themes on Retina. |

### Approved implementation plan

The approved plan changes only the existing CLI and Terminal Extensions components. `XenoCells`
gains the two locked accessors and its eight-member contract allowlist; `ParagraphSelection`,
`TextInteraction` and `HeadingImage` implement card range, policy and logical heading rendering;
Terminal Extensions adds a guarded menu delegation callback. CLI and extension READMEs document
their respective boundaries. Focused bridge, interaction and menu tests precede full tests,
zero-warning Native AOT publish, package verification and installed default-path acceptance.
`ParagraphSelection` already finds nested code Paragraphs, so `ForgeCodeBlockRenderer` does not
change.

| `[plan:implementer]` | 2026-10-07 17:16:27 | 2026-10-07 17:23:00 | Initial plan returned. |
| `[review-plan:simplicity:r10]` | 2026-10-07 17:23:00 | 2026-10-07 17:24:54 | Required removal of duplicate code-renderer registration. |
| `[plan:implementer:r2]` | 2026-10-07 17:24:54 | 2026-10-07 17:26:20 | Removed renderer change; reused wrapper discovery. |
| `[review-plan:simplicity:r11]` | 2026-10-07 17:26:20 | 2026-10-07 17:27:21 | Full PASS. |
| `[review-plan:ownership:r6]` | 2026-10-07 17:27:21 | 2026-10-07 17:27:58 | Full PASS; plan approved. |

## Done when

The locked continuous range works across all promised rich content, including logical image
heading text; copied text follows the defined semantics and never contains Kitty placeholders,
decorative chrome or soft wraps. Selection survives or is predictably resolved under streaming,
reflow, scrolling and menu focus according to the approved contract. Link and snippet actions
remain usable. Clipboard failure cannot cancel a turn. The operator performs installed
default-path and Retina checks in both themes, the supervisor records attributed evidence,
and every changed repository is merged/clean.
