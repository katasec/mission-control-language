# Forge UI — Design System

> **Audience:** anyone (human or agent) touching `src/ForgeUI`. Read this before restyling,
> adding a page, or debugging why the app "looks broken." It is the contract for how the UI
> is themed and structured.

The `ForgeUI` project (Blazor Server, .NET 10, **no npm/Node build step**) is styled by a
**single tokenized stylesheet**: [`src/ForgeUI/wwwroot/css/forge.css`](../../src/ForgeUI/wwwroot/css/forge.css).
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
| Semantic | `--success{,-bg,-border}`, `--danger{,-bg,-border}`, `--warning{,-bg}` | Verified / unverified / retry (per-**response**) |
| Seal | `--seal-official` (gold), `--seal-verified` (blue), `--seal-check` | Per-**agent** identity seal — a different claim; see §5 |
| Radii | `--radius-sm`, `--radius`, `--radius-lg`, `--radius-pill` | Corners |
| Spacing | `--space-1`…`--space-6` (4→32px) | Padding / gaps |
| Elevation | `--shadow-sm`, `--shadow`, `--shadow-lg` | Cards / dropdowns |
| Type | `--font-sans`, `--font-mono` | Font stacks |
| Motion | `--transition` (130ms), `--focus-ring` | Hover/focus |

**To rebrand:** change ~40 lines of tokens at the top of the file. Want a different accent than
the ember orange? Set `--accent` (and `--accent-hover`/`--accent-soft`) and the whole app
follows — links, `@mentions`, focus rings, active session, own-message bubbles.

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

---

## 4. Reusable primitives

Use these before inventing new classes.

- **Buttons:** `.btn` (base) + `.btn-primary` (ink solid) / `.btn-accent` (ember) / `.btn-link`
  (quiet text button, e.g. "sign out", "show thinking").
- **Inputs:** `.field` (36–38px, themed focus ring). The chat composer uses `.chat-input` /
  `.room-input` which share the same tokens.
- **Account control:** `.account-menu` — a native `<details>` dropdown (no JS) rendered by the
  `AccountMenu` component; avatar + name summary, identity + sign-out in the panel.
- **Avatars:** `.avatar` + size modifier `.avatar-sm`; initial-in-a-circle.
- **Layout helpers:** `.app-shell`/`.app-main` (sidebar + main), `.page`/`.page-narrow`
  (centred surfaces), `.flex-spacer`, `.header-actions`.

`forge.css` is organised into numbered sections (tokens → base → primitives → layout → chat →
agent card/trust/trace → rooms → auth → framework error UI). Add new rules to the section they
belong to.

---

## 5. Trust surface: two distinct seals (never conflate them)

Trust is the product's core differentiator, so the system carries **two separate trust marks
that make two different claims.** Collapsing them — especially letting either go green when it
shouldn't — is the one styling bug that damages the product, not just the polish.

| Mark | Claim | Scope | Vocabulary | Colour |
|---|---|---|---|---|
| **Verified badge** | "*this run* was verified" | per-**message** | `.trust-badge` + `.verified`/`.unverified`/`.not-verified`, `.card-verified`/`.card-unverified`/`.card-raw` | green `--success` / red `--danger` / neutral `--text-subtle` |
| **Identity seal** | "*who* this is, is legit" | per-**agent** (on the handle) | `.identity-seal` + `.seal-official`/`.seal-verified` | **gold** `--seal-official` / **blue** `--seal-verified` |

The **verified badge** is a *tri-state*: green `✓ Verified` (a verifier ran and passed), red
`✗ Unverified` (a verifier ran and failed), and neutral grey `○ Not verified` (a **raw-model
agent** — no verifier exists, so it is neither pass nor fail, 38.5 task 7). Green is earned only
by real verification; a raw `@claude`/`@openai` gets the neutral chip *and* the gold identity
seal (`AgentDescriptor.VerifiesAnswers` gates the green). Never render a raw answer green or red.

