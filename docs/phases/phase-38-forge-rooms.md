# Phase 38 — Forge Rooms (Agents as `@`-Addressable Members)

> **Status: In progress — 38.1–38.4a + 38.7 Done (live on Azure at `https://forge.katasec.com`); 38.5 in progress (tasks 1, 2, 3, 6, 7, 8a, 9 done — registry, @-autocomplete, add/remove agent, bare handles, raw-model agents (@openai/@claude/@grok), identity seal, `/agents`; tasks 4, 5 remain); 38.6 next.**
> **Priority: TOP — this precedes every other phase in `plan.md`.**
> **Depends on:** Phase 34/35 (Forge UI / Blazor — this *is* their evolution), Phase 25
> (mission composition), Phase 25a (`role: judge`), Phase 19 (`forge serve` — mission on
> an endpoint), Phase 12 (`StepEnvelope` — trace + trust signal).
> **Purpose:** Make Forge's reasoning structures *accessible* by turning missions into
> `@`-addressable members of multi-party chat rooms. Spectacular reasoning that no one can
> reach is the same as reasoning that does not exist. This phase builds the surface where
> humans actually reach it.

---

## 1. The problem — an accessibility gap, not a capability gap

Every prior phase built *capability*: expert composition, loops, debate, judges, symbolic
gates (`rule`/`onnx`), safe execution (`exec`), verification. The engine can do MoE, can
have one model judge another, can catch a hallucination and retry. **None of it is reachable
by a normal person.**

The way the world interacts with LLMs is a **chat box in a browser** (ChatGPT, Claude, Grok).
In that box you cannot `@gpt` for a draft and `@claude` to review it; you cannot summon a
verified pipeline. Forge's power lives in *author-time, file-based, run-once* `.mcl` missions.
The world lives in a *live, ad-hoc, conversational* surface. **The two do not meet.**

> The gap is the **interaction surface**, not the engine.

This phase closes it.

---

## 2. Core thesis — agents as members, not tools

The realization the whole design rests on:

> **A Forge Agent is interface-compatible with an LLM.** From the user's seat, `@claude` and
> `@forge/hallucination-guard` are the *same gesture*: address a participant, get an answer.
> One is a raw model, the other a 6-step verify-loop pipeline. The difference is invisible at
> the point of use — it shows up only as *better answers and a visible trust badge.*

And it extends the human ritual everyone already knows: **you pull expertise into a
conversation by adding a person to it** — the BA, the coder, the lawyer, someone who knows
the domain. Teams, Signal, Telegram, WhatsApp (Slack via channels) all share this gesture.
A Forge Agent slots into a slot humans already recognise. **Zero new behaviour to learn** —
that is the accessibility unlock.

Composition becomes **recursive and invisible**: "`@`chatgpt for a draft, `@`claude to review"
*is itself an agent* — the user `@`s one handle (`@forge/reviewed-answer`) and the agent `@`s
the models internally. The mission is the packaged, shareable, single-handle unit of good
reasoning. A user who would never write `.mcl` gets MoE-with-a-judge by typing one mention.

An LLM can only be `@`ed. **A Forge Agent can be `@`ed *and* can `@` others** (including other
agents). It is a participant that is also a composition — the one asymmetry, and the valuable one.

---

## 3. Design tenets (non-negotiable)

1. **Agents are members, not tools.** The intelligence is already capable; the *tooling* to let
   it participate as a peer is what is missing. Treating agents as personalities in the room is
   not anthropomorphic fluff — the social/personality frame is *the only zero-training interaction
   protocol every human already runs* (Dennett's intentional stance; Reeves & Nass, *The Media
   Equation*; the ELIZA effect). An API is accessible to programmers; a member in the room is
   accessible to everyone.
2. **Pull, never push — the "Computer" model.** No proactive chiming. The agent is silent until
   `@`-addressed, acts, then goes quiet. Star Trek's *"Computer, …"*. Proactivity is deferred; we
   (and society) are not ready, and an agent that volunteers into a family group is creepy.
3. **Room-scoped confidentiality.** The agent/LLM is an *instance bound to that room*. Outside
   agents and models never receive its history. A group chat is assumed private unless a member
   explicitly says otherwise — the agent inherits the same confidentiality a human member would.
