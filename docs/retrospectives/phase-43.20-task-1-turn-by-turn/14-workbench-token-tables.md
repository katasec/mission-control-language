# Turn 14 — Workbench token tables

## 1. Codex prompt

````text
+-------------------------------------------------------------------------+
| DESIGN CORRECTION — THEMED RESKIN, NOT LOCAL COLOUR OVERRIDES           |
+-------------------------------------------------------------------------+
| My earlier direction to use launcher-local sampled custom properties is |
| superseded. Do not implement it.                                        |
|                                                                         |
| The SVG colour samples are reference targets only. The implementation   |
| must map them to the existing semantic design tokens through a named    |
| Workbench product theme.                                                |
|                                                                         |
| Required architecture:                                                   |
| - Extend src/ForgeUI/wwwroot/css/forge.css with a named Workbench theme |
|   token map.                                                           |
| - Select it in Client Runtime with data-surface-theme="workbench" on   |
|   the document root. Do not overload data-theme: it retains its current |
|   light/dark mode meaning.                                              |
| - Define Workbench light tokens from the approved SVG reference and a   |
|   paired dark token map that composes with both automatic and explicit  |
|   light/dark mode.                                                      |
| - ProjectLauncher.razor.css may contain layout rules only; it consumes  |
|   semantic tokens and declares no local colour, typography, radius, or  |
|   spacing token values.                                                 |
| - ForgeUI remains on its default theme unless it explicitly selects     |
|   Workbench.                                                            |
|                                                                         |
| The active spoke has been corrected accordingly. No implementation is   |
| approved yet. Reply with an amended implementation plan covering the    |
| theme selector, token-map file changes, light/dark verification, and    |
| the updated file list. Do not change files until that plan is approved. |
+-------------------------------------------------------------------------+
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| WORKBENCH TOKEN TABLES + AMENDED FILE LIST (Claude -> Codex)            |
| Phase 43.20 Task 1. Only the active spoke's Task 1 spec was amended.    |
| No implementation, test, build, package, commit, or PR update.          |
|                                                                         |
| Values are complete and exact. Nothing is left as derived, to match,    |
| inherited, or say-the-word. Provenance is labelled per token:           |
|   S = sampled from the approved reference                               |
|   D = derived from a sampled value by the relationship the default      |
|       theme already uses between a base and its variants                |
|   N = no reference evidence (the reference has no failure, success,     |
|       warning, or seal state)                                           |
|                                                                         |
| === WORKBENCH LIGHT =================================================   |
|                                                                         |
|   color-scheme        light                                             |
|   --bg                #f7faff   S   page canvas                         |
|   --surface-sunken    #f6f8fd   S   header band                         |
|   --surface           #ffffff   S   card, fields                        |
|   --surface-hover     #eef3fb   D                                       |
|   --surface-active    #e4ecf8   D                                       |
|   --border            #e7ecf4   S   card border                         |
|   --border-strong     #d5dae5   S   field border                        |
|   --text              #101d33   S   title, field values                 |
|   --text-muted        #5b6b83   S   Location label                      |
|   --text-subtle       #8b99ad   S   placeholders                        |
|   --accent            #0f6eeb   S   primary action, links               |
|   --accent-hover      #0f56d2   S   hover, focused field border         |
|   --accent-soft       #eff5fe   S   notice band                         |
|   --accent-contrast   #ffffff   S   text on the primary action          |
|   --ink               #0b2447   D   neutral solid control               |
|   --ink-hover         #143a6b   D                                       |
|   --ink-contrast      #ffffff   D                                       |
|   --success           #4d7c0f   N                                       |
|   --success-bg        #f2fbe6   N                                       |
|   --success-border    #84cc16   N   the locked language's lime          |
|   --danger            #b42318   N   failure message                     |
|   --danger-bg         #fef3f2   N   failure band                        |
|   --danger-border     #fda29b   N                                       |
|   --warning           #8a5a06   N                                       |
|   --warning-bg        #fef6e7   N                                       |
|   --seal-official     #8a5a06   N                                       |
|   --seal-verified     #0f6eeb   N                                       |
|   --seal-check        #ffffff   N                                       |
|   --radius-sm         8px       S   Location field                      |
|   --radius            10px      S   goal, name, button                  |
|   --radius-lg         16px      S   card                                |
|   --radius-pill       999px                                             |
|   --space-1..6        4 8 12 16 24 32 px                                |
|   --space-7           44px      S   card padding (measured 45-46,       |
|                                     inside the approved +/-2px)         |
|   --font-size-display 36px      S   wordmark, card title                |
|   --font-size-title   28px      S   AI Workbench                        |
|   --font-size-lead    22px      S   goal text, button label             |
|   --font-size-body    21px      S   Project name value                  |
|   --font-size-label   20px      S   Project name label                  |
|   --font-size-meta    16px      S   Location label                      |
|   --font-size-mono    15px      S   location path                       |
|   --font-sans         system-ui, -apple-system, "Segoe UI", Roboto,     |
|                       Helvetica, Arial, sans-serif                      |
|   --font-mono         ui-monospace, SFMono-Regular, "SF Mono", Menlo,   |
|                       Consolas, monospace                               |
|   --transition        130ms ease                                        |
|   --focus-ring        0 0 0 3px rgba(15, 110, 235, 0.30)          D     |
|   --shadow-sm         0 1px 2px rgba(11, 36, 71, 0.06)            D     |
|   --shadow            0 1px 3px rgba(11, 36, 71, 0.08),                 |
|                       0 4px 12px rgba(11, 36, 71, 0.06)           D     |
|   --shadow-lg         0 8px 30px rgba(11, 36, 71, 0.14)           D     |
|                                                                         |
| The seven --font-size-* names are new. They get declared in the         |
| default :root with the sizes components already use as literals         |
| (30 / 22 / 17 / 15 / 14 / 12.5 / 12 px), so adding them changes         |
| nothing that exists, and the Workbench map overrides them above.        |
|                                                                         |
| === WORKBENCH DARK ==================================================   |
|                                                                         |
| Applies to both the automatic and the forced-dark selector. Radii,      |
| spacing, type sizes, fonts and transition are restated identically to   |
| light; every token that differs is listed here.                         |
|                                                                         |
|   color-scheme        dark                                              |
|   --bg                #071426   page canvas                             |
|   --surface-sunken    #0a1a30   header band                             |
|   --surface           #0f2440   card, fields                            |
|   --surface-hover     #16304f                                           |
|   --surface-active    #1d3a5c                                           |
|   --border            #1d3556   card border                             |
|   --border-strong     #2b4a72   field border                            |
|   --text              #e6edf7                                           |
|   --text-muted        #9fb2ca                                           |
|   --text-subtle       #8aa3c2   placeholders                            |
|   --accent            #4d9bff   primary action, links                   |
|   --accent-hover      #74b2ff                                           |
|   --accent-soft       #12294a   notice band                             |
|   --accent-contrast   #06121f   text on the primary action              |
|   --ink               #e6edf7                                           |
|   --ink-hover         #c9d8ec                                           |
|   --ink-contrast      #071426                                           |
|   --success           #a3e635                                           |
|   --success-bg        #16300a                                           |
|   --success-border    #65a30d                                           |
|   --danger            #ff9d95   failure message                         |
|   --danger-bg         #3a1512   failure band                            |
|   --danger-border     #b42318                                           |
|   --warning           #e8c06a                                           |
|   --warning-bg        #3a2c10                                           |
|   --seal-official     #e8c06a                                           |
|   --seal-verified     #4d9bff                                           |
|   --seal-check        #071426                                           |
|   --focus-ring        0 0 0 3px rgba(77, 155, 255, 0.35)                |
|   --shadow-sm         0 1px 2px rgba(0, 0, 0, 0.45)                     |
|   --shadow            0 1px 3px rgba(0, 0, 0, 0.50),                    |
|                       0 4px 12px rgba(0, 0, 0, 0.40)                    |
|   --shadow-lg         0 8px 30px rgba(0, 0, 0, 0.55)                    |
|                                                                         |
| === MEASURED CONTRAST ==============================================    |
|                                                                         |
| Computed from the values above, not asserted:                           |
|                                                                         |
|   pair                            light    dark                         |
|   --text on --surface             16.86   13.23                         |
|   --text on --bg                  16.12   15.67                         |
|   --text-muted on --surface        5.42    7.20                         |
|   --text-subtle on --surface       2.89    6.01                         |
|   --accent-contrast on --accent    4.72    6.69                         |
|   --accent on --surface            4.72    5.53                         |
|   --danger on --danger-bg          6.05    8.11                         |
|                                                                         |
| Every pair clears AA except light --text-subtle at 2.89. It is the      |
| sampled reference placeholder colour and is used only for placeholder   |
| and hint text. I am recording it rather than quietly shipping it:       |
| raising it diverges from the reference, so the reference wins unless    |
| you say otherwise. Dark --text-subtle was moved off the light value's   |
| direct analogue to #8aa3c2 precisely so dark clears AA at 6.01.         |
|                                                                         |
| === CASCADE ========================================================    |
|                                                                         |
|   :root[data-surface-theme="workbench"]                     (0,2,0)     |
|   @media (prefers-color-scheme: dark) {                                 |
|     :root[data-surface-theme="workbench"]:not([data-theme="light"])     |
|                                                             (0,3,0)     |
|   }                                                                     |
|   :root[data-surface-theme="workbench"][data-theme="dark"]  (0,3,0)     |
|                                                                         |
| Each outranks its default-theme counterpart, so the map does not        |
| depend on source order. data-theme="light" under a dark OS lands on     |
| Workbench light, because the automatic-dark block excludes it exactly   |
| as the default theme's does.                                            |
|                                                                         |
| === AMENDED FILE LIST ==============================================    |
|                                                                         |
|   ALREADY CHANGED (this reply's authorized amendment)                   |
|     docs/phases/phase-43.20-project-workbench-mvp.md                    |
|         Task 1 spec: theme architecture + cascade table, the complete   |
|         light and dark token tables above with per-token provenance,    |
|         the measured contrast table and its one exception, the          |
|         dark-mode acceptance criteria, and the approved +/-2px          |
|         geometry tolerance. The superseded launcher-local paragraph     |
|         is gone; the "not sampled" paragraph now points at the N rows   |
|         instead of restating values.                                    |
|                                                                         |
|   TO CHANGE AT IMPLEMENTATION (nothing done yet)                        |
|     src/ForgeUI/wwwroot/css/forge.css                                   |
|         new section: Workbench light + paired dark maps with the        |
|         S/D/N provenance in comments; the seven --font-size-* names     |
|         and --space-7 added to the default :root                        |
|     src/ForgeMission.ClientRuntime.Presentation/wwwroot/index.html      |
|         data-surface-theme="workbench" on <html>; scoped-bundle link    |
|         (ForgeMission.ClientRuntime.Presentation.styles.css)            |
|     src/ForgeMission.ClientRuntime.Presentation/Components/             |
|         ProjectLauncher.razor       (new)                               |
|         ProjectLauncher.razor.css   (new, layout only, tokens only)     |
|     src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor        |
|         renders the component, drops the launcher markup and its        |
|         inline style block, header wording                              |
|     src/ForgeMission.Tests/Presentation/HomeSessionOperationTests.cs    |
|         selector updates and the goal-commit trigger; assertions        |
|         unchanged                                                       |
|     src/ForgeMission.Tests/Architecture/ForgeCssThemeScopingTests.cs    |
|         (new) every Workbench declaration is reachable only through     |
|         [data-surface-theme="workbench"], and no Workbench value        |
|         appears in an unscoped :root block                              |
|                                                                         |
|   NOT TOUCHED: ForgeUI's host page, layouts and components;             |
|   ProjectStore; the transport contracts; the endpoints; the session     |
|   store; ProjectStoreTests; ProjectTransportContractTests.              |
|                                                                         |
| Awaiting implementation approval.                                       |
+-------------------------------------------------------------------------+
````

## 3. Fault found

The tables arrived as a separate corrective artifact because token ownership and contrast evidence

## Requested

Supply concrete token tables and the implementation file list for the Workbench surface.

## Better prompt

“Return one complete token table in the first theme plan: semantic token, light value, dark value,

## Prevention

Make token tables, contrast pairs, and selector ownership required fields of the first visual spec,
