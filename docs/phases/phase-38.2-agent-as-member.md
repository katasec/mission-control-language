# Phase 38.2 — Agent as Member

> **Status: Todo** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** 38.1 + existing engine (`PipelineRunner`, Phase 35 `MissionService`,
> `forge serve`)
> **Done when:** `@forge/hallucination-guard "which month has an X in the middle"` typed
> in a room returns the converged answer ("none") as an agent message visible to every
> member.

The core magic: a mission becomes an `@`-addressable member. Reuses the engine untouched —
this phase is the *bridge* between a room message and a mission run, plus streaming the
result back. **Pull-only** is enforced here (an agent runs only when addressed).

## Tasks (dependency order)

1. **Agent members.** A `Member` of `Kind: Agent` references an agent handle
   (`@handle → mission`). Seed one or two built-in agents (e.g.
   `@forge/hallucination-guard`) into a test room. (Full registry is 38.5; this is a
   minimal hardcoded map.)
2. **`@mention` detection + pull gate.** Parse an incoming human message for an addressed
   agent member; extract target + prompt. **Only invoke if an agent member is addressed** —
   otherwise the message is just chat (tenet: pull, never push).
3. **Room-scoped context assembly.** *(Q1 resolved.)* The agent receives the `@`-prompt +
   a **bounded recent window** — last N messages *or* a token cap, whichever hits first —
   **room-only**, never cross-room. If the message is a reply (`reply_to`), the referenced
   message is included/prioritised ("the above"). All senders in the window are visible (human
   + other agents). N / token-cap is configurable. The room is the confidentiality boundary.
4. **Invocation bridge.** Map room context → mission inputs; run via `PipelineRunner` /
   `MissionService` (in-proc reuse) or the mission endpoint.
5. **Stream back into the room.** Broadcast the mission's streaming output to the room group
   as an agent message (reuse Phase 35 per-step streaming). Persist the final message.
6. **Verify.** The hallucination-guard trick question converges (unverified → retry →
   "none") and appears as an agent message all members see. A non-addressed message triggers
   no agent run.

## Notes / resolved
- Context-scope assembly (task 3) resolves parent **Q1** (see parent §12).
- **Concurrency (Q2 resolved):** each `@`-invocation is independent; concurrent runs are fine,
  each posts its own attributed message. No cross-agent awareness in v1.
- **Artifact input (Q4 resolved):** PDF-upload → agent is a second increment — text first, then
  files. Bytes go to blob storage behind an `IArtifactStore` seam with a **reference** in the
  message payload jsonb; returned-file rendering is in 38.3. (There is no Phase 38.7.)

## Not in scope
Trust badge/trace rendering (38.3 — this phase just produces the agent message + its
envelope), registry/save-as-agent (38.5), and **cross-agent orchestration** (a judge reading
other agents' outputs / live debate — a later feature). Independent concurrent invocations
*are* supported here (Q2).
