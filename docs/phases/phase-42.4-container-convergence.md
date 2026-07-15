# Phase 42.4 — One `/v1` image: Docker ≡ ACA

> **Status: Design (2026-07-15).** Converge the two serving surfaces onto **one container image** that
> exposes `/v1/messages` **and** `/v1/responses`, scheduled identically on local Docker and Azure Container
> Apps. This is what makes `local ≡ cloud` *literally the same artifact* rather than two things that behave
> alike — and it turns "host it" into scheduling + auth, not a rewrite.
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:**
> [42.1](phase-42.1-anthropic-serve-responder.md) (Anthropic serve), [42.3](phase-42.3-tool-capable-enriching-responder.md)
> (tool round-trip) · **Blocks:** [42.6](phase-42.6-hosted-endpoint-ttfa.md) · **AOT rules:** [CLAUDE.md](../../CLAUDE.md).
>
> **Done when:** a single image serves both `/v1/messages` and `/v1/responses` for a mission; the exact same
> image runs a mission on local Docker (`forge claude --container`) and on ACA (the runner). The Phase-39
> `ForgeMission.Runner` either *is* that image or shares its serving core, so there is no "local wire vs
> cloud contract" fork.

## The problem being solved (verified 2026-07-15)

Today there are **two serving surfaces with two contracts**:
- **Local `forge serve`** → `Katasec.OaiServer` / `Katasec.AnthropicServer`, the **`/v1` wire**
  (OpenAI + Anthropic shape). Consumed by external clients (Claude Code, Codex, curl).
- **Cloud `ForgeMission.Runner`** (Phase 39) → a **`/run` + `/run/stream`** contract
  (`RunContracts`, `MissionRunHandler`), consumed by **ForgeUI/Rooms**, with `UsageTrackingChatClient` +
  billing wrapped around it.

So "same container everywhere" is **not true yet.** The convergence: make the runner image *also* expose
the `/v1` wire (the doors), keeping `/run` for the existing Rooms path if still needed, and use that one
image locally too.

## Design

**One image, multiple front-doors, one mission core.** The image entrypoint hosts:
- `/v1/messages` (Anthropic wire — Door A)
- `/v1/responses` (OpenAI Responses wire — Door B; Codex)
- `/run`, `/run/stream` (existing Rooms contract — kept until Rooms migrates, or forever as the internal
  contract)
- `/missions` probe, health

all over the **same `MissionChatClient` / mission runtime**, so a request on any door executes the identical
pipeline (including the 42.3 tool-capable responder + enrich-once seam).

**Serving both wires from one process.** `OaiServer.Build` and `AnthropicServer.Build` each build their own
`WebApplication` today. Converge: a single `WebApplicationBuilder` where **both** servers' `.Map(app)` are
called (they map disjoint routes — `/v1/chat/completions`+`/v1/responses`+`/v1/models` vs `/v1/messages`).
`forge serve` then serves all wires at once (retire the 42.1 `wire:` selector, or keep it to *disable* a
wire; default = both). This is a small refactor because both `Map` methods already exist and don't collide.

**Metering wrapped in cloud only.** Locally the image runs the mission with the developer's own provider
keys, unmetered. In cloud, the *same* image is fronted by the Phase-39 `UsageTrackingChatClient` + billing
(the wrapper lives at the hosting layer, 42.6 — not baked into the image).

**The invariant, stated honestly (sharpened in external design review, 2026-07-15):**

> **The same mission-execution and wire implementation runs locally and in the cloud.**

That is the defensible claim — **not** "everything is identical." The full request *stacks* legitimately
differ: cloud adds authentication, billing, provider credentials, network policy, persistent state, rate
limiting, scale-out and observability. Claiming byte-identical environments would be false and would
invite someone to "fix" the differences that are supposed to exist. **Same execution core; different
surrounding stack.**

## Tasks (chronological)

1. **Merge the two `Build`s into one app.** A `ForgeServe.BuildApp(missionClient, config)` that maps both
   `OaiServer` and `AnthropicServer` routes on one `WebApplication`. `forge serve` uses it (serves both
   wires); confirm AOT publish + no route collision.
2. **Decide the runner's relationship to the image.** Two options, pick in design review:
   (a) `ForgeMission.Runner` gains the `/v1` doors (adds `OaiServer`/`AnthropicServer` mapping alongside its
   `/run` handlers) → the runner image *is* the one image; or (b) extract a shared `ForgeServe` serving core
   library both `forge serve` and the runner reference. **Recommendation: (a)** — fewer moving parts, the
   runner already has the mission-loading + Phase-39 hooks.
3. **`forge claude --container` uses this image** (42.2) — verify a local container serving `/v1/messages`
   drives the real `claude` CLI identically to the in-process path.
4. **Build/publish the converged image** to `ghcr.io/katasec` (the tag `forge agent start` already pulls),
   ensuring python3 (for `@guard`'s exec verifier, per Phase 39 runner img 0.2.1) and any Scout deps are
   present.
5. **Regression:** the existing Rooms `/run` path still works against the converged image (don't break
   Phase 39/41 room agents). Deploy to `ca-forge-runner-dev`, verify a room agent + a `/v1/messages` call
   against the same revision.

## Out of scope

- Auth / key→mission routing / metering wiring — **42.6** (this spoke makes the *image* right; 42.6 makes
  the *hosting* right).
- Retiring `/run` in favour of `/v1` for Rooms — a separate migration; keep both here.
- Multi-replica shared session store — **42.6** (the re-entrancy store seam from 42.3 gets its cloud
  implementation there).
