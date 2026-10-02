# Phase 56 — completed tasks

> Active spoke: [phase-56-tui-graphics.md](phase-56-tui-graphics.md). Evidence for each row is in
> [tui-graphics.md](../design/tui-graphics.md#phase-56-spike--drawn-edges-and-proportional-text-verified-2026-10-01).

## Task 1 — spike (done 2026-10-01)

Prove the finish-line look in Ghostty before writing product code.

| Check | Observation that proves it |
|---|---|
| Cell size in device pixels is available inside a XenoAtom app (terminal reply or XenoAtom API) | Logged width × height on a Retina display; behaviour when no reply arrives |
| One card framed by edge tiles (corners, hairline, shadow) around a XenoAtom card with `CardSurface` interior | Ghostty capture, both themes; no visible seams at tile joins |
| The card grows (simulated streaming) with no new transmits | Transmit count stays at the initial tile count |
| One heading in Inter SemiBold via StbTrueTypeSharp, gamma-correct blend, drawn 1:1 | 4× crop of the capture: no resampling blur; compared with the mockup |
| Native AOT publish with StbTrueTypeSharp and the embedded fonts | 0 IL warnings; binary size delta recorded |

**Done when:** every row has its observation recorded in [tui-graphics.md](../design/tui-graphics.md),
the supervisor compares the captures with the mockup (PASS/FAIL per row), and G8 is decided.

## Task 2 — start-up check and card edges (done 2026-10-02)

Participant cards get the mockup's rounded edges and shadow. Every other element keeps today's look until Task 3.

```mermaid
flowchart LR
  S[ForgeChat.RunAsync] --> C{G8 check: kitty, truecolor, cell size}
  C -- fails --> X[exit 1 with message]
  C -- passes --> R[Terminal.RunAsync]
  R --> T[transmit card tiles once, raw stdout]
  T --> F[CardFrame paints tile cells around each card]
```

| Area | Decision |
|---|---|
| G8 check | Two stages, one message. **Before sign-in** (`ForgeChat.RunAsync`, after the theme is read; `UsesTui` computed there), from the environment only: `Terminal.Graphics.Capabilities.SupportedProtocols` contains Kitty, `IsMultiplexer` is false, and `Terminal.Capabilities.ColorLevel` is TrueColor. This catches Terminal.app, tmux (including tmux inside kitty) and missing truecolor before any network call. **On the first TUI tick**: `QueryPixelMetricsAsync()` returns a value. If not, the app stops and `forge chat` prints the same G8 message and exits 1. Pure decision functions for both stages, unit-tested. |
| Ctrl-C and input | **No probe before `Run`.** The cell-size query runs on the TUI's first tick, while XenoAtom already owns input with Ctrl-C as input (the spike's method). There is no `StartInput`/`StopInputAsync` before `Run`, so the `--hands` y/N prompt and Ctrl-C behave exactly as on `main`. (Task 2 review: probing before `Run` made `Console.ReadLine` echo twice.) |
| Cell size | Read once at start-up, like the theme. Window resizes need no new images. A font-size change while running is not handled: Ghostty rescales the images until `forge chat` restarts (accepted). |
| Transmit | Raw stdout, once, on the first UI tick, after `Run` has entered the alternate screen (the verified method). XenoAtom's `GraphicsPresenter` is not used. **Image ids are derived from theme and cell size**, so a later run in the same window with another theme or display never reuses an id at a different size (the spike's stale-placement rule). No kitty delete command. |
| Ring | The fit ring from the spike only (no strict mode). Geometry is solved from the cell size (4 columns, 2 rows, 2 rows at both 19×42 and 10×21). The seam, edge and interior checks run in tests over the whole realistic cell-size range (every height 12–60 px at widths of 0.40–0.60 × the height, which covers how terminal cells are shaped; both themes). There is no runtime check and no start-up failure for the ring. |
| Layout | Card position and spacing follow the mockup: transcript gutter 4 columns (the border lands in column 4), text at column 7, and one blank row above each card. That gives about 50 mockup px border to border against the mockup's 40, the nearest whole row. Today the cards touch and have no gutter (`ChatScreen.cs:21`). The layout numbers (mockup unit 20, gutter, padding, gap) are named shared constants in `ForgeTheme.cs`, each citing its mockup CSS rule, so every visual value still lives in that file. |
| Owners | `Tui/Graphics/` (new): `Raster`, `Png`, `RingGeometry`, `CardTiles`, `KittyImages` (the only code that writes images to stdout), `PlaceholderDiacritics`, `TerminalFacts` (environment facts, the cell query, both G8 decisions). They are ported from the spike under [code style](../design/code-style.md). The spike's seam, edge and interior checks become unit tests. `ChatScreen` uses a card-frame visual and never builds pixels. The Cli README gains the `Tui/Graphics/` ownership line. |
| Tokens | New `ForgeTheme` tokens: card radius, hairline, and two shadow layers (colour with alpha, y offset, blur), all in mockup px where 1 mockup px = cell height ÷ 20. Dark `CardSurface` becomes `#151f2e` and `CardBorder` becomes `#22304a` (the mockup). Other dark mismatches wait for the task that owns those elements. |
| Colour scan | `TuiColourLiteralTests` scans `Tui/` recursively and also flags RGBA byte literals. One named exception: `KittyImages`, whose `Color.Rgb` carries an image id, not a visual colour. |
| Packages | None new. StbTrueTypeSharp and Inter arrive in Task 4. |
| Accepted for now | Mouse-selecting across a card edge copies placeholder characters. |

