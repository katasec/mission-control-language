# Phase 56 — `forge chat` TUI graphics (finish line)

> **Status: Tasks 1–4 and 2b done (2026-10-02); Task 5 design locked, plan next.** Origin: Ameer, 2026-10-01 — "so beautiful people can't
> tell if it's a GUI or a TUI." Builds on [Phase 53](phase-53-forge-client.md)'s TUI (53.5–53.9) and
> [TUI graphics](../design/tui-graphics.md) (kitty placeholders, verified 2026-09-30).

**Goal:** `forge chat` in Ghostty matches the
[finish-line mockup](../design/forge_tui_finish_line_mockup.html) in both themes: anti-aliased card
edges and shadows, proportional-type headings and names, avatars, and motion. Body text stays real
terminal text. The mockup's **X-ray** view is the contract for which cells are images.

## How it renders

```mermaid
flowchart LR
  T[ForgeTheme tokens] --> R[Raster: SDF shapes, blur, gamma blend]
  F[Inter TTF, embedded] --> G[GlyphText: StbTrueTypeSharp adapter]
  G --> R
  R --> P[Png encoder: System.IO.Compression]
  P --> K[KittyImages: transmit once, by id]
  K --> C[Placeholder cells in XenoAtom layout]
  M[CellMetrics: cell size in device pixels] --> R
```

A card is plain text cells filled with `CardSurface`, framed by a one-cell ring of image **tiles**
(four corners, top, bottom, left, right). Tiles are drawn once per theme and cell size and reused by
image id, so a card that grows while a reply streams sends no new image.

## Evidence (2026-09-30 – 2026-10-01)

| Fact | Source |
|---|---|
| Kitty images via Unicode placeholders survive XenoAtom redraw and scroll in Ghostty; transmit after entering the alternate screen, straight to stdout | [tui-graphics.md](../design/tui-graphics.md) |
| StbTrueTypeSharp 1.26.13 and SkiaSharp 4.153.1 render Helvetica Neue at 2× practically identically in both themes; Stb applies the font's kern table, Skia does not without `SkiaSharp.HarfBuzz` (a second native library) | Text spike, supervisor scratchpad `textspike` (not in any repo) |
| `Tui/` owns the TUI; `ForgeTheme` is the only file with colour literals; the literal scan (`TuiColourLiteralTests`) is not recursive and misses raw RGBA bytes | forge-mcl `src/ForgeMission.Cli/README.md:39`, `tests/ForgeMission.Mcl.Tests/Cli/TuiColourLiteralTests.cs:37-55` |
| `make install` publishes the Native AOT `forge` to `~/.local/bin` | forge-mcl `Makefile:131-132` |

## Decisions

