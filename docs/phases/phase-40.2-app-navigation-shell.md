# Phase 40.2 — App Navigation Shell (rail ↔ bottom tab bar)

> **Status: DONE (2026-07-12, verified in-browser)** · **Parent:** [Phase 40 — App Shell](phase-40-forge-ui-shell.md) ·
> **Depends on:** [40.1](phase-40.1-design-system-foundation.md) · **Regression risk:** low (net-new nav;
> Rooms gains a rail beside it, its internals unchanged until [40.3](phase-40.3-responsive-collapse.md))
>
> **Done when:** a signed-in user sees persistent navigation — a left **rail** on desktop, a bottom **tab
> bar** on a phone — linking **Rooms · Library · Account**, with the active surface marked; `/library` shows
> the read-only agent directory and `/account` shows identity + prepaid balance; Rooms is still the default
> landing (`/` → `/rooms`).

This is the net-new nav layer. It does **not** yet collapse the Rooms two-pane (that is 40.3) — on mobile,
Rooms will still be desktop-shaped *inside* the content region until 40.3; the tab bar itself works at all
widths from this spoke.

## Context an implementer needs

- **Layout seam:** [`MainLayout.razor`](../../src/ForgeUI/Shared/MainLayout.razor) is today a thin `@Body`.
  It becomes the shell: `NavShell` + a content region hosting `@Body`. This reverses design-system §8 — 40.1
  Task 7 left a forward note; **this spoke rewrites §8 + the surface map** (Task 8).
- **Routing/landing:** [`Pages/Index.razor`](../../src/ForgeUI/Pages/Index.razor) redirects `/` → `/rooms`;
  keep it. [`App.razor`](../../src/ForgeUI/App.razor) gates everything (`AuthorizeRouteView` →
  `RedirectToLogin`), so new pages just need `@attribute [Authorize]`.
- **Rooms shell today** ([`Pages/Rooms.razor`](../../src/ForgeUI/Pages/Rooms.razor)) is `.app-shell` =
  `.rooms-nav` (sidebar, with `AccountMenu` docked at its foot) + `.app-main` (conversation). The app rail
  wraps *around* this; the **account control moves from the rooms-sidebar foot up to the rail foot** so it
  isn't duplicated.
- **Data for the new surfaces already exists** (no backend work):
  - Account balance: `BillingService.GetBalanceMicroUsdAsync(memberId)` → micro-USD `long`
    ([`Services/BillingService.cs`](../../src/ForgeUI/Services/BillingService.cs)); identity via
    `CurrentUser.GetMemberAsync()` (`DisplayName`, `Email`) + `ForgeClaims`.
  - Library directory: `AgentRegistry.List()` → `IReadOnlyList<AgentDescriptor>` (Handle, Description,
    Publisher, Seal) ([`Services/AgentRegistry.cs`](../../src/ForgeUI/Services/AgentRegistry.cs)). Render the
    identity seal via the existing `IdentitySealMark` component. **Never** the green verified badge (§5).
- **Blazor best practice:** use the built-in **`NavLink`** component for each nav item — it applies an
  `active` CSS class and `aria-current="page"` automatically. Use `Match="NavLinkMatch.Prefix"` for `/rooms`
  (so `/rooms/{id}` keeps Rooms active) and `NavLinkMatch.All`/prefix as appropriate for the others.

## Tasks (chronological)

### Task 1 — Inline SVG icon set

1. Create a tiny `Shared/Icon.razor` (or three static SVG snippets) holding the nav glyphs as **inline SVG
   paths copied from Lucide (MIT)** — pick: `message-circle`/`messages-square` (Rooms), `library`/`layout-grid`
   (Library), `user`/`circle-user` (Account), plus `arrow-left` (back, used in 40.3). No icon font, no package.
2. Icons inherit `currentColor` (`stroke="currentColor" fill="none"`), sized via CSS (e.g. 22px), so they
   theme with the nav's text token automatically.
- **Done when:** an `<Icon Name="rooms" />` (or equivalent) renders a crisp, theme-aware SVG.

### Task 2 — `NavShell.razor` (rail on desktop, tab bar on mobile)

1. Create `Shared/NavShell.razor`: a semantic `<nav aria-label="Primary">` containing three `NavLink`s
   (Rooms `/rooms` prefix-match, Library `/library`, Account `/account`), each = icon + short label, plus the
   account/identity affordance (see Task 4). Include a **skip-to-content** link (`<a class="skip-link"
   href="#main">`) as the first focusable element.
2. **Mobile-first CSS** (new forge.css section, e.g. `§13 App nav shell`): base rules render a **fixed bottom
   tab bar** (`position: fixed; bottom: 0; left/right: 0; display: flex`), items evenly spaced, icon-over-label,
   `padding-bottom: calc(var(--space-2) + var(--safe-bottom))` (safe-area from 40.1). At
   `@media (min-width: 720px)` (align to the 40.1 `--bp` block) flip to a **left rail**: `flex-direction:
   column; width: 64px; height: 100dvh; border-right`, items icon-first with the label hidden or shown small.
