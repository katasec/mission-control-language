# Phase 38.8 — Mobile Access (Responsive + PWA)

> **Status: Backlog (raised 2026-07-10)** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** the two-pane rooms shell (shipped `forge-ui:0.3.2`, 2026-07-10)
> **Done when:** a person can open Forge Rooms on a phone from an invite link, use it comfortably
> (list ⇄ conversation, not a squished desktop layout), and — optionally — add it to their home
> screen so it launches like an app.

On-thesis for Phase 38: "make the engine reachable." The chat surface is only as accessible as the
device people actually carry. Today Forge Rooms is desktop-shaped and browser-only; this sub-phase
closes the mobile gap **without** building native iOS/Android apps (no app-store accounts, review
queues, or two-platform build pipeline — wrong cost for a solo founder right now). Distribution is
already link-based (invite links), so "open link on your phone → optionally install" is a natural
extension of a flow that already exists.

## Current state (baseline)

The rooms UI is a **persistent two-pane shell** (2026-07-10): a rooms sidebar (~288px) beside the
active conversation, a create-room modal (name + pick agents), and a consolidated Members modal
(rename + roster + add-agent + invite). This is **desktop-shaped**: on a ~380px phone the 288px
sidebar sits *beside* the conversation, leaving the chat unusably narrow. The manifest/PWA wrapper
is the easy 10%; the responsive layout is the real work — hence Task 1 first.

**Shipped 2026-07-10 (the baseline this builds on) — live `forge-ui:0.3.2`, revision
`ca-forge-ui-dev--0000012/13`:**
- `Pages/Rooms.razor` — single page owning `/rooms` + `/rooms/{id}` (route change = pane swap, list
  never reloads); sidebar with member-subline room list, `+ New room`, account menu at the foot.
- `Shared/RoomConversation.razor` — conversation extracted into a component keyed on RoomId; header
  is a plain "N members ›" button; the old inline add-agent panel became a **Members modal**.
- Services: `RoomCreationService` (name + seed picked agents), `RoomAdminService` (provisioner-gated
  rename). `OnRoomChanged` EventCallback keeps the sidebar in sync after rename/membership changes.
- Data seam (no schema migration — reuses existing `metadata` jsonb + a query): `GetRoomRosterAsync`
  + `RoomMemberInfo` (member+role in one read), `RenameRoomAsync` (mutates owned `ToJson` metadata).
- `forge.css` §11 (shell) + §12 (modal) added, token-driven; no existing rules touched. Also fixed
  the composer Send button (was missing the `.btn` base → squashed; disabled state white-on-grey).
- **Decisions:** two-pane persistent shell over separate pages; create-room = name + pick agents
  up front; Members modal **consolidates** add-agent (fewer modals, not more); header = plain
  "N members" (not an avatar stack); rename lives inside the Members modal; WhatsApp's
  Info/Media/Starred/Encryption nav column kept **out** (no content for it yet — honest scope).
- Commits `9271719` (shell + modals + rename) and `9c812e9` (Send fix), on `main`. Rollout is a
  manual `az containerapp update` after the `forge-ui-image` workflow (build↔roll gap — see Open
  issue #3); **no DB migration needed.**

## Task 1 — Responsive mobile layout (the real usability win)

Collapse the two panes into **master/detail** on narrow viewports: show the rooms list **or** the
conversation, never both. Tap a room → slide into the conversation; a back affordance returns to
the list. This is how the app behaved *before* the two-pane shell, and how WhatsApp mobile works.

- `@media (max-width: ~720px)`: sidebar becomes full-width when no room is selected (`/rooms`), and
  the conversation becomes full-width when one is (`/rooms/{id}`). The existing route split already
  encodes "which pane" — lean on it rather than adding much new state.
- Add a **back-to-rooms** control in the conversation header on mobile (hidden on desktop, where the
  sidebar is always present).
- Account menu, modals, composer, and mention menu re-checked at phone width (modals already use
  `max-width` + `max-height: 86vh` + scroll, so they should hold; verify).
- **Done when:** the app is comfortable at 375px — list and conversation each get the full width,
  navigation between them is one tap, and nothing overflows horizontally.

## Task 2 — PWA shell (installable, online-only)

Layer installability on top once Task 1 lands. Explicitly **Tier 1 only** — installable, *not*
offline (see Decisions). ~half a day, no architecture change.

- `manifest.webmanifest` — name, `display: standalone`, `start_url`, `theme_color` /
  `background_color` from the `forge.css` tokens (ember + surface), icon set.
- Icons — 192px, 512px, and a maskable variant.
- Meta tags in `src/ForgeUI/Pages/_Host.cshtml` — `theme-color`, `apple-touch-icon`,
  `apple-mobile-web-app-capable` (iOS needs these).
- A **minimal, deliberate service worker**: cache-first for the static shell (`forge.css`, icons,
  framework JS) + a branded offline fallback page; **network-only** (never intercept/cache) for the
  SignalR circuit (`/_blazor`, the WebSocket negotiate) and auth (`/auth/*`, `/signin-oidc`). This
  carve-out is the whole reason the Blazor *Server* template ships no SW — a naïve "cache
  everything" worker silently breaks the circuit or the login redirect.
- **iOS install hint** — a small "📲 Add to Home Screen" affordance with the Safari 3-step ritual;
  on Android/desktop, an optional custom install button via `beforeinstallprompt`.
- **Done when:** the app is installable on Android/desktop (install prompt) and iOS (Add to Home
  Screen), launches standalone with the ember status bar, and the SignalR circuit + OIDC login still
  work in the installed window.

## Decisions / non-goals

- **No offline (by design).** Forge Rooms is Blazor **Server**: every render/click round-trips over
  SignalR, so there is no client-side app to run when the network drops. This app is online-by-nature
  anyway (agents run server-side, live chat), so the one real limitation of a Blazor-Server PWA
  **costs nothing here** — it's a good fit, not a compromise. True offline would mean re-architecting
  the client to Blazor WASM (+ an API layer for everything currently in-process) — a separate project,
  explicitly out of scope.
- **iOS install friction is inherent.** iOS has no install prompt and only installs from Safari
  (not iOS Chrome). Task 2 mitigates with an install hint; it cannot be made automatic.
- **Push notifications — out of scope, noted.** Web Push (VAPID + a server-side sender) works on
  Android/desktop and on iOS 16.4+ *only after* home-screen install. Often the actual reason people
  want a PWA; if desired it's a separate bolt-on, not required for installability.

## Sequencing

1. **Task 1 — responsive mobile layout.** The usability win; worth doing regardless of PWA.
2. **Task 2 — PWA shell.** The "feels like an app" polish; layers cleanly on top.

See also: [UI Design System](../design/ui-design-system.md) (tokenized `forge.css`, dark mode,
primitives) — the responsive breakpoint and PWA theme colors derive from those tokens.
