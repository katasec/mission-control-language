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
3. **Room-scoped context assembly.** Gather the relevant room thread as the mission input.
   Define the v1 scope explicitly (e.g. this room only, last N messages). The room is the
   confidentiality boundary — no cross-room history.
4. **Invocation bridge.** Map room context → mission inputs; run via `PipelineRunner` /
   `MissionService` (in-proc reuse) or the mission endpoint.
5. **Stream back into the room.** Broadcast the mission's streaming output to the room group
   as an agent message (reuse Phase 35 per-step streaming). Persist the final message.
6. **Verify.** The hallucination-guard trick question converges (unverified → retry →
   "none") and appears as an agent message all members see. A non-addressed message triggers
   no agent run.

## Notes / open
- Context-scope assembly (task 3) is Open Question #1 in the parent — start minimal, refine.
- Artifact input (PDF upload → agent) is a second increment within this phase — text first,
  then files. Returned-file rendering is in 38.3. (There is no Phase 38.7.)

## Not in scope
Trust badge/trace rendering (38.3 — this phase just produces the agent message + its
envelope), registry/save-as-agent (38.5), multiple concurrent agents in one room (parent
Q2).
