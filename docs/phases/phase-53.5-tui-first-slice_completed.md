# Phase 53.5 — `forge chat` TUI first slice: completion record

> Active spoke: [phase-53.5-tui-first-slice.md](phase-53.5-tui-first-slice.md). Completed and verified
> 2026-09-30.

| Item | Evidence |
|---|---|
| Implementation | [forge-mcl#19](https://github.com/katasec/forge-mcl/pull/19): `Tui/Transcript.cs` (pure event→block mapping, duplicate-reply rule, empty card dropped after an ended turn), `Tui/ChatScreen.cs`, `Tui/ChatTui.cs`, `Tui/ForgeTheme.cs` (12 tokens), `ForgeChat.cs` (TUI only when stdin and stdout are terminals). XenoAtom.Terminal.UI 3.10.0, core package. |
| Tests | 426 total: 421 passed, 4 skipped (live integration), 1 failed (`PipelineRunnerTests.MissingApiKey_ThrowsClearly`, also failing on `main`; environment-dependent). New mapping tests all pass. Build 0 warnings; Native AOT publish 0 IL warnings. |
| Piped mode | `echo hi \| forge chat` stays in line mode. |
| Visual acceptance (supervisor) | Ghostty window captures (`screencapture -l`) of the installed binary replaying the live Chat conversation: header `forge │ PROJECT chat` / `CHAT · V1 · APPROVED · anthropic`, right-aligned You pills, bordered `Answerer` cards, each reply once, composer and full-width key bar. Matches the [first-slice mockup](../design/forge_tui_first_slice_mockup.html). |
| Live review (Ameer) | Passed: Enter / Shift+Enter, PgUp/PgDn, Ctrl-C stops a running turn and stays in the TUI, Ctrl-D quits, no flicker. |
| Default path | `forge` from `make install` on merged forge-mcl `main`, in Ghostty, after `forge login`, no overrides: opens full-screen, replays the conversation, sends and shows turns. PASS. |

Known and deferred: replies arrive whole (token streaming in the [backlog](../backlog.md)); Markdown in
replies is shown as plain text.
