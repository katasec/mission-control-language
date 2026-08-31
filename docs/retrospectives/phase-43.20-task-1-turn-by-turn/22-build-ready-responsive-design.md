# Turn 22 — Build-ready responsive design

## 1. Codex prompt

````text
+-------------------------------------------------------------------------+
| DESIGN REVIEW — TWO FINAL SPEC CORRECTIONS (Codex -> Claude)            |
+-------------------------------------------------------------------------+
| The responsive launcher plan is nearly approved. Do not edit source,    |
| tests, or SVG references. Keep all work uncommitted.                    |
|                                                                         |
| Amend the spoke only, then return the final build-ready summary.        |
|                                                                         |
| 1. RECTANGULAR RANGE, NOT A DIAGONAL                                   |
|    The stated supported range is widths 800–1536 and heights 568–1024. |
|    The current resize sweep reduces both together, which does not test  |
|    all important aspect-ratio cases. Retain the representative sweep,  |
|    and add these binding browser checks for every launcher state:       |
|                                                                         |
|      800×568     800×1024     1536×568     1536×1024                   |
|                                                                         |
|    Also require an observed continuous drag-resize through the range.   |
|    The purpose is to catch wide/short and narrow/tall failures, not to  |
|    generate a screenshot at every pixel.                                |
|                                                                         |
| 2. REMOVE THE UNJUSTIFIED 320px POLICY                                 |
|    800×568 is Forge's agreed fully-supported lower boundary. Do not     |
|    declare a 320px support floor merely because it is a familiar web    |
|    number. Below 800×568, say only that the page may scroll and must    |
|    degrade without overlap or clipping where the host permits it.       |
|    If you believe a lower formal guarantee is needed, bring its user/   |
|    host rationale as a separate design decision; do not choose it here.|
|                                                                         |
| Everything else in the amended plan is accepted: browser-first visual   |
| work; compact primary-action rule; content-led fluid layout;            |
| evidence-led queries only; named theme tokens; Client Runtime/TUI       |
| contract parity; and native package parity last.                        |
+-------------------------------------------------------------------------+
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| BUILD-READY DESIGN SUMMARY - responsive launcher (Claude -> Codex)      |
| Spoke amended only. No source, test, or SVG change. Nothing committed.  |
|                                                                         |
| === CORRECTION 1: RECTANGULAR RANGE ================================    |
|                                                                         |
| The four corners are now binding for every launcher state, and the      |
| diagonal sweep is demoted to a representative check alongside them:     |
|                                                                         |
|                 568 high        1024 high                               |
|   800 wide      narrow+short    narrow+tall                             |
|   1536 wide     wide+short      wide+tall                               |
|                                                                         |
| Plus an observed continuous drag-resize through the range, rather than  |
| a screenshot per pixel.                                                 |
|                                                                         |
|   THIS FOUND A REAL FLAW, which is the point of asking for it. My       |
|   --wb-card-pad was width-driven, so at 1536x568 the card would have    |
|   taken 44px of VERTICAL padding from its width while the viewport was  |
|   at its shortest. Modelled, the worst state stacked to 593px in a      |
|   568px viewport - a 25px overflow that the diagonal sweep would never  |
|   have hit, because it never visits wide-and-short.                     |
|                                                                         |
|   The fix is an axis rule, now written into the spec: vertical rhythm   |
|   follows height, horizontal rhythm follows width. Every gap, field     |
|   height and type size in the vertical stack ramps on vh; only insets,  |
|   gutters and the action width ramp on vw. The card's padding splits    |
|   into --wb-card-pad-x (vw) and --wb-card-pad-y (vh).                   |
|                                                                         |
|   With that, the vertical total is a linear interpolation between the   |
|   two heights, so fitting at 568 and 1024 proves fitting between them.  |
|   Modelled at all four corners:                                         |
|                                                                         |
|     corner       empty   band(worst)   open-folder   viewport           |
|     800x568       489        539           537         568              |
|     1536x568      489        539           537         568              |
|     800x1024      904        989           971        1024              |
|     1536x1024     904        989           971        1024              |
|                                                                         |
|   29-35px of headroom at the short corners. The spec says plainly that  |
|   the proof constrains the design and the corner checks still run - it  |
|   does not replace evidence.                                            |
|                                                                         |
| === CORRECTION 2: THE 320px POLICY IS GONE =========================    |
|                                                                         |
| Removed. It was a familiar web number I reached for without a reason,   |
| which is exactly the kind of unjustified floor you flagged. The spec    |
| now says: 800x568 is Forge's agreed lower boundary; below it - a        |
| shrunken window, or zoom at 125/150/200% - the page may scroll and      |
| must degrade without overlap or clipping where the host permits. No     |
| smaller formal guarantee is claimed, and if one is ever wanted it       |
| needs its own user/host rationale as a separate decision.               |
|                                                                         |
| The zoom evidence item was reworded to match: at high zoom the          |
| effective viewport falls below the boundary, so scrolling there is      |
| recorded as expected behaviour, not reported as a pass against the      |
| compact-height rule.                                                    |
|                                                                         |
| === THE EVIDENCE PLAN, FINAL ORDER =================================    |
|                                                                         |
| Browser-rendered Client Runtime, after build + full suite + package:    |
|                                                                         |
|   1. Boundary frames: six states at 1536x1024 vs the large references   |
|      and at 800x568 vs the compact ones, raster edges, +/-2px.          |
|   2. The four rectangular corners, every state.                         |
|   3. Representative sweep 1536 -> 800 with heights 1024 -> 568, plus a  |
|      continuous drag-resize: no horizontal scroll, nothing wider than   |
|      the card, no clipping or overlap, compact-height rule from 568.    |
|   4. Long content: ~400-char goal, ~120-char name, ~300-char path, a    |
|      long failure message - fields shrink instead of widening the       |
|      card; only the message band scrolls inside itself.                 |
|   5. Zoom 125/150/200%: degrades by scrolling, nothing overlaps.        |
|   6. Theme modes x4 plus the attribute-removed ForgeUI check.           |
|   7. Token audit: every computed colour, radius, spacing and type       |
|      traced to a named token, plus the structural test that forge.css   |
|      adds no raw surface/theme colour outside the Workbench maps.       |
|   8. Native parity LAST: packaged Desktop at its own default window,    |
|      one screenshot and the numeric no-scroll assertion.                |
|                                                                         |
| Each item records PASS/FAIL. A FAIL returns to design or                |
| implementation; I do not forward one. Codex reviews after I record      |
| PASS; operator acceptance only after both, and it is yours to request.  |
|                                                                         |
| === UNCHANGED AND ACCEPTED =========================================    |
|                                                                         |
| Browser-first visual work; content-led fluid layout with grid,          |
| minmax(0, 1fr) and min-width: 0; clamp()/calc() supporting spacing and  |
| type rather than defining responsiveness; no breakpoint without         |
| recorded failing evidence; named theme tokens only, with no raw colour  |
| added to forge.css; contract and TUI parity untouched; the              |
| compact-height rule with a contained scroll region allowed only for a   |
| genuinely secondary long failure message.                               |
|                                                                         |
| === FILES ==========================================================    |
|                                                                         |
|   CHANGED   docs/phases/phase-43.20-project-workbench-mvp.md            |
|   BINDING, unchanged: six task1-launcher-compact-*.svg at 800x568 and   |
|             six task1-project-launcher-*.svg at 1536x1024               |
|   UNTOUCHED: forge.css, the component, the page, every test, every      |
|             contract. Nothing committed.                                |
|                                                                         |
|   One consequence to note for implementation: --wb-card-pad becomes     |
|   --wb-card-pad-x / --wb-card-pad-y, so the token count changes by one  |
|   and the theme-scoping test's geometry list grows accordingly.         |
|                                                                         |
| Ready to build on your approval.                                        |
+-------------------------------------------------------------------------+
````

## 3. Fault found

No new defect was identified in this response. It incorporated the missing corners, removed the

## Requested

Correct the four-corner and lower-bound gaps, then provide a build-ready plan.

## Better prompt

“Show the four-corner sizing model, actual browser acceptance matrix, and the exact implementation

## Prevention

The lesson is positive: a rectangular viewport matrix and axis-specific sizing should have been in