4. **Multi-party is the primitive; 1:1 is a room of two.** Never build a 1:1 product and bolt
   groups on later — that throws away the identity, attribution, and scoping assumptions baked
   into every layer.

---

## 4. Why the surface must be verified, not just accessible

The under-use of AI is real and *distributional*: optimistically <5% of the ChatGPT population
uses it for high-value work. The cause is not that people are dim — the current surface **gates
value behind two barriers**: the *skill* of prompting, and the *intent* to bring a problem to a
blank box. Forge's two moves attack both: the **mission** removes the skill requirement; the
**social surface** removes the intent requirement (problems surface naturally in group chats —
"which contractor," "is this email too harsh," "what's actually on this bill").

But the social protocol that makes AI *accessible* is the same one that makes it *dangerous*:
social cognition ships bundled with **credulity** — we believe confident personalities.
Sycophancy and over-trust are the *cost* of the accessible protocol. Therefore the trust layer
(Verifier, badge, `kind:exec` measurement, the visible trace) is **not decoration** — it is the
necessary counterweight that lets us use the accessible protocol *without inheriting its fatal
flaw*. The mission statement in one line:

> **Make intelligence social enough to be reachable, and verified enough to be trustable — at
> the same time.** Neither half works alone.

---

## 5. Decision log (the reasoning, so it is not lost)

### 5.1 Inject into existing surfaces vs own a native surface
- **Inject** (browser extension / bot in WhatsApp/Telegram/Slack): maximal reach, rides the
  existing "add-an-expert" ritual — **but the trust surface flattens to a plain text bubble.**
  The differentiator (badge, trace, show-thinking) *degrades most on the surface that gives most
  reach.* This is the Phase 34 "existing AI surfaces dissolve MCL's structured reasoning" tension,
  now on chat.
- **Own**: full control of the trust presentation; no platform risk — **but the adoption wall**
  ("yet another app").
- **Decision: OWN a native surface.** Confirmed by the WhatsApp analysis below (there is no
  durable injection path anyway) and by the refusal to maintain 3+ fragile integrations while
  also building the best native surface.

### 5.2 WhatsApp reality check (why injection is not even available)
- **Official Groups API** exists (generally available) but is **gated**: requires an Official
  Business Account, **max 8 participants**, groups are **business-provisioned** (the bot does not
  join your *existing* family group — the business *creates* a new ≤8-person group). Unusable for
  a 60+-person class or an existing family thread.
- **Linked-device / WhatsApp Web hacks** (Baileys, whatsapp-web.js, wa-automate): unofficial,
  reverse-engineered, **ToS-violating and ban-prone**, fragile to protocol changes. Disqualified
  as a product foundation.
- **Conclusion:** the full menu for "agent in a WhatsApp group" is exhausted and empty. Native-first
  is *forced by reality*, not preference. There was no durable WhatsApp reach to give up.

### 5.3 Acquisition — replacing the injection funnel
Native-first sacrifices in-context viral discovery (the "BBC channel → click-through" mechanic).
Its replacement, using Forge's own strengths:
- **Shareable verified outputs.** Make outputs so visibly, verifiably better that people paste
  *them* into their WhatsApp/Signal groups. A screenshot carrying "✓ Verified · forge/…" is
  itself the ad — someone asks "what gave you that?" The old copy-paste *laundering step* becomes
  the *growth loop*, flowing into the very surfaces we cannot inject into.
- **Share-an-agent.** The provisioner builds `@quran-class-helper`, sends a link; others clone or
  join. Virality without integrations.

### 5.4 Build vs buy — do not adopt a whole chat product
- Adopting Mattermost / Rocket.Chat / Matrix+Element gives fast plumbing **but** puts the most
  novel UX (agent-as-member, trust surface) inside *someone else's opinionated UI paradigm* — the
  self-inflicted version of the "differentiator degrades in a guest house" problem — and takes on
  **impedance-mismatch cost** and **upstream/roadmap risk** (the project shuts down or relicenses;
  cf. real-world OSS relicensings). Even Matrix, philosophically closest (rooms-as-primitive, E2E),
  carries protocol worldview + operational weight + sustainability risk.
