# Phase 53.7 — `forge chat` Markdown replies: completion record

> Active spoke: [phase-53.7-tui-markdown.md](phase-53.7-tui-markdown.md). Completed and verified 2026-09-30.

| Item | Evidence |
|---|---|
| Implementation | [forge-mcl#21](https://github.com/katasec/forge-mcl/pull/21), merged `b80a7d4`: `XenoAtom.Terminal.UI.Extensions.Markdown` 3.10.0; `MarkdownStyle` in `ForgeStyles` (every slot from a token; alerts Note/Important → Accent, Tip → Success, Warning → Warning, Caution → Error); `ForgeCodeBlockRenderer` (rounded box, no label, no highlighting; border cells on `CardSurface` so the fill stays inside the line); reply bodies are `MarkdownControl`. |
| Optimistic echo (added in review, Ameer) | On Enter the TUI shows a pending You pill and a pending `▌` card keyed by the submit's `commandId`; the Host's `UserMessage` carries that id as its `EventId` (`forge-conversations` `ConversationGrain.cs:1182`) and replaces the pill in place; `ParticipantStarted` titles the card. A failed submit becomes an `error:` line and restores the composer text. All in the pure `Transcript` mapping; replay never shows pending blocks. |
| Tests | 451 total: 446 passed, 4 skipped (live), 1 failed (`MissingApiKey_ThrowsClearly`, also failing on `main`). New: Markdown style mapping (both themes), code-block style, 7 echo/transcript tests; colour-literal scan green. Build 0 warnings; AOT 0 IL warnings; binary +1.4 MB. |
| Visual acceptance (supervisor) | Ghostty captures in light and dark: code blocks as a single rounded box without label, inline code and list styling from tokens, matching the [light mock](../design/forge_tui_light_mockup.png). |
| Live review (Ameer) | Accepted, including the instant echo. |
