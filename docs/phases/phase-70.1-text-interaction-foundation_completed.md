# Phase 70.1 — Completed discovery evidence

**2026-10-05. Source and controlled investigation only.** This record does not mark
[70.1](phase-70.1-text-interaction-foundation.md) or [Phase 70](phase-70-tui-text-interaction.md)
complete. Product design, implementation, installed default-path and Retina visual acceptance
remain pending. No product code, package publication, ACL, account or hosted chat was changed.

## Provenance

| Item | Observed baseline |
|---|---|
| Forge source | `katasec/forge-mcl` main `2022b512dd2bd108626124f22bc1cfe7652648d7`; owning [CLI README](https://github.com/katasec/forge-mcl/blob/2022b512dd2bd108626124f22bc1cfe7652648d7/src/ForgeMission.Cli/README.md). |
| UI packages | `XenoAtom.Terminal.UI`, `.Extensions.Markdown`, `.Extensions.TextMate` 3.10.0; UI NuGet repository commit `6f4e0cde3890d8ce2510ac0451b861863e4aeeaa`. |
| Terminal package | `XenoAtom.Terminal` 2.2.0; NuGet repository commit `5517cb3d8cdf0532ecc89260067064f98cde6137`. |
| CodeAlta reference | `/Users/ameerdeen/progs/CodeAlta`, commit `823f847297aafb0662ee317c3434c96a755e32fa`, UI 3.9.0. Read-only reference, not a newer-version capability claim. |
| Installed CLI metadata | `/Users/ameerdeen/.local/bin/forge`, version `1.0.0+4d232d66bf3aef83e5d249b178b17fe95b0f67b5`; SHA256 `575E18D960136EA788D45F7C8D737B11384D57C83ACFF95ECC7E770B6A5AE1C1`. `git diff 4d232d66bf3aef83e5d249b178b17fe95b0f67b5 HEAD -- src/ForgeMission.Cli/Tui` was empty. This is source parity, not execution acceptance. |
| Controlled CLI assembly | Existing `src/ForgeMission.Cli/bin/Debug/net10.0/forge.dll`; SHA256 `1B38A2F5ED3DEB77B96AD6C946F0CD4A56B93809D8AE9C8A32F134C4906EB62D`, 681984 bytes, built 2026-10-04 22:33:51 UTC. Loaded into a disposable JIT probe. |
| User reference | [Chat example](../images/phase-70/chat-user-reference.png), copied unchanged from the operator attachment (1014×601). An appearance reference, not a fresh running-TUI capture. |

## Discovery results

| Surface / capability | Evidence | Consequence |
|---|---|---|
| Start, chat, edit | CLI README and `Tui/StartPage.cs`, `ChatScreen.cs`, `FileEditor.cs`. Start offers Chat with a mission; Create is a placeholder. `/edit` uses CodeEditor, TextDocument and TextMate with Ctrl+S/Esc. | Preserve native editing; start/create work is outside this phase. |
| Native keyboard selection | UI `Controls/TextEditorBase.cs` and `TextEditorCore.cs`; PromptEditor/CodeEditor share selection/navigation. Controlled Shift+Left selects `beta`. | The library already provides the main editor mechanics. Physical terminal delivery/highlight still needs live reproduction. |
| Forge Copy removal | [ChatScreen](https://github.com/katasec/forge-mcl/blob/2022b512dd2bd108626124f22bc1cfe7652648d7/src/ForgeMission.Cli/Tui/ChatScreen.cs#L433) removes `TextEditor.Copy` at line 445; [ChatTui](https://github.com/katasec/forge-mcl/blob/2022b512dd2bd108626124f22bc1cfe7652648d7/src/ForgeMission.Cli/Tui/ChatTui.cs#L85) has Ctrl+C Stop. | Keyboard-only Copy can reach Stop despite an editor selection. Confirmed in the controlled Forge screen. |
| Mouse-dependent routing | UI `TerminalApp.cs` calls `TryCopyActiveSelection` before command routing; active owner is assigned on left mouse-down, not programmatic/keyboard focus. | Explains why clicking first changes the controlled result. A restored command alone cannot govern all clipboard failures. |
| Context menus | Public `Visual.ContextMenuFactory`, `ContextMenuService.Show`, `CommandPresentation.ContextMenu`; editor clipboard commands have empty labels and `Presentation.None`. | Native mechanism exists, but needs deliberate presentation and selection/paste lifetime handling. Opening a popup cleared selection in the probe. |
| Public editor integration | `HasSelection`, `TryCopySelection(out string)`, `TextDocument`, `ClipboardPasteHandler`, `Visual.Commands` and `AddCommand`; command `CanExecute`, `RouteGesture`, `ConsumesGestureWhenUnavailable`. | Use public hooks. `SelectionStart`/`SelectionLength` are protected; do not plan against them as public APIs. |
| Paragraph selection | Pinned [Paragraph](https://github.com/XenoAtom/XenoAtom.Terminal.UI/blob/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src/XenoAtom.Terminal.UI/Controls/Paragraph.cs) is sealed, implements `ISelectionOwner`, is selectable by default and copies underlying text. | Single-paragraph drag/Copy is already supported and passed controlled checks in both themes. |
| Rich selection gap | Pinned `Controls/DocumentFlow.cs`, `Controls/FlowDocument.cs`, Markdown `Controls/MarkdownControl.cs`; no document selection owner/coordinator. Two-paragraph Forge probe selected only the first. | Real library/adapter design work is needed for continuous ranges, not a blanket disable/enable toggle. |
| Overlays | Forge FadeIn, StreamCaret and LinkPointer.Probe have `IsHitTestVisible=false`. | No source evidence that these overlays block native selection. Retain the invariant. |
| Code snippets | [ForgeCodeBlockRenderer](https://github.com/katasec/forge-mcl/blob/2022b512dd2bd108626124f22bc1cfe7652648d7/src/ForgeMission.Cli/Tui/ForgeCodeBlockRenderer.cs) creates styled Paragraph runs and TileFrame. Markdown context provides `.Code`; display trims trailing LF. | A native Button can wrap/decorate the existing renderer. Copy the original context payload, not the trimmed/wrapped display. Route pseudo-fence headings before snippet decoration. |
| Clipboard outcomes | Terminal `TerminalClipboard.TrySetText`/`TryGetText` return bool; capability checks are best effort. UI editor and app pre-routed Copy discard write results; native Cut deletes after its copy call. | Do not claim success from command handling or capabilities. Failed Copy must not fall through to Stop. Newly exposed Cut needs safe failure semantics. |

Primary library source roots: [UI pinned tree](https://github.com/XenoAtom/XenoAtom.Terminal.UI/tree/6f4e0cde3890d8ce2510ac0451b861863e4aeeaa/src)
and [Terminal pinned tree](https://github.com/XenoAtom/XenoAtom.Terminal/tree/5517cb3d8cdf0532ecc89260067064f98cde6137/src).
Version/commit mappings were checked against local NuGet manifests, not inferred from current main.

## CodeAlta patterns to reuse

| Reference | Finding |
|---|---|
| [ChatPromptEditor](https://github.com/CodeAlta/CodeAlta/blob/823f847297aafb0662ee317c3434c96a755e32fa/src/CodeAlta/Views/ChatPromptEditor.cs#L54), PromptComposerView | Custom Enter/completion behaviour delegates other input to the native editor. |
| [PromptImageAttachmentStripView](https://github.com/CodeAlta/CodeAlta/blob/823f847297aafb0662ee317c3434c96a755e32fa/src/CodeAlta/Views/PromptImageAttachmentStripView.cs#L92) | Image interception leaves ordinary text Paste to the editor. |
| [ChatTimelineVisualFactory](https://github.com/CodeAlta/CodeAlta/blob/823f847297aafb0662ee317c3434c96a755e32fa/src/CodeAlta/Presentation/Timeline/ChatTimelineVisualFactory.cs#L359) | A real Button with a copy icon occupies Group.TopRightText and reads current Markdown on activation. It ignores the clipboard bool result. |
| [CodeAltaAppTests](https://github.com/CodeAlta/CodeAlta/blob/823f847297aafb0662ee317c3434c96a755e32fa/src/CodeAlta.Tests/CodeAltaAppTests.cs#L188) | Real TerminalApp/in-memory mouse-click test checks latest streaming Markdown copied. |

Reuse native composition and event-driven tests. CodeAlta's message copy is not snippet copy or
continuous document selection, and its success handling does not meet this phase's failure rule.

## Kitty and theme preservation

| Existing design | Interaction implication |
|---|---|
| Body/code are terminal text; frames, pills, spinner and supported proportional headings use Kitty Unicode placeholders. Tiles and text images are cached. | Keep the hybrid UI; select/copy logical text rather than a dump of rendered cells. |
| HeadingImage uses embedded Inter and falls back to terminal text for unsupported text. | Logical heading selection must cover both rendering paths consistently. |
| ForgeTheme owns dark/light tokens; ForgeStyles applies them to native controls and Markdown. TextMate supplies language-specific code runs. | Selection/menu/button feedback extends the current semantic theme system; no separate palette or replacement code renderer. |

See [TUI graphics design](../design/tui-graphics.md) for the rendering foundation. No graphics
rewrite is justified by these text-interaction problems.

## Controlled probes

Scratch project: `/tmp/forge-text-interaction-20261005/Probe.csproj`; UI/Markdown/TextMate 3.10.0
packages, existing Forge Debug assembly, `InMemoryTerminalBackend`, 100×32 cells. Both Forge theme
instances were exercised. Cell metrics 19×42 were **synthetic**, and image transmission was a
no-op. Reflection accessed internal Forge controls and TerminalApp test lifecycle in this JIT
investigation only; it is not proposed production integration and proves no Native AOT safety.

The probe used actual Forge ChatScreen/ComposerEditor and Paragraphs, but a synthetic root Stop
counter, not ChatTui's hosted cancellation. It used an in-memory clipboard, not the OS clipboard.
No real project, account, turn, file edit or terminal GUI was involved.

| Action | Named observation |
|---|---|
| Focus native PromptEditor; text `alpha beta`; End, Shift+Left four times, Ctrl+C | `beta` selected/copied; Stop counter 0, with or without preceding click. |
| Same action in Forge composer, no preceding click | Dark and light: `beta` selected; clipboard remains `sentinel`; Stop counter 1; editor Copy command absent. |
| Click Forge composer, then repeat selection and Copy | Dark and light: `beta` copied; Stop counter 0. |
| Native editor selection, open native menu | Selection changed from present to absent. A custom invocation snapshot of `beta` still copied on Enter. This demonstrates a public snapshot technique for Copy, not a complete approved Paste/range/focus design. |
| Drag within first Forge reply paragraph, then Ctrl+C | Dark and light: `first` selected/copied. |
| Drag from first paragraph into second, then Ctrl+C | Dark and light: only `first paragraph alpha beta` selected/copied; second paragraph unselected. |

[Raw observations](../evidence/phase-70/controlled-probes.json) preserve all 11 cases and baseline
metadata. `dotnet run --project /tmp/forge-text-interaction-20261005/Probe.csproj` exited 0 with
no warnings. Independent assertions checked every recorded case: **PASS: all 11 controlled
observations match their recorded expectations**. These are baseline defect/capability
observations, not passing tests for a fix.

### Remaining verification limits

`cua.getApp("Ghostty")` was rejected with: “Computer Use is not allowed to use the app
'com.mitchellh.ghostty' for safety reasons.” No alternate UI automation was used to bypass that
denial. Installed launch, physical Shift/Cmd/Control delivery, terminal-native selection,
clipboard contents and Retina appearance remain unverified for this discovery. Source and
in-memory evidence cannot establish why every reported mouse/keyboard action fails in the
operator's running window. Continue at the live reproduction task before product handoff.

## Documentation delivery and timing

Default-path acceptance: **N/A — documentation-only delivery**. Supervisor prepared the scope,
requirements, gates and evidence; product designer/plan/implementation and their persona reviews
have not run. That is the workflow's documentation-only exception, not product approval.

Documentation verification: five Markdown files, 42 local links/anchors, the whole global hub's
top-level-only shape, unchanged reference-image hash, and all 11 JSON observations passed the
supervisor's validation. `git diff --check` passed. Product tests/AOT are N/A for this document
change; baseline controlled results are recorded above. No `project_` memory files existed in
this project's memory directory at checkpoint; non-project memory was left intact.

`python3 tools/task-timing/timing.py '70 text interaction'` reported:

| Stage | Agents | Start | End | Wall | Tokens |
|---|---|---|---|---|---|
| `investigate` | 1 | 10-05 19:26:15 | 10-05 19:32:18 | 6m 03s | 111,292 |

**End to end:** 10-05 19:26:15 → 10-05 19:32:18 = 6m 03s; 111,292 subagent tokens;
0 revision rounds. No product PR: endpoint is the last tagged investigation. Earlier untagged
investigations and supervisor probes/documentation are excluded by the timing tool. Tokens are
the tool's last-context-size metric, not an account usage measurement.
