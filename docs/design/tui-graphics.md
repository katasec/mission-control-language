# TUI graphics (kitty protocol) — foundation

> Verified 2026-09-30 by spike in Ghostty with XenoAtom.Terminal.UI 3.10.0 (supervisor window captures).
> Applies to images in `forge chat` (mission graph, artifacts). Owner: forge-mcl `ForgeMission.Cli/Tui`.

**Method: kitty graphics with Unicode placeholders.** Transmit the image once as a virtual placement
(`ESC_G a=T,U=1,f=100,i=<id>,c=<cols>,r=<rows>,q=2`, base64 PNG in 4096-byte chunks). Display it by
writing placeholder cells: `U+10EEEE` plus the row and column diacritics from the kitty spec, with the
image id encoded as the cells' 24-bit foreground colour. To XenoAtom these are ordinary width-1 text
cells, so layout, diff redraw and scrolling carry the image with them.

| Rule | Why (observed) |
|---|---|
| Transmit **after** the TUI enters the alternate screen | An image sent before `Terminal.Run` did not display: terminals keep separate image storage per screen. |
| Write the transmit escape straight to stdout (`Console.OpenStandardOutput`) | `Terminal.Write` output did not reach the terminal while the app ran. |
| Use Unicode placeholders, never direct placement | A direct placement is not a cell, so XenoAtom's redraws overwrite or misplace it (53.2 spike). Placeholder images survived inserts above them and scrolling. |
| Keep truecolor (`COLORTERM=truecolor`, set by Ghostty) | The image id is the placeholder's foreground colour; XenoAtom downgrades to 16 colours without truecolor, destroying the id. |
| Render the placeholder grid in a `Paragraph` with `Wrap = true`, in a pane at least as wide as the image | With `Wrap = false` only the first row rendered; a narrower pane splits placeholder rows. |

Fallback that also worked: one strip image per row drawn with bare `U+10EEEE` cells (no diacritics).
Spike sources: supervisor scratchpad `gfx-spike` (not in any repo).

Images forge draws itself (e.g. the mission graph) take their colours from the active `ForgeTheme`, so a light theme gets a light image.

## Phase 56 spike — drawn edges and proportional text (verified 2026-10-01)

> Ghostty 1.3.1 on the MacBook's Retina display (cell 19×42 px) and a 1× external display
> (10×21 px), XenoAtom.Terminal.UI 3.10.0, Native AOT osx-arm64. Supervisor reviewed every
> capture. Spike sources: supervisor scratchpad `gfx56` (not kept). Live-check tools: [tools/tui-capture](../../tools/tui-capture/README.md).
> Decisions: [Phase 56](../phases/phase-56-tui-graphics.md).

| Check | Observation | Verdict |
|---|---|---|
| Cell size in device px | `Terminal.Graphics.QueryPixelMetricsAsync()` (core `XenoAtom.Terminal`): 19×42 px in 1 ms on Retina, 10×21 px in 34 ms at 1×. No reply (Terminal.app): `null` after 256 ms (250 ms timeout). | PASS |
| Card edge tiles, no seams | 8 tiles around plain `CardSurface` cells. Captures match the source PNGs: max channel difference 1 (light), 4 (dark, uniform offset from the capture's colour profile). No seam at any join. | PASS |
| Card grows, no new images | 10 transmits (8 tiles + 2 headings) in every run, counted in-app and from a `script` typescript. The card grew to 13 rows (strict) and 15 rows (fit). | PASS |
| Inter heading via StbTrueTypeSharp, drawn 1:1 | Max difference 3–5 against the source; 4× crops sharp, no resampling. | PASS |
| Native AOT | 0 IL warnings. Stb plus two Inter TTFs add 958,088 B, of which the fonts are 840,172 B. JIT and AOT renders are identical. | PASS |

| Rule | Why (observed) |
|---|---|
| A one-cell ring is too thin for the mockup's corners | At 19×42 px the 14 px CSS radius is about 29 device px and the shadow needs 40–64 px to fade. A one-cell ring forces about a 0.3× shrink (radius about 9 px, almost no shadow). A ring 4 columns wide at the sides and 2 rows top and bottom matches the mockup unshrunk. |
| Repeated 1-cell tiles need explicit row and column diacritics in every cell | Bare `U+10EEEE` cells count up columns automatically, so the second copy of a 1-column image is blank. |
| Never re-send an image id with a different size | In one window, reusing ids across tile geometries left stale placements. A theme or cell-size change takes new ids. |
| Stb applies no kerning to Inter 4.1 | Inter keeps pair kerning in GPOS (Extension lookup type 9) and has no `kern` table, so `stbtt_GetCodepointKernAdvance` returns 0. |
| The better blend depends on the theme | Naive sRGB blending looks heavier and closer to the mockup for dark text on light; linear-light looks better for light text on dark. |
| tmux shows no images | tmux 3.5a answers the cell-size query itself, then drops kitty images without an error (blank cells). XenoAtom reports "No graphics protocol detected". |
| A missing embedded font fails at startup | It exits with a named error before `Terminal.Run`. |