- **Heuristic adopted:** *Does the dependency have opinions about my domain?* **Yes → build it.
  No (domain-agnostic primitive) → rent it.** Impedance mismatch and roadmap-hostage risk come
  from *opinionated whole-product* dependencies, never from Postgres or a websocket library.
- **Decision:** **Own the domain + surface; rent domain-agnostic primitives.** MVP is *not* a
  Slack rebuild — the expensive commodity parts (multi-device sync, offline, push-at-scale, native
  mobile) are **deferrable past MVP** and, when needed, adopted as narrow rented primitives.

### 5.5 Stay in .NET — maximise reuse (recurring theme)
The existing Blazor UI (Phase 34/35) was fast and simple *because* it stays in-stack and shares
`ForgeMission.Core` types with no serialisation boundary. That reuse discipline is a standing
theme: SignalR, ASP.NET Core Identity, EF Core — the .NET "legos" — cover the rented layers.

---

## 6. The architecture strawman (build / reuse / rent)

| Component | Tier |
|---|---|
| **Room model** — rooms as container; 1:1 = room of two | 🔨 BUILD (domain) |
| **Members & agents-as-members** — `Member = Human \| Agent`; Agent → Mission | 🔨 BUILD (domain) |
| **Agent registry / "GAL"** — `@handle → mission`, scope (personal/room/shared), *save-as-agent* | 🔨 BUILD (domain) |
| **Trust surface** — show-thinking, trace, Verified badge, in a multi-party message list | ♻️ REUSE + extend (Phase 35 `ChatMessage`/`PipelineTraceEvent`/`TrustSignal`) |
| **Agent engine** — `@mention` invokes the mission with room-scoped context; pull-only | ♻️ REUSE (`PipelineRunner`, missions, `forge serve`) |
| **Realtime delivery** — hub; SignalR groups = rooms; token-streaming into the room | 🏠 RENT (SignalR, in-stack) |
| **Identity & permissions** — room membership *is* the confidentiality boundary | 🏠 RENT (ASP.NET Core Identity / OIDC) |
| **Persistence** — rooms, members, messages, traces, registry | 🏠 RENT (EF Core + Postgres) |
| **Client** — extend the 1:1 session UI into multi-party rooms + member list + `@`-mention | ♻️ REUSE + extend (existing Blazor app) |

The BUILD band is deliberately *thin*: the only genuinely new code is the room/member/registry
domain plus the SignalR wiring. Everything else carries forward.

---

## 7. Identity & onboarding

- **Federated OIDC — no home-grown passwords.** Sign in with **Google / Microsoft / Apple** via
  ASP.NET Core external auth. Rationale: it is a *rented* primitive (in-stack, near-zero code); no
  password storage/liability; one-tap; verified email out of the box (needed for invites + trust).
- **Segments:** family/consumer → Google + Apple; work/colleague → Microsoft + Google Workspace.
  Email magic-link as a later fallback, not day one.
- **Invitation-driven onboarding is the primary on-ramp**, not the landing page. The common path
  is *invited into a room* (tap link → sign in → land inside the room with the agent already
  present). This *is* the acquisition loop of §5.3.
- **Identity = the person; inside a room they are a Member.** Provisioner vs consumer is a **role**
  difference, not an auth difference — keep it simple.

---

## 8. Interaction workflows (the raw material for the surface)

Actors: the **human**, **raw LLMs** (`@gpt` — addressable only), **Forge Agents** (`@forge/x` —
addressable *and* can address others).

| # | Workflow | Stresses / requires |
|---|---|---|
| 1 | Address one agent → verified answer + badge | `@handle` → endpoint resolution; trust badge render |
| 2 | Compose live: `@gpt draft` then `@claude review the above` | **threading** (pass prior output) = `->` as a conversational reference |
| 3 | Fan-out `@a @b @c` then `@judge` merge | **multi-address in one turn** (`parallel {}`) + judge over several outputs |
| 4 | Agent as persistent member of a shared room | **discovery** + scope (personal/team/global) |
| 5 | Promote an ad-hoc chain into a named handle | **save/promote** — authoring on-ramp with no `.mcl` written by hand |

