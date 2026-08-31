# Turn 11 — Task 1 visual re-plan v3

## 1. Codex prompt

````text
+-------------------------------------------------------------------------+
| PLAN CORRECTION — NOT YET APPROVED                                      |
+-------------------------------------------------------------------------+
| The corrected visual-acceptance sequence is approved. Keep it exactly   |
| as written, including: Claude PASS first, Codex PASS second, operator   |
| review only after both pass.                                            |
|                                                                         |
| The implementation plan itself remains unapproved. I have not received  |
| its full contents, and it still names an unresolved “sparse canvas at   |
| 1536×1024” question. A visual design question cannot be left for        |
| implementation.                                                        |
|                                                                         |
| Reply with the complete revised plan in one message, including:         |
| - both gate results;                                                    |
| - the exact Task 1-owned elements and states;                           |
| - every element explicitly deferred to Task 3 or later;                 |
| - your resolved recommendation for the sparse canvas, with rationale;  |
| - the exact SVG/spec files, component files, tests, and verification;  |
| - the corrected eight-step acceptance sequence.                         |
|                                                                         |
| Do not create SVGs, specifications, code, tests, commits, or PR updates |
| yet. Wait for my explicit approval of that complete plan.               |
+-------------------------------------------------------------------------+
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| TASK 1 RE-PLAN v3 - amended Sections 6-9 only (Claude -> Codex)         |
| Nothing created or modified. Sections 1-5 stand as sent.                |
|                                                                         |
| === 6. RESOLVED DESIGN DECISIONS (4 rewritten) =====================    |
|                                                                         |
|   1. No activity rail in Task 1. An inert rail fails Principle 2 and    |
|      the Norman signifier check, and Task 3 would replace it. Shipping  |
|      the real rail now is Task 3 scope creep.                           |
|                                                                         |
|   2. Location stays, demoted. The mock shows goal + name only, but the  |
|      spoke's locked model requires the location "visible and editable". |
|      Where the mock is silent about a manifest-task requirement, the    |
|      spoke governs. It renders below Project name in the smaller,       |
|      muted label/field treatment so the two reference fields keep       |
|      their prominence.                                                  |
|                                                                         |
|   3. "Open an existing folder..." is a quiet text link directly below   |
|      the card, in the reference's link treatment for the same action.   |
|      Not a second card - a co-equal second card is what was rejected.   |
|                                                                         |
|   4. REPLACED - the launcher adopts the reference's visual language.    |
|      You are right: while that mock is the binding source, its visible  |
|      language is part of what must match, and the ember tokens are an   |
|      implementation constraint rather than an acceptance override. I    |
|      was using "deferred difference" to protect a token set the gate    |
|      never exempted.                                                    |
|                                                                         |
|      What the launcher adopts, sampled from the reference PNG when the  |
|      SVG is authored rather than guessed now: canvas tint, header       |
|      wordmark and its divider, card surface/border/radius/elevation,    |
|      the ink and muted text ramps, field border and focus treatment,    |
|      the blue primary action, and the blue link treatment. Lime is      |
|      reserved for healthy/approved states in the locked visual          |
|      language and the launcher has none, so it is not used here.        |
|                                                                         |
|      Scoping, so ForgeUI is not silently retokened: the values live as  |
|      launcher-local custom properties declared on the component root,   |
|      never on :root, in a stylesheet that only this component's markup  |
|      can reach. Nothing in forge.css is edited, and no other surface    |
|      changes appearance. The rest of the app keeps ember until a        |
|      separate, approved design-system change says otherwise.            |
|                                                                         |
|      Mechanism: Blazor CSS isolation - ProjectLauncher.razor.css -      |
|      because the framework scopes it by generated attribute, so it      |
|      cannot leak even by class-name accident. That bundle is not        |
|      linked in index.html today, so the link is named in Section 7 as   |
|      a real file change. I have not yet confirmed this WASM host        |
|      serves the isolated bundle; I will confirm it at the first build   |
|      of step 2 and report the result. If it does not, the fallback is   |
|      wwwroot/css/project-launcher.css with every rule and custom        |
|      property nested under a single .project-launcher root selector -   |
|      same scoping guarantee, manual instead of compiler-enforced. I     |
|      will say which one shipped; I will not leave it ambiguous.         |
|                                                                         |
|      Dark mode: forge.css defines a dark scheme via                     |
|      prefers-color-scheme. The reference is a light composition only,   |
|      so the launcher's scoped values are light-fixed for Task 1, and    |
|      the spec records that as an explicit, named limitation rather      |
|      than an accident.                                                  |
|                                                                         |
|   5. Header adopts "Forge | AI Workbench". "Local Client Runtime" goes. |
|                                                                         |
|   6. Viewport 1536x1024 for both the SVGs and the live comparison.      |
|                                                                         |
|   7. Sparkle glyph included, decorative inline SVG, non-interactive     |
|      and styled so it cannot read as a button.                          |
|                                                                         |
| === 7. FILES =======================================================    |
|                                                                         |
|   STEP 1 - design, no code                                              |
|     docs/images/phase-43.20/task1-project-launcher-before.svg           |
|         the rejected two-card launcher, so the defect stays on record   |
|     docs/images/phase-43.20/task1-project-launcher-empty.svg            |
|     docs/images/phase-43.20/task1-project-launcher-drafted.svg          |
|     docs/images/phase-43.20/task1-project-launcher-busy.svg             |
|     docs/images/phase-43.20/task1-project-launcher-failed.svg           |
|     docs/images/phase-43.20/task1-project-launcher-goal-required.svg    |
|         one file per acceptance state, each a full 1536x1024 frame, so  |
|         every state is directly inspectable and diffable on its own     |
|     docs/phases/phase-43.20-project-workbench-mvp.md                    |
|         component/state spec under Task 1: control hierarchy, exact     |
|         copy strings, geometry, the primitive each control uses, the    |
|         sampled colour/type values and their scoping rule, the named    |
|         reference and viewport, the owned/deferred inventory, the       |
|         acceptance rule from Section 2, the light-only limitation, and  |
|         the Cooper / Rams / Norman answers                              |
|                                                                         |
|   STEP 2 - implementation, only after your design sign-off              |
|     src/ForgeMission.ClientRuntime.Presentation/Components/             |
|         ProjectLauncher.razor            (new)                          |
|         presentation only: takes draft/busy/error state in, raises      |
|         create and open events out, owns no rule                        |
|     src/ForgeMission.ClientRuntime.Presentation/Components/             |
|         ProjectLauncher.razor.css        (new - the scoped styles)      |
|         all launcher-local custom properties and rules; nothing on      |
|         :root; fallback wwwroot/css/project-launcher.css only if the    |
|         host does not serve the isolated bundle, as stated in 6.4       |
|     src/ForgeMission.ClientRuntime.Presentation/wwwroot/index.html      |
|         add the scoped-bundle link                                      |
|         (ForgeMission.ClientRuntime.Presentation.styles.css)            |
|     src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor        |
|         renders the component, keeps its existing @code, and drops the  |
|         launcher markup and styles it holds today; header wording       |
|     src/ForgeMission.Tests/Presentation/HomeSessionOperationTests.cs    |
|         selector updates only. The assertions - zero-call boot, one     |
|         draft request, verbatim overrides, GoalRequired renders the     |
|         proposal, Failed renders the message, replacement-only mission  |
|         switch - do not change.                                         |
|                                                                         |
|   NOT TOUCHED: src/ForgeUI/wwwroot/css/forge.css and every other        |
|   surface that consumes it; ProjectStore; the transport contracts;      |
|   the endpoints; the session store; ProjectStoreTests;                  |
|   ProjectTransportContractTests. If any of them needs to change, the    |
|   slice has left presentation and I stop and say so rather than widen   |
|   the task.                                                             |
|                                                                         |
| === 8. VERIFICATION EVIDENCE I WILL PRODUCE ========================    |
|                                                                         |
|   Gating build evidence, produced BEFORE any screenshot:                |
|     dotnet build src/ForgeMission.slnx - 0 warnings, 0 errors           |
|     dotnet test src/ForgeMission.slnx  - full suite, counts reported    |
|     make desktop-publish               - AOT publish, ILC warning       |
|                                          count reported                 |
|   If any of the three fails, there is no visual comparison to make and  |
|   I go back to step 2.                                                  |
|                                                                         |
|   Visual evidence, from the PACKAGED surface only:                      |
|     the published dist/forge-desktop/ForgeMission.ClientRuntime run     |
|     against an empty temp profile, browser viewport set to 1536x1024    |
|     five screenshots, one per state - empty, drafted, busy, failed,     |
|     goal-required - each named for its state                            |
|     each placed beside its own SVG frame, with a point-by-point         |
|     comparison against the Section 2 acceptance rule and an explicit    |
|     PASS or FAIL for that state                                         |
|   No development-mode rendering is used as visual evidence, and the     |
|   build/test results are reported separately and never offered as       |
|   visual evidence.                                                      |
|                                                                         |
| === 9. ACCEPTANCE SEQUENCE (reordered) =============================    |
|                                                                         |
|   1. Author the before SVG, the five state SVGs, and the spec.          |
|      STOP for your design approval.                                     |
|   2. Implement against the approved spec; confirm and report which      |
|      scoping mechanism shipped.                                         |
|   3. Update bunit selectors.                                            |
|   4. dotnet build, then the full dotnet test suite, then                |
|      make desktop-publish. All three green before anything visual.      |
|   5. Claude's visual comparison of the packaged, running surface at     |
|      1536x1024, all five states.                                        |
|   6. Claude records PASS or FAIL per state with the packaged            |
|      screenshots as evidence. A FAIL returns to step 1 or 2; I do not   |
|      forward a FAIL, and I do not argue a mismatch down to "close       |
|      enough". On PASS I commit and push onto                            |
|      codex/phase-43.20-project-manifest and update PR #78, so your      |
|      review runs against the real branch as well as the screenshots.    |
|      Say the word if you would rather no commit exist before your own   |
|      PASS.                                                              |
|   7. Codex independently reviews that evidence and records PASS/FAIL.   |
|   8. Only after both Claude and Codex record PASS does the operator's   |
|      final independent visual acceptance happen, and it is yours to     |
|      request, not mine. Merge stays blocked until then.                 |
|                                                                         |
| Awaiting your approval of the amended plan before I create anything.    |
+-------------------------------------------------------------------------+
````

## 3. Fault found

The needed correction arrived only after two earlier re-plans. The design had not initially

## Requested

Amend the visual plan to avoid inert deferred UI and match the approved visual language.

## Better prompt

“For each reference element, state its owning task. Omit anything deferred unless it has a real

## Prevention

Require every visual specification to classify each reference element as owned, deferred, or a
