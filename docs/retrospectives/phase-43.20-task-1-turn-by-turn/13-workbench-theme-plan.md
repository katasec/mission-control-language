# Turn 13 — Workbench theme plan

## 1. Codex prompt

````text
+-------------------------------------------------------------------------+
| PLAN APPROVAL — STEP 1: VISUAL DESIGN ARTIFACTS ONLY                    |
+-------------------------------------------------------------------------+
| Approved: create the Task 1 visual-design artifacts and update the      |
| active spoke specification. Do not begin implementation afterwards.     |
|                                                                         |
| Resolved stylesheet decision: use Blazor CSS isolation. When            |
| implementation is later approved, add ProjectLauncher.razor.css and    |
| link ForgeMission.ClientRuntime.Presentation.styles.css from index.html.|
| Do not plan a fallback stylesheet. If CSS isolation fails in the        |
| actual build, stop and report it; do not substitute a mechanism.        |
|                                                                         |
| Authorized now:                                                        |
| - Create the rejected before SVG.                                       |
| - Create five separate 1536×1024 SVG references: empty, drafted, busy, |
|   failed, and goal-required.                                            |
| - Update Task 1's active spoke with the complete component/state spec, |
|   exact copy, geometry, sampled reference values, owned/deferred scope,|
|   light-only limitation, and visual acceptance rule.                    |
|                                                                         |
| The SVGs must use the reference's visual language for every owned       |
| element. Sample colours/type values from the PNG; do not approximate    |
| them from the existing ember theme.                                     |
|                                                                         |
| Not authorized:                                                        |
| - No Razor, CSS, HTML, transport, runtime, or test changes.             |
| - No build, package, commit, push, or PR update.                        |
|                                                                         |
| Next step: send a DESIGN ARTIFACT SUMMARY only: every file created or   |
| changed, the exact slice and state decisions captured, and any mismatch |
| or obstacle. Do not claim Task 1 is complete. Wait for explicit        |
| approval before implementation.                                         |
+-------------------------------------------------------------------------+
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| AMENDED IMPLEMENTATION PLAN - Workbench theme (Claude -> Codex)         |
| Phase 43.20 Task 1. No files changed. The six SVGs and the spoke spec   |
| from step 1 stand, except the two corrections named at the end.         |
|                                                                         |
| --- WHAT I VERIFIED IN forge.css FIRST -----------------------------    |
|                                                                         |
| It already has a three-state mode model, and the new axis has to        |
| compose with all three rather than replace any:                         |
|   section 1  :root                              light, the default      |
|   section 2  @media (prefers-color-scheme: dark)                        |
|              :root:not([data-theme="light"])    automatic dark          |
|              :root[data-theme="dark"]           forced dark             |
| So data-theme means light/dark mode and keeps that meaning.             |
| data-surface-theme is the orthogonal product axis.                      |
|                                                                         |
| --- THE THEME SELECTOR AND ITS CASCADE -----------------------------    |
|                                                                         |
| New section 3 in forge.css, "Design tokens - Workbench theme",          |
| immediately after the existing dark section:                            |
|                                                                         |
|   :root[data-surface-theme="workbench"]                    (0,2,0)      |
|       Workbench light                                                   |
|   @media (prefers-color-scheme: dark) {                                 |
|     :root[data-surface-theme="workbench"]:not([data-theme="light"])     |
|                                                            (0,3,0)      |
|       Workbench automatic dark                                          |
|   }                                                                     |
|   :root[data-surface-theme="workbench"][data-theme="dark"] (0,3,0)      |
|       Workbench forced dark                                             |
|                                                                         |
| Every Workbench selector outranks its default-theme counterpart on      |
| specificity, so the map composes without depending on source order:     |
|   auto light      -> Workbench light                                    |
|   auto dark       -> Workbench dark                                     |
|   data-theme=dark -> Workbench dark                                     |
|   data-theme=light under a dark OS -> Workbench light, because the      |
|       automatic-dark block excludes it exactly as the default theme's   |
|       does                                                              |
| Nothing without the attribute matches any Workbench selector, so        |
| ForgeUI keeps its default theme untouched.                              |
|                                                                         |
| Selection: data-surface-theme="workbench" on <html> in the Client       |
| Runtime Presentation's index.html. Static markup, no JavaScript, no     |
| runtime switch, and no way for it to leak into ForgeUI's own host       |
| page.                                                                   |
|                                                                         |
| --- TOKEN MAP: WHAT IS MEASURED AND WHAT IS NOT --------------------    |
|                                                                         |
| I will not present derived or invented values as sampled. Three         |
| groups, labelled as such in the CSS comments and the spoke:             |
|                                                                         |
|   A. SAMPLED from the approved SVG reference                            |
|      --bg #f7faff   --surface #ffffff   --surface-sunken #f6f8fd        |
|      --border #e7ecf4   --border-strong #d5dae5                         |
|      --text #101d33   --text-muted #5b6b83   --text-subtle #8b99ad      |
|      --accent #0f6eeb   --accent-hover #0f56d2                          |
|      --accent-contrast #ffffff   --ink / --ink-contrast to match        |
|   B. DERIVED from group A by the same relationships the default         |
|      theme already uses between a surface and its hover/active/soft     |
|      variants: --surface-hover, --surface-active, --accent-soft.        |
|   C. NO LAUNCHER EVIDENCE. --danger/-bg/-border take the trio the       |
|      spec already had to invent for the failure state, now living in    |
|      the theme instead of the component. --success takes the locked     |
|      visual language's lime, --warning an amber in the same register,   |
|      seals the language's blue. Task 1 exercises none of these except   |
|      danger; they are mapped so a later Workbench surface cannot fall   |
|      back to an ember green. Say the word and I map only danger and     |
|      leave the rest inherited.                                          |
|                                                                         |
|   Radii, from the reference: card 16 -> --radius-lg, fields and the     |
|   button 10 -> --radius, the demoted location field 8 -> --radius-sm.   |
|   Only --radius-lg and --radius-sm shift inside this theme.             |
|                                                                         |
|   Type scale: the reference needs 36/28/22/21/20/16 and forge.css has   |
|   no size tokens today, only font families. Rather than let the         |
|   launcher declare literals, I add named size tokens with the current   |
|   effective values in section 1 and override them in the Workbench      |
|   map. Adding token names changes nothing that exists - no current      |
|   rule consumes them.                                                   |
|                                                                         |
|   Spacing: card padding measures 45-46. I add --space-7 (44px) to the   |
|   scale rather than have the component compute a one-off. See the       |
|   tolerance correction below.                                           |
|                                                                         |
| --- WHAT ProjectLauncher.razor.css MAY CONTAIN ---------------------    |
|                                                                         |
| Layout only: grid/flex, sizing, positioning, and the state rules for    |
| disabled, focus, and hover. Every colour, radius, spacing, font         |
| family, and font size resolves through var(--token). It declares no     |
| custom property of its own. If a value the reference needs has no       |
| token, the token is added to the Workbench map - not inlined in the     |
| component.                                                              |
|                                                                         |
| --- FILE LIST ------------------------------------------------------    |
|                                                                         |
|   src/ForgeUI/wwwroot/css/forge.css                                     |
|       new section 3, the Workbench light + paired dark maps, with the   |
|       A/B/C provenance in comments; new size-token names defaulted in   |
|       section 1; --space-7 added to the scale                           |
|   src/ForgeMission.ClientRuntime.Presentation/wwwroot/index.html        |
|       data-surface-theme="workbench" on <html>, and the scoped-bundle   |
|       link (ForgeMission.ClientRuntime.Presentation.styles.css)         |
|   src/ForgeMission.ClientRuntime.Presentation/Components/               |
|       ProjectLauncher.razor         (new)                               |
|       ProjectLauncher.razor.css     (new, layout only)                  |
|   src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor          |
|       renders the component, drops the launcher markup and its inline   |
|       style block, header wording                                       |
|   src/ForgeMission.Tests/Presentation/HomeSessionOperationTests.cs      |
|       selector updates and the goal-commit trigger; assertions          |
|       unchanged                                                         |
|   src/ForgeMission.Tests/Architecture/ForgeCssThemeScopingTests.cs      |
|       (new) every declaration in the Workbench sections is reachable    |
|       only through [data-surface-theme="workbench"], and no Workbench   |
|       value appears in an unscoped :root block. This is what keeps a    |
|       later edit from retokening ForgeUI by accident, instead of a      |
|       comment asking someone to remember.                               |
|   docs/phases/phase-43.20-project-workbench-mvp.md                      |
|       replace the spec's "launcher-local custom properties" paragraph   |
|       with the theme architecture, and add the tolerance line below     |
|                                                                         |
|   NOT TOUCHED: ForgeUI's host page, layouts, and components;            |
|   ProjectStore; the transport contracts; the endpoints; the session     |
|   store; ProjectStoreTests; ProjectTransportContractTests.              |
|                                                                         |
| --- LIGHT / DARK VERIFICATION --------------------------------------    |
|                                                                         |
| Four mode combinations against the packaged surface at 1536x1024:       |
|   automatic light                      -> compared to the SVGs          |
|   automatic dark (emulated)            -> composition check             |
|   data-theme="light" under a dark OS   -> must render Workbench light   |
|   data-theme="dark"                    -> must render Workbench dark    |
| Plus a ForgeUI page loaded in the same session to show it is visually   |
| unchanged.                                                              |
|                                                                         |
| Honest criterion: only the light renders are compared to a reference,   |
| because the approved SVGs are a light composition. Dark is verified     |
| for correct composition - the Workbench map resolves, no ember value    |
| leaks through, and every text/surface pair stays legible - not for      |
| matching a mock that does not exist. If you want dark held to a         |
| reference, it needs its own approved SVG first, and I would rather be   |
| told that now than argue it at the gate.                                |
|                                                                         |
| --- TWO CORRECTIONS TO THE STEP-1 ARTIFACTS ------------------------    |
|                                                                         |
| 1. The spec paragraph that says the sampled values live as              |
|    launcher-local custom properties is superseded and will be           |
|    rewritten to the architecture above. The SVG files themselves do     |
|    not change: they are reference targets, and their literal colours    |
|    are correct as targets.                                              |
| 2. The acceptance rule needs a tolerance it currently lacks, or the     |
|    gate is undecidable: hand-written CSS and a hand-authored SVG will   |
|    differ by a pixel or two in text metrics. Proposed: geometry within  |
|    +/-2px; field order, copy strings, type scale, hierarchy, and        |
|    resolved colours exact. Without this, --space-7 at 44px against a    |
|    measured 45-46 would technically be a FAIL.                          |
|                                                                         |
| --- SEQUENCE (unchanged except where noted) ------------------------    |
|                                                                         |
|   1. Amend the two spec paragraphs above. STOP for your approval.       |
|   2. Implement: theme map, selector, component, layout CSS. Confirm     |
|      and report whether CSS isolation works; if it does not, stop and   |
|      report rather than substitute.                                     |
|   3. Update bunit selectors; add the theme-scoping test.                |
|   4. dotnet build, full dotnet test, make desktop-publish. All green    |
|      before anything visual.                                            |
|   5. Claude's visual comparison of the packaged surface at 1536x1024:   |
|      five states in light against the SVGs, plus the four mode          |
|      combinations and the ForgeUI check above.                          |
|   6. Claude records PASS or FAIL per state with the packaged            |
|      screenshots. A FAIL returns to step 1 or 2.                        |
|   7. Codex independently reviews and records PASS/FAIL.                 |
|   8. Only after both PASS does the operator's final acceptance          |
|      happen, and it is yours to request. Merge stays blocked.           |
|                                                                         |
| --- ONE CONSEQUENCE YOU SHOULD DECIDE ON ---------------------------    |
|                                                                         |
| Setting the attribute on the document root themes the whole Desktop     |
| client, not only the launcher. The post-create session view consumes    |
| the same semantic tokens, so it will render in Workbench blue rather    |
| than ember - no layout change, no behaviour change, but it will not     |
| match the screenshots from the last round. I think that is right for a  |
| named product theme and wrong to hide, but the slice says the           |
| post-create surface is Tasks 2/3, so I am naming it rather than         |
| letting it appear in a screenshot you did not expect. If you want it    |
| contained, the attribute goes on the launcher's own container instead   |
| and the rest of the client stays ember until Task 3.                    |
|                                                                         |
| Awaiting your approval before changing any file.                        |
+-------------------------------------------------------------------------+
````

## 3. Fault found

Theme architecture was investigated reactively, after the visual artifacts existed. It should have

## Requested

Ensure the new launcher reskins through the existing theming system instead of hard-coded colours.

## Better prompt

“Inspect the current theme system first. Document mode versus product-surface axes, selector

## Prevention

Every UI plan must begin with a design-system reconnaissance step: existing theme axes, selector
