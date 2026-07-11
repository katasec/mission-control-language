# Phase 40.1 — Design System Foundation (mobile primitives + conventions)

> **Status: Design** · **Parent:** [Phase 40 — App Shell](phase-40-forge-ui-shell.md) ·
> **Depends on:** none · **Regression risk:** none (global-safe / additive) ·
> **Design system:** [UI Design System](../design/ui-design-system.md)
>
> **Done when:** `forge.css` has no dead rules, no shell uses `100vh`, no focusable input triggers iOS
> zoom, motion respects `prefers-reduced-motion`, safe-area insets are wired, and a documented
> breakpoint + mobile-first authoring convention exists — so every surface built in 40.2–40.4 inherits
> a mobile-correct foundation without re-solving these.

This spoke touches **only** `forge.css`, `_Host.cshtml`'s viewport meta, and the design-system doc. No
`.razor` layout changes. It is intentionally first: these are global-safe improvements that cannot break
layout structurally, and they establish the conventions the later spokes lean on.

## Context an implementer needs

- The stylesheet is a single tokenized file, [`src/ForgeUI/wwwroot/css/forge.css`](../../src/ForgeUI/wwwroot/css/forge.css)
  (~606 lines), organised into numbered sections. **No npm, no Node, no preprocessor** — plain CSS with
  custom properties. To re-theme you edit tokens in `:root`, not rules.
- **A known CSS limitation to respect:** you *cannot* use a custom property in a media-query condition
  (`@media (min-width: var(--bp-md))` does **not** work). Breakpoint pixel values must be literals in the
  `@media` rule. We keep them consistent by declaring the canonical numbers in **one commented block** and
  referencing that comment everywhere — the `--bp-*` tokens below are documentation/JS-interop aids, not
  usable in `@media`.
- Baseline facts (verified 2026-07-12): `_Host.cshtml` already has a correct
  `<meta name="viewport" content="width=device-width, initial-scale=1.0">`; `100vh` appears in **5** shell
  rules; the old Phase 34/35 chat surface classes (`.session-nav*`, `.chat-shell`, `.chat-*`, the centred
  `.room*` column, `.mission-*`) are referenced by **zero** `.razor` files (dead).

## Tasks (chronological)

### Task 1 — Prune dead CSS

Shrink the file before we build on it, so 40.2+ isn't maintaining two shell systems.

1. Confirm-then-delete. For each candidate class, grep the `.razor`/`.cshtml` sources and delete the rule
   only if there are **zero** references:
   ```bash
   grep -rn "session-nav\|chat-shell\|chat-messages\|chat-topbar\|chat-input\|chat-send\|mission-picker\|mission-label" src/ForgeUI --include=*.razor --include=*.cshtml
   grep -rn 'class="room \|class="room"' src/ForgeUI --include=*.razor   # the OLD centred .room column (§8), not .room-* rooms classes
   ```
2. Remove the confirmed-dead rules: forge.css **§6 (Chat surface)**, the `.session-*` block in §5, the old
   centred `.room { … max-width: 940px }` column in §8 (superseded by `.convo`), and any `.mission-*` picker
   rules. **Keep** everything the rooms shell uses (`.room-msg*`, `.msg-*`, `.agent-card`, `.trust-*`,
   `.identity-seal`, `.convo*`, `.rooms-*`, `.room-item*`, modal/§12, auth/§9).
3. **Careful ambiguity:** `.room-*` (hyphen) classes are live rooms styles — do **not** delete them. Only the
   bare `.room` centred-column rule is dead. `.agent-card`/`.trust-badge`/`.trace-*` in §7 are **live** (used
   by `RoomConversation`/`PipelineTrace`) — keep them.
- **Done when:** the app renders identically (rooms, login, modals, trust cards) with the dead rules gone;
  `git diff` shows only removals from §5/§6/§8.

### Task 2 — Dynamic viewport height (`100vh` → `100dvh`)

The #1 mobile bug: `100vh` ignores the dynamic URL bar, so the composer/input hides behind Safari's address
bar. Replace with `100dvh` and a `100vh` fallback for old engines.

1. In forge.css, for each of the 5 shells (`.app-shell`, `.convo`, `.auth-shell` uses `min-height`, plus the
   two dead ones removed in Task 1 — so effectively `.app-shell`, `.convo`, and `.auth-shell` remain):
   ```css
   .app-shell { height: 100vh; height: 100dvh; }   /* fallback then dvh */
   .auth-shell { min-height: 100vh; min-height: 100dvh; }
   ```
