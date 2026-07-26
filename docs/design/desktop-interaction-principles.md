# Desktop Interaction Principles — Forge Desktop

> **Audience:** anyone (human or agent) designing or building `ForgeMission.Desktop` (Avalonia).
> Distinct from [ui-design-system.md](ui-design-system.md), which is CSS-token/theming guidance
> for the Blazor web app (`ForgeUI`) and doesn't apply here — Avalonia has no CSS tokens. This doc
> is about *interaction design* (what appears, when, and why), not visual tokens.

## Why this doc exists

Raised 2026-07-26 during review of [43.2](../phases/phase-43.2-avalonia-vanilla-shell.md)'s
folder-open flow: a top-right "Open Folder" button that never goes away, next to dead placeholder
text styled like a clickable link. Compared side-by-side with Claude Desktop's equivalent flow
(folder-adding lives behind the compose box's `+` menu and disappears once used), the gap wasn't
a one-off bug — it was the absence of a stated interaction philosophy for this project. This doc
is that philosophy, expected to grow as new surfaces (43.3's mission picker, 43.4's workbench)
raise new cases.

## Principle 1 — Progressive disclosure: persistent chrome only for the primary task

Chat is the primary task; almost everything else (opening a folder, switching a mission, changing
a setting) is occasional setup, not a per-turn action. Occasional actions belong behind a control
that reveals them on demand (a `+` menu, a settings sheet) and then gets out of the way — never as
a standing fixture competing with the chat surface for space and attention. This is usually called
**progressive disclosure** (Nielsen Norman Group) or "contextual / just-in-time UI." Reference
behavior: Claude Desktop's `+` menu — folder-adding appears only when invoked, and the UI reverts
to plain chat immediately after.

## Principle 2 — Honest affordances

An element's visual treatment (color, shape, position, cursor) must signal interactivity *only* on
elements that are actually interactive, and the signal must match what actually happens. This is
Don Norman's **affordance / signifier** distinction (*The Design of Everyday Things*): a signifier
that promises an action the element doesn't deliver breaks the user's trust in every other signal
in the UI, not just that one element. Concretely: never style static/informational text so it
*looks* like the real trigger — if it's not clickable, it shouldn't look clickable; if it is
clickable, the click must do the thing the styling implies.

## Principle 3 — No redundant entry points (minimal chrome)

One job, one control. Two elements that both nominally "do" the same thing (a button plus
dead-looking text near it) is chrome tax — space and attention spent with no added capability.
This is Nielsen's "aesthetic and minimalist design" heuristic: every extra element competes with
the ones that matter and should justify its presence.

## The assessment gate — "What would Rams do? What would Norman do?"

Run any new or reworked UI surface through both lenses before implementation. This is a checklist,
not a formality — if an answer is "no," the design isn't ready to build yet.

**Rams gate** (distilled from Dieter Rams' ten principles of good design, applied to software UI):

- **As little design as possible** — could this control disappear when not needed, or be merged
  into an element that already exists, instead of adding a new one?
- **Honest** — does it look like exactly what it does, and nothing more?
- **Understandable** — would a first-time user get it without a tooltip or label?
- **Thorough** — are the empty, disabled, loading, and error states designed, not just the happy
  path?
- **Long-lasting** — will this still make sense once [43.3](../phases/phase-43.3-mission-attach-point.md)'s
  mission picker and [43.4](../phases/phase-43.4-ide-trace-surface.md)'s workbench land, or is it a
  patch that the next surface will have to work around?

**Norman gate** (signifiers, feedback, mapping, constraints):

- **Signifier check** — does every element that looks interactive actually respond, and does every
  element that doesn't respond avoid looking interactive? (This is the exact bug Principle 2
  names.)
- **Feedback check** — after the user acts, does the UI confirm what happened immediately (a path
  shown, a chip added, a message appended) rather than leaving them to guess?
- **Mapping check** — is the control where the user's existing mental model expects it? (Attach/add
  actions live in the compose bar across every reference app people already use — Claude Desktop,
  Slack, iMessage. Don't invent a new location without a reason.)
- **Constraint check** — are invalid actions prevented or hidden (e.g. a disabled compose box before
  a folder is open) rather than present but silently failing?

## Design-first process for UI-facing tasks

Per [AGENTS.md's "Design first"](../../AGENTS.md#design-first) rule, this is how that applies to
anything touching `ForgeMission.Desktop`'s UI, before any XAML is written:

1. **Mock it.** Hand-author a flat SVG wireframe (box/text only — no photographic content) for the
   proposed state. If reworking an existing flow, add a **before** SVG next to the **after** one so
   the defect and the fix are both on record, not just the fix. For transient states (a popup menu,
   a loading indicator), mock the *before* and *after* as separate frames rather than one image
   that hides the interesting moment.
2. **Save it** under `docs/images/phase-<N>[.M]/<task-slug>-{before,after}.svg` — one folder per
   phase, one image pair per task. SVG (not PNG): it's git-diffable text, renders inline in
   markdown natively with no rasterization step, and keeps copy strings grep-able so a later agent
   can match wording exactly.
3. **Write a component spec** directly under the image(s): control hierarchy, states, exact copy
   strings, and which existing primitive it should use (button, flyout/menu, list item). This is
   what an implementing agent actually builds against — the image is for human sanity-check and
   agreement, the spec is the executable instruction.
4. **Run the gate** above and note the answers (even briefly) in the spoke.
5. **Get sign-off** before implementation starts. Embed everything in the relevant phase spoke
   under the task it belongs to — never a standalone design doc per task; the spoke stays the one
   place a fresh agent looks.

## Worked examples

- [43.2 — folder-open UX fix](../phases/phase-43.2-avalonia-vanilla-shell.md#design-note-folder-open-affordance-fix) —
  the case that prompted this doc.
- [43.2 — Task 3, tool-call indicators](../phases/phase-43.2-avalonia-vanilla-shell.md#task-3-design-tool-call-indicators) —
  first case of the process applied to new (not reworked) UI, before implementation.
