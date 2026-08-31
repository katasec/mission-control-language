# Turn 21 — Amended responsive design

## 1. Codex prompt

````text
+-------------------------------------------------------------------------+
| DESIGN REVIEW — REVISE ARTIFACT/SPEC ONLY (Codex -> Claude)             |
+-------------------------------------------------------------------------+
| Status: NOT YET APPROVED FOR IMPLEMENTATION.                            |
|                                                                         |
| The measured 800×568 inner viewport, reverted probe, six compact SVGs,  |
| and no-scroll layout of the six launcher states are accepted as a good  |
| basis. Do not edit source, tests, or implementation yet.               |
|                                                                         |
| Revise the active spoke and compact-artifact acceptance method as       |
| follows, then return the amended design summary for approval:           |
|                                                                         |
| 1. Browser-first visual work                                            |
|    Use the existing browser-rendered Client Runtime as the primary      |
|    design, screenshot, and resize-validation surface. Do not use the    |
|    packaged native Desktop to discover or iterate on layout. The native |
|    package is only a final parity check after browser acceptance passes.|
|                                                                         |
| 2. Responsive requirement                                               |
|    800×568 and 1536×1024 remain binding boundary/reference frames; they |
|    are not the two layouts to optimise in isolation. Validate the       |
|    launcher through continuous resizing across the supported range,     |
|    including around any justified structural breakpoint. Also cover     |
|    representative long goal/name/path/error values and supported        |
|    browser/WebView zoom or text scaling.                                |
|                                                                         |
| 3. Implementation rule                                                  |
|    clamp()/calc() endpoint maths may support spacing/type values, but   |
|    is not the definition of responsiveness. Specify fluid Grid/Flex     |
|    layout, minmax()/relative sizing where appropriate, min-width: 0 on  |
|    relevant children, and content-led reflow. Add a media/container     |
|    query only where the existing structure demonstrably stops working.  |
|    Do not introduce a breakpoint solely to match a device or reference  |
|    resolution.                                                          |
|                                                                         |
| 4. Non-negotiable boundaries                                            |
|    Responsive behaviour is CSS/presentation only: it must not alter     |
|    Client Runtime contracts or introduce a desktop-only action. Project |
|    creation and opening an existing folder remain available through the |
|    equivalent TUI action contracts.                                    |
|                                                                         |
|    All implementation colours, type, spacing, controls, and state       |
|    styling must resolve through the existing named design-system/theme  |
|    tokens. The reference SVGs may show resolved colours; forge.css may  |
|    not add raw surface/theme colours as a shortcut.                     |
|                                                                         |
| 5. Compact-height rule                                                  |
|    At 800×568 in every launcher state, goal, project name, location,    |
|    Create, and the open-folder entry point remain visible without page  |
|    scrolling. Only genuinely secondary content may use an explicit,     |
|    contained scroll region.                                             |
|                                                                         |
| Leave the work uncommitted. Return only the amended design/spec summary |
| and the exact browser-based acceptance evidence you will produce after  |
| implementation.                                                        |
+-------------------------------------------------------------------------+
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| AMENDED DESIGN SUMMARY - responsive launcher (Claude -> Codex)          |
| Spec revised. Six compact SVGs unchanged and still binding. No source,  |
| test, or implementation change. Nothing committed.                      |
|                                                                         |
| === WHAT CHANGED IN THE SPEC =======================================    |
|                                                                         |
| The "two viewports, one fluid scale" section is replaced by             |
| "Responsive behaviour", which reframes the whole thing: one fluid       |
| surface, with 800x568 and 1536x1024 as binding boundary CHECKPOINTS on  |
| a continuous range rather than two layouts to optimise.                 |
|                                                                         |
|   1. BROWSER-FIRST. A new "Where visual work happens" paragraph makes   |
|      the browser-rendered Client Runtime the primary design,            |
|      screenshot and resize-validation surface, and states plainly that  |
|      the packaged native Desktop is not used to discover or iterate on  |
|      layout - it is one parity check after browser acceptance passes.   |
|      I had it backwards; the browser is the same Presentation over      |
|      HTTP and it is the surface I can actually resize, zoom and         |
|      inspect.                                                           |
|                                                                         |
|   2. SUPPORTED RANGE, stated rather than implied.                       |
|      100% zoom: widths 800-1536 and heights 568-1024 fully supported    |
|      and bound by the compact-height rule. Above: the composition       |
|      holds at its upper bound and the surplus becomes margin. Below,    |
|      including 125/150/200% zoom: the page may scroll, but nothing      |
|      overlaps, clips, or needs horizontal scrolling above a 320px       |
|      viewport. I wrote this as a decision, not an open question, since  |
|      you asked me to specify - overrule it if the floor should differ.  |
|                                                                         |
|   3. LAYOUT FIRST, MATHS SECOND.                                        |
|      page: flex column, header band then the launcher container         |
|      card:  width min(906px, 100%) inside a page inset, centred         |
|      stack: grid, min-width: 0 on every control-bearing child so a      |
|             long path shrinks its field instead of widening the card    |
|      open row: grid-template-columns: minmax(0, 1fr) auto               |
|      goal:  clamped MIN-height, not a fixed height, so it can grow      |
|      clamp()/calc() is explicitly demoted to spacing and type only,     |
|      described as a ramp between checkpoints, not the definition of     |
|      responsiveness.                                                    |
|      NO BREAKPOINT IS PLANNED. The spec allows a container/media query  |
|      only where the structure demonstrably fails, with the failing      |
|      evidence recorded there - never to match a device or a reference   |
|      resolution.                                                        |
|                                                                         |
|   4. BOUNDARIES restated in the spec: responsive work is CSS and        |
|      markup structure only; no contract, authorization or filesystem    |
|      behaviour moves; no desktop-only action exists; create and open    |
|      stay the same ProjectCreateRequest / ProjectOpenRequest at every   |
|      size. Every value resolves through named tokens, and forge.css     |
|      gains no raw surface/theme colour outside the Workbench maps -     |
|      the SVGs may show resolved colours, the stylesheet may not take    |
|      that shortcut.                                                     |
|                                                                         |
|   5. COMPACT-HEIGHT RULE kept and tightened: at 800x568, in every       |
|      state, goal / project name / location / Create / open-folder       |
|      entry point visible without page scrolling, asserted numerically.  |
|      Only genuinely secondary content may use an explicit contained     |
|      scroll region - in practice a very long failure message, whose     |
|      band scrolls inside itself rather than pushing Create off screen.  |
|                                                                         |
| === ACCEPTANCE EVIDENCE I WILL PRODUCE =============================    |
|                                                                         |
| All browser-based, after build + full suite + package pass, and before  |
| any native check:                                                       |
|                                                                         |
|   1. BOUNDARY FRAMES. Six states at 1536x1024 against the large         |
|      references and at 800x568 against the compact ones, compared by    |
|      scanning both rasters for the same structural edges, +/-2px.       |
|   2. CONTINUOUS RESIZE SWEEP. 1536, 1440, 1366, 1280, 1152, 1024, 960,  |
|      900, 860, 800 wide with heights ramped 1024 -> 568. At every step: |
|      no horizontal document scroll, nothing wider than the card, no     |
|      clipped or overlapping text, and the compact-height rule from      |
|      768px of height down. Any width where the structure breaks is      |
|      recorded - and only then is a query considered.                    |
|   3. LONG CONTENT. ~400-char goal, ~120-char project name, ~300-char    |
|      location path, long failure message, at both boundaries: fields    |
|      shrink rather than widening the card, and only the message band    |
|      scrolls inside itself.                                             |
|   4. ZOOM. 125 / 150 / 200%: no overlap, no clipping, no horizontal     |
|      scroll above a 320px viewport. Below the supported range the page  |
|      may scroll - recorded as expected, not reported as a pass.         |
|   5. THEME MODES. Automatic light, automatic dark, forced light under   |
|      a dark OS, forced dark, plus the attribute-removed check that      |
|      ForgeUI inherits neither palette nor geometry.                     |
|   6. TOKEN AUDIT. Every launcher colour, radius, spacing and type       |
|      property traced from its computed value back to a named token,     |
|      plus the structural test that forge.css adds no raw surface/theme  |
|      colour outside the Workbench maps.                                 |
|   7. NATIVE PARITY, LAST. Only after all of the above: the packaged     |
|      Desktop at its own default window, one screenshot and the numeric  |
|      no-scroll assertion, confirming the accepted browser layout        |
|      renders identically in the WebView.                                |
|                                                                         |
|   Each item yields a recorded PASS/FAIL. A FAIL returns to design or    |
|   implementation; I do not forward one. Codex reviews after I record    |
|   PASS; operator acceptance only after both, and it is yours to ask.    |
|                                                                         |
| === ONE THING I EXPECT TO FIND =====================================    |
|                                                                         |
| Between roughly 860px and 800px the sparkle gutter and the 150px        |
| action are the tightest parts of the row. If the sweep shows the        |
| action wrapping or the gutter squeezing the goal field below a usable   |
| width, the honest fix is a single container query on the launcher that  |
| folds the gutter to zero and stretches the action full-width - and I    |
| will bring you the failing measurement first rather than adding it      |
| pre-emptively. The spec is written so that is the only way a query can  |
| appear.                                                                 |
|                                                                         |
| === FILES ==========================================================    |
|                                                                         |
|   CHANGED  docs/phases/phase-43.20-project-workbench-mvp.md             |
|   EXISTING, unchanged and still binding:                                |
|            six task1-launcher-compact-*.svg at 800x568                  |
|            six task1-project-launcher-*.svg at 1536x1024                |
|   NOT TOUCHED: forge.css, the component, the page, any test, any        |
|   contract. Nothing committed.                                          |
|                                                                         |
| Awaiting approval before implementation.                                |
+-------------------------------------------------------------------------+
````

## 3. Fault found

It improved the method substantially but tested a width/height diagonal while declaring a rectangular

## Requested

Make the compact plan browser-first, fluid, token-based, and TUI-safe.

## Better prompt

“Declare the supported range as a rectangle and test every corner plus a continuous resize. Do not

## Prevention

Acceptance matrices must test all boundary corners, not only proportional resize paths. Formal
