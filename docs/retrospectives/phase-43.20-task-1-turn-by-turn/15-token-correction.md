# Turn 15 — Token correction

## 1. Codex prompt

````text
+-------------------------------------------------------------------------+
| TOKEN-TABLE CORRECTION — ACCESSIBILITY AND PRIMARY-ACTION FIDELITY      |
+-------------------------------------------------------------------------+
| The named Workbench theme architecture, root selection, cascade model,  |
| complete-map approach, and ±2px geometry tolerance are accepted.        |
|                                                                         |
| Implementation is still not approved. Amend the Task 1 specification    |
| and its affected SVG references only; do not touch source code.         |
|                                                                         |
| 1. Primary action                                                       |
| The Workbench primary action is blue in the approved reference. Set     |
| --ink, --ink-hover, and --ink-contrast to the same blue action family  |
| as --accent, --accent-hover, and --accent-contrast in both light and    |
| dark maps. Do not retain a separate navy --ink path.                    |
|                                                                         |
| 2. Placeholder accessibility                                             |
| A known 2.89:1 contrast failure is not an acceptable exception.         |
| Accessibility wins over the earlier sampled placeholder value. Select   |
| an exact Workbench light --text-subtle value that reaches at least      |
| 4.5:1 against --surface, update every affected SVG reference to that    |
| value, and report the measured ratio. The revised SVG is then the       |
| binding reference; implementation must match it exactly.                |
|                                                                         |
| Add measured contrast rows for success/success-bg, warning/warning-bg, |
| and seal-check on each seal background as well. Every text-bearing      |
| semantic pairing must meet its applicable AA threshold.                 |
|                                                                         |
| Reply with the amended token rows, contrast table, and list of SVGs     |
| changed. Do not implement, test, build, package, commit, or update PR   |
| #78 yet.                                                                |
+-------------------------------------------------------------------------+
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| TOKEN CORRECTION APPLIED (Claude -> Codex)                              |
| Phase 43.20 Task 1. Spec + two SVG references amended. No source code,  |
| test, build, package, commit, or PR update.                             |
|                                                                         |
| === 1. PRIMARY ACTION - INK NOW THE BLUE ACTION FAMILY ==============   |
|                                                                         |
| The separate navy --ink path is gone. Both maps:                        |
|                                                                         |
|   LIGHT                             DARK                                |
|   --ink            #0f6eeb          --ink            #4d9bff            |
|   --ink-hover      #0f56d2          --ink-hover      #74b2ff            |
|   --ink-contrast   #ffffff          --ink-contrast   #06121f            |
|                                                                         |
| Identical to --accent / --accent-hover / --accent-contrast in the same  |
| map, so a solid action is the same blue whichever family a component    |
| reaches for. Provenance changes from D to S: these are now the          |
| sampled button and focused-border blues, not derived navy.              |
|                                                                         |
| === 2. PLACEHOLDER ACCESSIBILITY ===================================    |
|                                                                         |
|   LIGHT --text-subtle   #8b99ad  ->  #62748c                            |
|         measured 4.78 on --surface, 4.57 on --bg  (was 2.89)            |
|   DARK  --text-subtle   #8aa3c2  unchanged, 6.01 on --surface           |
|                                                                         |
| Accessibility beat sample fidelity, as directed. I picked #62748c       |
| rather than the thinnest passing value: #667891 clears --surface at     |
| 4.51 but only reaches 4.31 on --bg, and I did not want a token that     |
| passes in one context and fails in another.                             |
|                                                                         |
| Recorded consequence, not hidden: --text-subtle now sits at 4.78        |
| against --text-muted's 5.42, so the visual step between hint text and   |
| secondary labels is smaller than the reference's. That is the cost of   |
| the fix, and it is written into the spec.                               |
|                                                                         |
| SVGs CHANGED - both regenerated and re-rendered at 1536x1024:           |
|   docs/images/phase-43.20/task1-project-launcher-empty.svg              |
|       three placeholder strings: goal, project name, location           |
|   docs/images/phase-43.20/task1-project-launcher-goal-required.svg      |
|       one placeholder string: goal                                      |
| UNCHANGED, they render no placeholder text: drafted, busy, failed,      |
| and before.                                                             |
| The revised SVGs are the binding reference; the implementation must     |
| match them exactly.                                                     |
|                                                                         |
| === 3. FULL CONTRAST TABLE =========================================    |
|                                                                         |
| Computed from the values, not asserted. Every text-bearing semantic     |
| pairing now meets its AA threshold in both maps:                        |
|                                                                         |
|   pair                               light    dark                      |
|   --text on --surface                16.86   13.23                      |
|   --text on --bg                     16.12   15.67                      |
|   --text-muted on --surface           5.42    7.20                      |
|   --text-subtle on --surface          4.78    6.01                      |
|   --text-subtle on --bg               4.57    5.19                      |
|   --accent-contrast on --accent       4.72    6.69                      |
|   --accent on --surface               4.72    5.53                      |
|   --ink-contrast on --ink             4.72    6.69                      |
|   --ink-contrast on --ink-hover       6.40    8.58                      |
|   --danger on --danger-bg             6.05    8.11                      |
|   --success on --success-bg           4.69    9.53                      |
|   --warning on --warning-bg           5.51    7.87                      |
|   --seal-check on --seal-official     5.92   10.71                      |
|   --seal-check on --seal-verified     4.72    6.55                      |
|                                                                         |
| No exception rows remain. The lowest light pairing is success at        |
| 4.69, the lowest dark is subtle-on-bg at 5.19.                          |
|                                                                         |
| === 4. PROVENANCE LEGEND GAINS ONE LETTER ==========================    |
|                                                                         |
|   S sampled   D derived   N no reference evidence                       |
|   A accessibility override of a sampled value, carried back into the    |
|     SVG references so they stay binding rather than being contradicted  |
|     by the build                                                        |
|                                                                         |
| --text-subtle (light) is the only A row.                                |
|                                                                         |
| === FILES CHANGED ==================================================    |
|                                                                         |
|   docs/phases/phase-43.20-project-workbench-mvp.md                      |
|       ink rows in both maps, the light --text-subtle row, the           |
|       provenance legend, the full contrast table above, and a new       |
|       "Accessibility override" paragraph replacing the old exception    |
|   docs/images/phase-43.20/task1-project-launcher-empty.svg              |
|   docs/images/phase-43.20/task1-project-launcher-goal-required.svg      |
|                                                                         |
|   NOT TOUCHED: any source file, any test, forge.css, PR #78.            |
|                                                                         |
| Awaiting implementation approval.                                       |
+-------------------------------------------------------------------------+
````

## 3. Fault found

The initial token proposal checked a value in one context and missed its use against another

## Requested

Correct a Workbench token contrast/context defect before implementation.

## Better prompt

“Audit every foreground/background pairing in every state and mode before approval. Report measured

## Prevention

Token review should enumerate every foreground/background pair used by each state and verify the
