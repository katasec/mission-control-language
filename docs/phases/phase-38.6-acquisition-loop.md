# Phase 38.6 — Acquisition Loop

> **Status: Todo** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** 38.3 (trust surface) + 38.5 (registry / shareable agents)
> **Done when:** an agent answer can be shared outward carrying its verified badge, and a
> share-an-agent link lets someone else add that agent to their own room.

The growth layer — the deliberate replacement for the in-context injection we *can't* do on
WhatsApp (parent §5.3). Native-first sacrifices in-surface discovery; this reclaims it by
making outputs and agents self-propagating.

## Tasks (dependency order)

1. **Shareable verified output.** Render an agent answer as a provenance-stamped,
   share-friendly artifact carrying the ✓ Verified badge + a link back to the full trace.
   Progressive disclosure: "tap for how" → deep view on the Forge surface.
2. **Public read-only view.** A no-auth landing page for a shared output (the click-through
   target): shows the answer + badge + trace, no room access. This is the paste-into-WhatsApp
   ad made real.
3. **Share-an-agent link.** Generate a link that lets a recipient clone/add a `shared`-scope
   agent (38.5) to their own room.
4. **Verify.** Share an answer → a non-member opens it, sees the badge + trace; share an agent
   → a recipient adds and invokes it.

## Notes
This is where the "laundering step becomes the growth loop" idea (parent §5.3) ships: the
screenshot/paste people already do carries verified provenance and links back to Forge.

## Not in scope
Analytics/attribution funnels, referral incentives, SEO/marketing pages, billing, native
mobile share sheets.
