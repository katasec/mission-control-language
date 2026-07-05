# Phase 38.3 — Trust Surface

> **Status: Done** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** 38.2
> **Done when:** an agent's message in a room shows a ✓ Verified / ✗ Unverified badge with
> sender attribution and an expandable "show thinking" trace — the February-screenshot
> experience, inside a multi-party room.

Surfaces the differentiator. Reuses the Phase 35 trust view models (`ChatMessage`,
`PipelineTraceEvent`, `TrustSignal`) and adapts them to the multi-party message list.

## Tasks (dependency order)

1. ✅ **Carry the envelope through.** The agent envelope persisted in the message jsonb (38.2)
   is mapped into `RoomMessageDto.Trust` (`AgentTrustDto` + trace steps) in `RoomsMappings.ToDto`.
   `AgentStep` gained `Reason` (the judge's onFail feedback) to drive the retry line. The room
   view reconstructs the Phase 35 `TrustSignal` + `PipelineTraceEvent` from the DTO — the existing
   trust components render unchanged.
2. ✅ **Badge + attribution.** `TrustSignalBadge` renders ✓ Verified / ✗ Unverified on agent
   messages in an `agent-card`, with the producing handle (`@forge/hallucination-guard`) and time.
3. ✅ **Show-thinking trace.** Per-message expand toggle; expanded state renders the reused
   `PipelineTrace` component (per-step expert · text · pass/fail).
4. ✅ **Convergence visual.** The `↺ Retry N` line + red fail rows render the
   unverified → retry → verified progression inside the room.
5. ✅ **Trust-integrity guard.** `TrustIntegrity.IsVerified` (pure, in `ForgeMission.Rooms`) is
   the single source of green, computed at **mapping time** so a false-green can't reach the wire:
   green requires the pass verdict **and** a trace terminating on a passing step. 5 unit tests
   (incl. a corrupt `verified:true` on a fail-terminated trace → not green).
6. ✅ **Verify.** Live: a passing run renders ✓ Verified + expandable convergence trace
   (Answerer "February" → Verifier fail → retry → "none" → pass); a (seeded) failing run renders
   ✗ Unverified with its fail-terminated trace. 17 tests pass (6 integration + 6 mention + 5 trust).

## Not in scope
Identity/attribution to *real* users (38.4 — attribution here uses 38.1's sender/dev-stub
identity), registry (38.5), sharing the badge externally (38.6).
