# Desktop Interaction Principles — Forge Desktop

> **Audience:** anyone (human or agent) designing or building Forge Desktop's client. **Framework
> note (2026-07-27):** Forge Desktop's UI framework pivoted from Avalonia to a web-rendered client
> (Electron shell, or a browser tab for `forge webui`) built on Blazor Server — see
> [forge-desktop-client-runtime.md](forge-desktop-client-runtime.md) for the architecture and why.
> The interaction principles below are framework-agnostic and carry over unchanged; the
> Avalonia-specific tooling section further down does not and has been replaced. Distinct from
> [ui-design-system.md](ui-design-system.md), which is the CSS-token/theming guidance for `forge.css`
> — under the Electron/Blazor Server client that doc's tokens now apply **directly**, with no porting
> step (unlike Avalonia, which had no CSS and needed its tokens re-expressed as XAML resources). This
> doc's main subject is still *interaction design* (what appears, when, and why), not visual tokens.

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

## The assessment gate — "What would Cooper do? What would Rams do? What would Norman do?"

Run any new or reworked UI surface through all three lenses before implementation. This is a
checklist, not a formality — if an answer is "no," the design isn't ready to build yet. Added
2026-07-27 as the direct consequence of naming Forge Desktop's actual positioning: not a chat app
with a dev workflow shoehorned in ([Claude Code](https://claude.com/product/claude-code)/Codex),
not an AI-less IDE (Visual Studio) — the thing in between, closer to
[the debugger framing](../brainstorm/forge-trace-ide-surface.md#the-debugger-framing) than to a
chat log. **Cooper gate is checked first** — it's the one that catches "this exists because it's
the chat-app default," which Rams/Norman alone won't flag if the *element itself* is honest and
minimal, just aimed at the wrong job.

**Cooper gate** (distilled from Alan Cooper's goal-directed design, *About Face* / *The Inmates Are
Running the Asylum*):

- **Persona/goal check** — does this serve the professional developer's actual workflow goal in
  this moment (stay in flow while steering a running mission), or does it exist to expose a
  feature / because an agent action needed somewhere to render?
- **Implementation-model check** — does the UI reflect the developer's mental model of what's
  happening (mission steps, a running pipeline, a gate awaiting their input), or does it leak the
  system's internal model (raw chat turns, generic message bubbles) onto the user?
- **Perpetual-intermediate check** — Cooper's caution against the inverse failure: don't optimize
  purely for an imagined power-user's keyboard-shortcut efficiency at the cost of everyday
  discoverability. Most users plateau as competent-but-not-expert, forever — design for that real
  usage curve, not a hypothetical elite one. (This is where Cooper and the Rams gate below
  reinforce each other: progressive disclosure is what gets expert efficiency *without* the
  discoverability tax.)

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

## Visual identity direction (decided 2026-07-27, superseded 2026-07-27 by the Electron pivot)

> **Historical — kept for reference, not current guidance.** This direction targeted Avalonia
> specifically (porting `forge.css` tokens into XAML `DynamicResource` brushes) and was abandoned,
> unimplemented, the same day it was written, when Forge Desktop's client pivoted to Electron/Blazor
> Server (see [forge-desktop-client-runtime.md](forge-desktop-client-runtime.md)). Under that client,
> `forge.css` tokens apply **directly** — there is no porting step, no Avalonia resource catalogue to
> draft. Full detail on the abandoned Avalonia Task 4 is preserved in
> [phase-43.2-avalonia-vanilla-shell_completed.md](../phases/phase-43.2-avalonia-vanilla-shell_completed.md#task-4-design-visual-identity-skin).

Forge Desktop currently runs stock Avalonia `FluentTheme` with zero customization
([App.axaml:7](../../src/ForgeMission.Desktop/App.axaml)) — every control renders in Avalonia's
default gray. Direction agreed, not yet implemented (blocked on no active UI task needing it yet):

- **Port the existing "Forge ember" tokens into Avalonia, don't adopt a pre-themed library.**
  [ui-design-system.md](ui-design-system.md) already defines a validated token system (surfaces,
  lines, text, accent, radii, spacing, elevation) for the Blazor app (`ForgeUI`). Re-express that
  same token set as Avalonia `DynamicResource` brushes rather than adopting FluentAvalonia or
  Semi.Avalonia — keeps Forge Desktop and `ForgeUI` visually consistent and reuses design work
  already done, instead of learning/forking a third-party control-template library.
- **Token catalogue mirrors `forge.css`'s existing groups** (surfaces, lines, text, accent, radii,
  spacing, elevation) rather than inventing a new taxonomy — same names where the concept
  transfers, Avalonia-native brush/color types underneath. **Updated 2026-07-27:** starts now, as
  [43.2 Task 4](../phases/phase-43.2-avalonia-vanilla-shell.md) — 43.2 is the live surface that's
  actually ugly today, so it's the right trigger for "first spoke to need real theming," not 43.3/
  43.4. Scoped as a skin over the existing structure only; the chat-shape-to-IDE-shape restructure
  stays [43.4](../phases/phase-43.4-ide-trace-surface.md)'s job, untouched by this.
- **Never build on Avalonia Pro-tier premium controls** (the 6 premium controls / charts / rich
  text editor / tree data grid gated behind the $49/mo Pro subscription vs. $17/mo Plus). Avalonia's
  own docs don't confirm what happens to those controls if the subscription lapses — treat that as
  a forever-subscription risk with no documented exit, not worth taking since Forge Desktop doesn't
  need any of them today. Plus tier (DevTools MCP + Build MCP, see below) has no such lock-in
  concern — it gates a build-time/design-time tool, not a runtime-linked control.

## Design-first process for UI-facing tasks

Per [AGENTS.md's "Design first"](../../AGENTS.md#design-first) rule, this is how that applies to
anything touching Forge Desktop's UI, before any markup is written:

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
6. **Verify against the running app, not just the diff.** Once implemented, use Claude's existing
   browser tooling (Chrome DevTools Protocol) to screenshot/inspect the live app and compare against
   the mockup from step 1. Not required for non-visual changes.

## Visual-reference acceptance gate — non-negotiable

For every user-visible Desktop or ForgeUI change, a mockup is a **binding acceptance artifact**, not
inspiration. Before implementation, its task in the active spoke must name the exact reference
(repository path or design URL), target viewport, task-owned visual slice, and the states that must
match. A non-visual task may record a concise N/A rationale instead.

If a journey mockup spans several tasks, the spoke must say which part this task owns and what stays
deferred. An implementer may not quietly substitute a smaller or different layout because the
reference is broader than the immediate work.

The visual reference must also map to the existing design-system tokens. Do not hard-code sampled
colours, spacing, radii, or type values in a component-local stylesheet to mimic a mockup. When a
new visual language is needed, add or select a named token theme (including its dark-mode values)
and have the surface select that theme; rules continue to consume the ordinary tokens. See
[UI Design System](ui-design-system.md#named-product-themes-and-reskins).

Completion requires a screenshot or live inspection of the running surface compared with that named
reference, and a recorded reviewer PASS or FAIL. **Do not ask the operator for visual acceptance
until the implementing/reviewing agents have first recorded PASS against the reference.** The
operator's review is the final independent acceptance check, not a substitute for internal visual
QA. A material mismatch is a FAIL: the task is not complete and its implementation is not approved,
even when behavior and automated tests pass. Revise the scoped design, then repeat visual
acceptance. This governs visual surfaces; the cross-surface capability requirement remains the
Presentation-surface parity gate in
[Engineering Philosophy](engineering-philosophy.md#presentation-surface-parity-gate).

## AI-assisted design & verification tooling (updated 2026-07-27 — this is the reason for the pivot)

For a web-rendered UI (Electron's local Blazor Server host, or `forge webui`'s browser tab), Claude's
existing browser tooling — the Chrome DevTools Protocol integration already available in this
environment — gives live screenshot and DOM/element inspection against the actually-running app,
with:

- **Zero setup** — no CLI tool install, no license key, no per-client MCP registration.
- **No license** — no paid tier, no forever-subscription risk to weigh.
- **No per-machine configuration** — it works the same on any machine this environment runs in,
  with no "fresh machine setup gotchas" list to maintain.

This is, concretely, **the reason the Electron/Blazor Server pivot happened**: the prior Avalonia
setup needed a paid DevTools MCP tier, a per-machine license key, and hit a multi-day saga (a broken
system `PATH` entry from the .NET SDK installer, per-AI-client license/env registration gaps,
stale-process-needs-restart gotchas) before it worked at all, across both Claude Code and Codex. A
web-rendered client sidesteps all of that by construction — any browser-automatable surface already
gets this verification path for free.

**What this doesn't cover:** automated visual-regression enforcement (render-and-diff against a
reference image, running unattended in CI on every build/PR). Browser tooling here is
interactive-only — nothing calls it unless a session (human or agent) actively asks. Building real
CI-level enforcement would still need a headless-render + diff step wired into the build.
**Deferred deliberately** — not worth the setup cost unless mockup-drift becomes a recurring problem;
revisit if it does.

### Verified gotchas (2026-07-31) — read before re-discovering these

- **Electron's `BrowserWindow` is not CDP-attachable as launched.** `main.cjs` starts it with no
  `--remote-debugging-port`, so browser tooling cannot screenshot/inspect the native Electron window
  directly. This doesn't block verification — the Client Runtime underneath is a plain
  `dotnet run --project src/ForgeMission.ClientRuntime/...` ASP.NET host that prints
  `FORGE_CLIENT_RUNTIME_URL=<url>`; run it directly (same env vars as `scripts/desktop.ps1`:
  `MISSIONRUNTIME__MODE`, `MISSIONRUNTIME__DOCKER__MISSIONREF`, `WORKSPACE__INITIALROOT`) and point
  browser tooling at that URL instead of trying to reach into Electron's window. Confirmed working
  this way, screenshot succeeded.
- **`file://` paths do not get live screenshot/inspect support, regardless of location** (inside or
  outside the project folder) — they render as a static, non-interactive snapshot; `computer`
  screenshot/inspect calls fail with "No site is open in this tab" against them. Live
  screenshot/inspect requires an actual HTTP-served origin.
- **A fresh, not-yet-approved local HTTP origin can be denied by plain `navigate`.** The
  confirmed-working pattern is `preview_start({url: "http://host:port"})` pointed directly at an
  already-running server, not `navigate` cold to a brand-new origin.
- **For a one-off static HTML mockup that needs a persisted screenshot file** (not just an inline
  view in the session), headless Chrome is simpler than fighting the above:
  `"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless --disable-gpu
  --screenshot=<out>.png --window-size=W,H file://<path>.html`. This is how
  [phase-43.2](../phases/phase-43.2-electron-forge-desktop-shell.md)'s Task 3 target mockup
  (`docs/images/phase-43.2/task3-electron-visual-polish-after.png`) was produced. Bonus: this same
  loop caught two real overlap bugs in that mockup's own CSS (a negative-margin header overlap, and a
  flyout with no closed state) that reading the source alone hadn't surfaced — reinforcing rule 6
  above, not just a tooling note.

## Worked examples

Both examples below were built under the now-shelved Avalonia shell; the design reasoning (the
gate, progressive disclosure, honest affordances) is unchanged, only the framework they were applied
in.

- [43.2 (Avalonia) — folder-open UX fix](../phases/phase-43.2-avalonia-vanilla-shell_completed.md#design-note-folder-open-affordance-fix) —
  the case that prompted this doc.
- [43.2 (Avalonia) — Task 3, tool-call indicators](../phases/phase-43.2-avalonia-vanilla-shell_completed.md#task-3-design-tool-call-indicators) —
  first case of the process applied to new (not reworked) UI, before implementation.
