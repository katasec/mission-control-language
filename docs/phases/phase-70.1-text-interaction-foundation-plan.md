# Phase 70.1 — Foundation implementation plan

**Status: R3 PLAN APPROVED at 2026-10-06 01:15:19 UTC after both full plan reviews; execute only this plan against locked R12 design. Product checks, full code reviews and delivery/acceptance remain open.**

Parent: [foundation contract](phase-70.1-text-interaction-foundation.md). Historical complete R2 plan is [preserved](phase-70.1-text-interaction-foundation_completed.md#superseded-complete-r2-implementation-plan).

## 1. Files

This complete R3 plan implements the locked R12 foundation contract. The held draft remains unreviewed and unverified; previous results do not establish a current PASS. Supervisor granted explicit `PLAN APPROVED` at 2026-10-06 01:15:19 UTC; the same implementer may resume only this plan.

Requirement: Make composer, file-editor, and rendered-text Copy/Paste behave consistently, using native XenoAtom selection, commands, context menus, buttons, and clipboard transport wherever they meet the contract. Add a copy icon to real code snippets while preserving syntax highlighting and the established UI. Shared behaviour belongs at the TUI's text-interaction boundary; individual controls retain editing/rendering ownership.

Product work remains on `/Users/ameerdeen/progs/forge-mcl`, branch `adeen/phase70-text-foundation`, base `2022b512dd2bd108626124f22bc1cfe7652648d7`. The Desktop change remains one atlas row on `/Users/ameerdeen/progs/forge-desktop`, branch `adeen/phase70-extension-atlas`, base `4e29b0e5e46c4ec911d8aec1e96e3f6b21e8330c`. Root owns mission-control documentation and all Git, CI execution, publication and installation.

| Files | Change and ownership |
|---|---|
| [Extension README](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Terminal.Extensions/README.md), [extension project](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Terminal.Extensions/ForgeMission.Terminal.Extensions.csproj) | Retain component admission established before code: Why, Owns, Does not own, Change admission, API and dependency diagram. Independently packable net10.0/AOT-compatible assembly `ForgeMission.Terminal.Extensions`; package/namespace `Katasec.Forge.Terminal.Extensions`; candidate version 0.1.0. |
| [ClipboardResult](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Terminal.Extensions/ClipboardResult.cs), [ClipboardText](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Terminal.Extensions/ClipboardText.cs), [editor extensions](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Terminal.Extensions/EditorClipboardExtensions.cs), [Paragraph extensions](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Terminal.Extensions/ParagraphClipboardExtensions.cs), [menu guards](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Terminal.Extensions/ClipboardMenus.cs) | Complete the exact public result/configuration contract and internal fixed menu guards. References remain exactly UI `[3.10.0]` and Terminal `[2.2.0]`, without CLI/client/domain/Markdown/TextMate dependencies. |
| [Solution](/Users/ameerdeen/progs/forge-mcl/ForgeMission.slnx), [CLI project](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/ForgeMission.Cli.csproj), [test project](/Users/ameerdeen/progs/forge-mcl/tests/ForgeMission.Mcl.Tests/ForgeMission.Mcl.Tests.csproj) | Admit the component and retain same-repo CLI and direct extension-test references. |
| [TextInteraction](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/TextInteraction.cs) | Own current view/editor scope, live source registration, source claims, selection-aware Copy versus Stop and reactive feedback. Preserve source-level Bubble observers and metadata-preserving native command decoration. |
| [ParagraphSelection](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/ParagraphSelection.cs) | One zero-inset native-child wrapper for direct Paragraph or MarkdownControl. Configure direct Paragraphs before attachment, register only realized attached sources after native Arrange, reconcile/retire Paragraphs, disable owned TextBlock selection, and synchronously retire/resume realized snippet Buttons through the R12 lifecycle. |
| [ChatScreen](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/ChatScreen.cs), [ChatTui](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/ChatTui.cs), [FileEditor](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/FileEditor.cs) | Wire composer, `/edit`, Markdown and direct user/pending-user/notice/error sources through the common policy. Preserve the single named Markdown update boundary and retire before changed setters. Prefix existing progress/save status with clipboard feedback. Explicitly owned chrome is nonselectable. |
| [StartPage](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/StartPage.cs) | Set explicitly constructed prompt and option-label/description TextBlocks nonselectable. Preserve OptionList activation, focus, layout and appearance. |
| [ForgeCodeBlockRenderer](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/ForgeCodeBlockRenderer.cs), [CodeCopyControl](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/CodeCopyControl.cs) | Preserve complete immutable `context.Code` independently of display trimming. Compose the reserved native header/Button/tooltip. Implement exact R12 `Retire`/`Resume`, effective ancestor guards, generation/feedback continuation and existing detach/hide reset. |
| [ForgeTheme](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/ForgeTheme.cs), [ForgeStyles](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/Tui/ForgeStyles.cs) | Retain semantic theme ownership and named geometry. Map every native button/menu/tooltip/feedback state in both themes; no component-local palette. |
| [ClipboardTextTests](/Users/ameerdeen/progs/forge-mcl/tests/ForgeMission.Mcl.Tests/Terminal/ClipboardTextTests.cs), [ClipboardMenuTests](/Users/ameerdeen/progs/forge-mcl/tests/ForgeMission.Mcl.Tests/Terminal/ClipboardMenuTests.cs), [TerminalInteractionTestHost](/Users/ameerdeen/progs/forge-mcl/tests/ForgeMission.Mcl.Tests/Terminal/TerminalInteractionTestHost.cs) | Exercise actual public APIs, native routing/menu ordering and clipboard operation counts/failures through the memory-only backend. |
| [TextInteractionTests](/Users/ameerdeen/progs/forge-mcl/tests/ForgeMission.Mcl.Tests/Cli/TextInteractionTests.cs), [CodeCopyControlTests](/Users/ameerdeen/progs/forge-mcl/tests/ForgeMission.Mcl.Tests/Cli/CodeCopyControlTests.cs) | Verify actual CLI routing, whole-source/chrome audit, registration lifetime, native rendered feedback and R12 setter-before-input/layout containment. |
| [FileEditorTests](/Users/ameerdeen/progs/forge-mcl/tests/ForgeMission.Mcl.Tests/Cli/FileEditorTests.cs), [ChatScreenTileTests](/Users/ameerdeen/progs/forge-mcl/tests/ForgeMission.Mcl.Tests/Cli/ChatScreenTileTests.cs), [ChatScreenMotionTests](/Users/ameerdeen/progs/forge-mcl/tests/ForgeMission.Mcl.Tests/Cli/ChatScreenMotionTests.cs), [StartPageTests](/Users/ameerdeen/progs/forge-mcl/tests/ForgeMission.Mcl.Tests/Cli/StartPageTests.cs) | Reconcile constructor/header-row expectations and preserve save/close, graphics, motion, user-pill geometry and StartPage regressions. |
| [Makefile](/Users/ameerdeen/progs/forge-mcl/Makefile), [package verifier](/Users/ameerdeen/progs/forge-mcl/eng/verify-terminal-extensions-package.sh), [package workflow](/Users/ameerdeen/progs/forge-mcl/.github/workflows/publish-terminal-extensions-package.yml) | Complete package test/pack/verifier routes. Add R12 `pull_request` verification on `macos-14`, with read-only authority, complete warning enforcement, native identity/artifacts and event-gated publication depending on verification. |
| [Repository README](/Users/ameerdeen/progs/forge-mcl/README.md), [CLI README](/Users/ameerdeen/progs/forge-mcl/src/ForgeMission.Cli/README.md), [Desktop atlas](/Users/ameerdeen/progs/forge-desktop/src/README.md) | Reconcile component/API/build inventory and one external-package atlas row. Desktop remains documentation-only and does not consume the package. |

Reuse `ComposerEditor`, `ForgeMarkdown`, `Transcript`, `EditFile`, CodeColours, Kitty adapters and the current native package family. Preserve existing Core files/versions/workflows, `release.yml` and CLI linker targets.

## 2. Reuse

| New or changed item | Existing equivalent checked | Decision |
|---|---|---|
| Clipboard result/helper | Forge source search; native `ISelectionOwner.TryCopySelection` and TerminalClipboard bool transport | Add only the locked common result vocabulary. Extract once and use one native write; no transport replacement. |
| Editor/Paragraph configuration | Native commands, menu factories, `CommandTarget` and nullable Paste context | Replace editor Copy only. Retain native ranges, editing, menu invocation/focus and Paste/Undo. |
| Fixed menu guard | Public App/Parent, effective enabled/visible ancestors, document identity/version and Paragraph.Text | Capture invocation facts and validate the same pure guard for availability and execution. No CLI registry callback or range snapshot. |
| TextInteraction | Existing ChatTui commands, Wake/Stop, routed events and `ClearSelection` | One presentation policy supplies missing source claims and Copy precedence without copying editing logic. |
| Native command decoration | Public Command metadata and Execute | Preserve every public field; replace only Execute with Claim followed by the original delegate exactly once. |
| ParagraphSelection | Native Padder, enumeration, Measure/Arrange and existing content factories | One wrapper handles realized-source lifetime. Configuration before attachment does not register discarded visuals. |
| R12 synchronous button retirement | Existing host pre-setter boundary, wrapper subtree enumeration and Button.Reset | Extend those existing owners. No separate button registry, attachment-only resume or global sweep. |
| R12 enabled-state restoration | Public Button.IsEnabled and existing reset/generation | One nullable prior-enabled marker preserves deliberately disabled state and repeated retirement. |
| Effective button eligibility | Public attachment and ancestor eligibility already used by fixed menus | Apply the same declared eligibility requirement before transport and feedback continuation. |
| Owned TextBlock chrome | Native `TextBlock.IsSelectable` pointer gate | Set false at construction or scoped realization; add no TextBlock selection implementation. |
| Snippet composition | Existing renderer, Paragraph.Runs, TextMate, TileFrame, native Button/TooltipHost | Add one reserved row and retain syntax/display ownership. |
| Feedback/styles | Native reactive State/SetStyle and existing ForgeTheme/ForgeStyles | Keep source identity and result reactive so already-focused controls update without resize or focus changes. |
| Tests | Serialized `XenoAtomUiCollection`, native app loop, ForgeText and public rendering | Exercise real routing and output. Existing CLI test loading remains test-only; no production private access/reflection. |
| Package verification/publication | Core metadata, Makefile/verifier and merged-ref publication pattern | Follow the existing package route without changing Core or adding a release framework. |
| Pre-merge AOT/delivery | Existing release macOS-14 environment, prerequisites, native publish and complete archives | Add verification to the planned package workflow; retain the unchanged main-only CLI release route. Historical successful release logs establish environment viability only. |

`TileFrame.OneLineOr` accepts Visual and chooses framing from native measured height. The wrapper must preserve those measurements.

Implement and verify this unchanged public surface:

```csharp
namespace Katasec.Forge.Terminal.Extensions;

public enum ClipboardResult
{
    NoSelection, Copied, CopyFailed, PasteReadSucceeded, PasteReadFailed
}

public static class ClipboardText
{
    public static ClipboardResult CopySelection(
        XenoAtom.Terminal.UI.Input.ISelectionOwner source,
        XenoAtom.Terminal.TerminalInstance terminal);

    public static ClipboardResult CopyText(
        string text, XenoAtom.Terminal.TerminalInstance terminal);
}

public static class EditorClipboardExtensions
{
    public static void ConfigureClipboard(
        this XenoAtom.Terminal.UI.Controls.TextEditorBase editor,
        Action<ClipboardResult> report);
}

public static class ParagraphClipboardExtensions
{
    public static void ConfigureClipboard(
        this XenoAtom.Terminal.UI.Controls.Paragraph paragraph,
        Action<ClipboardResult> report);
}
```

| Contract | Implementation |
|---|---|
| Copy result | NoSelection only when `HasSelection=false`. Failed or empty extraction returns CopyFailed with zero writes. Otherwise CopySelection calls CopyText once. CopyText writes the exact string, including valid empty whole-code text, and maps the native bool. Null arguments are programmer errors; exceptions retain the UI-loop boundary. |
| Configuration | Set owned source `IsSelectable=false`. Configure editors once before attachment; harmless Paragraph factory replacement accumulates no handlers. Editor Copy uses HasSelection and `ConsumesGestureWhenUnavailable=false`. |
| Native menu | Editor items exactly Copy/Paste; Paragraph exactly Copy. Explicit original `CommandTarget`; retain invocation-before-close. No newly exposed Cut. |
| Menu lifetime | Capture original source/app/parent and validate current attachment/effective ancestor eligibility plus editor document reference/version or complete Paragraph.Text. Copy additionally requires HasSelection. Stale actions perform no transport/edit and produce no fabricated failure. |
| Menu Paste | Resolve the retained current `TextEditor.Paste` command when the factory runs, after CLI decoration; guarded execution invokes its public `Execute(editor)`. |
| Paste result | Feedback-only handler reads `context.Text` and returns null. Null reports PasteReadFailed; successful empty/nonempty reports PasteReadSucceeded and clears clipboard feedback. Empty remains native no-op; nonempty uses native replacement/undo. Bracketed Paste remains native event-text insertion without rereading clipboard. |
| Reporting | Synchronous UI-thread transport facts only. Callbacks do not edit, restore ranges, change focus/tree or close menus. CLI maps results and claims the fixed source. |

The native command decorator preserves `Id`, `LabelMarkup`, `Name`, `DescriptionMarkup`, `SearchText`, `Gesture`, `Sequence`, `Importance`, `Presentation`, `CanExecute`, `IsVisible`, `ConsumesGestureWhenUnavailable` and `RouteGesture`.

## 3. Sequence

1. Complete both sequential full plan reviews and await explicit `PLAN APPROVED`. Root retains branch/Git/document ownership.
2. Reconcile the held README, inventories, project references and package admission against the complete contract before correcting code.
3. Complete and verify the public extensions: one extraction/write routine, fixed guarded menus, replacement editor Copy and feedback-only native Paste handler.
4. Complete TextInteraction:
   - Configure editors before attachment.
   - Subscribe on each source’s actual Bubble KeyDown/TextInput/Paste delivery without handling native input.
   - Decorate existing native editor commands except Copy after construction.
   - Preview left Down resolves the nearest registered owned editor/Paragraph through OriginalSource’s parent chain, claims it and clears other ranges.
   - Copy chooses a selected focused owned editor first, otherwise the remembered current rendered source. Selected failures consume; no-selection chat retains Stop; `/edit` remains isolated.
   - Retire old screen/editor targets synchronously; popup focus excursions do not claim another source.
5. Complete the single ParagraphSelection lifecycle:
   - Direct Paragraph configuration occurs before attachment without live registration.
   - Delegate native Measure/Arrange with zero added geometry.
   - After native Arrange, retire absent Paragraphs and configure/register attached current Paragraphs; disable owned descendant TextBlock selection.
   - Registry contains live realized sources only and releases removed trees.
6. Retain ChatScreen’s one named Markdown mutation boundary. Compare both Pipeline and Markdown first. When either differs, call wrapper Retire once before either native setter. Unchanged content retains selection/feedback under normal lifetime rules.
7. Implement exact R12 retirement:

| Entry point | Exact lifecycle |
|---|---|
| `ParagraphSelection.Retire(): void` | Retire/unregister/clear/disable registered Paragraphs. While attached to a non-null app, enumerate currently attached CodeCopyButton descendants within its own child subtree and call Retire synchronously. No button registry. |
| `CodeCopyButton.Retire(): void` | Capture current IsEnabled only when `bool? _enabledBeforeRetirement` is null. Reuse Reset to invalidate generation/pending/feedback and clear public IsPressed; then disable. Repeated calls preserve the first captured value. |
| `CodeCopyButton.Resume(): void` | If the marker is null, return. Otherwise clear it and restore precisely the captured enabled value, including false. Restore no old focus, press, payload or feedback. |
| Wrapper post-native Arrange | Enumerate current attached descendant buttons with `button.App == wrapper.App` and call Resume. Obsolete removed visuals cannot resume; retained/recycled current visuals can. Header remains width/Tab owner. |
| Activation/continuation | Before transport and before feedback, require the original app/parent, current generation and effective enabled/visible ancestor path. Retirement invalidates the attempt even if later resumed. Clear pending state in finally. Accepted writes cannot be undone; obsolete feedback cannot revive. |
| Detach/width0 | Retain existing Button reset and wrapper retirement. No private capture/hover manipulation or OnAttached resume override. |

8. Finish the complete source/chrome sweep: composer/CodeEditor; direct user/pending-user/notice/error Paragraphs; realized Markdown body/list/quote/HTML/table/code Paragraphs; construction-time ChatScreen/FileEditor/StartPage/snippet-label chrome; scoped realized Markdown alert-title TextBlocks. Preserve unrelated native menu/framework controls. A newly discovered owned source type returns to the supervisor.
9. Finish native snippet composition, exact immutable payload, reserved header measurement, styles and reactive feedback. Keep pseudo-headings excluded and native Paragraph/Runs/TextMate/wrapping/TileFrame intact.
10. Complete package workflow R12 verification: add `pull_request`; a `macos-14` verify job with `contents: read`, `packages: read`; existing scoped NuGet authentication and OpenSSL/Brotli prerequisites; selected-commit package route and CLI native publish; full warning/identity artifacts. Publish depends on verify and runs only on the existing tag/manual publication events.
11. Run fresh verification below and return material deviations immediately. Return the full completion evidence without marking the task complete.
12. Root obtains both complete sequential code reviews, independently checks evidence/tree identity, then owns CI, merge, package publication, CLI release, ZIP installation and attributed operator acceptance.

## 4. Verification

All checks apply to the corrected current artifact. Scratch feasibility, held-draft results and historical release logs are supporting history only.

| Layer | Required positive and negative observations |
|---|---|
| Public Copy API | No selection; failed/empty extraction; exact nonempty and empty whole-text writes; true/false transport; explicit retry; propagated exceptions. Assert zero or one extraction/write as appropriate, without logging payloads. |
| No-click editor input | Actual composer and CodeEditor Shift+arrows/Home/End, native word selection and Ctrl+A→Ctrl+C in one pending batch before clicking. Verify native range/caret/highlight and exact copied text. |
| Source claims/native delegation | Mouse drag/double/Shift-click; source Bubble raw navigation/typing/bracketed Paste; native command claims after Paragraph selection; every metadata field retained; original editing delegates execute once. |
| Copy versus Stop | Actual root/chat routing with an active turn: selected editor or remembered Paragraph Copy success, extraction failure and write failure never Stop. No applicable selection preserves chat Stop. `/edit` Copy/no-selection/navigation cannot trigger chat shortcuts. Popup scope blocks underlay actions. |
| Native menus/Paste | Real right-click factory, explicit source target and keyboard/mouse activation; backwards/multiline range continuity. Copy keeps range. Escape and outside/Tab close milestones preserve native behavior. Failed read and successful empty read retain document/range/caret/undo with distinct feedback. Nonempty menu/keyboard Paste replaces the native range; one native Undo restores it. Bracketed Paste rereads no clipboard. Retain keyboard Cut regression behavior without menu Cut. |
| Menu stale refusal | Document replacement/version change, Paragraph.Text change, source/ancestor disable or hide, detach and screen swap. Test availability and execution separately; zero stale writes/reads/edits and no false result. Do not use a CLI registry predicate to substitute for the package guard. |
| Whole-source audit | Exercise direct user/pending-user/notice/error text and representative native Markdown list/quote/HTML/table/alert/code paths. Chrome drag cannot intercept content Copy or applicable no-selection Stop. No owned native unchecked app selection path remains. Preserve StartPage OptionList input/layout and custom renderer’s unreachable Log fallback. |
| Registration/layout | First realization, inner scrolling, genuine outer DocumentFlow recycling, streamed replacement and editor/screen swaps. Configure without registering discarded/unrealized direct wrappers. Prove live bounded registry and released removed trees; unchanged-source ranges remain. Cover direct reattachment and single-row/wrapped user-pill geometry. |
| Paragraph pre-layout retirement | Changed setter followed by pointer/menu/Copy in the same pending drain before layout cannot revive a retired Paragraph or captured menu. Current post-Arrange sources become eligible through native realization/configuration. |
| R12 actual snippet race | Through actual `ChatScreen.Show`, post changed content before a queued old mouse release drains. Observe old Button disabled, IsPressed=false, no revived feedback and zero writes before layout; fresh replacement mouse/Enter/Space copies exact new payload. Repeat for Pipeline-only change. |
| R12 reuse/continuation | Repeated Retire preserves the first enabled value; Resume restores true or deliberately false precisely. Cover unchanged content, retained current visuals, outer recycling, detach/reuse, width0/show and old release. Source/ancestor hide/disable and retirement during synchronous transport suppress obsolete feedback; pending clears and fresh eligible activation succeeds. |
| Exact snippet payload | Indentation, blank lines, LF normalization, deliberate trailing newline, empty payload, wrapping and unknown language. Compare the actual complete render-context Code; do not mistake the fence separator newline for payload or reconstruct fences. Pseudo-headings receive no button. |
| Reactive native feedback | An already-focused idle button immediately renders Copied/Copy failed label, tooltip and style without resize/focus changes. Consecutive success on different buttons updates the current button and resets the previous one. Observe actual native cells/runs/state, not only ResultFor assertions. |
| Focused tests | Run serialized Terminal interaction tests, then TextInteraction, CodeCopyControl, FileEditor, ChatScreenTile, ChatScreenMotion and StartPage filters. Record actual counts/results and fix failures before broadening checks. |
| Full suite | `make build` and `make test`, with zero warnings/errors; record existing skips accurately. Preserve save/close, graphics, motion, syntax/color ownership and private-access boundary checks. |
| Actual package | `make verify-terminal-extensions-package`: actual tests/build/pack; ID/version, exact pins, README/license, net10.0 assembly and source-commit verification. Inspect nupkg digest and contents. |
| Isolated public consumer | Restore/build/run a fresh external scratch consumer from the packed nupkg with an isolated cache and no CLI/project reference. Compile/use every public API/result and inspect resolved extension/native dependency versions. Candidate base-HEAD metadata is labelled a controlled check; it is not proof that uncommitted code originated from that commit. Repeat provenance against committed/released artifacts under root. |
| Canonical local AOT | Publish actual CLI `osx-arm64` with `-warnaserror`, unchanged linker target and full retained output; run native help/version and record digest. A zero exit with linker warnings remains FAIL. Do not alter OS, installed libraries, environment, minimum OS or suppression to manufacture PASS. |
| Pre-merge CI AOT | Root executes the new read-only macos-14 verify job on the reviewed tree. Package tests/pack/verifier and `dotnet publish src/ForgeMission.Cli -c Release -r osx-arm64` use `-warnaserror` and exact SourceRevisionId. Capture complete output; fail on nonzero exit or any actual compiler/linker warning, including `ld: warning:`. `0 Warning(s)` summaries do not count as warning lines. Retain full log, source identity, native help/version/digest and artifacts; compare tested tree to merge candidate. Actual PASS is required before merge. |
| Publication guard | Verify PR events cannot publish and publication depends on successful verify. Root publishes only an approved merged ref, rechecks 0.1.0 immutability, and observes private repository association, exactly one version, committed provenance and authenticated restore. No unrelated Core or ACL changes. |
| CLI release/install | Root rechecks unused patch version—v0.9.4 is a candidate, not a reserved release—then dispatches unchanged `release.yml` from merged main. Observe all four host/RID native builds/help/version and all eight ZIP/checksum assets; inspect actual warning logs. Download/verify the complete macOS ARM64 ZIP, preserve every sidecar, check native version/source, extract complete matching payload into `/Users/ameerdeen/.local/bin`, and record installed digests and normal PATH resolution. |

Concrete focused commands use the existing test project:

```text
dotnet test tests/ForgeMission.Mcl.Tests/ForgeMission.Mcl.Tests.csproj -warnaserror --filter "FullyQualifiedName~ForgeMission.Tests.Terminal"
dotnet test tests/ForgeMission.Mcl.Tests/ForgeMission.Mcl.Tests.csproj -warnaserror --filter "FullyQualifiedName~TextInteractionTests|FullyQualifiedName~CodeCopyControlTests|FullyQualifiedName~FileEditorTests|FullyQualifiedName~ChatScreenTileTests|FullyQualifiedName~ChatScreenMotionTests|FullyQualifiedName~StartPageTests"
make build
make test
make verify-terminal-extensions-package
```

**UI reference contract**

Apply [Desktop Interaction Principles](../design/desktop-interaction-principles.md), [UI Design System](../design/ui-design-system.md) and [TUI graphics](../design/tui-graphics.md).

Binding owned-slice galleries are [light](../images/phase-70.1/foundation-light.svg), [dark](../images/phase-70.1/foundation-dark.svg) and [colour evidence](../evidence/phase-70/foundation-colours.json). Preserve surrounding appearance from [finish-line mockup](../design/forge_tui_finish_line_mockup.html), [plain turn](../images/phase-64-plain-turn.png), [hands turn](../images/phase-64-hands-read.png) and [operator reference](../images/phase-70/chat-user-reference.png).

| Owned slice | Required comparison |
|---|---|
| Geometry | Synthetic 10×20 cells; normal100×32/narrow60×24; all four `[60,100]×[24,32]` corners and continuous resize. Frame `(10,8,W−20,height)`, insets left/right4/top2/bottom1, content `W−28`; added header y10 moves unchanged code to y11; Button `(W−29,10,15,1)`. Values remain named theme/composition geometry. |
| Width states | Width0 retains one header row and hides/untabs/resets the same Button. Restore positive-width visibility before native measurement. Width1–2 glyph with zero padding; width3 one-cell side padding; below15 icon/full tooltip; at15 full fixed-width label. Native focus repair stays authoritative. |
| Native button states | `⧉ Copy code`, `✓ Copied`, `! Copy failed`; tooltips Copy code/Copied/Copy failed. Bold in every native state, focus underline, semantic hover, pressed/focused underline and combined states; disabled precedence. Retire/Resume cannot restore old focus/press/feedback. |
| Menus/ranges/status | Editor Copy/Paste17×6, Paragraph Copy-only16×5; native border/padding1, shortcut gap2. Selected/hovered/disabled states. Backwards beta selection/caret continuity through cancellation and failed Paste. Feedback row at H−2 prefixes progress/save status using ` · `. |
| Theme token pairs | CodeBlockText, TextMuted, Success or Error on CodeBlockFill; pressed CodeBlockText/Selection. Menu/tooltip Text/SurfaceAlt; selected TextStrong/Selection; hover TextStrong/SurfaceAlt; disabled TextMuted/SurfaceAlt; Border/SurfaceAlt. Preserve body/editor/TextMate selection foregrounds. |
| Theme/reset | Existing ForgeConfig dark/light; absent theme remains dark. ForgeTheme→ForgeStyles owns mappings. Reset on next action/source edit/loss of both focus and hover/detach/replacement/hide; no timer or new theme selector. |
| Fresh controlled evidence | Compare actual current product native bounds/cells/runs/tooltips and combined states against both complete galleries. Include recognized-language selected syntax runs beyond the historical csharp fixture. An unreadable pair or reference mismatch returns to design. Preserve Kitty/headings/frame/motion/composer/StartPage. Synthetic native evidence does not establish Retina acceptance. |

**Installed default-path acceptance**

Root delivers the verified merged release artifact before manual acceptance. The operator uses normal saved sign-in, absent `FORGE_API_ENDPOINT`, normal hosted dependencies, Ghostty with truecolor/Kitty and no multiplexer. Use a dedicated scratch folder initialized through normal `forge project create`, plain Chat without hands, synthetic messages and a disposable `/edit` file.

Record artifact/version/commit/digest, Ghostty version, laptop Retina provenance, font/cell/window metrics, both themes and normal/narrow windows. Observe no-click keyboard gestures, app mouse selection versus terminal selection, exact pasted text, menu cancellation/Paste/Undo, editor save/close, active-turn Copy, snippet payload/feedback/replacement and graphics comparison. Restore temporary theme configuration. Operator observations are attributed; root assesses and records coverage. No agent Ghostty/OS clipboard access, alternate capture/input or bypass.

Done when:

> Composer and `/edit` provide the agreed selection and contextual Copy/Paste behaviour without requiring a prior click; selection-aware Copy never cancels a turn; snippet controls copy exact code with truthful feedback; themes and graphics match the binding reference on laptop Retina; focused interaction tests, required suite and Native AOT checks pass with zero warnings; operator-run installed default-path acceptance passes and the supervisor records its evidence. Continuous rich selection closes in 70.2, not through a snippet-button substitute.

Until manual observations pass, delivered status remains **implemented; awaiting manual acceptance**. Rich logical headings, alert-title and continuous multi-Paragraph semantics remain required in the dependent rich-selection spoke.

Security tier/store/service-identity changes are N/A. This is local presentation with explicit clipboard actions; copied payloads are neither telemetry nor model input. CI verification uses scoped read authority; publication remains a separate deliberate permission boundary.

## 5. Principles that changed a decision

| Implementer rule | Choice |
|---|---|
| 1 — No NIH | Retain native ranges/editing/menu/Paste/Undo/Button/tooltip/transport and existing packaging/release routes. Reuse current host/wrapper/reset for R12. |
| 2 — No duplicate paths | One extraction/write result, one Paragraph lifetime wrapper and one pre-setter retirement boundary; no second clipboard or unchecked owned-source path. |
| 3 — Minimum needed | Add only the nullable button retirement marker and planned verification job. TextBlock chrome uses its native flag; rich selection remains separate. |
| 4 — No speculative abstractions | No button registry, lifecycle callback framework, wrapper mode, attachment-only resume or generic selection snapshot. |
| 5 — Stay in scope | Implement locked R12 exactly; return new source types, visual mismatches or delivery-contract deviations to root. |
| 6 — Verified means done | Require fresh product/package/consumer/CI observations and attributed installed acceptance. Historical held checks establish no current PASS. |
| 7–9 — Outline, small functions, top-down | Put routing and lifecycle entry points first; helpers express coherent guard, transport, reconciliation or presentation steps. |
| 10–12 — Explicit errors, shallow flow, separate side effects | Preserve bool/null facts, reject stale actions early, isolate clipboard writes and retain native insertion. Workflow fails on actual warnings even when publish exits zero. |
| 13 — Zero warnings | Preserve canonical linker ownership, require enforced current macos-14 evidence, and retain local warnings as FAIL without suppression or environment alteration. |
| 14–15 — Real extraction, bounded complexity | Extract only meaningful ownership/failure/test seams. Assess materially changed functions at ≤15, preferably≤10, without splitting trivial helpers to reduce a score. |

## 6. Open questions and assumptions

None. R12 resolves the lifecycle and verification/delivery placement.

Candidate package0.1.0 and CLI patch version require root’s immutable-version recheck before publication; collisions return to root rather than silently changing versions. Actual CI/AOT success, code-review acceptance, publication, installation and operator observations remain execution gates, not assumed results.
