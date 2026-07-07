# Phase 38.5 — Registry / GAL + Save-as-Agent

> **Status: Todo** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** 38.2 (agents work) + 38.4 (identity — scope needs real owners)
> **Done when:** you compose a chain live in a room, `/save-as-agent` it, then `@`-address
> it in a different room and it runs.

The discovery + authoring layer — the "Global Address List." Turns the hardcoded agent map
from 38.2 into a real, scoped directory, and gives non-technical users an authoring on-ramp
that writes no `.mcl` by hand. Also fixes the handle/trust/provenance model: **short bare
handles** for typing, a **visual seal** for trust, and **publisher name in the listing** for
provenance — three concerns the old `@forge/` prefix was conflating.

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
6. **Handle namespace — bare + globally unique (X model).** Drop the `@forge/` prefix; handles
   are short bare strings (`@assistant`, `@guard`, `@claude`) in one flat, globally-unique,
   **claimed** namespace (first-come-first-served). **Reserve official handles** now
   (`@assistant`, `@guard`, `@claude`, `@openai`, `@grok`) so no one else can claim them. No
   collision risk while F&F is built-ins only, but reservation is cheap insurance before
   custom/marketplace (39.5). Collision/impersonation protection is the seal (task 8), not the
   string.
7. **Raw-model passthrough agents (`@claude`/`@openai`/`@grok`).** Thin single-expert
   passthrough missions (the `vanilla` "raw LLM" shape) bound to a provider profile —
   "generalised experts," addressable because the room addresses *missions*, not experts.
   `ProviderClientBuilder` already supports `anthropic`/`openai`/`xai`/`ollama`. **Gating
   change:** `MissionRegistry.LoadAsync` currently overwrites every mission's key with one global
   key (`src/ForgeUI/Services/MissionRegistry.cs:44-50`, key read from the *first* forge.toml in
   `Program.cs`) — switch to **per-mission key resolution** so each `forge.toml` resolves its own
   `env(...)` key (`MCL_API_KEY` / `ANTHROPIC_API_KEY` / `XAI_API_KEY`). Seed as members like
   `@forge/assistant` (`RoomsSeeder.SeedEssentialAgentsAsync`). On-thesis: raw `@claude` beside
   verified `@assistant` in one room *is* the "same gesture, invisibly better" demo.
8. **Two distinct trust seals (no false-green).** (a) **Identity / publisher seal** — *per-agent*,
   on the handle, X-checkmark style: anti-impersonation ("this is the official `@claude` / a
   verified publisher"). (b) **Per-response Verified badge** — *per-message*, **already shipped**
   (38.3 `TrustSignal` / `AgentMeta.Verified` + expandable trace): "this *run* was verified." Keep
   them visually and semantically **separate** — a raw `@claude` may carry the identity seal while
   its individual answers get **no** verified badge (unverified by design); that contrast is the
   product story, and the 38.3 no-false-green guard must hold (never green-check raw output).
9. **`/agents` directory MVP (slash command).** Lists available agents: **handle · description ·
   publisher (+ identity seal)**. Data: add `List()` to `AgentCatalog` (`Label`/`Description`
   already present) + a new `Publisher` field on agent/mission metadata; the per-response side
   (`AgentMeta.Verified`) already exists. Simplest surface of the GAL — the `@`-autocomplete
   picker is the richer version later. Scoped to what the addressing user can consume (built-ins
   only for F&F). The listing does double duty (discovery + provenance) — which is what makes a
   bare `@` safe: look up who's behind a short handle before trusting it.

## Handle & trust model (added 2026-07-08)

The `@forge/` prefix made one string do three jobs — address, brand, and implied trust — all
badly (long to type, ambiguous trust, quiet namespacing). Split them:
- **Handle = addressing only.** Short, bare, globally unique, claimed (X model).
- **Trust = a visual seal**, not the string. Two seals, different jobs (task 8): identity seal
  (who you're talking to is legit) vs per-response verified badge (this answer was verified).
  Forge is unusual in having the second at all — X has only the first.
- **Provenance = publisher name** in `/agents` (task 9), not the string.

Committing to bare unique handles inherits X's rule: one flat namespace, first-come-first-served,
seal disambiguates the real `@claude` from an impostor. That implies **handle reservation +
impersonation protection** once custom/marketplace opens (39.5) — free now, so reserve official
handles immediately.

## Notes
The registry is a **directory of expertise-as-personas** (`@legal-reviewer`,
`@quran-class-helper`), not a model picker. An agent enters the list by being *saved* at a
*scope* by a *provisioner* — that is the democratisation unit (one author, many consumers).

**Provider keys (Q5 resolved, v1):** registered/built-in agents use Forge's platform-configured
provider. BYOK is deferred — when added, keys live encrypted in a secrets store (never in the
registry jsonb or logs), scoped to the owner, never exposed to consumers.

## Not in scope
Public/global marketplace of agents, ratings/trust-of-authors, sharing links (38.6),
billing. The **identity-seal mechanism** and the `Publisher` field are in scope; the *process*
for verifying a publisher at marketplace scale (who earns a seal, how) is deferred.
