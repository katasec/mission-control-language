# Phase 40 — Forge UI App Shell & Responsive Foundation

> **Status: Design — NEXT ACTION ITEM to implement (chosen 2026-07-12).** Phase 39 groups A+B are
> shipped/live (F&F is billable); the runtime is ahead of the *surface*. This phase turns the
> single-surface, desktop-shaped Blazor app into a **multi-surface, mobile-first app shell** so the
> product has somewhere to put Account, a Mission Library, and future surfaces — and works on the
> phone people actually carry.
>
> **Parent:** grows out of [Phase 38 — Forge Rooms](phase-38-forge-rooms.md) (the UI) ·
> **Design system:** [UI Design System](../design/ui-design-system.md) ·
> **Absorbs:** [Phase 38.8 — Mobile Access](phase-38.8-mobile-access.md) (its Task 1 → [40.3](phase-40.3-responsive-collapse.md), Task 2 → [40.4](phase-40.4-pwa-shell.md)).
>
> **Done when:** a signed-in user lands in a persistent app shell whose navigation (Rooms · Library ·
> Account) is one gesture away on **any** viewport — a left rail on desktop, a bottom tab bar on a
> phone — with Rooms remaining the default surface and every surface comfortable at 375px.

---

## 1. Why this, why now

Today the entire authenticated app is **one surface**. [`Pages/Index.razor`](../../src/ForgeUI/Pages/Index.razor)
redirects `/` → `/rooms`, and [`Pages/Rooms.razor`](../../src/ForgeUI/Pages/Rooms.razor) *is* the app —
a WhatsApp-style two-pane shell (rooms sidebar + conversation). [`MainLayout.razor`](../../src/ForgeUI/Shared/MainLayout.razor)
is a deliberately thin `@Body` pass-through, and the only cross-surface primitive is
[`AccountMenu.razor`](../../src/ForgeUI/Shared/AccountMenu.razor).

Two forces make an app shell the right next investment:

1. **The product needs more surfaces.** Account (identity + prepaid balance from 39.2), a Mission/Agent
   Library (the `/agents` directory, promotable to a real marketplace with 39.5), and later billing,
   settings, shared-output views. There is nowhere to hang these today — every one would be an orphan
   route with no way to reach it.
2. **Mobile is on-thesis and currently broken.** Phase 38's north star is "make the engine reachable,"
   and the reachable device is a phone. The current shell is fixed-desktop (a 288px sidebar *beside* the
   conversation) — unusable at 375px. [Phase 38.8](phase-38.8-mobile-access.md) already scoped the
   responsive/PWA work; this phase is where it lands, on top of a nav layer built responsive from line one.

**The key insight that makes this low-risk:** the navigation layer is *net-new code*. Building it
mobile-first adds no regression surface — we are adding, not modifying. The only existing surface that
changes shape is the Rooms two-pane (→ [40.3](phase-40.3-responsive-collapse.md)), and that collapse is
CSS-driven over routing that already encodes "which pane." So the work is sequenced **additive-first,
modify-existing-last**, quarantining regression risk to a single, testable spoke.

## 2. The chosen architecture — persistent nav (Slack/Discord model)

We evaluated two shapes:

- **A — Dashboard hub.** `/` becomes a landing page of cards (Rooms, Library, Account); each surface is
  its own route. Simple, but every context switch bounces "out" to the hub — and Rooms is the 95% surface,
  so it adds a click to the common path. It also fights the codebase's "own the surface" chrome philosophy
  by demoting Rooms to a sub-page.
- **B — Persistent nav rail (chosen).** A thin app-wide navigation lives in `MainLayout`; each surface
  renders beside it. Rooms stays the default landing; Library/Account are always one click away without a
  hub round-trip. **Crucially, B is the better *mobile* choice, not just a compatible one:** a left rail on
  desktop becomes a **bottom tab bar** on a phone — same routes, repositioned by a media query. That is the
  dominant mobile pattern; A's card-hub gets *worse* on a phone (the bounce-out cost rises).

Desktop and mobile silhouettes (Rooms selected):

```
DESKTOP (≥ rail breakpoint)                 MOBILE (< rail breakpoint)
┌──┬───────────┬────────────────────┐       ┌────────────────────────────┐
│  │           │                    │       │  Rooms list  OR  Conversation │
│R │ rooms     │   conversation     │       │  (master/detail by route)     │
│a │ list      │   pane             │       │                               │
│i │ (288px)   │                    │       │                               │
│l │           │                    │       ├───────────────────────────────┤
│  │           │                    │       │  [Rooms] [Library] [Account]  │  ← bottom tab bar
└──┴───────────┴────────────────────┘       └───────────────────────────────┘
 64px                                        (tab bar hidden in the immersive
                                              conversation detail — back to list)
```

There are **two independent responsive collapses**, handled at the right layers so neither is a rewrite:

| Layer | Owner | Desktop | Mobile |
|---|---|---|---|
| App navigation (net-new) | `MainLayout` + `NavShell` | left rail | bottom tab bar |
| Rooms internal (exists) | `Rooms.razor` | list + conversation side-by-side | one pane, back-nav between them |

