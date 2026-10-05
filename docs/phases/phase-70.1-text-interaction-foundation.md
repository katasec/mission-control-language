# Phase 70.1 — Text-interaction foundation

**Status: discovery complete at source/controlled layers; live reproduction and product design open.**
**Not build-ready.** Parent: [Phase 70](phase-70-tui-text-interaction.md).

## Requirement

Make composer, file-editor, and rendered-text Copy/Paste behave consistently, using native
XenoAtom selection, commands, context menus, buttons, and clipboard transport wherever they
meet the contract. Add a copy icon to real code snippets while preserving syntax highlighting
and the established UI. Shared behaviour belongs at the TUI's text-interaction boundary;
individual controls retain editing/rendering ownership.

## Constraints for design

| Area | Required behaviour / existing public capability |
|---|---|
| Keyboard editing | Retain PromptEditor/CodeEditor selection, word/line navigation, Select All, paste events and undo. Prove Shift+arrows/Home/End and word selection without a preceding mouse click. |
| Copy vs Stop | A selection's Copy attempt consumes the gesture even if clipboard writing fails; it never cancels a turn. With no applicable selection, preserve the current chat Stop contract. `/edit` continues to isolate chat shortcuts. |
| Mouse editing | Drag, double-click and Shift-click use the native text editor. Right-click offers Copy/Paste and appropriate native editing actions; menu cancellation restores focus and selection. Define paste replacement range before opening a popup can change focus. |
| Shared ownership | One reviewed policy for choosing the current text owner, taking its payload and reporting clipboard results; composer, editor, snippet and later transcript actions use it. Do not create a second editor, OS clipboard backend, global input framework, or speculative control hierarchy. |
| Clipboard | Use `App.Terminal.Clipboard.TrySetText` / `TryGetText`, not screen scraping or shell commands. `CanSetText`/`CanGetText` are hints, not proof. Report success only after a successful operation; a failed paste leaves draft and selection unchanged. Successful empty text is distinct from a failed read. No automatic clipboard polling. |
| Code payload | Copy the Markdown render context's complete `.Code` (its LF-normalized text), without fences, line numbers, UI labels, syntax markup, image placeholders or visual wraps. Preserve indentation, blank lines and trailing newlines; current display `TrimEnd('\n')` must not trim the copy payload. |
| Code control | Use a native focusable `Button` and shared clipboard policy. Label/tooltip identifies Copy code; reserve layout space rather than cover code. Support mouse and keyboard activation, focus/hover, copied and failed states. Image-heading pseudo-fences are excluded. |
| Streaming | Define whether an activation copies current displayed content or an invocation snapshot, including menu-open updates and code-fence replacement. A stale visual must never copy a different snippet accidentally. |
| Existing rendering | Keep TextMate `Paragraph.Runs`, wrapping, TileFrame and Kitty images. FadeIn, StreamCaret and LinkPointer overlays stay non-hit-testable. Copy operates on text; decorative image cells are not content. |

These are outcome constraints, not a proposed API. Native commands currently ignore clipboard
failure, and popup focus can clear selection. Merely restoring `TextEditor.Copy` or adding
`CommandPresentation.ContextMenu` does not satisfy the complete contract. Native Cut can delete
after a failed copy: do not newly expose Cut without defining safe failure behaviour. A broader
clipboard/editor rewrite is outside the requested slice.

## Design questions to close before an implementation plan

| Question | Required design output |
|---|---|
| Keyboard and mouse Copy routing | Concrete owner precedence and actual public hooks, including TerminalApp's pre-command active-selection interception; no-selection Stop and failed-copy handling. Confirm what Ghostty delivers for Ctrl/Cmd/terminal-native gestures. |
| Context-menu lifetime | Exact payload/range capture, focus restoration, cancelled-menu behaviour and document-version handling, using public APIs. `SelectionStart`/`SelectionLength` are protected, not public integration points. |
| Honest results | Concrete way all owned Copy/Paste paths observe the bool result. Decide a narrow Forge adaptation or reviewed upstream extension; define types, signatures, ownership and Native AOT implications. No invented library APIs or new private access. |
| Visual interaction | Binding revised reference for snippet icon/menu and feedback; exact placement, focus order, disappearance/reset rules, light/dark tokens and contrast pairs. |

## Files and tests to inspect

All product paths below are relative to `/Users/ameerdeen/progs/forge-mcl`.

| Responsibility | Entry paths |
|---|---|
| Composer and routing | `src/ForgeMission.Cli/Tui/ChatScreen.cs`, `ComposerEditor.cs`, `ChatTui.cs` |
| Editor | `src/ForgeMission.Cli/Tui/FileEditor.cs`, `EditFile.cs` |
| Snippets and source text | `src/ForgeMission.Cli/Tui/ForgeCodeBlockRenderer.cs`, `ForgeMarkdown.cs`, `Transcript.cs` |
| Theme/style ownership | `src/ForgeMission.Cli/Tui/ForgeTheme.cs`, `ForgeStyles.cs`, `Graphics/CodeColours.cs` |
| Existing verification | `tests/ForgeMission.Mcl.Tests/Cli/ChatScreenTileTests.cs`, `ChatScreenMotionTests.cs`, `FileEditorTests.cs`, `ChatTranscriptTests.cs`, `StartPageTests.cs` |

