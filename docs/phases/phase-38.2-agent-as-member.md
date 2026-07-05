# Phase 38.2 — Agent as Member

> **Status: Done** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** 38.1 + existing engine (`PipelineRunner`, Phase 35 `MissionService`,
> `forge serve`)
> **Done when:** `@forge/hallucination-guard "which month has an X in the middle"` typed
> in a room returns the converged answer ("none") as an agent message visible to every
> member.

The core magic: a mission becomes an `@`-addressable member. Reuses the engine untouched —
this phase is the *bridge* between a room message and a mission run, plus streaming the
result back. **Pull-only** is enforced here (an agent runs only when addressed).

## Tasks (dependency order)

1. ✅ **Agent members.** A `Member` of `Kind: Agent` references an agent handle
   (`@handle → mission`). Seeder (38.1) already provides `@forge/hallucination-guard`;
   `AgentCatalog` is the minimal hardcoded `@handle → MissionEntry` map (full registry is 38.5).
2. ✅ **`@mention` detection + pull gate.** `MentionParser.Detect` (pure domain, in
   `ForgeMission.Rooms`) parses a human message for an addressed agent member; extracts target +
   prompt (strips quotes; longest handle wins). **Only invokes if an agent member is addressed** —
   otherwise it's just chat (tenet: pull, never push). Verified: a non-addressed message produces
   only the human row, no agent run.
3. ✅ **Room-scoped context assembly.** *(Q1 resolved.)* `RoomContextAssembler` composes the
   `@`-prompt + a **bounded recent window** (last N=10, configurable), **room-only** via
   `IReadStore` keyset pagination. A `reply_to` target is prioritised ("the above"). All senders
   in the window are visible. The @-prompt is always the explicit question so the transcript
   never displaces it.
4. ✅ **Invocation bridge.** `RoomAgentInvoker` maps room context → the mission's input and runs
   it by **reusing the Phase 35 `MissionService`/`PipelineRunner` untouched** (in-proc).
5. ✅ **Stream back into the room.** Runs in the background (each invocation independent, Q2) so
   the sender's hub call returns at once; broadcasts a transient `AgentThinking` indicator, then
   persists the final agent message (answer + trust/trace **envelope** in the jsonb payload) and
   broadcasts it via `IHubContext<ChatHub>` to the room group. `reply_to` links back to the
   triggering message.
6. ✅ **Verify.** Live: `@forge/hallucination-guard "which month has an X in the middle"` — raw
   LLM confidently answered "February" (attempt 1) → deterministic Verifier **failed** → loop
   retried → converged to **"none"** → `verified: true` (retries 1, 4 steps), posted as an agent
   message all members see. Non-addressed message triggered no run. Envelope persists with pinned
   lowercase keys (`payload->'agent'->>'verified'` promotion path works). 12 automated tests pass
   (6 Testcontainers integration + 6 `MentionParser` unit).

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
