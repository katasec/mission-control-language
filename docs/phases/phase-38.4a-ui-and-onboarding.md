# Phase 38.4a — UI Foundation, Auth Gating & Onboarding

> **Status: Done (2026-07-07)** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** 38.1–38.4 (rooms, agents, trust surface, identity)
> **Done when:** the app looks/behaves like a real chat product — themed (light+dark), gated
> behind sign-in, and a brand-new user lands in a usable chat instead of an empty list.

Cross-cutting foundation delivered alongside 38.4, ahead of 38.5. Three strands: a proper
design system, a coherent auth information-architecture, and first-run onboarding. The design
system itself is documented separately in
[`docs/design/ui-design-system.md`](../design/ui-design-system.md) (the how-to reference);
this spoke is the **decision log**.

---

## 1. Design system (theming foundation)

**Problem:** `ForgeUI` was a single hand-rolled `forge.css` of hardcoded hex literals, plus
dead default-Blazor scaffold (Bootstrap, open-iconic, `NavMenu`), plus ~30 inline `style=""`
in the newest (Rooms) code. Re-theming meant find-and-replace across scattered literals; no
dark mode; the newest surface was the least themeable.

**Done:**
- `forge.css` is now a **tokenized design system** — one `:root` block of CSS custom properties
  (surfaces, text, brand ember accent, semantic verified/unverified/retry, radii, spacing,
  shadows, type, motion) drives every rule. Re-theme = edit tokens.
- **Dark mode** is automatic via `@media (prefers-color-scheme)` and force-able via
  `[data-theme]` on `<html>` (hook for a future toggle).
- Inline styles pulled into token-driven classes; Rooms got a real chat treatment (avatars,
  bubbles, sticky composer).
- Deleted dead scaffold (`bootstrap/`, `open-iconic/`, `site.css`, `NavMenu.razor`(+css),
  `MainLayout.razor.css`); `Error.cshtml` made self-contained on `forge.css`.
- **No npm/build step** — pure CSS custom properties, AOT/.NET-native. A component library
  (FluentUI Blazor / MudBlazor) can be layered later without redoing this.

**Decision:** tokens over a framework. Bootstrap/Tailwind would discard the existing (good)
bespoke look and, for Tailwind, bolt a Node toolchain onto a .NET project. CSS custom
properties are what those frameworks' themes are built on anyway.

---

## 2. Auth information-architecture ("gate everything")

**Problem:** `/` served full app chrome (Sessions sidebar + chat) to **anonymous** visitors
with no sign-in button, while `/rooms` was `[Authorize]`d and sign-out was buried on the rooms
list. Two disconnected surfaces, no consistent identity control.

**Decision (user's call): gate everything.** Identity is *required* — participants must have
login identities like WhatsApp. Rejected alternatives: public "try-it" demo at `/`, or a
marketing landing. Rooms is the product; the single-player mission chat is a dev/power surface.

**Done:**
- **All app routes `[Authorize]`.** `/` redirects authed→`/rooms`, anon→`/login` (via
  `App.razor` → `RedirectToLogin`).
- The single-player Phase 34/35 mission chat moved from `/` to authed **`/playground`** (owns
  the Sessions rail), reachable via a "Mission playground" link and the `Forge` brand link.
- **`MainLayout` reduced to a thin `@Body`** — each surface composes its own chrome, so the
  Sessions rail no longer bleeds onto Rooms/Login.
- New **`AccountMenu`** component (native `<details>` dropdown, no JS): avatar + name, identity
  + **sign-out** inside. Persistent top-right on Rooms / RoomView / Playground. Room-scoped
  `PROVISIONER` badge kept separate from the account-scoped menu.
- Sign-out loop verified: POST `/auth/logout` → `/` → gate → `/login`.

**Note — OIDC needs HTTPS.** Real Entra sign-in must run on `https://localhost:7177`; the
correlation cookie (`SameSite=None; Secure`) is dropped over plain HTTP → "Correlation failed."
Dev sign-in works over HTTP. See [`docs/design/ui-design-system.md`](../design/ui-design-system.md) §8.

---

## 3. Onboarding — starter "room of two" + verified assistant

**Problem:** a brand-new (real) user landed on an empty rooms list — "ask a provisioner for an
invite" — a dead end, since they're in no room and can't create one. This contradicts the
tenet *"1:1 = a room of two."*

**Done:**
- **`StarterRoomService.EnsureStarterRoomAsync`** (idempotent on "has any room"): auto-creates a
  private **"Getting started"** room with the user as **provisioner** + the `@forge/assistant`
  agent as a member. `RoomList` drops the new user straight in (redirect); returning users see
  their list. An empty-state hint tells them the assistant will reply and answers are verified.
- **1:1 auto-reply (scoped exception to pull-only):** `RoomMessageService` — when a room is
  exactly one human + one agent, the sole agent answers **every** message with no `@mention`;
  group rooms still require explicit addressing. Makes the starter room feel like a normal LLM
  chat, becoming multi-party once people are invited.

**Implementation note (Blazor gotcha):** the create+redirect had to move into
`OnAfterRenderAsync(firstRender)` with its own fresh reads. A prerender `NavigateTo` after an
`await` does not redirect, and `OnInitializedAsync` state isn't ready when `OnAfterRender` first
fires — the first two attempts created the room but didn't drop the user in.

### 3a. `@forge/assistant` — general assistant, LLM-verified

**Decision (user's call):** the default agent is a **general LLM answer verified by an
LLM-as-judge** (`role: judge, kind: llm`), **not** the existing deterministic
`@forge/hallucination-guard`. Rationale: there is no general-purpose deterministic / dynamic
verifier yet (that is the program-synthesis "dynamic guard" spike); a non-deterministic LLM
verifier with a clear mandate in the expert `.md` is the right general default.

- New mission `missions/assistant/` — `mission Assistant(goal) loop(2) = { Answerer -> Verifier }`.
  - `Answerer` (`kind: llm`) — helpful, concise, honest-about-uncertainty; consumes `{{feedback}}` on retry.
  - `Verifier` (`role: judge, kind: llm`) — mandate: no fabrication, responsive, honest about
    uncertainty, coherent; pass reproduces the answer verbatim, fail returns a one-line reason.
  - `forge.toml` (openai provider), hand-written `mcl.lock` (sha256 of each expert.md).
- Wired: registered in `Program.cs` (label `"Assistant"`), mapped `@forge/assistant` in
  `AgentCatalog`, agent member seeded in `RoomsSeeder` (`AssistantId`, `AssistantHandle`).
- **Verified live:** "capital of France" → ✓ Verified "Paris" (trace: Answerer PASS → Verifier PASS).

---

## Files

New: `Shared/AccountMenu.razor`, `Pages/Playground.razor`, `Services/StarterRoomService.cs`,
`missions/assistant/**`.
Changed: `wwwroot/css/forge.css`, `Pages/Index.razor`, `Pages/Login.razor`,
`Pages/RoomList.razor`, `Pages/RoomView.razor`, `Pages/Error.cshtml`, `Shared/MainLayout.razor`,
`Shared/Chat.razor`, `Program.cs`, `Services/AgentCatalog.cs`, `Services/RoomMessageService.cs`,
`ForgeMission.Rooms.Data/RoomsSeeder.cs`.
Deleted: default-Blazor scaffold (see §1).

## Not in scope / follow-ups
Dark-mode toggle UI; a left-rail room switcher (arrives with 38.5); public read-only share links
(38.6); moving `MCL_API_KEY` into user-secrets; Email-OTP display name still shows "unknown".