## 3. Front-end engineering principles (the contract for this phase)

Right-sized for a solo-founder, AOT-adjacent, **no-npm/no-Node** Blazor Server app. These extend, and do
not replace, the [UI Design System](../design/ui-design-system.md) rules (token-first, two trust seals,
dark mode mandatory).

1. **Token-first, mobile-first.** No hardcoded colour/space/radius (existing rule). **New:** author CSS
   mobile-first — base rules target the phone, `@media (min-width: …)` *layers up* desktop. New surfaces
   are then responsive by default, not "responsive if someone remembers a query."
2. **Right-size dependencies — platform first, assets over runtimes, escape hatch named.**
   - **Prefer the Blazor platform:** `NavLink`/`NavLinkMatch` (free active-state + `aria-current`), layouts
     as the app-shell seam, JS interop only where CSS/HTML can't reach. This *is* the Microsoft best practice
     for this size — not a component-library dependency.
   - **Hand-roll the layout/nav in semantic HTML + CSS grid/flex.** A rail + bottom tab bar + two thin pages
     is not a job for MudBlazor/Radzen/FluentUI-Blazor: those bring rival theming, a second CSS reset, JS
     bundles, and large surface area that would *fight* the tokenized `forge.css` that is a genuine asset.
     Staying library-free here is right-sizing, **not** NIH.
   - **Reach for OSS *assets*, not OSS *runtimes*, when an asset suffices.** Nav icons = inline **SVG paths
     copied from a permissive set (Lucide, MIT)** — no icon font, no package, no build step. That honours
     "lean on open source, don't hand-draw icons" without adding a runtime dependency.
   - **Named escape hatch:** if a genuinely complex primitive later appears (virtualized data grid, date
     picker, typeahead combobox), adopt **[FluentUI-Blazor](https://www.fluentui-blazor.net/)** (Microsoft's
     own, closest to the platform) for *that component only* — deliberately, not pre-emptively.
   - **UX is never sacrificed to avoid a library.** If hand-rolling would produce a worse experience than a
     fit-for-purpose library, we take the library. The bar is "does the UX need it," not "can we avoid it."
3. **Progressive enhancement / graceful degradation.** Prefer patterns that survive a dropped SignalR
   circuit or no-JS render (the `AccountMenu` native `<details>` is the model). On mobile, the reconnect
   banner (`#blazor-error-ui`) must be legible and non-blocking — flaky networks are the norm.
4. **Semantic HTML + accessibility landmarks.** `<nav aria-label>`, a skip-to-content link, `aria-current`
   on the active nav item (via `NavLink`), visible focus (the existing `--focus-ring`), and
   `prefers-reduced-motion` honoured. Touch targets ≥ 44px (already the button/field height).
5. **Thin pages, logic in services.** Pages stay declarative; data comes from existing services
   (`CurrentUser`, `BillingService`, `AgentRegistry`, `IReadStore`). No new state machine for the collapse —
   lean on the route (`/rooms` vs `/rooms/{id}`) as the source of truth.
6. **Carry the trust surface into every new surface.** Any place showing agent identity uses the
   `.identity-seal` (gold/blue), never the green verified badge — see design-system §5. The Library must not
   fake a green ✓.

## 4. Spokes (dependency-ordered — implement in this sequence)

| Spoke | Scope | Depends | Regression risk |
|---|---|---|---|
| [40.1 — Design System Foundation](phase-40.1-design-system-foundation.md) | Mobile primitives + conventions, applied once so every surface inherits them: prune dead CSS, `100vh→100dvh`, iOS input-zoom fix, `prefers-reduced-motion`, safe-area insets, breakpoint convention + mobile-first authoring. Update the design-system doc. | none | **None** (global-safe / additive) |
| [40.2 — App Navigation Shell](phase-40.2-app-navigation-shell.md) | The nav layer: `NavShell` (rail↔bottom-tab-bar) wired into `MainLayout`; inline-SVG icon set; relocate the account control; new `/account` (identity + balance) and `/library` (read-only agent directory) surfaces. | 40.1 | **Low** (net-new; Rooms gains a rail beside it) |
| [40.3 — Responsive Surface Collapse](phase-40.3-responsive-collapse.md) | Rooms two-pane → master/detail on mobile; back-to-rooms control; immersive conversation (tab bar hidden on detail). **Absorbs 38.8 Task 1.** | 40.2 | **Contained** (the one modified surface — regression-test here) |
| [40.4 — PWA Shell](phase-40.4-pwa-shell.md) | Installable (online-only): manifest + icons from tokens, deliberate service worker (network-only for `/_blazor` + `/auth/*`), iOS add-to-home hint. **Absorbs 38.8 Task 2.** | 40.3 | Low (additive) |

## 5. Decisions locked (so spokes don't re-litigate)

- **Model B (persistent nav), not a dashboard hub.** Rooms stays the default landing; `/` → `/rooms`
  unchanged. The "landing page" is the shell itself with Rooms selected, plus reachable Library/Account.
