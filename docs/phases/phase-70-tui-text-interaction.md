# Phase 70 — TUI text interaction

**Status: discovery recorded; product design and implementation pending. Not build-ready.**
**Next:** obtain permitted live reproduction of the reported gestures in the installed TUI on
the laptop Retina display, then run the foundation design through the supervisor workflow. Computer-use access to Ghostty
was denied by the tool; this live verification remains open.

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
| [70.1 — Foundation](phase-70.1-text-interaction-foundation.md) | Source and controlled discovery complete; live reproduction and design gates open. | First: establish common command, selection, clipboard, context-menu and code-copy behaviour using public library capabilities. |
| [70.2 — Rich transcript selection](phase-70.2-rich-transcript-selection.md) | Scoped; design open. | Builds on the approved foundation; resolve the library's document-selection gap before planning implementation. |

[Discovery evidence](phase-70.1-text-interaction-foundation_completed.md) includes source pins,
CodeAlta findings, controlled results, limitations, and timing. Neither spoke has an approved
implementation plan. A snippet button alone does not close transcript selection.

Fresh read-only investigators reconfirmed the unchanged source/artifact baseline and narrowed
the public-API gaps; see [resumption evidence](phase-70.1-text-interaction-foundation_completed.md#resumption-check).
The foundation's [scope gate](phase-70.1-text-interaction-foundation.md#live-reproduction-scope-gate)
remains open: no product designer, plan author or implementer has been launched or approved.

## Ownership and workflow

Product owner: `/Users/ameerdeen/progs/forge-mcl`, component
`src/ForgeMission.Cli/Tui` ([CLI README](https://github.com/katasec/forge-mcl/blob/2022b512dd2bd108626124f22bc1cfe7652648d7/src/ForgeMission.Cli/README.md)).
This repository owns only mission-control documents. CodeAlta is a read-only reference.

Use [supervisor workflow](../design/supervisor-workflow.md) and inline the full
[persona](../../personas/README.md) in every applicable assignment. The supervisor handles
routine investigation, design review, plan approval, implementation review, merging, and
acceptance without a human relay. Type-1 ownership/public-contract/security decisions still go
to the operator. Scope → design → simplicity/ownership reviews → implementer plan →
simplicity/ownership reviews → supervisor approval → fresh implementer →
simplicity/ownership/style reviews → merge/publish → default-path acceptance → closure.

This delivery is documentation-only: product design/plan/code review stages are pending,
not implicitly passed. Default-path acceptance is N/A for this documentation change.

## Standing constraints and authorizations

| Fact | Constraint |
|---|---|
| Visual review | Laptop Retina display only, including normal and narrower terminal windows. No external-monitor acceptance requirement. |
| Computer use | Operator explicitly authorized launch and inspection of the TUI. The tool's Ghostty denial is still an access limitation; authorization does not override it. |
| Publishing | Operator explicitly authorized publishing packages within the defined Forge repositories and updating their ACLs for consuming solutions. Use normal repository routes once the approved task requires it. Nothing was published or changed in ACLs during discovery. |
| Library extension | Prefer existing public XenoAtom APIs. An upstream change, Forge adapter, or package ownership change needs a concrete reviewed contract before code; permission to publish Forge packages does not grant write access to XenoAtom. |
| Themes | Existing `ForgeTheme` → `ForgeStyles` is the token owner. Every owned state must work in dark and light and permit future theme instances. |

## Done when

Both spokes meet their own acceptance conditions; the supervisor has personally verified the
installed, default-path TUI on the laptop Retina display in light and dark; Copy/Paste preserve
the intended text and never turn a copy attempt into cancellation; rich transcript selection
works beyond a single paragraph; all changed repositories are merged and clean on `main`.
