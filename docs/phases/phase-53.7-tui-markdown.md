# Phase 53.7 — `forge chat` Markdown replies

> **Status: done (2026-09-30), verified.** Evidence:
> [phase-53.7-tui-markdown_completed.md](phase-53.7-tui-markdown_completed.md). Hub: [Phase 53](phase-53-forge-client.md). Builds on
> [53.6 theming](phase-53.6-tui-theming.md). Reference (patterns only): CodeAlta's timeline cards
> (`~/progs/CodeAlta/src/CodeAlta/Presentation/Timeline/ChatTimelineVisualFactory.cs`), BSD-2-Clause;
> no code copied.

**Goal:** participant replies render as Markdown (code blocks, inline code, links, lists, headings,
emphasis, quotes) in the theme's colours, matching the [light mock](../design/forge_tui_light_mockup.png).

## Locked decisions

| Area | Decision |
|---|---|
| Library | `XenoAtom.Terminal.UI.Extensions.Markdown` 3.10.0 (Markdig 1.4.0; no native deps). One `MarkdownControl` per participant card body. User messages stay plain pills. |
| Styling | `ForgeStyles` builds one `MarkdownStyle` from the theme and sets it on the root, like `Screen`. Every slot carries an explicit colour (a slot equal to the package default is replaced by theme defaults). Mapping: paragraph, list and quote text, headings (bold), bold → `TextStrong`; italic → italic only; inline code → `InlineCode`; link → `Link` + underline (OSC 8 hyperlink); quote bar, raw HTML → `TextMuted`. |
| Code blocks (Ameer) | A small forge code-block renderer (`IMarkdownCodeBlockRenderer`): rounded box, border `CodeBlockBorder`, fill `CodeBlockFill`, text `CodeBlockText`, **no language label**, wrapped. The package has no code-block style slot. |
| Syntax highlighting | Off. No TextMateSharp. |
| Streaming | Replies still arrive whole; setting `.Markdown` again is cheap (under 0.1 ms under AOT) for token streaming later. |
| Owner | forge-mcl `ForgeMission.Cli/Tui` (`ForgeStyles`, `ChatScreen`, new code-block renderer). No Client or server change. |

## Verified by spike (2026-09-30)

All 17 element types rendered with their intended light tokens in an in-memory truecolor terminal;
Native AOT publish had 0 IL warnings with identical output; the binary grows about 2.3 MB; supervisor
Ghostty capture confirmed the look. Spike sources: supervisor scratchpad `md-spike` (not in any repo).

## Gates

| Gate | Result |
|---|---|
| Security | N/A: rendering only. Links are shown as OSC 8 hyperlinks; nothing is fetched. |
| Engineering philosophy | Reuses the library's control; one small renderer; styles from existing tokens; no new settings. |
| Default path | `forge` from `make install` on merged forge-mcl `main`, in Ghostty, light default: a reply with code, inline code, a link and a list renders as in the light mock; dark via config renders with the dark tokens. |
| Visual acceptance | Supervisor Ghostty captures in both themes compared with the light mock; Ameer reviews live. |

## Tasks

| # | Task | Repo |
|---|---|---|
| 1 | MarkdownControl in participant cards; `MarkdownStyle` in `ForgeStyles`; forge code-block renderer; tests (style mapping carries every token; the colour-literal scan still passes) | forge-mcl |
| 2 | Visual and default-path acceptance | — |

## Done when

1. Replies render Markdown in both themes with the token mapping above; code blocks have no label.
2. The colour-literal scan still passes (all colours from `ForgeTheme`).
3. Build 0 warnings; Native AOT publish 0 IL warnings.
4. Supervisor captures match the light mock; Ameer accepts live.