| # | Decision | Why |
|---|---|---|
| G1 | **Shapes: a small pure-C# renderer** (signed-distance rounded rectangles, hairlines, box-blur shadows, gamma-correct blending) and a PNG encoder on `System.IO.Compression`. | Only shapes are needed; AOT-clean, no native dependency. |
| G2 | **Proportional text: StbTrueTypeSharp**, behind one adapter (`GlyphText`); the only file that references the package. | Same quality as Skia in the text spike, pure managed, AOT-clean (Task 1). **Correction (Task 1):** Stb reads only the legacy `kern` table, and Inter keeps its kerning in GPOS, so headings are unkerned. Kerning is G9. |
| G3 | **No SkiaSharp.** | Native library per platform, several MB, needs a second native library for kerning; no visible gain (G2 evidence). Keeps the 53.2 decision. |
| G4 | **Font: Inter (OFL), SemiBold and Bold static TTFs, embedded** in the binary. | Mockup's face; two weights cover brand (Bold) and names, crumb, headings (SemiBold). Licence allows embedding. |
| G5 | **Images only where the X-ray marks them**; every other cell is terminal text. | Selection and copy keep working; images never carry body text. |
| G6 | **Tiles, not per-card images** (see *How it renders*). | Streaming grows cards several times a second (53.8); tiles make growth free. |
| G7 | **Every visual value is a theme token**: colours, shadow colour/alpha, radii, avatar fills. The literal scan becomes recursive over `Tui/` and also flags raw RGBA. | Themes as data (53.6); closes the scan gaps found above. |
| G8 | **Start-up check, no fallback.** On a terminal (not piped), before the TUI starts, `forge chat` checks two things: XenoAtom reports kitty graphics with truecolor, and the cell-size query answers (exact check: [Task 2](phase-56-tui-graphics_completed.md#task-2--start-up-check-and-card-edges-done-2026-10-02)). If either fails it exits with code 1 and the message: `forge chat needs a terminal that can show images, such as Ghostty or Kitty (not inside tmux). Open forge chat again from one of those.` Piped line mode is unchanged. (Ameer, 2026-10-01) | Early stage: one rendering path, no plain-look copy to maintain. Accepted cost: `forge chat` inside tmux stops working until a later phase adds a plain look. The spike observed both signals (Terminal.app: no reply after 256 ms; tmux: no graphics protocol). |
| G9 | **Kerning: our own GPOS pair-kerning reader inside `GlyphText`** (PairPos formats 1 and 2, Extension lookups, Coverage and ClassDef tables), verified against HarfBuzz's output for Inter. **Image text is simple Latin only**: a heading or name with any other script is drawn as bold terminal text, which Ghostty shapes itself. (Ameer, 2026-10-01) | Every serious renderer uses HarfBuzz, but HarfBuzzSharp brings a native library per platform, and forking it means owning a C++ build per platform. SixLabors.Fonts needs a paid licence above $1M revenue; Typography.OpenFont is unmaintained. We need only pair kerning for one known font: about 200 testable lines and no dependency. Move to stock HarfBuzzSharp, unforked, only if image text must cover every script. |
| G10 | **Default theme is dark**: a missing `~/.forge/config.json` or a missing `theme` key means **dark**. This supersedes the 53.6 default (light). `light` stays selectable, and an unknown name is still an error. (Ameer, 2026-10-01) | The market prefers dark; Ameer uses light via config. |
| G11 | **The window look comes from forge launching its own Ghostty window**: a separate Ghostty instance started with per-instance settings (hidden title bar, padding, cell height). The user's Ghostty config and other windows are untouched. The exact command and settings are designed in Task 6. (Ameer, 2026-10-02) | Ghostty's title-bar and padding settings are app-wide; putting them in the user's config would change every Ghostty window. |

**Gates.** Security: N/A — local rendering only; no entry point, store, identity or secret.
Engineering philosophy: named owners per box in the diagram, one adapter per external dependency
(Stb, compression), no on/off setting and no renderer interface. Failure boundary: font loading is
an embedded resource proven by a test; an unsupported terminal stops at start-up with a named message (G8).
UI: the finish-line mockup is the binding reference by analogy with
[Desktop Interaction Principles](../design/desktop-interaction-principles.md#visual-reference-acceptance-gate--non-negotiable)
(which scopes itself to Desktop/ForgeUI); supervisor Ghostty captures, then Ameer's live review.

## Tasks

| Task | Repo | Done when |
|---|---|---|
| 1 — **Spike** (supervisor scratchpad, not a repo) | — | **Done**, see [completed](phase-56-tui-graphics_completed.md#task-1--spike-done-2026-10-01). |
| 2 — **Start-up check and card edges**: G8, `Tui/Graphics/`, participant cards framed by the fit ring | forge-mcl | **Done** (#33), see [completed](phase-56-tui-graphics_completed.md#task-2--start-up-check-and-card-edges-done-2026-10-02). |
| 2b — **Default theme dark** (G10) | forge-mcl | **Done** ([#36](https://github.com/katasec/forge-mcl/pull/36)): the two default tests went red→green, suite 540 passed, AOT 0 IL; live, with no config `forge chat` opened dark (supervisor capture 2026-10-02). |
| 3 — **Other shapes** | forge-mcl | **Done** ([#37](https://github.com/katasec/forge-mcl/pull/37)), see [completed](phase-56-tui-graphics_completed.md#task-3--other-shapes-done-2026-10-02). |
| 4 — **Proportional text** | forge-mcl | **Done** ([#38](https://github.com/katasec/forge-mcl/pull/38)), see [completed](phase-56-tui-graphics_completed.md#task-4--proportional-text-done-2026-10-02). |
| 5 — **Motion**: fade-in, spinner, streaming caret, card hover, link pointer | forge-mcl | See [Task 5](#task-5--motion). |
| 6 — Window: hidden title bar, padding, cell height, via forge's own Ghostty window (G11) | forge-mcl | Design locked after Task 5. |
| 7 — Acceptance | — | `forge` from `make install` on merged `main`, in Ghostty, dark by default and light via config: supervisor captures match the mockup; Ameer accepts live. |

### Task 5 — motion

Facts (read-only investigation, 2026-10-02): XenoAtom ticks about every 15 ms while forge runs (the update callback), and visuals implementing `IAnimatedVisual` repaint on their own; mouse tracking is already on (motion mode) and XenoAtom does its own selection, with Shift-drag as Ghostty's native selection; the composer caret is Ghostty's cursor and already blinks; XenoAtom has no OSC 22 support; links are OSC 8.

| Effect | Decision |
|---|---|
| Fade-in | The rows of a reply's body that changed in the last delta fade from `CardSurface` to their text colour over **220 ms** (the mockup's value), through an overlay visual drawn after the body that blends foreground colours (`OverlayCellStyle`). Rows holding image placeholders (headings, code-block edges) are skipped, using a layout hook from forge, so no image id is changed. |
| Spinner | Image frames of a rotating arc (as the X-ray marks it), sent once at start-up with the tiles; an `IAnimatedVisual` cycles the placeholder id, about 12 frames per second. It runs only while a reply is in flight (`Transcript.Replying`), on the progress row and on a running tool chip. |
| Streaming caret | The `▌` at the end of a streaming reply becomes an `Accent` block that blinks every 500 ms while the reply streams, then disappears. |
| Card hover | The card's edge darkens on hover (`Border` instead of `CardBorder`): a second card tile set, swapped through `IsHovered`. |
| Link pointer | While the pointer is over a link: a hand pointer (OSC 22 `pointer`) through `RawStdout`, reset when it leaves and on every exit path, like the caret colour. |
| Not doing | A reduced-motion setting (no setting without a need); hover action chips (forge has no card actions). |

**Done when:**
1. Live, Ghostty: a streamed reply fades in row by row with no flicker; headings and code blocks stay correct while it fades; the spinner turns while a reply runs and stops when it ends; the streaming caret blinks, then goes; hovering a card darkens its edge; hovering a link shows a hand, and the pointer is normal again after leaving and after exit; Cmd-click still opens a link.
2. Images: tiles + spinner frames + hover tiles sent once at start-up; text images as in Task 4. No images sent per animation frame.
3. CPU: an idle window uses no more CPU than before Task 5 (measured with `top` or `ps` over 30 s, before and after).
4. Build 0 warnings, tests pass, AOT 0 IL warnings; every value in `ForgeTheme`.
5. Default path: `make install` from merged `main`, dark default and light via config.

## Next

Task 5: assign it to an implementer (plan only), then approve.
