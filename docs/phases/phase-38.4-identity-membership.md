# Phase 38.4 — Identity & Membership

> **Status: Done** (real-OIDC sign-in hop pending Entra credentials) · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** 38.1 (hardens the multi-user story behind 38.2/38.3)
> **Done when:** a real person taps an invite link, signs in with Google, and lands inside
> the room; a non-member is denied both the hub connection and the room history.

**Identity provider:** Microsoft **Entra External ID**, wired as a *standard* OIDC provider
(no B2C custom policies) so the exit from any one IdP stays cheap. Google/Apple federate
*inside* the Entra tenant — the app holds a single OIDC registration. Authority/ClientId/
ClientSecret are drop-in config (`Oidc:*`, empty locally → dev sign-in). Membership is
**authorization** and stays FK-enforced in Postgres, keyed by the IdP `(issuer, subject)` —
never modelled as IdP groups/roles.

Replaces the dev-stub identity with real federated auth, adds invite-driven onboarding, and
makes room membership the enforced confidentiality boundary (tenet 3).

## Tasks (dependency order)

1. ✅ **Federated OIDC.** Cookie session + OpenID Connect (Entra External ID) wired in
   `Program.cs`; when `Oidc:*` is unconfigured (local), a `/auth/dev` endpoint drives the
   **same** cookie + provisioning path — only the identity source differs. The 38.1 dev stub
   (`StubIdentity` + user picker) is deleted; `App.razor` uses `AuthorizeRouteView`, unauthenticated
   visitors redirect to `/login`.
2. ✅ **User ↔ Member linkage.** `Member` gains `Subject`/`Issuer`/`Email` (unique filtered
   index on `(issuer, subject)`). `MemberProvisioningService` just-in-time provisions/looks up
   the Member from the principal; `CurrentUser` resolves it for the circuit. Attribution now uses
   real identity + verified email.
3. ✅ **Invite-link flow.** `RoomInvite` (opaque token, granted role, optional TTL). Provisioner
   clicks *+ create invite link*; `/invite/{token}` challenges sign-in if needed, then
   idempotently adds membership with the invite's role and redirects into the room.
4. ✅ **Confidentiality enforcement.** `ChatHub` is `[Authorize]` and derives the acting member
   from the connection principal (never a client-supplied id); membership re-checked on join and
   send. The Blazor client no longer self-connects to the hub — it subscribes to an in-process
   `RoomBroadcaster` from its already-authenticated circuit (the hub stays for external clients).
   `RoomView`/`RoomList` gate on `GetMembershipAsync`; a non-member gets nothing.
5. ✅ **Provisioner vs consumer roles.** `MembershipRole` on membership. Invite creation is
   provisioner-gated (server-side in `InviteService` *and* the button is provisioner-only);
   consumers can only talk. (Add-agent gating lands with the registry in 38.5.)
6. ✅ **Verify.** Live via dev auth (identical pipeline to real OIDC): unauth `/rooms` → `/login`;
   Bob (consumer) — no invite button, can chat + @mention (agent converges to ✓ Verified through
   the new broadcaster path); Alice (provisioner) — creates an invite link; Carol (new user) —
   JIT-provisioned, zero rooms, **denied** Demo Room directly (no history), then **tap invite →
   joined as consumer → in room**. 23 tests pass (17 + 6 new identity/invite). The only unverified
   step is the real Entra/Google sign-in hop (needs tenant credentials).

## Notes
Email magic-link fallback is a later add, not this phase. E2E encryption is a future
hardening (parent Q7) — rooms are the boundary here, enforced server-side.

**To go live with real sign-in:** register an Entra External ID app (external tenant), add
Google/Apple as identity providers in the tenant, then set `Oidc:Authority`, `Oidc:ClientId`,
`Oidc:ClientSecret` (env/Key Vault — never committed) and the redirect URI `…/signin-oidc`.

## Not in scope
Registry/scope (38.5 — scope depends on this phase's identity), sharing (38.6), org/tenant
management, SSO for enterprises.
