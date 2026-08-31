# Turn 24 — Responsive launcher completion

## 1. Codex prompt

````text
+-------------------------------------------------------------------------+
| IMPLEMENTATION APPROVAL — Phase 43.20 responsive launcher (Codex ->     |
| Claude)                                                                 |
+-------------------------------------------------------------------------+
| The amended responsive-launcher design is approved. Implement Task 1    |
| against the active spoke and its binding SVG references.                |
|                                                                         |
| Scope                                                                    |
| - Implement only the approved Workbench launcher responsive behaviour.  |
| - Use the existing Client Runtime contracts unchanged: ProjectCreate-   |
|   Request and ProjectOpenRequest remain the same Desktop/TUI actions.   |
| - Use named Workbench/theme tokens only. Do not introduce raw           |
|   surface/theme colours in forge.css.                                   |
| - Apply the height/width axis rule, including                            |
|   --wb-card-pad-x / --wb-card-pad-y.                                   |
| - Do not add a query unless browser evidence shows a real structural    |
|   failure; record that evidence if one becomes necessary.               |
|                                                                         |
| Validation order                                                        |
| 1. Build, run the required test suite, and package successfully.        |
| 2. In the browser-rendered Client Runtime, record your own PASS/FAIL    |
|    evidence for:                                                        |
|    - all six states at 800×568 and 1536×1024 against the SVGs;          |
|    - all four corners: 800×568, 800×1024, 1536×568, 1536×1024;          |
|    - representative sweep plus observed continuous drag-resize;        |
|    - long goal/name/path/failure content;                               |
|    - 125/150/200% zoom degradation;                                    |
|    - all four theme modes and the ForgeUI attribute-removed check;     |
|    - named-token audit.                                                 |
| 3. Only after the browser evidence passes, perform one packaged         |
|    Desktop parity check at its default window. Do not use the native    |
|    host for layout discovery or iteration.                              |
|                                                                         |
| A visual FAIL returns to design/implementation. Do not ask the operator |
| for review. Do not commit yet. Return a completion summary with the     |
| exact files changed, test/build results, screenshots/evidence paths,    |
| and explicit per-check PASS/FAIL for Codex review.                      |
+-------------------------------------------------------------------------+
````

## 2. Claude response

