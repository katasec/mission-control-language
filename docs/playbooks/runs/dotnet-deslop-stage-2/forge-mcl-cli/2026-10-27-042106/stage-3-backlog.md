# Stage 3 backlog: test reduction · run `2026-10-27-042106`

Repo `forge-mcl` · scope: tests for `src/ForgeMission.Cli` (`tests/ForgeMission.Mcl.Tests/Cli/`) · branch `dotnet-deslop-stage-2` · base `6554ddf`
Discover: one blind lens (`test-smell-detection`), 18:55:03–19:01:26 (6 min 23 s), 47 files → 16 rows: 7 tests and 30 copied helpers.
Baseline: 1141 passed / 3 failed (TuiColourLiteralTests ×2, PipelineRunnerTests.MissingApiKey_ThrowsClearly).
Shared helpers: `ForgeText` (`Forge`, `Type`, `Theme`, `Styles`, `Cell`, `Fonts`, `Get<T>`) and `TextInteractionTests.Show`.

| # | Kind | Delete | Still pinned by | File |
|---|---|---|---|---|
| 1 | removed code | `MissionFileResolutionTests.ResolveMission` mirror + its 5 tests (keep `Claude_without_a_target_needs_exactly_one_mcl_here`) | `DefaultPathTests.No_path_resolves_to_the_default_file_in_the_current_directory`; `MissionReadFailureTests.An_unreadable_mission_is_reported_and_exits_1` | MissionFileResolutionTests.cs |
| 2 | duplicate | `TextInteractionTests.Active_chat_assistant_card_drag_copies_real_code_body` | `Active_chat_rich_range_copies_heading_body_and_code_as_one_selection`; `Rich_assistant_card_drag_copies_one_canonical_range_across_paragraphs` | TextInteractionTests.cs |
| 3 | duplicate | `ChatTranscriptTests.Without_a_lost_handler_a_transport_failure_is_thrown` | `Without_a_lost_handler_a_cut_connection_stops_the_chat_with_connection_lost` | ChatTranscriptTests.cs |
| 4 | copied helper | ChatScreenTileTests `Forge`, `LoadForge`, `Type`, `Theme` | the file's own tests | ChatScreenTileTests.cs |
| 5 | copied helper | ChatTranscriptTests `Forge`, `LoadForge` | the file's own tests | ChatTranscriptTests.cs |
| 6 | copied helper | FileEditorTests `Forge`, `LoadForge`, `Type`, `Get<T>`, `Styles()` | the file's own tests | FileEditorTests.cs |
| 7 | copied helper | ForgeConfigTests `Forge`, `LoadForge`, `Theme` | the file's own tests | ForgeConfigTests.cs |
| 8 | copied helper | ForgeMarkdownStyleTests `Forge`, `LoadForge`, `Theme` | the file's own tests | ForgeMarkdownStyleTests.cs |
| 9 | copied helper | StartPageTests `Forge`, `LoadForge`, `Type`, `Get<T>`, inline theme/fonts/cell in `NewTui` | the file's own tests | StartPageTests.cs |
| 10 | copied helper | TerminalCaretTests `Forge`, `LoadForge`, `Type`, `Theme`, inline styles | the file's own tests | TerminalCaretTests.cs |
| 11 | copied helper | TileSetTests `Forge`, `LoadForge`, `Type`, `Theme`, `Cell`, `Get` | the file's own tests | TileSetTests.cs |
| 12 | copied helper | ForgeExecUrlInputTests `LoadCliType` | the file's own tests | ForgeExecUrlInputTests.cs |
| 13 | copied helper | ForgeChatTests forge.dll locator in `LoadForgeChatMethod`, `Property`, inline theme | the file's own tests | ForgeChatTests.cs |
| 14 | copied helper | ChatScreenLiveMotionTests `Show` | the file's own tests | ChatScreenLiveMotionTests.cs |
| 15 | copied helper | TuiColourLiteralTests inline filter in `Only_XenoCells_uses_UnsafeAccessor` → `CodeUsers` | the file's own tests | TuiColourLiteralTests.cs |
| 16 | copied helper | XenoInternalsContractTests `Get(object,string)` | the file's own tests | XenoInternalsContractTests.cs |

Every row touches a different file except 3 and 5 (both ChatTranscriptTests.cs), so those two are done in sequence.

## Committed (rows 1–16, 19:12–19:24)
1 4865eee · 2 414b0be · 3 3bb236e · 4 a35fe73 · 5 d833146 · 6 9a01108 · 7 0fb1b14 · 8 50d1bbf · 9 61873cb · 10 cca8a17 · 11 7a9cd66 · 12 65dea80 · 13 d541eec · 14 343537f · 15 0e3f9ed · 16 c71fefb

## Re-discover (19:24–19:31): 8 findings
| # | Kind | Change | Commit |
|---|---|---|---|
| 17 | copied helper | ChatTranscriptTests `Map` (copy of `Strings(Blocks(...))`) removed | fb733c3 |
| 18 | copied helper | ChatTranscriptTests `Blocks` fold loop → `ApplyAll` | 1df4d9b |
| 19 | copied helper | ChatScreenMotionTests `Show` body → `TextInteractionTests.Show` | 405fe9f |
| 20 | copied helper | ChatScreenTileTests `Show`, styles and fonts → shared helpers | 1e689ae |
| 21 | copied helper | ForgeMarkdownStyleTests types → `ForgeText.Type` (its `Styles(tokens)` kept: not a copy) | 5c0cb65 |
| 22 | unread field | ForgeConfigTests `ThemeType` removed | b0b6cea |
| 23 | copied helper | TileSetTests styles → `ForgeText.Styles`; unread `ThemeType` removed | 46861c4 |
| — | not done | ChatTranscriptTests `A_completed_turn_keeps_its_answered_cards`: not a true duplicate (it covers a turn with no final result) | — |
