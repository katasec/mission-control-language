# Phase 40.3 — Responsive Surface Collapse (Rooms master/detail)

> **Status: Design** · **Parent:** [Phase 40 — App Shell](phase-40-forge-ui-shell.md) ·
> **Depends on:** [40.2](phase-40.2-app-navigation-shell.md) · **Absorbs:** [Phase 38.8 Task 1](phase-38.8-mobile-access.md) ·
> **Regression risk:** contained — this is the **one** existing surface that changes shape, so it is the
> single place to regression-test.
>
> **Done when:** the app is comfortable at **375px** — the rooms list and the conversation each get the full
> width, navigation between them is one tap (tap a room → conversation; back → list), the conversation is
> immersive (bottom tab bar hidden on the detail view), and nothing overflows horizontally; desktop keeps the
> two-pane shell unchanged.

This is the second of the two responsive collapses ([Phase 40 §2](phase-40-forge-ui-shell.md#2-the-chosen-architecture--persistent-nav-slackdiscord-model)):
the app nav already flips rail↔tab-bar (40.2); this spoke collapses **Rooms' internal two-pane** into
master/detail on the phone.

## Context an implementer needs

- **Lean on the route, not new state.** [`Pages/Rooms.razor`](../../src/ForgeUI/Pages/Rooms.razor) already
  owns both `/rooms` (no room selected) and `/rooms/{RoomId:guid}` (a room selected) on one component. That
  route split *is* the "which pane" signal — the collapse is CSS driven by whether a room is selected, plus a
  back control. Do not introduce a viewport-width C# state machine.
- **Structure today:** `.app-shell` → `.rooms-nav` (list) + `.app-main` (`RoomConversation` or the
  `.convo-empty` placeholder). Post-40.2 an app rail sits left of `.rooms-nav`.
- **Baseline behaviour to reproduce:** before the two-pane shell (Phase 38 pre-`0.3.2`), mobile already
  worked as list→conversation→back. WhatsApp mobile is the reference.
- **The "two bottom bars" conflict:** on a phone, the conversation has a composer at the bottom *and* the app
  tab bar wants the bottom. **Decision (locked in Phase 40 §5): the conversation detail is immersive — hide
  the bottom tab bar on `/rooms/{id}` at phone width**; the back control returns to the list, where the tab
  bar reappears. This matches WhatsApp (no tab bar inside a chat) and frees the bottom for the composer +
  safe-area inset.

## Tasks (chronological)

### Task 1 — Collapse `.app-shell` to a single pane on mobile

1. In forge.css, **mobile-first** (base = phone): make the rooms shell show one pane at a time, driven by a
   class the page sets from its route. Simplest robust approach — `Pages/Rooms.razor` adds a state class on
   `.app-shell`, e.g. `class="app-shell @(RoomId is null ? "at-list" : "at-detail")"`:
   - base (`< 720px`): `.rooms-nav` full-width and shown when `.at-list`, hidden when `.at-detail`;
     `.app-main` full-width and shown when `.at-detail`, hidden when `.at-list`.
   - `@media (min-width: 720px)`: both panes visible side-by-side (today's layout) regardless of the state
     class — the desktop two-pane is unchanged.
   ```css
   /* base = mobile master/detail */
   .rooms-nav { width: 100%; }
   .app-shell.at-detail .rooms-nav { display: none; }
   .app-shell.at-list  .app-main  { display: none; }
   @media (min-width: 720px) {
     .rooms-nav { width: 288px; }
     .app-shell.at-detail .rooms-nav,
     .app-shell.at-list  .app-main { display: flex; }   /* both panes always */
   }
   ```
2. Tapping a room already navigates to `/rooms/{id}` (an `<a href>`), which re-renders with `RoomId` set →
   `.at-detail` → conversation fills the screen. No JS needed.
- **Done when:** at 375px, `/rooms` shows the list full-width and `/rooms/{id}` shows the conversation
  full-width; at ≥720px both panes show as today.

### Task 2 — Back-to-rooms control (mobile only)

1. Add a back affordance in the conversation header (in `Shared/RoomConversation.razor`, the `.convo-header`)
   — an `<a href="/rooms">` with the `arrow-left` icon from 40.2 Task 1, labelled for a11y
   (`aria-label="Back to rooms"`).
2. Show it **only on mobile**: hide at `@media (min-width: 720px)` (on desktop the list is always present, so
   back is meaningless).
- **Done when:** the back control appears in the conversation header < 720px and returns to the list; it is
  absent on desktop.

### Task 3 — Immersive conversation (hide the tab bar on detail)

1. Hide the app bottom tab bar when a room is open on mobile. Since `MainLayout`/`NavShell` sit *above* the
   Rooms page, coordinate via a **body/root class or a cascading flag**, not page-local CSS. Recommended:
   have `Pages/Rooms.razor` toggle a class on the app root when `RoomId` is set (e.g. via a small
   `CascadingValue`/layout state or a JS-interop `document.body.classList` toggle), and in forge.css:
   ```css
   @media (max-width: 719.98px) {
     body.in-conversation .nav-shell { display: none; }
   }
   ```
   Choose the least-magic mechanism that fits the codebase; a cascading "shell chrome" flag from `MainLayout`
   that `Rooms.razor` sets is cleaner than body-class interop if it's ergonomic.
2. The composer picks up `padding-bottom: calc(var(--space-3) + var(--safe-bottom))` (40.1 token) so it clears
   the iOS home indicator now that it owns the screen bottom.
- **Done when:** opening a room on a phone gives a full-screen conversation with no tab bar; backing out
  restores the list *and* the tab bar.

### Task 4 — Re-check overlays at phone width

1. Verify the **create-room** and **Members** modals (`.modal`, `max-width` + `max-height: 86vh` + scroll),
   the **@-mention autocomplete** (`.mention-menu`, `position: absolute` above the composer), the composer,
   and the account affordance all behave at 375px (no horizontal overflow, tappable, dismissible).
2. Fix any that overflow — typically by letting the modal go near-full-width on mobile
   (`@media (max-width: 719.98px) { .modal { max-width: 100%; } }`) and confirming the mention menu's
   `max-width` doesn't exceed the viewport.
- **Done when:** every overlay is usable and contained at 375px.

### Task 5 — Verify across viewports

1. `preview_resize` **375 / 768 / 1024** and **dark**: exercise list → tap room → conversation → back;
   confirm the tab bar hides/returns; open both modals + the mention menu; confirm **no horizontal scroll**
   at any width (`preview_eval document.documentElement.scrollWidth <= innerWidth`).
2. Confirm desktop (≥720) is byte-for-byte the prior two-pane behaviour (regression check — this is the one
   modified surface).
- **Done when:** all three widths pass and desktop is unregressed.

## Non-goals

- PWA/installability (that is 40.4).
- Redesigning the conversation or list contents — this spoke only changes *when each pane is shown* and adds
  the back/immersive affordances.
- Gesture navigation (swipe-back) — nice-to-have, out of scope; the back control + browser back suffice.
