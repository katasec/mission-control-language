# Turn 09 — Task 1 visual re-plan

## 1. Codex prompt

````text
+-------------------------------------------------------------------------+
| TASK ASSIGNMENT                                                         |
+-------------------------------------------------------------------------+
| Role: implementer. Do not write or modify code, markup, documentation, |
| or mockups until I approve your plan.                                  |
|                                                                         |
| Read first (do not summarize these back):                              |
| - AGENTS.md                                                             |
| - docs/plan.md                                                          |
| - docs/phases/phase-43-forge-desktop.md                                |
| - docs/phases/phase-43.20-project-workbench-mvp.md                     |
| - docs/design/engineering-philosophy.md                                |
| - docs/design/desktop-interaction-principles.md                        |
| - docs/brainstorm/images/mission-project-flow-02-create-project.png   |
| - docs/brainstorm/mission-conversations/README.md                      |
|                                                                         |
| Task:                                                                   |
| Re-plan Task 1 after visual acceptance failed. The current two-card   |
| launcher is technically functional but does not match the named        |
| Project-home reference; both Codex and the operator rejected it.       |
|                                                                         |
| Produce a plan for bringing Task 1 to visual acceptance. First define  |
| the precise Task 1 visual slice from the broader journey mockup,       |
| without silently pulling Task 3's rail/explorer or an unplanned        |
| recents index into this task. Identify every visible element that is:  |
| (a) owned by Task 1 now, (b) explicitly deferred, or (c) needs a      |
| Codex design decision.                                                  |
|                                                                         |
| Constraints:                                                            |
| - A binding, task-specific SVG reference under docs/images/phase-43.20 |
|   and an adjacent component/state specification are required before    |
|   implementation.                                                      |
| - The implementation must be visually compared with that reference at  |
|   its stated viewport in the running app; automated behavior tests do  |
|   not substitute for this gate.                                        |
| - Preserve Desktop/TUI parity: all product behavior must remain behind |
|   shared Client Runtime contracts; only presentation concerns may live |
|   in the Desktop.                                                       |
| - Do not treat the existing implementation as approved or mergeable.  |
|                                                                         |
| Done when:                                                              |
| A complete implementation plan identifies the scoped visual reference, |
| exact component/state work, files to touch or create, verification     |
| evidence (including the visual comparison), and any genuinely open     |
| design question.                                                       |
|                                                                         |
| Next step:                                                              |
| Reply with a plan only: begin with PASS/FAIL for the presentation-     |
| surface parity and visual-reference gates, then list files, approach,  |
| sequencing, and open questions. Wait for my explicit approval before  |
| changing anything.                                                      |
+-------------------------------------------------------------------------+
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| TASK 1 VISUAL RE-PLAN - Phase 43.20 (Claude -> Codex)                   |
| Nothing changed on disk. No code, markup, docs, or mockups written.     |
|                                                                         |
| --- GATES ------------------------------------------------------------  |
|                                                                         |
| PRESENTATION-SURFACE PARITY GATE: PASS                                  |
|   This rework changes presentation only: layout, hierarchy, copy, and   |
|   the five launcher states. No contract, rule, or filesystem behaviour  |
|   moves. Draft/create/open stay exactly as they are, so a TUI keeps the |
|   same authorization, outcomes, and failure semantics and simply        |
|   renders them differently. The one thing that would break this is      |
|   "Add context (optional)" - it needs an attachedContext contract - so  |
|   it stays deferred (see the inventory).                                |
|                                                                         |
| VISUAL-REFERENCE ACCEPTANCE GATE: FAIL, today                           |
|   No task-scoped reference exists under docs/images/phase-43.20/, and   |
|   the journey mock is broader than this task. That is precisely what    |
|   step 1 below produces. I am not treating the existing implementation  |
|   as approved or mergeable, and I will not write markup until you sign  |
|   off on the scoped SVG + spec.                                         |
|                                                                         |
| --- THE TASK 1 VISUAL SLICE ------------------------------------------  |
|                                                                         |
| Reference: docs/brainstorm/images/mission-project-flow-02-create-       |
| project.png (1536x1024). Task 1 owns its centre "New project" card      |
| and nothing else on that screen.                                        |
|                                                                         |
| The slice statement I propose to write into the spoke, so "matches the  |
| reference" stays meaningful with two columns removed: the card keeps    |
| the reference's internal proportions, field order, label wording, and   |
| type hierarchy, recomposed as a single centred canvas column at the     |
| same overall width the card occupies in the reference (~660-720 px      |
| content width), on the light canvas. It is not a shrunken variant and   |
| not a second layout.                                                    |
|                                                                         |
| (a) OWNED BY TASK 1 NOW                                                 |
|   Light content canvas and page shell                                   |
|   Header wordmark + subtitle divider ("Forge | AI Workbench" in the     |
|       reference; today it says "Local Client Runtime")                  |
|   Card titled "New project"                                             |
|   Multi-line goal textarea, ~3 rows, with the leading sparkle glyph     |
|       (today: a single-line input - a real mismatch, not cosmetic)      |
|   Labelled "Project name" field                                         |
|   Rule/divider above the action row                                     |
|   Primary "Create project" button, bottom-right of the card             |
|   An entry point to open an existing folder                             |
|   Five states, all of which must be drawn, not just the happy path:     |
|       empty (goal not yet entered) / drafted (name + location shown)    |
|       / busy / failed (typed ProjectOperationError message)             |
|       / goal-required (chosen folder has no manifest)                   |
|   The last two appear in no journey mock at all - they are Task 1's     |
|   own states and the spec has to invent them honestly.                  |
|                                                                         |
| (b) EXPLICITLY DEFERRED - named in the spec as deferred, not drawn      |
|   Dark navy activity rail (Explorer / Mission Control / Settings)       |
|       -> Task 3                                                         |
|   "Recent local projects" column and its five entries                   |
|       -> no recents index exists by design; needs its own bounded       |
|          design before any surface shows one                            |
|   "Add context (optional)" with API specification / Local directory     |
|       -> attachedContext stays empty in Task 1; these need a contract,  |
|          so drawing them would break parity                             |
|   The post-create workbench surface (chat, composer, mission picker)    |
|       -> Tasks 2/3. Task 1 keeps today's minimal session view; the      |
|          slice covers only that it shows the project title and home.    |
|                                                                         |
| (c) NEEDS YOUR DECISION - defaults proposed, override individually      |
|   1. Rail in Task 1?                                                    |
|      Default: NO rail. A rail drawn but inert fails Principle 2 and     |
|      the Norman signifier check, and Task 3 would rewrite it anyway.    |
|      The alternative - ship the real three-entry rail now - is Task 3   |
|      scope creep. I recommend no rail and a centred canvas.             |
|   2. Location field?                                                    |
|      The reference shows goal + project name only. The locked model     |
|      says location is "visible and editable, but no chooser is          |
|      required". Default: keep it, demoted below Project name as a       |
|      smaller muted row, so the reference's two prominent fields stay    |
|      prominent. Alternative: drop the field and show the resolved home  |
|      only after creation. Your call - the mock and the spoke disagree.  |
|   3. Where does "open an existing folder" live?                         |
|      In the reference it is a link at the bottom of the deferred        |
|      recents column. Default: a quiet secondary text link directly      |
|      under the card, styled as a link, not a second equal-weight card   |
|      - a second card is exactly what was rejected.                      |
|   4. Palette.                                                           |
|      forge.css is ember (--accent #c2410c). The mocks and the locked    |
|      MVP visual language are navy rail + Forge blue + cyan + lime.      |
|      Default: Task 1 keeps the existing tokens; a palette shift is a    |
|      design-system decision affecting ForgeUI too and should not ride   |
|      inside a manifest task. If you want blue now, that is its own      |
|      approved change to forge.css and I will scope it separately.       |
|   5. Header wording.                                                    |
|      Default: adopt "Forge | AI Workbench" from the reference and drop  |
|      "Local Client Runtime".                                            |
|   6. Target viewport for the SVG and the live comparison.               |
|      Default: 1536x1024, matching the reference composition so the two  |
|      can be laid side by side. Say if you want a real window size such  |
|      as 1280x800 instead.                                               |
|   7. Sparkle glyph.                                                     |
|      Default: include it as a decorative inline SVG marking the goal    |
|      field as the AI input, exactly as the reference does. It is not    |
|      interactive, so it must not read as a button.                      |
|                                                                         |
| --- FILES TO CREATE OR TOUCH -----------------------------------------  |
|                                                                         |
| STEP 1 - design (no code)                                               |
|   docs/images/phase-43.20/task1-project-launcher-before.svg             |
|       the rejected two-card launcher, so the defect is on record        |
|   docs/images/phase-43.20/task1-project-launcher-after.svg              |
|       the approved slice; separate frames for the five states rather    |
|       than one frame that hides the interesting moments                 |
|   docs/phases/phase-43.20-project-workbench-mvp.md                      |
|       component/state spec under Task 1: control hierarchy, exact copy  |
|       strings, which primitive each control uses, the named reference,  |
|       viewport, owned/deferred inventory, and the gate answers          |
|       (Cooper / Rams / Norman)                                          |
|                                                                         |
| STEP 2 - implementation (only after your sign-off)                      |
|   src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor          |
|       launcher markup and styles; no @code contract change              |
|   src/ForgeMission.ClientRuntime.Presentation/Components/               |
|       ProjectLauncher.razor  (new)                                      |
|       Home.razor now carries two unrelated surfaces in one file. The    |
|       launcher is a self-contained presentation component taking the    |
|       draft/error/busy state in and raising create/open events out; it  |
|       owns no rule. Say the word if you would rather I keep it inline.  |
|   src/ForgeMission.Tests/Presentation/HomeSessionOperationTests.cs      |
|       selector updates only, if the spec renames a class. The           |
|       assertions - zero-call boot, one draft request, verbatim          |
|       overrides, replacement-only mission switch - do not change.       |
|                                                                         |
| NOT TOUCHED: ProjectStore, the transport contracts, the endpoints, the  |
| session store, ProjectStoreTests, ProjectTransportContractTests. If any |
| of them needs to change, the slice has strayed out of presentation and  |
| I will stop and say so rather than widen the task.                      |
|                                                                         |
| --- SEQUENCING -------------------------------------------------------  |
|                                                                         |
|   1. Author the before/after SVGs and the spec. STOP for your approval  |
|      - this is the design gate, separate from the plan approval.        |
|   2. Implement the launcher against the approved spec.                  |
|   3. Update bunit selectors; full suite green.                          |
|   4. Live visual verification: run the packaged Client Runtime against  |
|      an empty temp profile, set the browser to the agreed viewport,     |
|      and screenshot all five states - empty, drafted, busy, failed,     |
|      goal-required - against the SVG. Attach them to the relay.         |
|   5. Record PASS or FAIL with you and the operator. A material          |
|      mismatch is a FAIL and returns to step 1; I will not argue a       |
|      mismatch down to "close enough".                                   |
|   6. dotnet build + dotnet test + make desktop-publish (AOT), then      |
|      commit onto codex/phase-43.20-project-manifest and update PR #78.  |
|                                                                         |
| --- WHAT I AM NOT DOING ----------------------------------------------  |
|                                                                         |
|   No recents index, and no directory scan standing in for one.          |
|   No rail, explorer, or Mission Control surface pulled forward.         |
|   No attachedContext buttons.                                           |
|   No palette change unless you approve item 4 separately.               |
|   No behaviour, contract, or filesystem change of any kind.             |
|                                                                         |
| --- ONE GENUINELY OPEN QUESTION --------------------------------------  |
|                                                                         |
| Beyond the seven decisions above: with the rail and recents removed,    |
| Task 1's screen is a single card on a large empty canvas. That is       |
| honest for this slice, but it will look sparse next to the reference    |
| at 1536x1024, and I would rather agree now what "matches" means than    |
| discover it at the acceptance gate. My reading: the card is judged on   |
| its own composition and the surrounding emptiness is expected until     |
| Task 3 fills it. Confirm that reading, or tell me to target a smaller   |
| viewport where a single centred card is the natural full screen.        |
|                                                                         |
| Awaiting your explicit approval before creating the SVGs or the spec.   |
+-------------------------------------------------------------------------+
````

## 3. Fault found

The first visual re-plan still put the operator too early in the acceptance sequence. It had not

## Requested

Recover from the visible mismatch between the running launcher and the journey mock.

## Better prompt

“Create an owned/deferred visual slice and define acceptance order exactly: Claude PASS, Codex

## Prevention

The workflow must state one fixed order: approved references, implementation, Claude comparison,