**Verbs the surface must support:** *address, thread, fan-out, judge, save/promote, discover.*
The last two — **discover** and **save/promote** — have no MCL equivalent today and *are* the
registry problem. Everything else is the existing engine in a chat costume.

**Anchor example (real):** aunt runs a weekend Islamic-education class for 60+ Tamil speakers.
She needs a formal course PDF trimmed to the requisite topics, sized for a weekend, with a cover
page. Today the nephew is the copy-paste *laundering step*. With Forge Rooms she `@`s
`@quran-class-helper` in the group and gets the customised PDF directly — the agent is the
nephew's capability *bottled and made always-available.* Note this is **not a chat toy**: it is
`LLM (pick pages) → kind:exec (edit PDF) → Verifier (nothing on kept pages altered)` — the full
heterogeneous stack. And the content is **faithful transformation of a trusted source, never
generation of doctrine** — the trust layer is load-bearing precisely because it is religious
teaching to elders.

---

## 9. The registry / "Global Address List"

Framed by how humans summon expertise (add the BA / coder / lawyer), the registry is **not a list
of AI models** — it is a **directory of expertise-as-personas**: `@legal-reviewer`,
`@quran-class-helper`, `@rails-architect`. This maps one-to-one onto MCL experts and missions.

- `@handle → mission (+ endpoint)`, plus **scope**: personal / room / shared.
- **How an agent enters the list:** it is *saved* (workflow 5) at a *scope* (workflow 4) by a
  *provisioner*. The provisioner authors once on behalf of a group; consumers just talk to it.
  That is the democratisation unit — not everyone authors; one capable person per group bottles a
  skill and everyone benefits.

---

## 10. Multi-party data model (the invariant)

Build the **room** as the primitive: a room has **members** (human or agent), messages have a
**sender**, agent instances are **scoped to a room**. 1:1 falls out as "a room with two members,
one an agent." What gets harder than 1:1 — and must be modelled from day one:

- **Attribution** — who said what (sender on every message).
- **Context scope** — the *room* (not the user) is the confidentiality boundary; the agent
  instance binds to the room.
- **Invocation & audience** — pull-model; addressed by whom; reply lands in the room, visible to all.
- **Roles/permissions** — provisioner vs consumer; special care with minors in a room.
- **Concurrency** — interleaved multi-human input, not clean turn-taking.

---

## 11. Sub-phases (executable breakdown)

This phase is decomposed into **six dependency-ordered sub-phases (38.1–38.6)**, each with its
own spoke doc and task list. Run them 1→6. The nine "spokes" originally sketched here became
**tasks redistributed across these sub-phases** — the mapping below ensures nothing was lost.

| Sub-phase | Covers (original spokes) |
|---|---|
| [38.1 Room Foundation](phase-38.1-room-foundation.md) | **Done.** domain model + SignalR + persistence + minimal client (orig. S1, S2, S9, part of S7) |
| [38.2 Agent as Member](phase-38.2-agent-as-member.md) | **Done.** `@mention` → mission, pull-only, room-scoped context, artifact **input** (orig. S4) |
| [38.3 Trust Surface](phase-38.3-trust-surface.md) | **Done.** badge/trace/show-thinking in the room, artifact **output** rendering (orig. S5) |
| [38.4 Identity & Membership](phase-38.4-identity-membership.md) | **Done.** OIDC + invites + confidentiality + roles (orig. S3) — real Entra External ID sign-in verified live |
| [38.4a UI Foundation & Onboarding](phase-38.4a-ui-and-onboarding.md) | **Done.** Tokenized design system + dark mode (see [UI Design System](../design/ui-design-system.md)), "gate everything" auth IA + `AccountMenu` + `/playground`, first-run "room of two" onboarding, and the LLM-verified `@forge/assistant` default agent |
| [38.5 Registry / GAL + Save-as-Agent](phase-38.5-registry-save-as-agent.md) | **In progress.** `@handle` directory + scope + save-as-agent (orig. S6). Done: task 1 (registry), 2 (@-autocomplete), 3 (add/remove agent + auto-reply guard), 6 (bare handles), 7 (raw `@openai`/`@claude`/`@grok` — no false-green), 8a (identity seal), 9 (`/agents`). Remaining: 4 (save-as-agent), 5 (verify). |
| [38.6 Acquisition Loop](phase-38.6-acquisition-loop.md) | shareable verified output + share-an-agent (orig. S8) |
| [38.7 Hosting & Deployment (Azure)](phase-38.7-hosting-deployment.md) | **Done.** Containerize + version the app, Azure Container Apps + ACR + Key Vault + Postgres via Bicep (`katasec/forge-infra`), passwordless CI/runtime, custom domain `forge.katasec.com` + managed TLS. Live. |

