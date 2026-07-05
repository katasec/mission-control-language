# Phase 38.4 — Identity & Membership

> **Status: Todo** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** 38.1 (hardens the multi-user story behind 38.2/38.3)
> **Done when:** a real person taps an invite link, signs in with Google, and lands inside
> the room; a non-member is denied both the hub connection and the room history.

Replaces the dev-stub identity with real federated auth, adds invite-driven onboarding, and
makes room membership the enforced confidentiality boundary (tenet 3).

## Tasks (dependency order)

1. **Federated OIDC.** ASP.NET Core external auth for **Google, Microsoft, Apple** (rented
   primitive — near-zero code). Replace the 38.1 dev stub with the authenticated user.
2. **User ↔ Member linkage.** Authenticated user → room membership; sender attribution
   (38.1/38.3) now uses real identity + verified email.
3. **Invite-link flow.** Generate a room invite link; tap → sign in → auto-join. This is the
   primary on-ramp (invitation, not cold signup).
4. **Confidentiality enforcement.** Authorise on `ChatHub` join and on all room queries —
   only members may read/subscribe to a room. Non-members get nothing (not even existence).
5. **Provisioner vs consumer roles.** Role field on membership; gate agent-configuration /
   add-agent actions to provisioners. Consumers can only talk.
6. **Verify.** Invite → Google sign-in → in-room; a non-member is denied hub join + history;
   a consumer cannot perform provisioner-only actions.

## Notes
Email magic-link fallback is a later add, not this phase. E2E encryption is a future
hardening (parent Q7) — rooms are the boundary here, enforced server-side.

## Not in scope
Registry/scope (38.5 — scope depends on this phase's identity), sharing (38.6), org/tenant
management, SSO for enterprises.
