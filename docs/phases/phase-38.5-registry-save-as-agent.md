# Phase 38.5 — Registry / GAL + Save-as-Agent

> **Status: Todo** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** 38.2 (agents work) + 38.4 (identity — scope needs real owners)
> **Done when:** you compose a chain live in a room, `/save-as-agent` it, then `@`-address
> it in a different room and it runs.

The discovery + authoring layer — the "Global Address List." Turns the hardcoded agent map
from 38.2 into a real, scoped directory, and gives non-technical users an authoring on-ramp
that writes no `.mcl` by hand.

## Tasks (dependency order)

1. **Registry model.** `AgentHandle` → mission (+ endpoint) + **scope** (`personal | room |
   shared`) + owner + version (reuse Phase 11 expert versioning). Replaces 38.2's hardcoded
   map.
2. **`@handle` resolution.** Resolve a mention to a registry entry, scoped by the addressing
   user/room. Autocomplete + disambiguation for similar handles (parent Q3).
3. **Add/remove agent from a room.** From the registry, add an agent as a room member (the
   "Add @agent" journey step) — provisioner-gated (38.4).
4. **Save-as-agent — snapshot (S1 resolved: rigid).** Capture the live chain as a **frozen
   snapshot**, not a parameterised template. Emit a **mission** where the pipeline shape and each
   step's instruction are **fixed**, and the **entry input is the sole parameter**. Capture the
   *structure* — the `->` sequence, each step's model/expert + its instruction (the human's
   per-step instruction becomes that step's fixed config) — **not** the *data* of the example
   run. Rationale: deterministic + explainable ("what you tested is what you get"), no
   inference-from-one-example risk, reuses the mission format. **Parameterise is deferred** — a
   provisioner can manually add knobs later (it's just a mission), and *auto*-parameterisation
   routes to the program-synthesis spike.
5. **Verify.** Compose draft→review live, `/save-as-agent @my-reviewer` (personal scope), add
   it to another room, invoke it; a shared-scope agent is discoverable by others.

## Notes
The registry is a **directory of expertise-as-personas** (`@legal-reviewer`,
`@quran-class-helper`), not a model picker. An agent enters the list by being *saved* at a
*scope* by a *provisioner* — that is the democratisation unit (one author, many consumers).

**Provider keys (Q5 resolved, v1):** registered/built-in agents use Forge's platform-configured
provider. BYOK is deferred — when added, keys live encrypted in a secrets store (never in the
registry jsonb or logs), scoped to the owner, never exposed to consumers.

## Not in scope
Public/global marketplace of agents, ratings/trust-of-authors, sharing links (38.6),
billing.
