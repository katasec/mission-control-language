# Phase 38.3 — Trust Surface

> **Status: Todo** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** 38.2
> **Done when:** an agent's message in a room shows a ✓ Verified / ✗ Unverified badge with
> sender attribution and an expandable "show thinking" trace — the February-screenshot
> experience, inside a multi-party room.

Surfaces the differentiator. Reuses the Phase 35 trust view models (`ChatMessage`,
`PipelineTraceEvent`, `TrustSignal`) and adapts them to the multi-party message list.

## Tasks (dependency order)

1. **Carry the envelope through.** Thread the mission run's `StepEnvelope` trace +
   `TrustSignal` from 38.2's invocation into the agent message model (reuse Phase 35 view
   models — no new serialisation).
2. **Badge + attribution.** Render Verified/Unverified on agent messages in the room list,
   with clear sender attribution (which agent produced it).
3. **Show-thinking trace.** Per-agent-message expandable trace: per-step pass/fail and loop
   convergence. Reuse the Phase 35 trace component; adapt layout to the room list.
4. **Convergence visual.** Render the unverified → retry → verified progression inside the
   room (the hallucination-guard experience).
5. **Trust-integrity guard.** A ✓ Verified badge must be impossible to render on an
   unverified/failed answer. Add a test that a failed `TrustSignal` can never produce green.
   *(Directly addresses the false-green concern spotted earlier in the standalone UI.)*
6. **Verify.** Badge + expandable trace render correctly for a passing and a failing run;
   the integrity test passes.

## Not in scope
Identity/attribution to *real* users (38.4 — attribution here uses 38.1's sender/dev-stub
identity), registry (38.5), sharing the badge externally (38.6).
