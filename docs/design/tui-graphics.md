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
