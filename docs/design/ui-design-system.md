# Forge UI — Design System

> **Audience:** anyone (human or agent) touching `src/ForgeUI`. Read this before restyling,
> adding a page, or debugging why the app "looks broken." It is the contract for how the UI
> is themed and structured.

The `ForgeUI` project (Blazor Server, .NET 10, **no npm/Node build step**) is styled by a
**single tokenized stylesheet**: [`src/ForgeUI/wwwroot/css/forge.css`](https://github.com/katasec/forge-rooms/blob/main/src/ForgeUI/wwwroot/css/forge.css).
There is no Bootstrap, no Tailwind, no component library. To re-theme the app you edit
**tokens**, not rules.

---

## 1. The one rule

**Never hardcode a colour, radius, or spacing value in a rule or a component.** Everything
reads from a CSS custom property (design token) declared in `:root`. A literal hex like
`#e0e0dc` in a rule is a bug — it won't adapt to dark mode and it fragments the palette.

```css
/* wrong */                          /* right */
.thing { border: 1px solid #e6e4dd; } .thing { border: 1px solid var(--border); }
```

The same applies in `.razor` markup: prefer a class over an inline `style=""`. Inline styles
can't be themed centrally and override the stylesheet. (A handful of layout-only inline styles
like `flex: 1` are tolerable; anything with a colour is not.)

---

## 2. Token catalogue

All tokens live in the `:root` block at the top of `forge.css`, grouped and commented.

| Group | Tokens | Use |
|---|---|---|
| Surfaces | `--bg`, `--surface-sunken`, `--surface`, `--surface-hover`, `--surface-active` | Backgrounds, back-to-front |
| Lines | `--border`, `--border-strong` | Hairlines / emphasis borders |
| Text | `--text`, `--text-muted`, `--text-subtle` | High→low emphasis |
| Brand | `--accent`, `--accent-hover`, `--accent-soft`, `--accent-contrast` | "Forge ember" — links, mentions, focus, active nav |
| Ink | `--ink`, `--ink-hover`, `--ink-contrast` | Primary solid buttons |
| Semantic | `--success{,-bg,-border}`, `--danger{,-bg,-border}`, `--warning{,-bg}` | Success / failure / warning states |
| Radii | `--radius-sm`, `--radius`, `--radius-lg`, `--radius-pill` | Corners |
| Spacing | `--space-1`…`--space-6` (4→32px) | Padding / gaps |
| Elevation | `--shadow-sm`, `--shadow`, `--shadow-lg` | Cards / dropdowns |
| Type | `--font-sans`, `--font-mono` | Font stacks |
| Motion | `--transition` (130ms), `--focus-ring` | Hover/focus |

**To rebrand:** change ~40 lines of tokens at the top of the file. Want a different accent than
the ember orange? Set `--accent` (and `--accent-hover`/`--accent-soft`) and the whole app
follows — links, focus rings, active nav.

---

## 3. Dark mode (mandatory, automatic)

Dark mode is **not optional** — every token has a dark value. It is delivered two ways:

1. **Automatic** — `@media (prefers-color-scheme: dark)` re-declares the tokens under
   `:root:not([data-theme="light"])`, so the app follows the OS with zero JavaScript.
2. **Explicit override hook** — `:root[data-theme="dark"]` / `[data-theme="light"]` on `<html>`
   forces a theme. No toggle UI ships yet, but the plumbing is there: set `data-theme` on the
   root element (persist in `localStorage`) to add one.

`color-scheme` is set so native controls and scrollbars theme too. **Mental test when adding a
rule:** if the background were near-black, would every text element still be readable? If you
used tokens, yes.

### Named product themes and reskins

A product surface may need a visual language different from the default Forge ember theme. Preserve
the system by adding a **named theme token map**, not by copying colour values into a component.
The map overrides the existing semantic tokens (`--bg`, `--surface`, `--text`, `--accent`, `--ink`,
and so on) for that named surface theme; component rules continue to use only those tokens.

Keep product-theme identity distinct from the existing `data-theme="light|dark"` colour-mode hook
so automatic dark mode and an explicit light/dark choice continue to work. A named theme therefore
defines both light and dark token values, and its selector must compose with the colour-mode
selectors. Never force a light mockup's values in a component and call that a theme.

---

## 4. Reusable primitives

Use these before inventing new classes.