**Dependency order:** `38.1 → 38.2 → 38.3`, `38.1 → 38.4`, `{38.2, 38.4} → 38.5`,
`{38.3, 38.5} → 38.6`. `38.7` (hosting) depends only on a working app (38.1–38.4a) and runs
orthogonally. Running 38.1…38.6 satisfies "no phase waits on a future phase."

**Artifact in/out** (the aunt PDF case, orig. S7) is *not* its own phase — it lands in **38.2
(upload as room-scoped input)** and **38.3 (render the returned file)**; text-first, files second.

---

## 12. Design questions — resolutions

| # | Question | Resolution |
|---|---|---|
| 1 | **Room-scoped context assembly** | **Resolved (v1):** on `@`, the agent receives the `@`-prompt + a **bounded recent window** (last N messages *or* a token cap, whichever hits first), **room-only** (never cross-room). If the message is a reply, the `reply_to` target is included/prioritised ("the above"). All senders in the window are visible (human + other agents). N / token-cap configurable. |
| 2 | **Multiple agents in one room** | **Resolved (v1):** each `@`-invocation is **independent**; concurrent runs are fine; each agent posts its own attributed message on completion. **No cross-agent awareness in v1** — a judge reading others / live debate is a later feature. |
| 3 | **`@`-disambiguation** | **Deferred → 38.5** (registry autocomplete + typo tolerance). |
| 4 | **Artifact storage** | **Resolved:** bytes → **blob store behind an `IArtifactStore` seam** (local volume in dev, S3-compatible later); a **reference** (id/uri/mime/size) in the message payload jsonb; retrieval gated by room membership. Not Postgres large objects. |
| 5 | **Provider keys for provisioners** | **Resolved (v1):** platform-provided keys (built-in agents use Forge's configured provider). **BYOK deferred** — when added, keys encrypted in a secrets store, **never in jsonb or logs**, scoped to owner, never exposed to consumers. |
| 6 | **Agent presence / "typing"** | **Deferred (UX polish)** — reuse the progress stream when built. |
| 7 | **E2E encryption** | **Deferred (later hardening)** — rooms are the boundary in v1 (see §13). |
| 8 | **Minors in rooms** | **Deferred → 38.4** (consent/permission model in the identity phase). |
| 9 | **Mobile** | **Out of scope (v1)** — web-first; native mobile post-v1. |
| S1 | **Save-as-agent: snapshot vs parameterise** | **Resolved: snapshot** (rigid). Freeze the exact chain as a mission — pipeline shape + each step's instruction fixed, **entry input the sole parameter**. Deterministic/explainable, reuses the mission format; auto-parameterise routes to the program-synthesis spike. See 38.5. |

---

## 13. What is NOT in scope (v1)

- Native mobile clients · offline / multi-device sync · webscale fan-out · federation
- **Proactive agents** (pull-only is a tenet, not a temporary limitation for v1)
- **Third-party surface integrations** (WhatsApp/Telegram/Slack bots) — explicitly rejected (§5)
- **E2E encryption** — rooms are the boundary in v1; crypto is a later hardening (Q7)
- Adopting any whole chat *product* or *protocol* as a foundation (§5.4)

---

## 14. Connection to existing phases

Forge Rooms is **Phase 34/35 (Forge UI) growing up**: from "a 1:1 UI that runs a mission" into
"the multi-party room where agents are `@`-addressable members." Same engine (`PipelineRunner`,
missions, `StepEnvelope`), same Blazor foundation, extended along the accessibility axis. It is the
surface that makes every capability built in Phases 12–37 actually *reachable* — and, with the
eval harness (Phase 37) proving the reasoning is better, and Forge Rooms making it accessible, the
project finally delivers on `why.md`: reasoning that is both **trustable and reachable.**