The nearest component README is `src/ForgeMission.Cli/README.md`; there is no Tui README.
Reconfirm dependency versions and owning repository instructions before design.

## Gates and acceptance specification

| Gate | Application |
|---|---|
| Security Architecture | Local presentation/input only; hosted tiers, stores and service identities are N/A. Clipboard reads occur only on explicit Paste; copied text is not telemetry or model input. Any package/public-contract ownership change must be resolved before handoff. |
| Engineering Philosophy | CLI TUI owns policy and feedback; native controls own editing; XenoAtom owns portable clipboard transport. Define failure containment and narrow lifetime ownership. Reject per-control patches, unneeded knobs, a replacement editor and private API expansion. |
| UI principles | Read and name [Desktop Interaction Principles](../design/desktop-interaction-principles.md) and [UI Design System](../design/ui-design-system.md) in assignments. Apply their interaction/token principles; this terminal UI does not consume ForgeUI CSS. |
| TUI reference | [Graphics design](../design/tui-graphics.md), [finish-line mockup](../design/forge_tui_finish_line_mockup.html), [plain turn](../images/phase-64-plain-turn.png), [hands turn](../images/phase-64-hands-read.png), and [operator's chat example](../images/phase-70/chat-user-reference.png). The operator example binds appearance, not proof of live Retina verification. A revised reference must lock new owned controls before build. |
| Theme map | Existing `ForgeConfig` `theme: dark|light` selects `ForgeTheme`; missing theme is dark. `ForgeStyles.Screen` maps text/surface, popup, selection, focus, controls, disabled, success and error tokens. Designer must map every new state to these named tokens or add semantic values to both theme instances; no local colour literals. |
| Viewports and states | Laptop Retina only: record real font, cell metrics, window dimensions, normal and narrowed widths. Both themes; idle/streaming composer, multiline selection, context menu open/cancel, editor selection, wrapped/unwrapped/unknown-language snippets, copy success/failure and paste failure. Start page remains visually intact. |

### Default-path facts — future product acceptance

| Fact | Required observation |
|---|---|
| Artifact | Installed Native AOT `forge` from merged code via `make install`, or the normal CLI release archive; record version, commit and digest. A JIT/in-memory harness is controlled evidence only. |
| Configuration | Normal saved sign-in and `forge.project.json`; `FORGE_API_ENDPOINT` absent, no URL/client/mission stubs. Dark default and supported light config, with any temporary theme change restored afterward. |
| Dependency route | Normal hosted ForgeAPI/Conversation Host route and existing published client dependencies; Ghostty with truecolor/Kitty support, no multiplexer. Record actual terminal version and display provenance. |
| Safe state | A dedicated scratch folder initialized through normal `forge project create`; plain Chat (no hands grant). Use synthetic non-sensitive messages and an explicitly created disposable file for `/edit`. |
| User actions | Open `forge chat`, obtain/replay a real normal-path reply with known code, select/copy/paste using keyboard and mouse, copy snippet, edit scratch file, and verify no copy attempt cancels a turn. |
| Observable result | Paste into a scratch destination and compare exact expected text. Supervisor sees themed selection and button/menu states on Retina. Default-path operation passes; denied/failed clipboard behaviour may additionally be verified with controlled fault injection. |

## Ordered tasks

| Task | State | Done when |
|---|---|---|
| 1. Source/library/CodeAlta discovery | Done — [evidence](phase-70.1-text-interaction-foundation_completed.md#discovery-results). | Pinned source and controlled observations distinguish native capability, Forge wiring defects, and library gaps. |
| 2. Installed Retina reproduction | Open; Ghostty computer-use access denied. | Record actual events/focus/highlight/clipboard results for reported gestures, and separate terminal-native mouse selection from app selection. No bypass of the tool restriction. |
| 3. Product design and review | Pending after scope gaps close. | Fresh designer; simplicity and ownership reviews; supervisor records one locked design with every question above resolved and complete API/visual contracts. |
| 4. Implementation plan and review | Pending after design approval. | Fresh plan author; simplicity and ownership reviews; supervisor explicitly approves bounded changes, meaningful interaction tests and AOT checks. |
| 5. Implementation and review | Pending after plan approval. | Fresh implementer; positive/negative interaction evidence including failed clipboard writes; independent simplicity/ownership/style review; no product task marked complete by implementer. |
| 6. Merge, install, accept, close | Pending after checks pass. | Normal artifacts merged/published as required; default-path observations above pass; evidence/timing archived; changed repos clean on main. |

## Done when

Composer and `/edit` provide the agreed selection and contextual Copy/Paste behaviour without
requiring a prior click; selection-aware Copy never cancels a turn; snippet controls copy exact
code with truthful feedback; themes and graphics match the binding reference on laptop Retina;
focused interaction tests, required suite and Native AOT checks pass with zero warnings;
installed default-path acceptance passes. Continuous rich selection closes in
[70.2](phase-70.2-rich-transcript-selection.md), not through a snippet-button substitute.