````text
+---------------------------------------------------------------------------+
| COMPLETION SUMMARY - Phase 43.20 responsive launcher (Claude -> Codex)     |
| Nothing committed. Branch codex/phase-43.20-project-manifest, working tree |
| dirty by design. Operator not asked for review.                            |
|                                                                           |
| === FILES CHANGED ================================================        |
|   M src/ForgeUI/wwwroot/css/forge.css                                     |
|       Workbench map: every geometry token now fluid between the two       |
|       boundary frames; two new tokens this round (below).                 |
|   M src/ForgeMission.ClientRuntime.Presentation/Components/               |
|       ProjectLauncher.razor.css   structure only; no literal geometry     |
|   M src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor          |
|       header band on header tokens; showOpenFolder cleared on Failed      |
|   M src/ForgeMission.Tests/Architecture/ForgeCssThemeScopingTests.cs      |
|       geometry-declaration guard gains --wb-band-max, --wb-action-pad-x   |
|   M docs/phases/phase-43.20-project-workbench-mvp.md                      |
|   A docs/images/phase-43.20/task1-launcher-compact-*.svg  (six, 800x568)  |
|   NOT touched: ProjectStore, transport contracts, endpoints, session      |
|   store. No behaviour, authorization or filesystem change.                |
|                                                                           |
| === BUILD / TEST / PACKAGE =======================================        |
|   dotnet build      succeeded, 0 warnings, 0 errors                       |
|   dotnet test       850 passed, 11 skipped, 0 failed (5 assemblies)       |
|   make desktop-publish   clean, ILC 0                                     |
|                                                                           |
| === PER-CHECK RESULT =============================================        |
|   Four corners x six states .................. PASS  24/24               |
|   Continuous drag-resize ..................... PASS  555 live steps, 0   |
|   Off-diagonal sweep ......................... PASS  6/6                 |
|   Long goal / name / path / failure .......... PASS  after fix 1         |
|   Zoom 125 / 150 / 200% ...................... PASS  4/4                 |
|   Four theme modes + attribute-removed ....... PASS  5/5                 |
|   Named-token audit .......................... PASS                     |
|   Packaged Desktop parity, one check ......... PASS  800x568, fits       |
|                                                                           |
|   Corner matrix, lowest required element vs viewport:                    |
|     state          800x568  1536x568  800x1024  1536x1024                |
|     empty/drafted/busy  492      496       908       908                 |
|     failed/goal-req     543      547       994       994                 |
|     open-folder         540      544       975       975                 |
|   Every cell fits, no horizontal scroll, no overflow, nothing missing.   |
|                                                                           |
| === TWO DEFECTS FOUND AND FIXED ==================================        |
|   1. The message band's ceiling was a fraction of the goal field. Those   |
|      are unrelated quantities: what bounds the band is the space left     |
|      below it. A 518-char failure message pushed the primary action 2px   |
|      past the fold at 800x568 and 35px at 1536x1024. Replaced with        |
|      --wb-band-max (54px/86px), sized from the measured band-less         |
|      allowance (60px at 568, 92px at 1024). Re-measured: all four         |
|      corners now fit a maximal message with 6-10px of slack. The six      |
|      reference states are untouched by it - their bands are 39px and      |
|      62px, under the ceiling - and re-measurement returned identical      |
|      geometry (492/543/908/994, cards 68..459 and 134..853).              |
|                                                                           |
|   2. `Create project` wrapped to two lines at the compact bound. The      |
|      label measures exactly 102px and a 24px inset left exactly 102px.    |
|      The binding compact reference draws it on one line in the same       |
|      150x40 box, so this was a genuine reference mismatch, and only a     |
|      screenshot caught it - every numeric check passed while it wrapped.  |
|      Fixed with --wb-action-pad-x (12px/24px, width-driven, matching the  |
|      axis of the box it sits in): the box stays reference geometry, the   |
|      label gains 24px of slack. At 1536 the padding (24px) and the box    |
|      (271x68 at 738..806) are unchanged.                                  |
|                                                                           |
| === PACKAGED PARITY DETAIL =======================================        |
|   Measured with the approved temporary probe applied ONLY to dist/ - a    |
|   gitignored build output, never source - then removed by re-publishing.  |
|   0 occurrences remain in src/, dist/ or docs/. No screen capture used.   |
|                                                                           |
|     element   browser     packaged    delta                              |
|     card      68..459     68..457     -2                                 |
|     goal      129..217    128..216    -1                                 |
|     name      256..300    255..299    -1                                 |
|     location  331..369    329..367    -2                                 |
|     create    398..438    396..436    -2                                 |
|     link      474..492    472..490    -2                                 |
|   Horizontal geometry identical (69/763, 613, 36/226, 16/784). Vertical   |
|   differs by at most 2px, consistent with sub-pixel rounding of the       |
|   fluid clamps in WebKit vs Chromium, inside the +/-2px tolerance.        |
|                                                                           |
| === EVIDENCE ====================================================         |
|   Full numeric record, all eight checks:                                  |
|     <scratchpad>/responsive-evidence.md                                   |
|   Screenshots at 800x568 (empty, and the long-failure case that drove     |
|   fix 1) were taken in-session in the browser pane.                       |
|   Method: same-origin iframe sized to the target viewport so vh/vw        |
|   resolve to it, driving the real transport contracts; the drag test      |
|   resizes one live document without reloading.                            |
|                                                                           |
| === ONE THING TO WEIGH ==========================================         |
|   Defect 2 passed every numeric gate while visibly wrong. The compact     |
|   acceptance rule measures boxes, not whether labels fit inside them. If  |
|   you want that closed structurally rather than by my having looked, the  |
|   check is cheap - assert label width < inner width for both actions at   |
|   both bounds - but it is a new requirement, so I have not added it.      |
|                                                                           |
|   Awaiting your review. Uncommitted.                                      |
+---------------------------------------------------------------------------+
````

## 3. Fault found

No new implementation defect remained in the final report. During validation it found and fixed a

## Requested

Implement the approved plan and return browser-first evidence, packaged parity, tests, and a

## Better prompt

“Add one explicit text-fit check to each visual state, publish concrete evidence paths, separate

## Prevention

Add text-fit assertions and screenshots to visual acceptance, alongside geometric checks. The final
