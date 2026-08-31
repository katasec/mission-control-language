# Turn 20 — Compact design artifact summary

## 1. Codex prompt

````text
+-------------------------------------------------------------------------+
| PLAN NOT APPROVED — PRIOR INSTRUCTIONS NOT YET APPLIED                  |
+-------------------------------------------------------------------------+
| Your reply repeats the pre-decision plan. The measurement route has     |
| already been chosen: use the temporary document-title probe, measure    |
| window.innerWidth × window.innerHeight in the packaged app, then remove |
| the probe in the same session without committing it.                    |
|                                                                         |
| Apply the two already-required corrections:                             |
| - valid CSS arithmetic, including calc(...) inside min()/max() where    |
|   needed, with exact 1536×1024 upper-bound values;                      |
| - all six compact states fit their required inputs and primary action   |
|   without scrolling, including failed and goal-required.                |
|                                                                         |
| Then create the six compact SVGs and update the spoke specification.    |
| Do not implement source changes yet.                                    |
|                                                                         |
| Reply with the DESIGN ARTIFACT SUMMARY requested in the prior approval, |
| not the earlier plan.                                                   |
+-------------------------------------------------------------------------+
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| DESIGN ARTIFACT SUMMARY - compact launcher (Claude -> Codex)            |
| Six compact SVGs created, spoke spec updated, probe applied and         |
| reverted. No source, test, or implementation change. Not committed.     |
|                                                                         |
| === MEASURED VIEWPORT: 800 x 568 ===================================    |
|                                                                         |
| Method, exactly as directed: a temporary probe in index.html set        |
| document.title to the size and posted it to a local sink, because the   |
| window title is not readable from my side without capturing your        |
| screen - and I was not going to do that again. Published, ran the       |
| packaged Desktop, read the value, reverted the probe, republished.      |
|                                                                         |
|   window.innerWidth  x  window.innerHeight  =  800 x 568                |
|                                                                         |
|   Verified reverted: grep for the probe marker returns 0 in the source  |
|   and 0 in dist/forge-desktop/wwwroot/index.html, and git status shows  |
|   no change under src/. It was never committed.                         |
|                                                                         |
|   Worth noting: the outer window is 865x636, so the arithmetic guess I  |
|   refused to make would have been wrong on BOTH axes - 65px too wide    |
|   and 40px too tall. The compact frames would have inherited that.      |
|                                                                         |
| === CORRECTION 1: VALID CSS ARITHMETIC =============================    |
|                                                                         |
|   --wb-card-width: min(906px, calc(100% - 2 * var(--wb-page-inset)))    |
|   calc() inside min(), since the subtraction is not a valid bare        |
|   min() argument.                                                       |
|                                                                         |
|   Every other token is clamp(compact, calc(A + B*vh|vw), large) with A  |
|   and B solved so the middle term passes through BOTH endpoints:        |
|     B = (large - compact) / (upper - lower)     A = compact - B*lower   |
|   so it lands on the compact value at 568 (or 800 for width-driven      |
|   tokens) and on the EXACT approved 1536x1024 value at the upper bound. |
|   I asserted both endpoints numerically for all 33 tokens while         |
|   generating them; none is off by more than 0.05px. Examples:           |
|                                                                         |
|     --wb-header-height  clamp(56px, calc(4.93px + 8.991vh), 97px)       |
|     --wb-goal-height    clamp(88px, calc(-14.14px + 17.982vh), 170px)   |
|     --wb-card-pad       clamp(20px, calc(-6.09px + 3.261vw), 44px)      |
|     --wb-action-width   clamp(150px, calc(18.48px + 16.44vw), 271px)    |
|     --font-size-display clamp(24px, calc(9.05px + 2.632vh), 36px)       |
|                                                                         |
|   One fluid scale, no breakpoint, no second fixed card. The 1536x1024   |
|   frames stay the upper bound and are untouched.                        |
|                                                                         |
| === CORRECTION 2: ALL SIX STATES FIT, INCLUDING THE TALL ONES ======    |
|                                                                         |
| Lowest element bottom in each compact frame, against the 568px          |
| viewport:                                                               |
|                                                                         |
|   empty           492 / 568                                             |
|   drafted         492 / 568                                             |
|   busy            492 / 568                                             |
|   failed          542 / 568     <- band present, still fits             |
|   goal-required   542 / 568     <- band present, still fits             |
|   open-folder     540 / 568     <- path row present, still fits         |
|                                                                         |
| The two message-band states are the tallest case and are held to the    |
| same rule as the rest: no scrolling to reach a required input or the    |
| primary action. 26px of headroom remains in the worst case.             |
|                                                                         |
| === FILES CREATED ==================================================    |
|                                                                         |
|   docs/images/phase-43.20/task1-launcher-compact-empty.svg              |
|   ...-compact-drafted.svg          ...-compact-busy.svg                 |
|   ...-compact-failed.svg           ...-compact-goal-required.svg        |
|   ...-compact-open-folder.svg                                           |
|       six full 800x568 frames, flat SVG, title + desc, each carrying a  |
|       comment recording its lowest element bottom                       |
|                                                                         |
| === FILE CHANGED ===================================================    |
|                                                                         |
|   docs/phases/phase-43.20-project-workbench-mvp.md                      |
|       new "Two viewports, one fluid scale" section: the measured        |
|       viewport and how it was obtained, the clamp/calc solve, a         |
|       compact-vs-large table for every geometry and type token, the     |
|       six compact references with their lowest-element figures, and     |
|       the compact acceptance rule - document.scrollHeight <=            |
|       innerHeight plus a per-element bottom check, asserted in the      |
|       running packaged app rather than judged from a screenshot.        |
|                                                                         |
| === WHAT THE COMPACT SURFACE ACTUALLY LOOKS LIKE ===================    |
|                                                                         |
| It is a different density, not a scaled photocopy: 24px title, 88px     |
| goal field, 150x40 action, 56px header. The card keeps the reference's  |
| field order, labels, copy, hierarchy and language exactly; what         |
| changes is the breathing room. I think it reads honestly at that size,  |
| but it is your call before I build against it - that is why these are   |
| artifacts for approval rather than an implementation.                   |
|                                                                         |
| === NOT DONE =======================================================    |
|                                                                         |
|   No forge.css change, no component or page change, no test change.     |
|   Nothing committed; the working tree carries only the six new SVGs     |
|   and the spec edit.                                                    |
|   No operator contact.                                                  |
|                                                                         |
| Awaiting approval of these artifacts before implementation.             |
+-------------------------------------------------------------------------+
````

## 3. Fault found

The artifacts correctly measured 800×568, but the specification still optimized two endpoints and

## Requested

Measure the real viewport, create compact SVGs, and specify the responsive launcher before code.

## Better prompt

“Treat 800×568 and 1536×1024 as boundary checkpoints, not layouts. Specify browser-first evidence

## Prevention

Treat reference sizes as boundary checkpoints on a fluid range. Browser-first evidence must cover
