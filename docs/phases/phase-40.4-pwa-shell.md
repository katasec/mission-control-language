# Phase 40.4 — PWA Shell (installable, online-only)

> **Status: Design** · **Parent:** [Phase 40 — App Shell](phase-40-forge-ui-shell.md) ·
> **Depends on:** [40.3](phase-40.3-responsive-collapse.md) · **Absorbs:** [Phase 38.8 Task 2](phase-38.8-mobile-access.md) ·
> **Regression risk:** low (additive — but the service worker is the one place a naïve change breaks the
> SignalR circuit or OIDC login; the carve-outs below are mandatory).
>
> **Done when:** the app is installable on Android/desktop (install prompt) and iOS (Add to Home Screen),
> launches standalone with the ember status bar, and the SignalR circuit + OIDC login still work in the
> installed window.

Layers "feels like an app" onto the responsive shell from 40.1–40.3. Explicitly **Tier-1 installable, not
offline** — Blazor *Server* round-trips every render over SignalR, so there is no client app to run offline;
this app is online-by-nature anyway, so the one Blazor-Server-PWA limitation costs nothing here. (Full offline
= a Blazor WASM rewrite + an API layer — a separate project, out of scope.)

## Context an implementer needs

- Theme/background colours come from `forge.css` tokens — `--accent` (ember) and `--surface`/`--bg`. The
  manifest and iOS meta must use the **same literal values** the tokens resolve to (a manifest can't read CSS
  vars), so copy the hex from `:root`.
- Meta tags go in the `<head>` of [`Pages/_Host.cshtml`](../../src/ForgeUI/Pages/_Host.cshtml) (already holds
  the viewport + favicon links; 40.1 added `viewport-fit=cover`).
- **Why the default Blazor Server template ships no service worker:** a "cache everything" SW silently breaks
  the SignalR circuit (`/_blazor`, the WebSocket negotiate) and the OIDC redirect (`/auth/*`,
  `/signin-oidc`). Those paths must be **network-only, never intercepted**. This carve-out is the entire
  reason to hand-write a *deliberate* worker rather than drop in Workbox.
- **No npm.** The service worker is ~30–40 lines of vanilla JS in `wwwroot/`; the manifest is static JSON.
  Do not introduce a Node build or Workbox for this.

## Tasks (chronological)

### Task 1 — Web app manifest

1. Add `wwwroot/manifest.webmanifest`: `name` ("Forge"), `short_name` ("Forge"), `display: standalone`,
   `start_url: "/rooms"`, `scope: "/"`, `theme_color` = the ember `--accent` hex, `background_color` = the
   `--bg`/`--surface` hex, `orientation: "portrait"` (optional), and the `icons` array (Task 2).
2. Link it in `_Host.cshtml`: `<link rel="manifest" href="manifest.webmanifest" />`.
- **Done when:** DevTools → Application → Manifest parses with no errors and shows the icons + colours.

### Task 2 — Icons (192 / 512 / maskable)

1. Add `wwwroot/icons/` with `icon-192.png`, `icon-512.png`, and a **maskable** variant
   (`purpose: "maskable"`, safe-zone padding so Android's mask doesn't clip). Derive from the Forge mark /
   favicon; ember on surface.
2. Reference all in the manifest `icons` array with correct `sizes`/`type`/`purpose`.
- **Done when:** the install preview shows a correct, non-clipped icon on Android and desktop.

### Task 3 — iOS meta + apple-touch-icon

1. In `_Host.cshtml` `<head>`, add: `<meta name="apple-mobile-web-app-capable" content="yes">`,
   `<meta name="apple-mobile-web-app-status-bar-style" content="default">`, `<meta name="theme-color"
   content="…ember hex…">`, and `<link rel="apple-touch-icon" href="icons/icon-192.png">` (iOS ignores the
   manifest icons for the home-screen glyph).
- **Done when:** iOS "Add to Home Screen" uses the Forge icon and launches standalone with the ember bar.

### Task 4 — Deliberate service worker (the load-bearing task)

1. Add `wwwroot/service-worker.js` — minimal and explicit:
   - **Network-only, never intercept:** any request whose path starts with `/_blazor`, `/auth`,
     `/signin-oidc`, `/signout`, or is a WebSocket/EventSource. Return early from `fetch` for these (let the
     network handle them) — this is the carve-out that keeps the circuit + login alive.
   - **Cache-first for the static shell only:** `forge.css`, the icons, `favicon.png`, and the Blazor
     framework JS (`_framework/blazor.server.js`). Precache on `install`; serve from cache, fall back to
     network.
   - Optional: a tiny branded offline fallback page for a navigation request that fails (since the app is
     online-only, this is just a friendly "you're offline" card, not a functional offline mode).
   - Bump a `CACHE_VERSION` const and clean old caches on `activate`.
2. Register it from a small inline script in `_Host.cshtml` (after the Blazor script):
   `if ('serviceWorker' in navigator) navigator.serviceWorker.register('/service-worker.js');`
3. **Test the carve-out explicitly:** install, then in the installed/standalone window confirm (a) a live
   `@mention` still streams (circuit intact) and (b) sign-out → sign-in round-trips (OIDC intact). If either
   breaks, the path filter in step 1 is wrong — that is the whole risk of this spoke.
- **Done when:** the shell loads from cache on repeat visits, and the SignalR circuit + OIDC login work
  unchanged in the installed window.

### Task 5 — iOS install hint (+ optional Android/desktop button)

1. Add a small, dismissible "📲 Add to Home Screen" affordance shown only on iOS Safari (no `beforeinstallprompt`
   there, Safari-only, no auto-prompt) with the 3-step ritual (Share → Add to Home Screen). Persist dismissal
   in `localStorage`.
2. Optional: on Android/desktop capture `beforeinstallprompt` and show a custom "Install Forge" button.
- **Done when:** an iOS user gets a one-time hint; Android/desktop users get a native or custom install path.

## Decisions / non-goals (carried from 38.8)

- **No offline** (by design — Blazor Server; a good fit, not a compromise). True offline = WASM rewrite, out
  of scope.
- **iOS install friction is inherent** — Safari-only, no prompt; the hint mitigates, cannot automate.
- **Push notifications — out of scope, noted.** Web Push works on Android/desktop and iOS 16.4+ *after*
  install; a separate bolt-on if wanted, not required for installability.

## Verify

Install on desktop Chrome (install prompt) and, if a device is available, iOS Safari (Add to Home Screen).
Confirm standalone launch, ember status bar, and — critically — a live `@mention` + a sign-out/sign-in
round-trip **inside the installed window** (the circuit + OIDC carve-out). Lighthouse PWA audit as a sanity
check (installable: pass; offline: N/A by design).
