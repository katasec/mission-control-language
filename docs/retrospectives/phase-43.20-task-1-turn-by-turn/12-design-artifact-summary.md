# Turn 12 — Design artifact summary

## 1. Codex prompt

````text
+-------------------------------------------------------------------------+
| PLAN CORRECTION — TWO ITEMS REQUIRED BEFORE APPROVAL                    |
+-------------------------------------------------------------------------+
| The owned/deferred inventory, sparse-canvas resolution, state coverage, |
| and two-stage visual-review sequence are accepted in principle.         |
|                                                                         |
| 1. Reference fidelity                                                    |
| Decision 4 is not acceptable as written. The journey mock's visible     |
| navy/blue/cyan visual language cannot be called a “known deferred       |
| difference” while that mock is the binding source. Current ember tokens |
| are implementation constraints, not an acceptance override.            |
|                                                                         |
| Revise the plan so the Task 1 SVG and its implementation match the      |
| reference's owned visual language: canvas, header, card, typography,    |
| borders, primary action, and link treatment. Keep this styling scoped   |
| to the Project launcher so it does not silently retoken ForgeUI. Name   |
| the required scoped style file(s) in the file list.                     |
|                                                                         |
| 2. Packaged visual evidence                                              |
| Correct the sequence: build, run the full test suite, and produce the   |
| Desktop package before Claude performs visual comparison. The five       |
| screenshots must be of the packaged/running surface at 1536×1024, not   |
| an un-packaged development rendering.                                   |
|                                                                         |
| Also replace the single multi-state “after.svg” with one separate SVG   |
| per state: empty, drafted, busy, failed, and goal-required. This keeps  |
| every acceptance state directly inspectable.                             |
|                                                                         |
| Reply with only the amended Sections 6, 7, 8, and 9. Do not create or   |
| modify files yet.                                                       |
+-------------------------------------------------------------------------+
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| DESIGN ARTIFACT SUMMARY - Phase 43.20 Task 1 (Claude -> Codex)          |
| Step 1 only. Six SVGs created, spoke spec written. No Razor, CSS, HTML, |
| transport, runtime, or test change. No build, package, commit, push, or |
| PR update. Task 1 is NOT complete.                                      |
|                                                                         |
| --- FILES CREATED --------------------------------------------------    |
|                                                                         |
|   docs/images/phase-43.20/task1-project-launcher-before.svg             |
|   docs/images/phase-43.20/task1-project-launcher-empty.svg              |
|   docs/images/phase-43.20/task1-project-launcher-drafted.svg            |
|   docs/images/phase-43.20/task1-project-launcher-busy.svg               |
|   docs/images/phase-43.20/task1-project-launcher-failed.svg             |
|   docs/images/phase-43.20/task1-project-launcher-goal-required.svg      |
|       each a full 1536x1024 frame, hand-authored flat SVG, box/text     |
|       only, with title and desc elements                                |
|                                                                         |
| --- FILE CHANGED ---------------------------------------------------    |
|                                                                         |
|   docs/phases/phase-43.20-project-workbench-mvp.md                      |
|       new subsection under Task 1: "Approved-for-review visual          |
|       specification (drafted 2026-08-30, awaiting Codex sign-off)".     |
|       The task's rejection record above it is untouched, and nothing    |
|       in it claims Task 1 is done.                                      |
|                                                                         |
| --- HOW THE VALUES WERE OBTAINED -----------------------------------    |
|                                                                         |
| Not eyeballed and not approximated from the ember theme. I decoded the  |
| reference PNG directly (pure-python PNG reader; no PIL or ImageMagick   |
| on this machine) and sampled it:                                        |
|                                                                         |
|   canvas          #f7faff     header band     #f6f8fd                   |
|   header rule     #c9d5e7     wordmark/link   #0468ed                   |
|   primary action  #0f6eeb     focused border  #0f56d2                   |
|   card            #ffffff     card border     #e7ecf4                   |
|   field border    #d5dae5     divider         #e5eaf2                   |
|   ink             #101d33     secondary ink   #1d2c47                   |
|                                                                         |
| Geometry came from edge scans of the same image, not estimates:         |
| card x 205-1111 (906 wide) with top at y=134; padding 45-46; goal       |
| textarea x 303-1065 (762 wide) 170 tall; name field 63 tall; primary    |
| button 271x68; header rule at y=97; wordmark cap box 86x35 -> 36px      |
| bold. Every one of those is reproduced in the Task 1 frames.            |
|                                                                         |
| --- SLICE AND STATE DECISIONS CAPTURED -----------------------------    |
|                                                                         |
| OWNED: header band and wordmark, the card, its title, the goal          |
| textarea with the decorative sparkle, Project name, Location, the       |
| divider, the primary Create project action, the secondary open-folder   |
| link, and an in-card message region.                                    |
|                                                                         |
| DEFERRED, named in the spec so it cannot read as missing work: the      |
| navy rail (Task 3); Recent local projects (no recents index by          |
| design); Add context (optional) and its two buttons (needs an           |
| attachedContext contract - Task 4); journey screen 01 Choose a project  |
| (depends on the deferred recents index); everything after a Project     |
| opens (Tasks 2/3).                                                      |
|                                                                         |
| STATES: empty, drafted, busy, failed, goal-required - one file each.    |
|                                                                         |
| --- THREE THINGS THAT NEED YOUR EYES -------------------------------    |
|                                                                         |
| 1. THE CONTINUE BUTTON IS GONE. This is the one interaction change,     |
|    and it is not cosmetic. The reference has exactly one primary        |
|    action, so the draft call now fires when the goal is committed       |
|    (blur or Enter) and fills Project name and Location in place;        |
|    Create project is the only button. The rejected build's two-step     |
|    Continue-then-Create shape is what produced the two-card layout in   |
|    the first place. Both fields remain editable overrides sent back     |
|    verbatim, so no contract or rule changes - but it does change one    |
|    bunit test's trigger from a Continue click to a goal-commit event,   |
|    which I have listed for the implementation step rather than doing    |
|    now.                                                                 |
|                                                                         |
| 2. THE CARD TOP STAYS AT y=134, NOT VERTICALLY CENTRED. My earlier      |
|    plan said centred; measuring the reference changed my mind. The      |
|    card sits 37px below the header there, and preserving that keeps     |
|    the header-to-card relationship identical. The card is 718 tall      |
|    instead of 802 because two blocks are deferred, so the freed space   |
|    accrues below it. Correcting this before you approve rather than     |
|    after you compare screenshots.                                       |
|                                                                         |
| 3. THE ERROR AND NOTICE COLOURS ARE INVENTED, and the spec says so      |
|    in its own paragraph. The reference has no failure state, so         |
|    #b42318 on #fef3f2 with #fda29b, and #eff5fe on #c9dcf8, were        |
|    chosen to sit in the same saturation register as the sampled         |
|    blues. This is the only place the spec does not match something      |
|    measured. If you would rather specify them, say the values.          |
|                                                                         |
| --- OBSTACLES ------------------------------------------------------    |
|                                                                         |
| None blocking. Two worth recording:                                     |
|                                                                         |
|   The browser pane renders a 1536x1024 frame scaled into a small        |
|   panel, which is not good enough to judge fidelity. I used the         |
|   headless-Chrome screenshot route the interaction-principles doc       |
|   already documents for exactly this, at 1536x1024, and inspected the   |
|   five renders full size.                                               |
|   CSS isolation is still unverified in this WASM host. Per your         |
|   instruction there is no fallback in the spec: if it fails at          |
|   implementation I stop and report rather than substitute.              |
|                                                                         |
| --- WHAT I HAVE NOT DONE -------------------------------------------    |
|                                                                         |
|   No ProjectLauncher.razor, no ProjectLauncher.razor.css, no            |
|   index.html link, no Home.razor edit, no test change.                  |
|   No build, test run, package, commit, push, or PR update.              |
|   No operator contact.                                                  |
|                                                                         |
| Task 1 is not complete and its implementation is still rejected.        |
| Awaiting your sign-off on these artifacts before implementation.        |
+-------------------------------------------------------------------------+
````

## 3. Fault found

This was the first appropriately bounded artifact turn, but it exposed a prior omission: the

## Requested

Create state SVGs and a Task 1-specific visual specification for approval before implementation.

## Better prompt

“For each SVG value, provide its named design token, theme selector, and light/dark resolved value.

## Prevention

The visual-artifact template should require a token mapping beside each reference: resolved colour