- **Buttons:** `.btn` (base) + `.btn-primary` (ink solid) / `.btn-accent` (ember) / `.btn-link`
  (quiet text button, e.g. "sign out", "show thinking").
- **Inputs:** `.field` (36–38px, themed focus ring).
- **Account control:** `.account-menu` — a native `<details>` dropdown (no JS) rendered by the
  `AccountMenu` component; avatar + name summary, identity + sign-out in the panel.
- **Avatars:** `.avatar` + size modifier `.avatar-sm`; initial-in-a-circle.
- **Primary nav:** `.nav-shell` — the `NavShell` component; a bottom tab bar < 640px, a
  slim left rail ≥ 640px. Items are `NavLink`s (`.nav-item` + `.nav-icon`/`.nav-label`); the active
  surface is marked by NavLink's own `.active` (ember text / inset accent bar). Glyphs come from the
  `Icon` component (inline Lucide SVG, `currentColor`). `.skip-link` is the first focusable element.
- **Layout helpers:** `.app-frame`/`.app-content` (nav shell + content region — the `MainLayout`),
  `.page`/`.page-narrow` (centred surfaces), `.surface-head`/`.surface-title`/`.surface-sub`
  (page headers), `.flex-spacer`, `.header-actions`, `.empty-state`.

`forge.css` is organised into numbered sections (breakpoints → tokens → base → primitives →
layout → auth → nav shell → framework error UI). Add new rules to the section they
belong to.

---

## 5. Surface map (which CSS styles which screen)

Every authed surface renders inside the **app shell** (`MainLayout` = `NavShell` + `.app-content`):
a bottom tab bar < 640px / left rail ≥ 640px. `/login` opts out via `BareLayout` (chromeless). The
account control sits at the **rail foot** on wide screens; on a phone it is the **Account tab**.

| Screen | Route | Key classes |
|---|---|---|
| App shell (all authed) | (frames every route below) | `.app-frame` → `NavShell` (`.nav-shell`, `.nav-item`) + `.app-content` (`@Body`) |
| Sign-in gate | `/login` | `BareLayout` (no nav) · `.auth-shell`, `.auth-card`, `.btn-primary` |
| Account | `/`, `/account` | `.page-narrow`, `.account-card`, `.account-head`, `.account-balance`, `.btn` sign-out |

The read-only run viewer ([Phase 78](../phases/phase-78-run-detail-capture.md)) will be the next
surface.

---

## 6. Conventions for new UI

- **New page:** add a `.razor` under `Pages/`, gate it with `@attribute [Authorize]` (see §9),
  compose from existing primitives, and put any new rules in `forge.css` under the right
  section using tokens. Don't add a scoped `*.razor.css` unless the style is genuinely local.
- **Visual reskin:** map the reference to a named product-theme token block in `forge.css`, with
  light and dark values. The task's visual contract records each semantic token, its provenance
  (sampled, derived, accessibility override, or newly chosen), and every foreground/background
  contrast pair used in its required states. A component may select that theme at its boundary, but
  may not redefine colour, type, radius, or spacing tokens locally.
- **Responsive themed surface:** keep reference geometry in named tokens, but use fluid bounded
  values (`min()`, `max()`, `clamp()`, and viewport/percentage terms) where it must fit different
  window sizes. Preserve the approved large viewport as the upper bound; do not add a second set of
  arbitrary component-local pixels merely to make a smaller window fit.
- **Icons:** the `Icon` component renders inline Lucide SVG (`currentColor`); small glyphs may be
  text. If you introduce an icon font, add it deliberately — the old `open-iconic` scaffold was
  deleted on purpose.

---

## 7. Responsive & mobile

The app is **mobile-first from Phase 40 on** — people reach the engine on the phone they carry.

- **Mobile-first authoring (the convention for all new rules).** Base rules target the phone;
  desktop is layered *up* with `@media (min-width: 720px) { … }`. Do **not** author desktop-first
  and walk it down. (Existing pre-40 rules are desktop-first and are *not* retrofitted — the
  convention governs new rules only.)
- **Breakpoints are declared once** in `forge.css` §0 as the canonical block. Custom properties
  **cannot** be used in a `@media` condition, so the pixel values are literals everywhere; the
  `--bp-*` tokens are JS-interop / documentation aids only:
  - `--bp-rail: 640px` — rail ↔ bottom-tab-bar flip
