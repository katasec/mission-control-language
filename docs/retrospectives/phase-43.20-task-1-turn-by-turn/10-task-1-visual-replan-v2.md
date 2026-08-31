# Turn 10 — Task 1 visual re-plan v2

## 1. Codex prompt

````text
+-------------------------------------------------------------------------+
| FOLLOW-UP: VISUAL ACCEPTANCE ORDER                                      |
+-------------------------------------------------------------------------+
| Do not ask the operator for visual review while planning or implementing|
| this work.                                                              |
|                                                                         |
| First, after Codex approves the plan and you implement it, provide      |
| evidence that the running UI passes your own comparison against the     |
| approved task-specific visual reference at its stated viewport. Codex   |
| will independently review that evidence.                                |
|                                                                         |
| Only after Claude and Codex have both recorded PASS may Codex ask the   |
| operator for final independent visual acceptance. A test pass or        |
| functional behavior does not replace this sequence.                     |
+-------------------------------------------------------------------------+
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| TASK 1 VISUAL RE-PLAN v2 - Phase 43.20 (Claude -> Codex)                |
| Complete plan, one message. Nothing created: no SVG, spec, code, test,  |
| commit, or PR update. Sparse-canvas question resolved below.            |
|                                                                         |
| === 1. GATES ========================================================   |
|                                                                         |
| PRESENTATION-SURFACE PARITY GATE: PASS                                  |
|   Presentation only. No contract, rule, filesystem behaviour, or        |
|   authorization moves. ProjectDraftRequest / ProjectCreateRequest /     |
|   ProjectOpenRequest and their typed outcomes are untouched, so a TUI   |
|   keeps identical semantics and only renders them differently. The one  |
|   element that would break this - "Add context (optional)" - needs an   |
|   attachedContext contract and is therefore deferred, not drawn.        |
|                                                                         |
| VISUAL-REFERENCE ACCEPTANCE GATE: FAIL today, by design                 |
|   No task-scoped reference exists under docs/images/phase-43.20/, and   |
|   the journey mock is broader than this task. Step 1 of the sequence    |
|   produces that reference and stops for your sign-off. The existing     |
|   implementation is not approved and not mergeable.                     |
|                                                                         |
| === 2. THE SLICE, AND THE SPARSE-CANVAS RESOLUTION ==================   |
|                                                                         |
| Binding journey source: docs/brainstorm/images/                         |
| mission-project-flow-02-create-project.png at 1536x1024.                |
| Measured from that image, the centre card occupies x 207-1110,          |
| y 137-935: 903 x 798, below a header band ending at y=100.              |
|                                                                         |
| RESOLVED: keep the reference card's own geometry and centre it.         |
|                                                                         |
|   Viewport 1536x1024, same as the reference, so the two can be laid     |
|   side by side without rescaling.                                       |
|   Header band full width, 100px tall, wordmark left at x=48.            |
|   Card width 903, horizontally centred -> x 316-1219 (the reference     |
|   puts it at 207 because a 157px rail sits to its left; with no rail,   |
|   left-aligning would leave a large right void, and centring is the     |
|   only composition that stays balanced).                                |
|   Card height drops to ~600 because "Add context (optional)" and its    |
|   two buttons are deferred - roughly 200px of the reference card. The   |
|   divider and action row keep their exact relationship to the field     |
|   above them.                                                           |
|   Card vertically centred in the canvas below the header.               |
|                                                                         |
|   My earlier "sparse canvas" worry was wrong, and I checked rather      |
|   than carried it forward: at 1536 wide a 903px card leaves ~316px per  |
|   side, which is an ordinary centred form, not a void. The removed      |
|   columns cost horizontal margin, not composition.                      |
|                                                                         |
|   ACCEPTANCE RULE, to be written into the spec so the gate is           |
|   decidable: the comparison is card-internal fidelity - field order,    |
|   labels, copy strings, type scale, control sizes, spacing rhythm,      |
|   divider and action-row placement - plus the header band. Page-level   |
|   side margins differ from the journey mock because two columns are     |
|   deferred; that difference alone is not a FAIL. Any difference inside  |
|   the card is.                                                          |
|                                                                         |
| === 3. TASK 1 OWNS - ELEMENTS ======================================    |
|                                                                         |
|   Page shell on the light canvas                                        |
|   Header band: "Forge" wordmark, vertical divider, "AI Workbench"       |
|   Card, radius 14, surface + hairline border + small shadow             |
|   Card title "New project"                                              |
|   Goal field: multi-line textarea, ~3 rows, 760x168, with the leading   |
|       sparkle glyph at its left (today a single-line input - a real     |
|       structural mismatch, not a cosmetic one)                          |
|   "Project name" label + text field, 760x56                             |
|   "Location" row - demoted, see decision 2                              |
|   Horizontal rule above the action row                                  |
|   Primary "Create project" button, ~270x59, bottom-right of the card    |
|   Secondary "Open an existing folder..." text link below the card       |
|   Inline error region inside the card                                   |
|                                                                         |
| === 4. TASK 1 OWNS - STATES (all five drawn, none assumed) =========    |
|                                                                         |
|   S1 Empty        goal blank; Create disabled; no name/location shown   |
|   S2 Drafted      Client Runtime returned title + home; both fields     |
|                   populated and editable; Create enabled                |
|   S3 Busy         in-flight draft or create; controls disabled; the     |
|                   primary button reads "Working..."                     |
|   S4 Failed       typed ProjectOperationError message rendered in the   |
|                   card's error region; nothing created; fields keep     |
|                   what the user typed                                   |
|   S5 GoalRequired chosen folder has no manifest; the card returns with  |
|                   the runtime's proposed name and that folder as the    |
|                   location, plus one line of explanation; no create     |
|                   until the goal is confirmed                           |
|   S4 and S5 appear in no journey mock. They are Task 1's own states     |
|   and the spec designs them rather than leaving them to implementation. |
|                                                                         |
| === 5. EXPLICITLY DEFERRED - named in the spec, not drawn ==========    |
|                                                                         |
|   Dark navy activity rail, Explorer / Mission Control / Settings        |
|       -> Task 3                                                         |
|   "Recent local projects" column and its entries                        |
|       -> no recents index exists by design; needs its own bounded       |
|          design before any surface shows one                            |
|   "Add context (optional)", API specification / Local directory         |
|       -> Task 4 territory; attachedContext stays empty in Task 1 and    |
|          these need a contract, so drawing them would break parity      |
|   Journey screen 01 "Choose a project" and its project cards            |
|       -> depends on the deferred recents/known-project index            |
|   Post-create workbench: Mission Control, composer, mission picker,     |
|   explorer panes                                                        |
|       -> Tasks 2/3. Task 1 keeps today's minimal session view; the      |
|          slice covers only that it shows the project title and home.    |
|                                                                         |
| === 6. RESOLVED DESIGN DECISIONS ===================================    |
|                                                                         |
|   1. No activity rail in Task 1. An inert rail fails Principle 2 and    |
|      the Norman signifier check, and Task 3 would replace it. Shipping  |
|      the real rail now is Task 3 scope creep.                           |
|   2. Location stays, demoted. The mock shows goal + name only, but the  |
|      spoke's locked model requires the location "visible and editable". |
|      Where the mock is silent about a manifest-task requirement, the    |
|      spoke governs. It renders below Project name in the smaller,       |
|      muted label/field treatment so the two reference fields keep       |
|      their prominence.                                                  |
|   3. "Open an existing folder..." is a quiet text link directly below   |
|      the card, matching the reference's link treatment for the same     |
|      action. Not a second card - a co-equal second card is exactly      |
|      what was rejected.                                                 |
|   4. Palette: Task 1 keeps the existing forge.css ember tokens, and     |
|      the Task 1 SVG is authored in those tokens, so the binding         |
|      reference and the build agree. The journey mock's navy/blue/cyan   |
|      is the locked MVP visual language and a real divergence; the spec  |
|      records it as a known deferred difference. Re-tokenising           |
|      forge.css affects ForgeUI too and should be its own approved       |
|      change, not a passenger inside a manifest task. If you want blue   |
|      first, say so at step 1 and I will scope it separately, before     |
|      this task rather than inside it.                                   |
|   5. Header adopts "Forge | AI Workbench". "Local Client Runtime"       |
|      goes.                                                              |
|   6. Viewport 1536x1024 for both the SVG and the live comparison.       |
|   7. Sparkle glyph included, decorative inline SVG, non-interactive     |
|      and styled so it cannot read as a button.                          |
|                                                                         |
| === 7. FILES ========================================================   |
|                                                                         |
|   STEP 1 - design, no code                                              |
|     docs/images/phase-43.20/task1-project-launcher-before.svg           |
|         the rejected two-card launcher, so the defect stays on record   |
|     docs/images/phase-43.20/task1-project-launcher-after.svg            |
|         five frames, one per state S1-S5, at 1536x1024 each             |
|     docs/phases/phase-43.20-project-workbench-mvp.md                    |
|         component/state spec under Task 1: control hierarchy, exact     |
|         copy strings, geometry, the primitive each control uses, the    |
|         named reference and viewport, the owned/deferred inventory,     |
|         the acceptance rule from section 2, and the Cooper / Rams /     |
|         Norman gate answers                                             |
|                                                                         |
|   STEP 2 - implementation, only after your design sign-off              |
|     src/ForgeMission.ClientRuntime.Presentation/Components/             |
|         ProjectLauncher.razor  (new)                                    |
|         Presentation only: takes draft/busy/error state in, raises      |
|         create and open events out, owns no rule. Home.razor currently  |
|         carries two unrelated surfaces in one file; this is the seam.   |
|     src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor        |
|         renders the component and keeps the existing @code; no          |
|         contract change                                                 |
|     src/ForgeMission.Tests/Presentation/HomeSessionOperationTests.cs    |
|         selector updates only. The assertions - zero-call boot, one     |
|         draft request, verbatim overrides, GoalRequired renders the     |
|         proposal, Failed renders the message, replacement-only mission  |
|         switch - do not change.                                         |
|                                                                         |
|   NOT TOUCHED: ProjectStore, transport contracts, endpoints, session    |
|   store, ProjectStoreTests, ProjectTransportContractTests. If any of    |
|   them needs to change, the slice has left presentation and I stop and  |
|   say so rather than widen the task.                                    |
|                                                                         |
| === 8. VERIFICATION EVIDENCE I WILL PRODUCE ========================    |
|                                                                         |
|   Per state S1-S5: a screenshot of the running packaged Client Runtime  |
|   at 1536x1024 against an empty temp profile, beside the corresponding  |
|   SVG frame, with my point-by-point comparison against the acceptance   |
|   rule and an explicit PASS or FAIL for that state.                     |
|   Plus, reported separately and never offered as visual evidence:       |
|   dotnet build (0 warnings), dotnet test (full suite), and              |
|   make desktop-publish with its ILC warning count.                      |
|                                                                         |
| === 9. ACCEPTANCE SEQUENCE (approved, unchanged) ===================    |
|                                                                         |
|   1. Author the before/after SVGs and the spec. STOP for your design    |
|      approval.                                                          |
|   2. Implement against the approved spec.                               |
|   3. Update bunit selectors; full suite green.                          |
|   4. Claude's own visual comparison at 1536x1024, all five states.      |
|   5. Claude records PASS or FAIL per state with the screenshots as      |
|      evidence. A FAIL returns to step 1 or 2; I do not forward a FAIL,  |
|      and I do not argue a mismatch down to "close enough".              |
|   6. Codex independently reviews that evidence and records PASS/FAIL.   |
|   7. Only after both Claude and Codex record PASS does the operator's   |
|      final independent visual acceptance happen, and it is yours to     |
|      request, not mine.                                                 |
|   8. dotnet build + dotnet test + make desktop-publish, then commit     |
|      onto codex/phase-43.20-project-manifest and update PR #78.         |
|                                                                         |
| === 10. NOT DOING ==================================================    |
|                                                                         |
|   No recents index, and no directory scan standing in for one.          |
|   No rail, explorer, or Mission Control surface pulled forward.         |
|   No attachedContext buttons.                                           |
|   No forge.css token change unless you approve decision 4 separately.   |
|   No behaviour, contract, or filesystem change of any kind.             |
|                                                                         |
| No open questions remain. Awaiting your explicit approval of this       |
| complete plan before I create the SVGs or the spec.                     |
+-------------------------------------------------------------------------+
````

## 3. Fault found

This was corrective rather than a new defect, but it shows that scope decisions such as the sparse

## Requested

Correct the visual acceptance order and define the launcher slice.

## Better prompt

“Return a decision-complete slice: every mock element marked owned/deferred/blocked, no open

## Prevention

The visual spec must include an owned/deferred inventory and answer all composition questions
