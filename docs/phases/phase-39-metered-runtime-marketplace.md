# Phase 39 — Metered Container Runtime & Mission Marketplace

> **Sequenced after Phase 38 (Forge Rooms).** Forge Rooms makes the engine *reachable*;
> Phase 39 makes it *runnable at cost, on other people's missions, for money*. The through-line:
> the trust boundary, the scaling boundary, the metering boundary, and the billing boundary
> are **all the same line** — built-in/trusted/priced on one side, custom/untrusted/priced-the-same
> on the other, differentiated only by a per-run policy and a ledger entry.

## Thesis

Today ForgeUI runs missions **in-process** (`MissionService.RunAsync` → `PipelineRunner`),
against **built-in** missions loaded once at boot, on **Ameer's API key**, with **no metering**.
That is correct for a demo and wrong for a product with variable cost and per-user customisation.

Phase 39 converts it into a **metered, containerised, multi-tenant runtime** that can (a) charge for
what a user runs, and (b) run missions the operator did not write — without ever forking the
execution or billing path.

## Core decisions (converged 2026-07-08)

These are load-bearing. Later sub-phases assume them.

1. **One uniform path, from day one.** *Everything* runs in a container — built-in, OCI-sourced,
   and custom alike. There is **no in-process path, ever.** Rationale: in-process-now →
   containers-later is *two* code paths over time (a build **and** a rewrite) — that migration is
   the real complexity. Build the one container path once; run everything through it.
2. **No built-in-vs-OCI distinction in execution or pricing.** All missions run in containers and are
   priced identically. The only variance in the entire system is a **credit/coupon ledger entry**.
3. **One meter, one ledger, one pricing.** The meter is a **cost-meter** = `tokens + compute-seconds`.
   The runner **emits** both signals; the orchestrator **prices and debits** a per-user balance.
4. **Balance = cap = meter — one mechanism.** Balance-check before a run; stop at zero. The "usage cap"
   is not a separate limiter; it is the balance hitting zero.
5. **F&F is not a separate path.** Friends & Family run the same containers, same meter, same pricing;
   they differ **only** by granted credits/coupons on the ledger (like a comped Netflix/Claude account),
   *not* a free code branch.
6. **No BYO key.** The operator carries token cost. Prepaid credits are what make that survivable
   (money is in before tokens burn — never run negative).
7. **Prepaid substrate, two pricing surfaces.** Underlying model = prepaid pay-per-use. Public pricing
   *surface* = **tiered subscription with usage caps** (Claude/Netflix-style: flat price, allowance
   resets per period, higher tier = higher cap). Same substrate (meter + ledger); **pricing is a policy
   layer on top** — pay-per-use / subscription / both run off it. The cap is what lets a **flat** price
   survive a **variable** cost. Tiers do double duty: **more usage + more capability**. Cap numbers and
   tier prices are set from **F&F cost data**, not guessed.
8. **Runner = stateless pure compute.** Input: mission ref + goal + vars. Output: result + trace +
   token counts + compute-seconds. **No DB, no secrets, egress-restricted** (LLM provider + OCI registry
   only). The orchestrator (ForgeUI) stays the owner of identity, DB, broadcast, persistence, and the
   ledger. `RoomAgentInvoker` is already the right (queue-shaped, fire-and-forget) seam.
9. **Keep it warm.** One runner, `minReplicas ≥ 1`. The container hop is single-digit ms **when warm** —
   negligible against multi-second LLM calls. The *only* thing that turns ms into seconds is a
   cold start from scale-to-zero, which bites hardest at low F&F traffic (long idle gaps). Do **not**
   split into warm/scale-to-zero profiles now; scale-to-zero is a later cost optimisation, not worth
   trading warm-path simplicity for pennies.
10. **Trust = a per-run policy attribute, not a path fork.** Built-in runs get a looser policy
    (broader egress, `exec`/`http` permitted); custom runs get a locked-down policy (no `exec`/`http`,
    restricted egress). Same runner image, policy is config on the run. Custom missions are additionally
    restricted at the language level to declare only `llm`/`rule` expert kinds.
11. **Proportion check.** Container idle is the *cheap* cost; **LLM tokens are the real bill** and
    scale-to-zero does nothing for tokens. Metering is where the money lives — do not let infra
    micro-tuning steal focus from it.

## Sub-phases (dependency-ordered)

### Group A — F&F foundation (uniform path + metering; built-in missions only)

