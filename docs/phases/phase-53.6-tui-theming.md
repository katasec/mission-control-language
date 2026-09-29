# Phase 53.6 — `forge chat` TUI theming (light + dark)

> **Status: build-ready (2026-09-30).** Hub: [Phase 53](phase-53-forge-client.md). Builds on
> [53.5](phase-53.5-tui-first-slice.md). Markdown rendering is the next step; its tokens are defined here.

**Goal:** make the TUI theme swappable by data only, proven by a light theme matching Ameer's light mock
(2026-09-30). Follows the common TUI pattern (Textual, Helix, lipgloss, glamour): one semantic token
layer, themes as data, component styles built only from tokens.

## Locked decisions

| Area | Decision |
|---|---|
| Selection (Ameer) | `~/.forge/config.json` with `{ "theme": "light" \| "dark" }`. Missing file or key → **light**. An unknown name stops `forge chat` with an error listing the valid names. No terminal detection, no environment variable, no flag. Read once at startup; no live switching. |
| Structure | `ForgeTheme` is a record of purpose-named `Color` tokens with two instances, `Light` and `Dark`. `ForgeStyles(ForgeTheme)` builds every component style (and the XenoAtom `Theme`) in one place. `ChatScreen` receives a `ForgeStyles` and never names a colour or a theme. A new theme is one more `ForgeTheme` instance plus its name. No theme files, registry or plugins. |
| Colours | Explicit RGB (the ANSI palette can't express the mock's pale fills). |
| Shapes | Cards and code blocks use rounded corners (`╭ ╮ ╰ ╯`). One-line pills (user message, APPROVED) use a coloured background with Nerd Font rounded caps `` `` (Ghostty and Kitty ship these symbols). The caps are theme data, so falling back to `▐ ▌` is a one-line change. |
| Plan rulings (supervisor, 2026-09-30) | Shapes apply to both themes (dark is the 53.5 mockup plus rounded cards and capped pills). The config is read on both the TUI and piped paths, before any network call. Theme names are exact lowercase. Caps only on one-line pills; multi-line messages keep a filled block. `error:` lines use the `Error` token. |
| Owner | forge-mcl `ForgeMission.Cli` (`Tui/` and a small `ForgeConfig` reader). No Client or server change. |

## Tokens

Light values sampled from the [light mock](../design/forge_tui_light_mockup.png)'s pixels; dark values are today's (53.5) plus new ones.

| Token | Light | Dark |
|---|---|---|
| `Surface` (page) | `#f7f8fe` | `#0f1622` |
| `SurfaceHeader` | `#f8faff` | `#0f1622` |
| `SurfaceAlt` (key bar) | `#f8faff` | `#1a2230` |
| `CardSurface` | `#ffffff` | `#0f1622` |
| `Text` | `#101d34` | `#c9d4e3` |
| `TextStrong` | `#101d34` | `#e8eef7` |
| `TextMuted` | `#63748c` | `#6b7a91` |
| `CardTitle` | `#5b6b83` | `#6b7a91` |
| `Accent` | `#0f6feb` | `#4f9bff` |
| `Prompt` | `#689df1` | `#24d5ee` |
| `Border` | `#d5dae5` | `#243044` |
| `CardBorder` | `#e7ecf4` | `#2a3850` |
| `Selection` | `#dbe7fb` | `#16345a` |
| `UserPillFill` / `UserPillText` | `#eff5fe` / `#103f87` | `#16345a` / `#e8eef7` |
| `Success` / `SuccessFill` | `#4e7c0f` / `#f2fbe6` | `#8cc152` / `#1d2a1a` |
| `Warning` | `#b45309` | `#f0b35a` |
| `Error` | `#b91c1c` | `#f07178` |
| `CodeBlockFill` / `CodeBlockBorder` / `CodeBlockText` (Markdown, next step) | `#f7f8fe` / `#e7ecf4` / `#101d34` | `#131c2b` / `#2a3850` / `#c9d4e3` |
| `InlineCode` / `Link` (Markdown, next step) | `#0f6feb` / `#0f6feb` | `#4f9bff` / `#4f9bff` |

## Gates

| Gate | Result |
|---|---|
| Security | N/A: local presentation and a local config file. |
| Engineering philosophy | One config key, default light; themes as data; styles derived in one place; no speculative registry. |
| Default path | `forge` from `make install` on merged forge-mcl `main`, in Ghostty, no `~/.forge/config.json`: `forge chat` opens in the light theme. With `{ "theme": "dark" }` it opens in the dark theme, identical to 53.5. |
| Visual acceptance | Supervisor Ghostty captures of both themes: light compared with the [light mock](../design/forge_tui_light_mockup.png), dark with the [53.5 mockup](../design/forge_tui_first_slice_mockup.html). Ameer reviews live. |

## Tasks

| # | Task | Repo |
|---|---|---|
| 1 | Token record + `Light`/`Dark`, `ForgeStyles`, `ForgeConfig` (`~/.forge/config.json`), rounded cards, pill caps, APPROVED pill; tests (config: missing, light, dark, unknown; no colour literals outside `ForgeTheme`) | forge-mcl |
| 2 | Visual and default-path acceptance | — |

## Done when

1. No config → light theme; `dark` → dark theme; unknown → clear error. Tests cover all four.
2. `ChatScreen` references no colour token or literal directly (only `ForgeStyles`); every colour
   literal lives in `ForgeTheme`.
3. Build 0 warnings; Native AOT publish 0 IL warnings.
4. Supervisor captures match the [light mock](../design/forge_tui_light_mockup.png) and the 53.5 dark mockup plus the new shapes; Ameer accepts live.