Rules:

- **The identity seal is never green and never red.** Green is reserved for "verified run,"
  red for "unverified run." A seal borrowing those hues would read as a per-response verdict —
  the false-green the whole trust surface exists to prevent.
- **Two seal levels, two hues:** gold = `IdentitySeal.Official` (Forge's own built-ins), blue =
  `IdentitySeal.Verified` (a verified third-party publisher). `IdentitySeal.None` shows no seal.
- **Different shapes, on purpose:** the verified badge is a *pill*; the identity seal is a small
  *filled check disc* on the handle (X-checkmark style). Shape + hue + position keep them from
  reading as the same thing even though both contain a check.
- **They are independent.** A raw `@claude` may carry an Official identity seal *and* get **no**
  verified badge on its answers (unverified by design) — that exact contrast is the product
  story. Never green-check raw model output.

The domain side is the `IdentitySeal` enum + `AgentDescriptor.Seal` in `ForgeMission.Rooms`;
the per-response side is `AgentMeta.Verified` (38.3). Keep them separate in markup too.

---

## 6. Surface map (which CSS styles which screen)

Since 2026-07-10 the rooms surface is a **persistent two-pane shell** (`forge-ui:0.3.2`): one page
component (`Pages/Rooms.razor`) owns both `/rooms` and `/rooms/{id}`, so switching rooms is a route
change on the same component — the sidebar never reloads. The old separate `RoomList.razor` /
`RoomView.razor` routes were removed; the conversation lives in `Shared/RoomConversation.razor`
(keyed on RoomId). See [Phase 38.8 baseline](../phases/phase-38.8-mobile-access.md) for the shipped
detail + decisions.

| Screen | Route | Key classes |
|---|---|---|
| Sign-in gate | `/login` | `.auth-shell`, `.auth-card`, `.btn-primary` |
| Rooms shell | `/rooms` + `/rooms/{id}` | `.app-shell` + `.rooms-nav` (sidebar: `.rooms-nav-header`, `.rooms-new`, `.room-item` + member subline, `.rooms-account` → `AccountMenu`) + `.app-main` |
| Conversation pane | (right pane of the shell) | `.convo`, `.convo-header`, `.convo-name`, `.convo-members-btn` ("N members ›"), `.room-stream`, `.room-msg`, `.msg-bubble`, `.agent-card`, `.room-composer`, `.room-hint`, `.convo-empty` |
| Create-room modal | (from `.rooms-new`) | `.modal-backdrop`, `.modal`, `.field`, `.agent-pick` (name + pick-agents-up-front) |
| Members modal | (from `.convo-members-btn`) | `.modal`, rename (`.modal-name-row`), `.member-row` + `.member-role`, agents (`.member-agent-handle` + `.identity-seal` + remove), `.agent-pick` add-agent, invite — consolidates the old inline add-agent panel |
| `/agents` directory (38.5) | (inline in a room) | slash-command output rendered as a listing **in the room stream** (not a route): handle · description · publisher + `.identity-seal`. Reuse room-message layout, not a new `.page`. |
| Trust card / trace | (in the conversation pane) | per-response: `.agent-card`, `.trust-badge`, `.verified`/`.unverified`, `.trace-panel`; per-agent: `.identity-seal` — see §5 |

---

## 7. Conventions for new UI

- **New page:** add a `.razor` under `Pages/`, gate it with `@attribute [Authorize]` (see §8),
  compose from existing primitives, and put any new rules in `forge.css` under the right
  section using tokens. Don't add a scoped `*.razor.css` unless the style is genuinely local.
- **New agent card / message type:** reuse `.agent-card` + `.trust-badge` so the trust surface
  stays consistent (this is the product's core differentiator — never fake a green ✓).
- **Anything showing agent identity:** use `.identity-seal` (gold/blue), **never** the green
  verified badge, for "who this is." The two trust marks are separate by design — see §5.
- **Icons:** the app currently uses inline text glyphs (✓ ✗ ▾ ←). The identity seal uses a
  bold check *inside a filled disc* (`.identity-seal`) so it never collides with the flat `✓`
  the verified badge owns. If you introduce an icon font, add it deliberately — the old
  `open-iconic` scaffold was deleted on purpose.

---

## 8. Responsive & mobile

The app is **mobile-first from Phase 40 on** — people reach the engine on the phone they carry.
Foundation laid in [`phase-40.1`](../phases/phase-40.1-design-system-foundation.md):

- **Mobile-first authoring (the convention for all new rules).** Base rules target the phone;
  desktop is layered *up* with `@media (min-width: 720px) { … }`. Do **not** author desktop-first
  and walk it down. (Existing pre-40 rules are desktop-first and are *not* retrofitted — the
  convention governs new rules only.)
- **Two breakpoints, declared once** in `forge.css` §0 as the canonical block. Custom properties
  **cannot** be used in a `@media` condition, so the pixel values are literals everywhere; the
  `--bp-*` tokens are JS-interop / documentation aids only:
  - `--bp-rail: 640px` — rail ↔ bottom-tab-bar flip (40.2)
  - `--bp-panes: 720px` — rooms master/detail ↔ two-pane flip (40.3)

  (Below 640 = phone → bottom tab bar + single pane; 640–720 = large phone / small tablet;
  ≥720 = full two-pane. Implementers may collapse to just 720 if 640 proves unnecessary — decide
  in 40.2/40.3 and keep the decision in that §0 block.)
- **`100dvh`, not `100vh`,** for full-height shells (`.app-shell`, `.auth-shell`, `.convo`), with a
  `100vh` fallback line first for old engines. `100vh` ignores the mobile URL bar and hides the
  composer behind it.
- **Inputs are ≥ 16px** (`.field`, `.invite-field`). Below 16px, iOS Safari auto-zooms the page on
  focus. 16px is the base body size, so it reads as intentional, not oversized. The rule is
  **inputs only** — small labels/metas stay small.
- **Safe-area insets** are wired via `viewport-fit=cover` (in `_Host.cshtml`) + `--safe-top` /
  `--safe-bottom` tokens (`env(safe-area-inset-*)`). The composer and bottom tab bar (40.2/40.3)
  pad with `calc(var(--space-3) + var(--safe-bottom))` so they clear the iOS home indicator.
- **`prefers-reduced-motion: reduce`** is honoured globally (base section) — the thinking-pulse and
  all `--transition` hover/focus animations collapse to effectively static.

## 9. Dependency policy (right-sizing vs NIH)

Mirrors [Phase 40 §3.2](../phases/phase-40-forge-ui-shell.md). We hand-roll layout and rent
domain-agnostic primitives — but never sacrifice UX to dodge a dependency.

- **Platform-first.** Use Blazor's own building blocks (`NavLink`, layouts, `@attribute
  [Authorize]`) before reaching for anything.
- **Hand-rolled layout.** The nav rail / bottom tab bar / two-pane shell are plain CSS + `.razor`
  (flexbox/grid, the §0 breakpoints). No CSS framework, **no npm, no Node, no preprocessor** — the
  stylesheet is one tokenized `forge.css`.
- **SVG icon *assets*, not an icon *runtime*.** Inline `<svg>` (or text glyphs) checked into the
  repo — not an icon font or a JS icon package. See §7.
- **Named escape hatch: FluentUI-Blazor.** If a genuinely complex interactive control is needed
  (virtualized lists, rich pickers), reach for FluentUI-Blazor deliberately rather than
  hand-rolling something worse. Reaching for it is a decision, not a default.
- **UX is never sacrificed to avoid a library.** If avoiding a dependency means shipping a worse
  experience, add the dependency — right-sizing, not not-invented-here purity.

---

## 10. Auth & information architecture (gate everything)

Identity is **required** (WhatsApp-style: participants must be signed in). The IA:

- **Every app route is `[Authorize]`.** `/` redirects authed→`/rooms`, anon→`/login` (via
  `App.razor` → `RedirectToLogin`). All the `/auth/*` plumbing (OIDC + dev sign-in) already
  exists.
- **`MainLayout` is a thin `@Body`.** Each surface composes its own chrome: `/rooms*` is the
  two-pane shell with the account control docked at the sidebar foot; `/login` is bare.
  > **⚠️ Changing in 40.2.** [Phase 40.2](../phases/phase-40.2-app-navigation-shell.md) rewrites
  > `MainLayout` into the persistent app shell (nav rail ↔ bottom tab bar; Rooms · Library ·
  > Account). This "thin `@Body`" description is about to be superseded — update this bullet then.
- **`AccountMenu` is the persistent identity control**, top-right on every authed surface —
  avatar + name, with sign-out inside. Logged-out, the gate *is* the sign-in affordance.

See the spoke [`phase-38.4a-ui-and-onboarding.md`](../phases/phase-38.4a-ui-and-onboarding.md)
for the full decision log.

---

## 11. Running it locally (and two gotchas that will bite you)

```bash
make dev-up   # Postgres for Rooms
# HTTP (fast, dev sign-in only — OIDC will fail, see below):
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5286 \
  dotnet run --project src/ForgeUI/ForgeUI.csproj --no-launch-profile
# HTTPS (required for real Microsoft/Entra sign-in):
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=https://localhost:7177 \
  MCL_API_KEY="$MCL_API_KEY" dotnet run --project src/ForgeUI/ForgeUI.csproj --no-launch-profile
```

**Gotcha 1 — OIDC needs HTTPS.** The OIDC correlation cookie is `SameSite=None; Secure`, so it
is silently dropped over plain `http://localhost` → real sign-in fails with
`AuthenticationFailureException: Correlation failed`. Do the *entire* login round-trip on
`https://localhost:7177`. Dev sign-in (`/auth/dev?user=alice`) works over HTTP because it sets
no correlation cookie. (Details: [`project_forge_infra_auth`] memory / `katasec/forge-infra`.)

**Gotcha 2 — `MCL_API_KEY` must be in the server's environment.** The mission registry only
loads when the key is present; without it the registry is empty and agents answer "No mission
is bound to @… yet." The key lives in the user's shell profile — a background/detached process
won't inherit it unless you pass it explicitly (`MCL_API_KEY="$MCL_API_KEY"` after sourcing the
profile). A cleaner long-term fix is to move it into ForgeUI user-secrets alongside the OIDC
secrets.

**Gotcha 3 — Blazor prerender navigation.** A `NavigateTo` after an `await` during prerender
does **not** redirect, and `OnInitializedAsync` state isn't guaranteed ready when
`OnAfterRenderAsync(firstRender)` first fires. For "do X once after load, then redirect" (e.g.
starter-room onboarding), do the work self-contained in `OnAfterRenderAsync(firstRender)` with
its own fresh reads — see `Pages/RoomList.razor`.

---

## 12. What was deliberately deleted

The default Blazor template scaffold was removed so the styling story is honest (one
stylesheet, no phantom framework): `bootstrap/`, `open-iconic/`, `site.css`, `NavMenu.razor`
(+`.css`), `MainLayout.razor.css`. Don't reintroduce them. `Error.cshtml` is self-contained on
`forge.css`.

Also removed: `RoomList.razor` + `RoomView.razor` (their `/rooms` and `/rooms/{id}` routes were
absorbed into the single-page two-pane shell `Pages/Rooms.razor`, 2026-07-10) and the earlier
`/playground` + `Chat.razor` mission playground (retired in Phase 39.1 when execution moved to the
containerized runner). `MainLayout.razor` is a thin `@Body` passthrough — each surface composes its
own chrome.
