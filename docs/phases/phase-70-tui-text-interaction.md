# Phase 70 — TUI text interaction

**Status: both complete public-extension/package reviews PASS; bounded feasibility pending. Not build-ready.**
**Next:** prove Markdown registration, multi-source input/Copy claim and snippet reset in bounded public-control probes before design/plan approval. Actual package/consumer/integration/AOT checks follow approved implementation, before merge/publication. The operator will manually
verify the installed result on laptop Retina after implementation; the agent's Ghostty access
remains denied.

## Outcome and scope

Text is an interactive product surface: select with keyboard or mouse, copy meaningful text,
paste into editable fields, and copy a highlighted code snippet in one action. Preserve the
existing Kitty graphics, typography, syntax highlighting, motion, and light/dark themes.

| Owned slice | Required outcome |
|---|---|
| Chat composer | Shift+navigation selection; mouse selection; keyboard and contextual Copy/Paste; selection-aware Copy takes precedence over Stop. |
| `/edit` | The same text-interaction rules using the existing XenoAtom CodeEditor and TextMate highlighter; save/close behaviour retained. |
| Transcript | Mouse selection and Copy for rendered text, including continuous selection through rich content. |
| Code snippets | A focusable copy icon with mouse/keyboard activation, discoverable label, and truthful feedback; exact code payload independent of display wrapping. |

Start-page mission creation, link launching, a new terminal window, a general editor redesign,
new themes, and hosted/client changes are outside this phase. The start page is a regression
surface, not a new text editor. See [backlog](../backlog.md) for unrelated work.

## Dependency order

| Spoke | State | Dependency / next action |
|---|---|---|
| [70.1 — Foundation](phase-70.1-text-interaction-foundation.md) | Both complete R9 reviews PASS; bounded feasibility pending. | Design reviews and bounded public feasibility precede plan approval; actual package/consumer/integration/AOT checks follow approved implementation, before merge/publication. |
| [70.2 — Rich transcript selection](phase-70.2-rich-transcript-selection.md) | Scoped; design open. | Requires an approved foundation; resolve the library's document-selection gap before planning implementation. |

[Discovery evidence](phase-70.1-text-interaction-foundation_completed.md) includes source pins,
CodeAlta findings, controlled results, limitations, and timing. Neither spoke has an approved
implementation plan. A snippet button alone does not close transcript selection.