- **Done when:** on a mobile viewport the composer sits flush above the address bar (verify with
  `preview_resize` mobile + `preview_inspect` the composer's bounding box against viewport height).

### Task 3 — iOS input-zoom fix (inputs ≥ 16px)

iOS Safari auto-zooms the page when a focused input's font-size is < 16px — jarring. Audit inputs and lift
any below 16px. Current offenders: `.chat-input` (14px, may be dead post-Task 1), `.room-input`/`.field`
(15px), `.invite-field` (12px, mono).

1. Set the effective font-size of every *focusable* input to **≥ 16px**. Prefer a targeted rule so desktop
   density is preserved where it doesn't matter, but the simplest correct move is to bump the base field
   tokens: `.field`, `.room-input`, `.invite-field` → `font-size: 16px`. (16px is also the base body size, so
   this reads as intentional, not oversized.)
2. Leave *non-editable* small text (labels, metas) alone — the rule is inputs only.
- **Done when:** focusing any input on iOS (or `preview_resize` mobile + focus) does not zoom the page.

### Task 4 — Respect `prefers-reduced-motion`

Accessibility gap: the `pulse` keyframe and `--transition` hover/focus animations ignore the OS setting.

1. Add near the top of the base section:
   ```css
   @media (prefers-reduced-motion: reduce) {
     *, *::before, *::after {
       animation-duration: 0.01ms !important;
       animation-iteration-count: 1 !important;
       transition-duration: 0.01ms !important;
       scroll-behavior: auto !important;
     }
   }
   ```
- **Done when:** with "reduce motion" on, the thinking-pulse and hover transitions are effectively static.

### Task 5 — Safe-area insets (iOS notch / home indicator)

The bottom tab bar (40.2) and composer must not sit under the iOS home indicator; the rail must clear the
notch. Wire the plumbing now so 40.2/40.3 just consume it.

1. In `_Host.cshtml`, extend the viewport meta to opt into the safe-area env vars:
   ```html
   <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
   ```
2. Add safe-area helper tokens/comment in forge.css `:root` for use by later spokes:
   ```css
   --safe-top:    env(safe-area-inset-top, 0px);
   --safe-bottom: env(safe-area-inset-bottom, 0px);
   ```
   (Composer and bottom tab bar in 40.2/40.3 add `padding-bottom: calc(var(--space-3) + var(--safe-bottom))`.)
- **Done when:** the tokens exist and `viewport-fit=cover` is set; visible effect lands with 40.2/40.3.

### Task 6 — Breakpoint convention + mobile-first authoring

Establish the responsive contract in one place so every later rule is consistent.

1. Add a **single canonical breakpoint comment block** at the top of a new forge.css section (e.g. a new
   `§0. Responsive breakpoints`), and matching `--bp-*` tokens (for JS-interop / documentation only — *not*
   usable in `@media`, see Context):
   ```css
   /* Breakpoints — LITERAL px in @media (custom props can't be used in media conditions).
      Keep every @media in this file aligned to these two numbers:
        --bp-rail:  640px   rail↔bottom-tab-bar flip (40.2)
        --bp-panes: 720px   rooms master/detail ↔ two-pane flip (40.3) */
   :root { --bp-rail: 640px; --bp-panes: 720px; }
   ```
   (Two breakpoints, deliberately close: below 640 = phone → bottom tab bar + single pane; 640–720 = large
   phone / small tablet → rail returns but panes may still collapse; ≥720 = full two-pane. Implementers may
   collapse to one breakpoint at 720 if 640 proves unnecessary — decide during 40.2/40.3, keep it in this
   block.)
2. **Adopt mobile-first authoring going forward** (document it in the design-system doc, Task 7): base rules
   target the phone; `@media (min-width: 720px) { … }` layers desktop *up*. Do **not** retrofit existing
   desktop-first rules in this task — the convention governs new rules in 40.2–40.4.
- **Done when:** the block exists and is referenced by the design-system doc.

### Task 7 — Update the design-system doc

Keep [`docs/design/ui-design-system.md`](../design/ui-design-system.md) the accurate contract.

1. Add a **"Responsive & mobile"** section documenting: mobile-first authoring, the two breakpoints (from
   Task 6), `100dvh`, the 16px-input rule, safe-area tokens, and `prefers-reduced-motion`.
2. Add a **"Dependency policy (right-sizing vs NIH)"** subsection mirroring [Phase 40 §3.2](phase-40-forge-ui-shell.md):
   platform-first (NavLink/layouts), hand-rolled layout, SVG icon *assets* not runtimes, FluentUI-Blazor as
   the named escape hatch, UX-never-sacrificed-to-avoid-a-library.
3. Flag that **§8 ("MainLayout is a thin `@Body`") is about to change in 40.2** — leave a forward note; 40.2
   rewrites it.
- **Done when:** the doc describes the responsive foundation and the dependency policy.

## Non-goals

- No `.razor` layout changes, no new pages (that's 40.2).
- No retrofitting existing desktop-first rules to mobile-first (only new rules follow the convention).
- No `rem`-migration of the fixed-`px` font scale (deferrable; noted in the design review, not in scope).
- Consolidating the duplicated dark-token block (`@media` vs `[data-theme=dark]`) is optional cleanup — do it
  only if trivial; not required for the "done when."