**Done when:**
1. In Terminal.app, and in tmux inside Ghostty, `forge chat` exits with code 1 and the G8 message before any network call. A kitty-advertising terminal that doesn't answer the probe exits 1 with the same message once the TUI starts. Piped line mode is unchanged. Unit tests cover the decision function.
2. Supervisor Ghostty captures of replies in both themes, on Retina and 1×: card edges match the mockup's card (PASS per capture). A streamed reply grows its card with no new transmit (the count from a `script` typescript stays at the start-up tile count).
3. A card scrolled partly off the top still draws its edges correctly (capture).
4. Live: Ctrl-C stops a running turn and Ctrl-D quits, as before.
5. Every visual value is in `ForgeTheme`, and the recursive colour scan passes with its one exception.
6. Build 0 warnings, all tests pass, Native AOT publish 0 IL warnings.
7. Default path: `forge` from `make install` on merged forge-mcl `main` in Ghostty, a two-turn chat in each theme (the default as of Task 2, and the other via config).

**Acceptance (2026-10-02, supervisor):** forge-mcl [#33](https://github.com/katasec/forge-mcl/pull/33) merged at `5bae237`.

| Done when | Evidence |
|---|---|
| 1 | Live: Terminal.app and tmux inside Ghostty exit 1 with the G8 message (rc read from bash). pty runs: no kitty, no truecolor, kitty inside tmux all exit 1 before sign-in; a kitty terminal with no probe reply exits 1 after the TUI starts. |
| 2 | Ghostty captures of the light and dark themes on Retina (19×42) and 1× (10×21): card edges, shadow, fill, full width and even padding match the mockup's card. 8 transmits per session in every run, including streamed replies (counted from typescripts). |
| 3 | A card cut off at the top, and another cut off at the bottom, keep their edges (PgUp capture). |
| 4 | Live: Ctrl-C mid-turn shows "(run interrupted)" and stays in the TUI; Ctrl-D exits 0. The `--hands` y/N prompt has the same terminal settings as on `main` and echoes once (pty). |
| 5 | Recursive colour/RGBA scan and stdout-writer rule pass; code review confirmed every visual value is in `ForgeTheme`. |
| 6 | Build 0 warnings; AOT 0 IL warnings; supervisor full-suite runs green apart from the pre-existing `ExecExpertRunnerTests` flake, which also fails 1 in 5 on `main` ([backlog](../backlog.md)). |
| 7 | `make install` on merged `main`, Ghostty: two-turn chats, light on Retina and dark on 1×, every reply shown. |

Found during acceptance, not caused by Task 2: the live stream dies after about 100 s idle, and also on `main` ([Phase 57](phase-57-chat-idle-stream.md)). Review fixes along the way: the probe moved to the first TUI tick (it had made the `--hands` prompt echo twice); a multiplexer check was added; ring checks became test-only; the narrow-card and padding mismatches were fixed; a test race on XenoAtom's shared dispatcher was isolated.

## Task 3 — other shapes (done 2026-10-02)

Every shape is a tile set: image tiles at the edges, plain text cells inside. Rules from Task 2 still hold (G6–G8, ids derived per set, transmit once on the first tick).

| Element | Decision | Size (cells) |
|---|---|---|
| Code block | Ring: radius 10, hairline `CodeBlockBorder`, fill `CodeBlockFill`, no shadow, drawn on `CardSurface`. Rendered by `ForgeCodeBlockRenderer` returning a frame visual. | 2 cols each side, 1 row top and bottom (today's footprint) |
| One-line user pill | Filled pill, `UserPillFill`; left and right **cap tiles** (half-round, full cell height) replace the Nerd Font caps. | text + 4 cols, 1 row |
| Multi-line user message | Ring, radius 14, `UserPillFill`, no border, no shadow; stays right-aligned (the frame does not stretch). | 2 cols, 1 row top and bottom |
| APPROVED pill | Cap pill on `SurfaceHeader`, `SuccessFill`; the green dot is drawn in the left cap. | 1 row |
| Tool (hands) lines | **One-row chip** (Ameer): cap pill, fill a new `ToolFill` token, no outline (a hairline can't cross text cells). Stays between cards (placement inside cards is a separate later task). | 1 row |
| Composer | Ring: radius 14, hairline `Accent`, a 4 mockup-px `Accent` glow at 14 %, **no shadow** (Ameer), fill `CardSurface`; wraps the `PromptEditor` (its background becomes `CardSurface`). | 2 cols, 1 row top and bottom; 3 rows for one line |
| Spacing (Ameer's note) | Remove the `Rule` above the composer; key bar: one blank row above it, no `SurfaceAlt` fill. Result: progress row / composer ring / blank row / keys. | — |
| Key-hint chips, send button | **Task 4** (they need image text). | — |

| Area | Decision |
|---|---|
| Graphics layer | Shapes without shadows (`CardTiles` must not assume a shadow); a glow layer (hard spread, no blur); padding per tile set; a cap renderer (left/right cap, 1 row); a **tile-set registry** in `ChatScreen` (`UseCards` becomes per set). |
| Image ids | Repack: theme 1 bit, cell width 7, cell height 8, set 3, slot 3 (22 of 24 bits). A test proves no two inputs share an id across all sets. |
| Tokens | New: `ToolFill` (light `#eceff6`, dark `#1a2333`; supervisor's starting values, confirmed on the captures), radii per set, glow. Reconciled with the mockup: dark `CodeBlockFill` `#0f1622`, dark `SurfaceAlt` `#141d2b`, light `SurfaceAlt` `#f1f4fb`. All in `ForgeTheme`. |
| Plan rulings (supervisor, 2026-10-02) | One `TileFrame` for all sets (a cap set is a ring with 0 top/bottom rows); rings stay solved, padding tops up to the sizes above. The mockup is binding: dark `CodeBlockText` → `#e8eef7` and dark `SurfaceHeader` → `#111a28`; tool chip text is `TextMuted`, not italic; the tool chip, progress row and key bar align to the 4-column gutter. 38 transmits per session (4 rings × 8 + 3 cap sets × 2). Review rounds (2026-10-02): the composer measures its height at the width it is drawn at (`ComposerEditor`; XenoAtom measures at ≤ 48 columns); wrapped user bubble ≤ 75 % wide, right-aligned, never left of the gutter; type-ahead before the TUI starts is discarded; caret colour from a `Caret` token (OSC 12, restored on exit; Ameer: light caret was invisible). Accepted (Ameer): Ghostty colour-manages cell backgrounds but not images, so a saturated fill such as the dark user bubble shows a faint band; no change. |
| Tests | Edge/seam/interior checks for every ring set over the Task 2 cell-size range; cap tiles: the cap joins its text cells with no step (edge level ≤ 1); id disjointness across sets; snapshot tests for a one-line pill, a tool chip and the composer frame. |

**Done when:**
1. Supervisor Ghostty captures, both themes, Retina and 1×: code block, one-line and multi-line user message, APPROVED pill, tool chip and composer match the decisions above and the mockup's shapes; spacing as specified.
2. Images are sent once per session (count from a typescript), however much the transcript grows.
3. The composer grows from 1 to 6 lines inside its ring; the caret and editing work as before.
4. Every visual value lives in `ForgeTheme`; the colour scan passes.
5. Build 0 warnings, tests pass, AOT 0 IL warnings.
6. Default path: `make install` on merged `main`, Ghostty, a chat with a code block and a hands tool line (`forge chat --hands`), dark default and light via config.

**Acceptance (2026-10-02, supervisor):** forge-mcl [#37](https://github.com/katasec/forge-mcl/pull/37) merged at `8bc9597`.

| Done when | Evidence |
|---|---|
| 1 | Ghostty captures, BenQ 1×, light and dark: code block, one-line pill, wrapped bubble (≤ 75 %, right-aligned), APPROVED with dot, tool chip, composer ring; spacing as specified. Retina is rechecked at Task 7 (Ghostty opens new windows on the BenQ; the Retina mechanism is proven in Task 2). |
| 2 | 38 transmits in every run (typescripts), including streamed replies. |
| 3 | The composer sizes to its wrapped text (3 rows for 3 lines) inside its ring; Ctrl-D at 15 s and 24 s exits 0; the caret is visible in both themes (Ameer). |
| 4 | The colour scan passes; every visual value is in `ForgeTheme` (code review PASS). |
| 5 | Supervisor build: 0 warnings; 567 then 566 passed; AOT 0 IL. |
| 6 | `make install` from `8bc9597`: dark with no config (`--hands`, tool chip shown), light via config (code blocks). |

Known gap: linux-x64 compiles with 0 IL warnings, but its native link can't run on a Mac, so the type-ahead flush's `libc` binding is unverified at runtime on Linux.

## Task 4 — proportional text (done 2026-10-02)

Text drawn as kitty images in Inter, blended on the surface below it (naive sRGB on light, linear-light on dark). Everything else stays terminal text.

| Element | When known | Font, size (mockup px) | Cells |
|---|---|---|---|
| Brand: logo square + "forge" | Start-up | Bold 17 | 1 row |
| Breadcrumb "project / **Chat**" | Start-up | SemiBold 14 (prefix in `TextMuted`) | 1 row |
| Card name ("Answerer") | First time a participant appears | SemiBold 14.5 | 1 row |
| Markdown heading (h1–h3) | When its line is complete | SemiBold 19 (h1 22, h3 16) | 2 rows per line, wrapped at words; one over-long word ends in `…` |
| Avatar: circle + initials, before the card name and after "You" | Per participant / user | SemiBold 12, `Accent` fill (user: `TextMuted`) | 1 row (shrunk from the mockup's 1.3) |
| Key-hint chips (`enter`, `⇧ enter`, `pgup/pgdn`, `ctrl c`, `ctrl d`) | Start-up | SemiBold 11 on a rounded `CardSurface` chip with a `Border` hairline | 1 row |
| Send button `↵` in the composer | Start-up | Bold, `Accent` rounded square | 1 row |

| Area | Decision |
|---|---|
| Glyphs | `GlyphText` (the only StbTrueTypeSharp reference, G2) plus our GPOS pair-kerning reader (G9: PairPos formats 1 and 2, Extension lookups, Coverage 1/2, ClassDef 1/2 — the subset writes ClassDef 1). Verified against a committed golden table of all pairs in the allowed set made once with `hb-shape --features=-calt` (HarfBuzz 12.1.0), so tests don't need HarfBuzz. |
| Allowed text (G9) | Printable ASCII, Latin-1 letters, and `↵ ⇧ … · → – — ‘ ’ “ ”`. Anything else, or a heading containing inline code, emphasis or a link, stays bold terminal text. |
| Fonts | Inter 4.1 SemiBold and Bold, **subset** with `hb-subset` to the allowed set, keeping only `kern`, no hinting, no GSUB (about 22 KB each instead of 420 KB). The subset TTFs and the OFL `LICENSE.txt` are committed to forge-mcl and embedded; the subset command is recorded in the README. No Medium weight: SemiBold stands in for the mockup's 500. |
| Streaming headings | Until a newline follows the heading or the reply ends, it shows as bold terminal text on **2 rows** (the image's height), so nothing jumps when the image replaces it. Route: a Markdig pipeline step turns a qualifying heading into a `forge-heading:N` fenced block that `ForgeCodeBlockRenderer` draws (XenoAtom's Markdown package has no heading hook). |
| Sending at runtime | Text images are made and sent when first needed, through `RawStdout` on the UI thread (XenoAtom runs UI-thread code strictly between frames, each frame being one synchronized write). `KittyImages.Transmit` asserts it is on the UI thread (`Dispatcher.VerifyAccess`). Start-up tiles are unchanged (38). |
| Ids and cache | Text ids have bit 23 set (tile ids use 22 bits); 23 bits from a hash of (theme, cell size, style, text, width). A per-session cache maps that key to its id: each image is sent once, and a re-parse only redraws cells. On a hash clash with different content, probe the next id. No deletes within a session. |
| Accepted | Copying a reply loses image headings. A window resize makes new images for wrapped headings. An image heading takes 2 rows plus XenoAtom's 1 blank row after it (the mockup shows none). |
| Plan rulings (supervisor, 2026-10-02) | Image headings: h1–h3 at the top level only. User avatar initial: the first letter of the local account name (`Environment.UserName`), uppercased; an empty circle if it isn't allowed (no display name is stored, and `/me` would need a network call). Expert initial: the first letter of the expert name. Pending heading text sits on row 1 of its 2 rows. AOT evidence includes a `-p:TrimmerSingleWarn=false` warning diff against `main` (forge's NoWarn hides per-assembly warnings). The fallback card name is bold `TextStrong`. |
| Tokens | Font sizes, the avatar fill, and the chip and send-button shapes in `ForgeTheme`; no literals elsewhere. |

**Done when:**
1. Supervisor Ghostty captures, both themes, at 1× (Retina at Task 7): brand, breadcrumb, card names with avatars, a streamed reply with an h2, key chips and the send button match the mockup. A heading in another script stays terminal text.
2. The kerning reader matches the HarfBuzz golden table for every pair, for both weights.
3. A streamed heading switches from text to image with no row jump. Each distinct image is sent once per session (typescript count = 38 + distinct text images).
4. AOT 0 IL warnings; binary growth recorded (subset fonts); build 0 warnings; tests pass.
5. Default path: `make install` from merged `main`, Ghostty, dark default and light via config.

**Acceptance (2026-10-02, supervisor):** forge-mcl [#38](https://github.com/katasec/forge-mcl/pull/38) merged at `f8c82c9`.

| Done when | Evidence |
|---|---|
| 1 | Ghostty: light on Retina, dark on 1×. Brand and logo, breadcrumb, avatars and names, a streamed h2 as an image, key chips and the send button match the mockup; "## Привет мир" stays terminal text. |
| 2 | All 28,224 pairs of the 168-character allowed set match HarfBuzz 12.1.0 for both weights; the golden tables are tied to each font's SHA-256. |
| 3 | 55 transmits per session (38 tiles + distinct text images); each image is sent once, checked off screen as well. |
| 4 | AOT 0 IL, and the `TrimmerSingleWarn=false` warning list is identical to `main` (28 entries); binary +265,456 B (subset fonts about 22.8 KB each); build 0 warnings; 628 passed. Code review PASS. |
| 5 | `make install` from `f8c82c9`: dark with no config, light via config. |