- **`100dvh`, not `100vh`,** for full-height shells (`.app-frame`, `.auth-shell`), with a `100vh`
  fallback line first for old engines. `100vh` ignores the mobile URL bar.
- **Inputs are ≥ 16px** (`.field`). Below 16px, iOS Safari auto-zooms the page on
  focus. 16px is the base body size, so it reads as intentional, not oversized. The rule is
  **inputs only** — small labels/metas stay small.
- **Safe-area insets** are wired via `viewport-fit=cover` (in `_Host.cshtml`) + `--safe-top` /
  `--safe-bottom` tokens (`env(safe-area-inset-*)`). The bottom tab bar pads with `calc(var(--space-3) + var(--safe-bottom))` so they clear the iOS home indicator.
- **`prefers-reduced-motion: reduce`** is honoured globally (base section) — all `--transition`
  hover/focus animations collapse to effectively static.

## 8. Dependency policy (right-sizing vs NIH)

We hand-roll layout and rent
domain-agnostic primitives — but never sacrifice UX to dodge a dependency.

- **Platform-first.** Use Blazor's own building blocks (`NavLink`, layouts, `@attribute
  [Authorize]`) before reaching for anything.
- **Hand-rolled layout.** The nav rail / bottom tab bar are plain CSS + `.razor`
  (flexbox/grid, the §0 breakpoints). No CSS framework, **no npm, no Node, no preprocessor** — the
  stylesheet is one tokenized `forge.css`.
- **SVG icon *assets*, not an icon *runtime*.** Inline `<svg>` (or text glyphs) checked into the
  repo — not an icon font or a JS icon package. See §6.
- **Named escape hatch: FluentUI-Blazor.** If a genuinely complex interactive control is needed
  (virtualized lists, rich pickers), reach for FluentUI-Blazor deliberately rather than
  hand-rolling something worse. Reaching for it is a decision, not a default.
- **UX is never sacrificed to avoid a library.** If avoiding a dependency means shipping a worse
  experience, add the dependency — right-sizing, not not-invented-here purity.

---

## 9. Auth & information architecture (gate everything)

Identity is **required**. The IA:

- **Every app route is `[Authorize]`.** An anonymous visitor is sent to `/login` (via `App.razor` →
  `RedirectToLogin`); `/` is the Account page. The `/auth/*` plumbing (OIDC + dev sign-in) exists.
- **`MainLayout` is the app shell.** It renders `NavShell` + an `.app-content` region hosting
  `@Body`. `/login` opts out via **`BareLayout`** so the shell never frames a logged-out visitor.
- **`AccountMenu` is the persistent identity control** — docked at the **rail foot** on wide
  screens (compact avatar; identity + sign-out in its dropdown). On a phone it is the **Account
  tab**, which also carries balance + sign-out.

---

## 10. Running it locally (and two gotchas that will bite you)

How to run ForgeUI locally (Postgres, Billing packages, the `forge-ui` launch config) is in the
[forge-rooms README](https://github.com/katasec/forge-rooms#readme). HTTPS
(`https://localhost:7177`) is required for real Microsoft/Entra sign-in.

**Gotcha 1 — OIDC needs HTTPS.** The OIDC correlation cookie is `SameSite=None; Secure`, so it
is silently dropped over plain `http://localhost` → real sign-in fails with
`AuthenticationFailureException: Correlation failed`. Do the *entire* login round-trip on
`https://localhost:7177`. Dev sign-in (`/auth/dev?user=alice`) works over HTTP because it sets
no correlation cookie.

**Gotcha 2 — Blazor prerender navigation.** A `NavigateTo` after an `await` during prerender
does **not** redirect, and `OnInitializedAsync` state isn't guaranteed ready when
`OnAfterRenderAsync(firstRender)` first fires. For "do X once after load, then redirect", do the
work self-contained in `OnAfterRenderAsync(firstRender)` with its own fresh reads.

---

## 11. What was deliberately deleted

The default Blazor template scaffold was removed so the styling story is honest (one
stylesheet, no phantom framework): `bootstrap/`, `open-iconic/`, `site.css`, `NavMenu.razor`
(+`.css`), `MainLayout.razor.css`. Don't reintroduce them. `Error.cshtml` is self-contained on
`forge.css`.

Also removed: the Rooms surface (rooms shell, conversation pane, members and agent pickers, the
trust badge and identity seal) and the Library page, deleted in Phase 79 (forge-rooms PR 4), and the
earlier `/playground` mission playground.