- **`MainLayout` stops being thin.** It becomes the nav shell (rail/tab-bar + content region). This is a
  deliberate reversal of design-system §8's "MainLayout is a thin `@Body`" — update that doc in 40.1/40.2.
- **Two left columns on desktop** for Rooms: app rail (~64px, icons) **+** rooms list (~288px) + conversation.
  The rooms-list sidebar stays; the **account control moves up to the rail foot** (and becomes the Account
  tab on mobile), so it is no longer duplicated in the rooms sidebar foot.
- **Mobile conversation is immersive.** On `/rooms/{id}` at phone width the bottom tab bar is **hidden**
  (full-screen chat, WhatsApp-style); a back control returns to the list, where the tab bar reappears. This
  resolves the "two bottom bars" conflict (tab bar vs composer) cleanly.
- **Library v1 = read-only agent directory** sourced from `AgentRegistry.List()` (handle · description ·
  publisher · identity seal) — real content today, no new backend. It becomes a true marketplace when 39.5
  lands user-authored missions. **Account v1** = identity (`CurrentUser`/`ForgeClaims`) + balance
  (`BillingService.GetBalanceMicroUsdAsync`, micro-USD → USD) + sign-out — also real content today.
- **Icons: inline SVG (Lucide, MIT), not a font/package.** No npm, no build step.
- **PWA: Tier-1 installable, no offline** — Blazor *Server* is online-by-design; carried from 38.8's
  decision log (a good fit here, not a compromise).

## 6. Building, running & verifying locally

A cold agent needs this to execute — full detail (and two auth gotchas) live in
[UI Design System §9](../design/ui-design-system.md#9-running-it-locally-and-two-gotchas-that-will-bite-you).

**Run it.** There is a ready launch config `forge-ui` in [`.claude/launch.json`](../../.claude/launch.json)
(`dotnet run` on `http://localhost:5286`, `ASPNETCORE_ENVIRONMENT=Development`). Use `preview_start forge-ui`
— for the pure UI/layout work in 40.1–40.3 that is all you need. **Sign in with dev sign-in over HTTP:**
navigate to `/auth/dev?user=alice` (it sets no OIDC correlation cookie, so it works on plain `http://`).

**Three gotchas that will make you think you broke something:**
1. **The Library / agent lists can be legitimately empty locally.** `AgentRegistry` is built **once at
   boot** from the runner's `GET /missions` probe ([`Program.cs:124-131`](../../src/ForgeUI/Program.cs)). If
   the `ForgeMission.Runner` isn't running, `availableMissionRefs` is empty → **no agents bind** → `/library`
   (40.2 Task 5), the create-room agent picker, and `@`-mentions all show nothing. This is expected, **not a
   bug in your layout.** To verify Library with real rows, run the runner with `MCL_API_KEY` set; to verify
   the *layout* only, the empty state is fine. (Design-system §9 gotcha 2: `MCL_API_KEY` must be in the
   server's environment or the registry stays empty.)
2. **Real OIDC login needs HTTPS** (`https://localhost:7177`) — the correlation cookie is `SameSite=None;
   Secure` and is dropped over plain HTTP. Only **40.4** (testing sign-in inside the installed PWA window)
   needs this; 40.1–40.3 can use dev sign-in over HTTP. (Design-system §9 gotcha 1.)
3. **Blazor prerender navigation** — a `NavigateTo` after an `await` during prerender doesn't redirect;
   relevant only if you touch the Rooms onboarding path in 40.3. (Design-system §9 gotcha 3.)

**Verify it.** Because most of this is CSS/layout, verify with the browser preview tooling at three widths —
**375px (mobile), 768px (tablet), ≥1024px (desktop)** — and in **dark mode** — not by screenshots alone:
`preview_resize` to flip the breakpoints, `preview_inspect` for computed CSS, `preview_snapshot` for
structure/a11y, `preview_click` to exercise nav + the master/detail collapse, and
`preview_eval document.documentElement.scrollWidth <= window.innerWidth` to assert no horizontal overflow.
Every spoke's "Done when" is a viewport-checked behaviour, not "it compiles."

## 7. Relationship to other phases

- **38.8** is absorbed here (see §4). Its baseline notes (the shipped two-pane shell, `forge-ui:0.3.2`)
  remain the starting point for 40.3.
- **39.5** (custom missions / save-as-agent) upgrades the Library from a read-only directory to an authored
  marketplace, and gives Account a "my agents" section. 40.2 leaves those seams stubbed, not built.
- **39.6** (Stripe top-up / tiers) gives Account a "top up" action against the balance 40.2 already displays.

## 8. Shipping it

Phase 40 is **all ForgeUI** (no runner change), so the deploy path is just: build the `forge-ui` image,
roll `ca-forge-ui-dev`, verify live. Full runbook (commands, the build≠deploy gap, gotchas) →
[Deploy Runbook](../design/deploy.md). Verify locally first (§6) before cutting an image.