- **39.1 — Containerized Mission Runner.** Extract the mission-execution body
  (`MissionService.RunAsync` core → `PipelineRunner`) into a stateless container service. Bake the
  built-in missions **into the runner image** for F&F (no OCI/blob/registry dependency yet — still 100%
  containerised). Deploy on ACA, warm (`minReplicas ≥ 1`), one runner. Wire orchestrator → runner
  transport; orchestrator keeps identity/DB/broadcast/persistence. Establish the **per-run policy
  attribute** hook (all built-in = trusted policy for now). Runner **emits token counts +
  compute-seconds**.
  *Done when:* an `@mention` in a live room executes in the container and returns result + trace + cost
  signals, indistinguishable to the user from today.

- **39.2 — Cost Meter, Ledger & Credits.** Per-user **cost-meter** in the orchestrator: price
  `tokens + compute-seconds`, debit a **balance ledger**, balance-check before run, stop at zero
  (cap = balance). Settlement = **debit actual cost after each run** (near-empty overshoot is bounded by
  timeout × max loop calls — no pre-auth holds). Grant **F&F credits/coupons** against the same ledger.
  Per-run **usage/cost telemetry** (one dashboard) — the data that later sets prices.
  *Done when:* every run debits a real balance; an F&F user with granted credits runs until credits run
  out; per-user cost is observable. **→ F&F ships on 39.1 + 39.2.**

### Group B — Custom missions & experts (trigger: first mission the operator did not write)

- **39.3 — Forge OCI Artifact Schema (B0 — blocks all publishing).** Define the artifact contract
  **before** publishing anything (re-tagging signed, published artifacts later is the painful path):
  - `artifactType` as the **primary discriminator**: `application/vnd.forge.expert.v1+json` vs
    `application/vnd.forge.mission.v1+json` — read at pull time to route before pulling blobs.
  - Typed layer `mediaType`s (e.g. `…expert.content.v1+markdown`, `…mission.bundle.v1+tar`).
  - **Annotations**: standard `org.opencontainers.image.{title,version,description,created,authors}`
    plus `dev.forge.schema.version` (format evolution — the one people forget), `dev.forge.kind`
    (human-readable mirror), and `dev.forge.mission.experts` (pinned expert digests) **only if** experts
    are referenced rather than bundled.
  - **Decision to lock here: self-contained vs referenced experts.** Recommend **self-contained**
    (experts bundled inside the mission artifact) — matches OCI immutability, lighter metadata, no
    recursive pull. This choice sizes all the mission→expert metadata.
  - `artifactType` + annotations are covered by the **cosign signature** → the discriminator *is* the
    trust boundary (one signed metadata surface). Both push-side (a future `forge publish`) and pull-side
    (`Katasec.OciClient` needs a type-aware pull; currently only `PullExpertAsync`) must set/read it.
  *Done when:* the artifact schema (types, mediaTypes, annotation keys, bundling) is documented and a
  round-trip publish→pull correctly classifies expert vs mission.

- **39.4 — OCI Mission Distribution.** Lift the expert-OCI plumbing (`OciExpertPuller`, `ForgeCache`,
  `CredentialStore`) to carry whole missions. Built-ins move from baked-in-image to **pulled from the
  trusted Forge OCI registry** (signed/verified, cached); runner pulls instead of bundling. Type-aware
  pull dispatches on `artifactType`. `ForgeCache` gains a mission cache namespace.
  *Done when:* the runner executes a mission pulled from the registry by digest, signature verified.

- **39.5 — Custom Missions & Experts.** **Blob storage** (Azure Blob, keyed by user) for custom experts.
  Replace the hardcoded `AgentCatalog` with a **real mission registry** (per-user/scoped
  `@handle → mission`, save-as-agent — ties into Phase 38.5). **Trust enforcement**: restrict custom
  missions to `llm`/`rule` expert kinds (deny `exec`/`http`) and run them under the locked-down runner
  policy (restricted egress).
  *Done when:* a user authors a custom mission, it is stored + resolved by handle, and it runs under the
  restricted policy with a custom expert pulled from blob.

### Group C — Public monetization (same meter; pricing as policy)

- **39.6 — Public Monetization.** Stripe (or equivalent) **top-up into the ledger** — flip prepaid
  credits to real money. **Subscription tiers with usage caps** (Claude/Netflix-style) as a
  pricing-policy layer over the same meter; tier = usage cap **+** capability bundle (custom missions,
  private experts, premium OCI-curated built-ins). Cap numbers and tier prices **derived from
  Group A cost data**.
  *Done when:* a public user tops up (or subscribes), runs against the cap, and the operator's margin
  math is grounded in real per-user cost.

## What Phase 39 deliberately does **not** do

- No in-process fallback path (rejected — see decision 1).
- No warm/scale-to-zero split (rejected for now — decision 9).
- No BYO key (decision 6).
- No OCI/blob/registry/tiers for F&F — those are gated behind their triggers (Groups B/C).