Fresh read-only investigators reconfirmed the unchanged source/artifact baseline and narrowed
the public-API gaps; see [resumption evidence](phase-70.1-text-interaction-foundation_completed.md#resumption-check).
The former live-before-design gate is superseded by the [manual verification exception](#manual-verification-exception).
Product design and plan approval remain required before implementation.

## Ownership and workflow

Product owner: `/Users/ameerdeen/progs/forge-mcl`, component
`src/ForgeMission.Cli/Tui` ([CLI README](https://github.com/katasec/forge-mcl/blob/2022b512dd2bd108626124f22bc1cfe7652648d7/src/ForgeMission.Cli/README.md)).
This repository owns only mission-control documents. CodeAlta is a read-only reference.

Use [supervisor workflow](../design/supervisor-workflow.md) and inline the full
[persona](../../personas/README.md) in every applicable assignment. The supervisor handles
routine investigation, design, review coordination, plan approval, implementation review, merging, and
automated verification without a human relay. The operator performs final live acceptance below.
Type-1 ownership/public-contract/security decisions still go
to the operator. The supervisor applies the designer persona. Use one implementer, one
simplicity/code-style reviewer, and one ownership reviewer, one active subagent at a time; reuse
those roles across stages and revisions. Scope → supervisor design → sequential simplicity/ownership
reviews → implementer plan → sequential reviews → supervisor approval → same implementer →
sequential simplicity/style and ownership reviews → merge/publish → default-path acceptance → closure.
The 2026-10-06 workflow change permits role-agent reuse; it does not approve the candidate or
approve any product implementation.

Current public-extension/package reviews — both complete R9 PASS; see [probe/review evidence and supervisor assessment](phase-70.1-text-interaction-foundation_completed.md#public-extension-evaluation-and-complete-r7r9-reviews). R6 is superseded and archived there.
This delivery is documentation-only. Design approval, plan and code remain pending;
default-path acceptance, product tests and Native AOT are N/A for this documentation change.

## Standing constraints and authorizations

| Fact | Constraint |
|---|---|
| Visual review | Laptop Retina display only, including normal and narrower terminal windows. No external-monitor acceptance requirement. |
| Computer use | Operator explicitly authorized launch and inspection of the TUI. The tool's Ghostty denial is still an access limitation; authorization does not override it. |
| Manual verification | Operator selected post-code manual checking on 2026-10-05: “once code complete - i can check manually”. This replaces agent-operated live reproduction as a prerequisite; it does not waive final live acceptance. |
| Publishing | Operator explicitly authorized publishing packages within the defined Forge repositories and updating their ACLs for consuming solutions. Use normal repository routes once the approved task requires it. Nothing was published or changed in ACLs during discovery. |
| Library extension | Operator selected minimal Forge-owned public extensions over unchanged UI/Markdown/TextMate 3.10.0 and Terminal 2.2.0, with a separate namespace/assembly/package. Skip fork comparison if clean; compare only concrete complexity/essential gap. No upstream/fork writes. Boundary/API reviews and bounded public feasibility precede plan approval; actual package/consumer/integration/AOT proof follows approved implementation, before merge/publication. |
| Themes | Existing `ForgeTheme` → `ForgeStyles` is the token owner. Every owned state must work in dark and light and permit future theme instances. |

## Manual verification exception

**Operator-approved, Phase 70 only, Type 2.** The product, default terminal, ownership,
security and text-interaction requirements do not change.

| Fact | Locked verification route |
|---|---|
| Reason | Computer Use still rejects Ghostty “for safety reasons”. System Settings inspection showed Codex Computer Use enabled, but a direct Ghostty access check was denied. No supported access fix was established. |
| Exact exception | Replace live reproduction before product design with the recorded source/controlled baseline. Replace mandatory supervisor-operated live UI inspection before merge and closure with operator-run installed acceptance after code is ready. This is a scoped exception to the supervisor workflow's live UI readiness check and the applied UI principles' internal-live-PASS prerequisite. No agent live PASS is implied. |
| Agent responsibility | Resolve product design and public APIs; bind mockups/states and theme tokens; run current-artifact persona design/plan/code reviews sequentially with the assigned role agents, interaction and failure tests, required suite and Native AOT checks. Controlled visuals/tests are labelled as such. The exception does not resolve library, ownership or design gaps. |
| Delivery for testing | After approved scope, code reviews and required automated checks pass, merge/publish/install through the normal repository route. Record exact artifact version/commit/digest. Status is **implemented; awaiting manual acceptance**, not complete. |
| Operator responsibility | Launch the installed default artifact in Ghostty on laptop Retina, both themes and normal/narrower windows; perform the spokes' actual user actions and compare copied text, selection, controls and graphics with their locked expectations/references. No endpoint/client/mission substitutions. |
| Evidence and closure | Record who performed each check, artifact/defaults/dependency/safe state, action and observed PASS/FAIL. Operator observations or voluntarily supplied captures are attributed to the operator; supervisor assesses coverage and records acceptance. Missing/failing cases keep the task open and feed the normal design/plan/fix workflow. Automated results never become default-path PASS. |
| Reversal and removal | Revert the Phase 70 exception in this hub/spokes if permitted native-app access returns and the operator chooses agent live verification; otherwise it expires when Phase 70 closes. It grants no exception to other phases. |
| Restriction preserved | No agent-operated alternate automation, capture, input injection or clipboard access to Ghostty. Human manual operation is the selected verification route. No permission/policy edits or alternative terminal are introduced. |

Baseline physical key delivery and terminal-native versus app selection remain unknown until
manual observation. Designers must name intended gestures and observable outcomes explicitly;
they must not describe those unknowns as reproduced. The normal Ghostty default-path facts in
[Default-Path Acceptance](../design/default-path-acceptance.md) remain binding.

## Done when

Both spokes meet their own acceptance conditions; the operator has manually verified the
installed, default-path TUI on the laptop Retina display in light and dark, with evidence
assessed and recorded by the supervisor; Copy/Paste preserve
the intended text and never turn a copy attempt into cancellation; rich transcript selection
works beyond a single paragraph; all changed repositories are merged and clean on `main`.