3. Active state: style `.nav-item.active` (NavLink's class) with the ember accent (`--accent`) — reuse the
   active-nav treatment already used for `.room-item.active` (inset accent bar / accent text) for consistency.
4. Touch targets ≥ 44px; visible `--focus-ring` on keyboard focus.
- **Done when:** the nav is a bottom bar < 720px and a left rail ≥ 720px, the active surface is marked, and
  it is keyboard-navigable.

### Task 3 — Wire `NavShell` into `MainLayout`

1. Rewrite `MainLayout.razor` from thin `@Body` to:
   ```razor
   @inherits LayoutComponentBase
   <div class="app-frame">
     <NavShell />
     <main id="main" class="app-content">@Body</main>
   </div>
   ```
   with `.app-frame` = `display:flex; flex-direction: column-reverse` on mobile (content above, tab bar
   below) and `flex-direction: row` ≥720px (rail left, content right); `.app-content { flex: 1; min-width: 0;
   min-height: 0 }`. Reserve space so content isn't hidden under the fixed bottom bar on mobile (e.g.
   `padding-bottom` = tab-bar height + safe-area, **or** make the tab bar part of the flex flow rather than
   `position: fixed` — implementer's call; fixed + padding is the robust default with a scrolling content
   region).
2. **`/login` must stay bare** (no nav). Login uses `MainLayout` today via `App.razor`; guard by either giving
   Login its own empty layout (`@layout` in `Login.razor`) or conditionally rendering `NavShell` only when
   authenticated. Prefer an explicit **`BareLayout.razor`** for `/login` — cleaner than a runtime auth check
   in the shell.
- **Done when:** every authed surface renders inside the shell with nav present; `/login` renders bare.

### Task 4 — Relocate the account control

1. Move the identity/account affordance from the **rooms-sidebar foot** to the **rail foot** (desktop) — the
   `AccountMenu` `<details>` dropdown, opening upward from the rail bottom. Remove the `.rooms-account` block
   from `Pages/Rooms.razor` (the rooms sidebar no longer hosts it).
2. On mobile the identity lives behind the **Account tab** (→ `/account`), so the bottom tab bar's Account
   item is the entry point; the `AccountMenu` dropdown is desktop-rail-only. Keep sign-out reachable in both
   (rail dropdown on desktop, on the `/account` page on mobile — Task 6 puts it there too).
- **Done when:** the account control is in the rail on desktop and reachable via the Account tab on mobile,
  and is **not** duplicated in the rooms sidebar.

### Task 5 — `/library` page (read-only agent directory)

1. Create `Pages/Library.razor` (`@page "/library"`, `[Authorize]`, inject `AgentRegistry`). Render
   `AgentRegistry.List()` as a directory: per row → `@handle` (accent), publisher, description, and the
   identity seal via `<IdentitySealMark Descriptor="d" />`. Reuse `.room-list*`/`.agents-directory*` styles
   or add a small `§ library` block; keep it token-driven.
2. Header: "Library" + a one-line subtitle ("Agents you can @mention in any room"). Add a **quiet placeholder
   note** that user-authored missions arrive with 39.5 — honest scope, no fake affordance.
3. **Trust surface:** identity seal only (gold/blue) — never a green verified badge here (design-system §5).
- **Done when:** `/library` lists the built-in agents with correct seals and reads well at 375px and desktop.

### Task 6 — `/account` page (identity + balance)

1. Create `Pages/Account.razor` (`@page "/account"`, `[Authorize]`, inject `CurrentUser` + `BillingService`).
   Show: avatar + display name + email; **balance** = `GetBalanceMicroUsdAsync(me.Id)` rendered as USD
   (`micro / 1_000_000m`, format `$0.00` — note it's a comped F&F credit today); and a **Sign out** form
   (`POST /auth/logout`, mirror `AccountMenu`).
2. Leave labelled seams (no build) for **Top up** (39.6) and **My agents** (39.5) — a disabled/"coming soon"
   affordance is fine; do not stub backend.
- **Done when:** `/account` shows real identity + balance and can sign out, at phone and desktop widths.

### Task 7 — Landing behaviour

1. Confirm `/` → `/rooms` (unchanged). The shell + Rooms-selected *is* the landing; no separate hub page.
2. Sanity-check the Rooms empty state (`.convo-empty`, "Pick a room") still reads correctly now that a rail
   sits to its left.
- **Done when:** signing in lands on `/rooms` inside the shell with Rooms active in the nav.

### Task 8 — Update the design-system doc

1. Rewrite design-system **§8** ("MainLayout is a thin `@Body`") to describe the new shell: `MainLayout` =
   `NavShell` + content region; `BareLayout` for `/login`; the account control lives in the rail/Account tab.
2. Add **Rooms, Library, Account** to the **surface map (§6)** with their routes + key classes, and add the
   nav to the primitives list (§4).
- **Done when:** the doc matches the shipped shell; no stale "thin `@Body`" claim remains.

## Verify (browser preview)

At **375 / 768 / 1024** and **dark**: `preview_resize` to flip rail↔tab-bar at 720; `preview_click` each nav
item and confirm `aria-current`/active styling via `preview_inspect`; load `/library` and `/account` and
`preview_snapshot` for structure; confirm content isn't clipped under the fixed bottom bar on mobile.

## Non-goals

- Rooms two-pane collapse (that is 40.3 — Rooms may still be desktop-shaped inside the content region here).
- Any billing action (top-up), custom missions, or writable Library — display-only, seams left for 39.5/39.6.
- A settings surface — out of scope; the nav is Rooms/Library/Account only for now.
