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

## Status & handoff (updated 2026-07-09)

**Groups A + B are DONE and LIVE on `forge.katasec.com`. F&F is shippable; built-ins are OCI-distributed.**

| Sub-phase | Status | Live evidence |
|---|---|---|
| **39.1** Containerized runner | ✅ **DONE + LIVE** | `ca-forge-runner-dev` (internal, warm 1 replica); `@guard` runs in-container, green ✓; in-process path removed |
| **39.2** Cost meter, ledger & credits | ✅ **DONE + LIVE** | micro-USD `ledger_entries`; login `Granted 5000000µ$`, run `Debited 224µ$` (exact cost-recovery). **F&F ships here.** |
| **39.3** OCI artifact schema (B0) | ✅ **DONE** | `artifactType` discriminator + self-contained bundle in `Katasec.OciClient`; 6 round-trip tests; [oci-artifact-schema.md](../design/oci-artifact-schema.md) |
| **39.4** OCI mission distribution | ✅ **DONE + LIVE** | runner 0.4.0 pulls all 5 built-ins from `ghcr.io/katasec` by pinned digest (anonymous), runs + meters a pulled mission |
| **39.5** Custom missions & experts | ⏭️ **NEXT** | not started — **design brainstormed 2026-07-12** (author/consumer lens, chat-native authoring, Library try-bench). Recommended lean-first slice: **39.5a Library try-bench**. See the [39.5 design brainstorm](#395--design-brainstorm-2026-07-12-not-yet-built) + decisions to lock below |
| **39.6** Public monetization | ⬜ pending | needs Stripe account; tier numbers from 39.2 cost data |

**Decisions locked this session** (beyond the 2026-07-08 core decisions below):
- **39.2 ledger**: integer **micro-USD** (`bigint`); pricing = **cost-recovery** (provider list price per model, no markup, dated `CostMeter` table + 15µ$/compute-sec); F&F credits = **auto-grant on first sign-in** (self-heals pre-existing members, idempotent).
- **39.4 distribution**: registry = **`ghcr.io/katasec`** (public packages, anonymous pull); trust = **digest-pin now, cosign deferred to 39.5**.

**Deployed artifacts**: `forge-ui:0.3.0`, `forge-runner:0.4.0` (ACR `crforgeroomsdev`); `Katasec.OciClient` **0.2.1** (GitHub Packages); 5 missions on `ghcr.io/katasec/forge-mission-*:0.1.0` (pinned digests recorded in the `project_forge_mission_marketplace` memory). Deploy = Bicep `500-app` via `az deployment` / `az containerapp update` (az authed locally); image builds via dispatched CI workflows.

**Known non-issue**: `hallucination-guard`'s `verify.py` is a demo verifier hardcoded for the "which month contains X" trick question — `@guard "capital of France"` legitimately shows Unverified. Not a distribution bug; a real general-purpose verifier is future work.

**To start 39.5 (next):** two entry points. **(a) Lean-first — 39.5a Library try-bench** (recommended): give the existing read-only Library page a verb — "Try" on built-ins (trivial, no new trust surface), then "Add from OCI" paste → on-demand pull + turn on the deferred `RunPolicyGate` (reject `exec`/`http`) + restricted run + meter. Buys arbitrary-pull + policy-enforcement (both needed by full 39.5) with **no blob/registry/persistence**. It's the author's pre-gift validation bench, not the consumer surface. **(b) Full 39.5** — lock decisions first (like 39.2): Azure Blob layout for custom experts; per-user mission registry (Postgres, like the ledger); save-as-agent snapshot shape (38.5 S1, confirmed rigid = consumer trust); gift target / frozen-vs-living / personal-vs-global-handle (proposed: scope = namespace boundary) / default-verifier nudge; restricted runner policy depth; cosign key management. Then build custom-mission authoring → store → resolve-by-handle → run restricted. **Full design + rationale: [39.5 design brainstorm](#395--design-brainstorm-2026-07-12-not-yet-built).**

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

#### 39.5 — design brainstorm (2026-07-12, not yet built)

Framing session for save-as-agent + the consuming surface. No code; decisions/direction below.
Three in-chat mockups were produced as reference (two-pane author surface; expert-prose edit;
Library try-bench) — concepts, not committed assets.

**The core structure — authors and consumers (Ameer's reframe).** Save-as-agent is **how someone
who can instruct an LLM packages that ability for someone who can't, and hands it over as a
person-to-person gift.** This is normal — every home has the one tech person; chef resources have
authors and consumers. It is *not* "democratizing authoring": the author still needs the skill
(prompt craft, knowing to add a check step, knowing failure modes). What it actually collapses is
**distribution, not authoring** — the niece could already write the `.mcl` by hand; what was hard was
files/CLI/"how does my aunt reach it?". Forge Rooms already solved reach (the aunt's whole
experience — room-of-two, `@`-address, verified badge — is **built + live**). So save-as-agent's real
delta is: **author builds in the same room the consumer lives in, on the consumer's real inputs, and
the result is instantly reachable.** Value = **supply onto an already-proven consumer surface** (low
risk — the scary question "will a non-technical person use an `@`-agent for real work?" was answered
*yes, live*, by the halaqa case). The **family/team relational gift is the near end**; the public
marketplace is the far-end generalization, not the point.

**Consequences that fall out of the author/consumer lens:**
- **Rigid snapshot (S1) is right — because rigidity is the consumer's trust.** "What the author tested
  is what you get." The consumer wants no knobs; they want the thing their author made. This *upgrades*
  the S1 rationale from "safe to build" to "a consumer guarantee."
- **Instruction vs. data maps exactly onto author vs. consumer.** The author freezes the instruction
  (their expertise); the consumer supplies only the entry input. The earlier "over-specific" worry
  dissolves — specialization *for a person* is the deliverable, not a bug.
- **Two trust regimes.** Relational (social — "my daughter made it," the cryptographic seal barely
  matters) vs. anonymous (marketplace — the seal is load-bearing). Save-as-agent **starts relational**;
  the atomic unit is author→known-consumer, not publish-to-bazaar.
- **The consumer can't judge output → the agent must carry the author's judgment.** Default to
  including a **verify step** (the `@assistant` Answerer→Verifier shape) — the verifier is the author's
  standard standing in when they're not in the room. Caveat: a *general* verifier doesn't exist yet
  (LLM-as-judge is weak; `@guard`'s `verify.py` is a demo). So verify **raises confidence, does not
  guarantee** — do not sell the consumer certainty we can't ship.
- **Saving = taking on a support role.** The consumer can't fix/tweak/debug; they return to the author.
  Rigid snapshot makes this cleaner (one responsible person, no consumer-side edits) but real. Lifecycle
  (re-save, version, notify) is a design surface, not one-shot.

**Authoring surface — chat-native, NOT a coding/Replit IDE.** Rejected a Replit-style IDE: it
re-adds the exact friction (files, editor, a separate place) Rooms removes, and raises the author skill
floor. Replit lowers the floor for people who still *code*; Forge's move is people who *converse*. The
one Replit virtue that matters (live iterate-and-see) the room **already gives free** — `@mention`,
see output, adjust, retry: the room *is* the run-loop. Shape = **two panes**: left = chat iteration
(messy, dead-ends fine); right = the **agent taking shape**. The loop flips from "human assembles
steps" to **converse → LLM drafts the mission → human curates in plain language → gift**; the human is
an **editor/approver, never an assembler** (this is what the deliberately-small, LLM-translatable
language buys). Capture is **not transcript-scraping** (live authoring is messy) — the right pane is
the explicit-assembly artifact; the LLM generates, the human curates.

**The language structure dictates the surface (grounded in the grammar + missions/).** A mission is
`mission.mcl` (tiny **wiring** only — the `->` sequence, `loop(N)`, `when`, `parallel`, `using`; see
`MclGrammar.g4`) **plus** the substance, which lives in the **expert markdown** (YAML frontmatter +
a **prose system prompt** — e.g. `missions/assistant/experts/Verifier/expert.md` *is* the four PASS
criteria in English). So a one-line step card hides the exact thing an author edits. Corrected surface
= **three strata, each with the right affordance**:
  1. **Pipeline (`.mcl`)** — skeleton. Small, glanceable, rarely touched; the few structural edits
     (reorder, add a check step, `loop(N)` = "retry up to N times") are **direct-manipulation**, not
     text (the grammar is small enough that a handful of controls cover it).
  2. **`llm` expert** — the 90% case. Card **expands to its editable system-prompt body** (a prose
     textarea). Authoring = *writing English*, not code. Frontmatter (`kind`, `role`) demotes to quiet
     LLM-set chips; the human never types them.
  3. **`rule`** = prose + one structured line (`check: word_count >= 50`); **`exec`** = the escape
     hatch (references a `verify.py`) — **walled off**: visible but "advanced · CLI only," read-only in
     the warm surface.
  This stratification **is the same line three times**: `llm` (pure prose) = generatable = safe under
  the custom policy = warmly editable; `exec` = real code = can't be safely auto-generated = the
  escape hatch. **The language's smallness and the trust boundary are one decision** — the surface
  inherits it for free.

**39.5a — Library try-bench (lean consuming slice; recommended to do first).** Pivot: defer full
authoring; ship the **simplest consuming surface** by giving the existing Library page a verb.
`src/ForgeUI/Pages/Library.razor` already lists agents read-only (handle, seal, publisher, desc via
`AgentRegistry.List()`) — it's the consuming skeleton, missing the ability to *do* anything.
  - **Increment 1 — "Try" on built-ins (trivial, do now).** A run box per row → existing
    `MissionRunnerClient` → runner `POST /run` → render with the existing `PipelineTrace` /
    `TrustSignalBadge`. **Zero new trust work** (built-ins = trusted). Turns Library from a directory
    into a testable directory in an afternoon; highest value-per-effort.
  - **Increment 2 — "Add from OCI" paste (small, but trips the trust wire).** Pasting an arbitrary
    `ghcr.io/…@sha256:…` is literally *the first mission the operator didn't write* (the 39.5 trigger).
    Thin slice: give `OciMissionPuller` an **on-demand pull-this-ref** path (today it's fed the
    hardcoded `BuiltinMissions` list); **classify + gate at pull** — if it declares `exec`/`http`,
    refuse ("needs advanced permissions") — **this is where the `RunPolicyGate` hook (built in 39.1,
    enforcement deferred) finally turns on**; run under the **restricted** policy; it **meters** like any
    run (39.2 ledger debits — a test isn't free, F&F credits cover it). **No blob, no per-user
    registry, no handle claim, no persistence** — a *transient* test of a pulled ref (cached, not
    owned). Buys the two things 39.5 genuinely needs (arbitrary-pull + policy-gate enforcing) while
    skipping the whole authoring/persistence stack.
  - **Two honest flags:** (1) a transient test is a **dead-end** — paste → run → works → *now what?*
    The natural next click ("add to a room") needs the registry = full 39.5. Honest as a *validation
    bench*, thin as a destination. (2) **No aunt will paste `…@sha256:…`** — so Library-with-OCI is
    **not the consumer surface; it's the author's pre-gift validation bench** (`forge publish` (CLI,
    exists) → paste ref → run → confirm → *then* gift). A coherent missing step, not the aunt's screen.

**Open decisions to lock before building 39.5 authoring (refined this session):**
- **Gift target** for "save & gift to…" — pick-a-person / drop-in-a-room / share-link? This is where
  distribution actually happens and is the least-designed part. Design it as the *endpoint from day
  one*, not personal-save-with-sharing-bolted-on.
- **Frozen vs. living** — when the author later tweaks the agent, does the consumer's copy update or is
  it a fork? (The maintenance/support edge.)
- **Personal handle vs. globally-unique bare handle** (38.5 locked bare + FCFS-global; 39.5 save is
  personal — they collide). Proposed resolution: **personal agents don't claim the global namespace**;
  they resolve only in the author's own rooms; **promote-to-shared is the moment a global bare handle
  is claimed** → *scope becomes the namespace boundary*.
- **Default-verifier nudge** — offer to wrap the captured chain in a check step so it's "verified for
  whoever you give it to."

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
