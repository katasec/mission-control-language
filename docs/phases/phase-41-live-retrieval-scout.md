# Phase 41 — Live Retrieval (Scout): reach the internet, reason in MCL

> **Status: Design + POC in progress (2026-07-12).** A new library, **`ForgeMission.Scout`**, gives the
> platform a **generic capability to reach the live internet** and return a **provider-neutral shape MCL
> reasons over** — so a conversation is no longer frozen at the model's training cutoff. Built against a
> single swap-point interface (`IWebSearch`); **first backend is Grok** (empirically best at
> summarizing news from YouTube in hands-on testing); OpenAI / Tavily / Exa are additive later.
>
> **Parent:** grows out of [Phase 38 — Forge Rooms](phase-38-forge-rooms.md) (the chat surface where the
> "no real-time data" wall shows up) · **Consumes:** provider wiring precedent in
> [`ProviderClientBuilder`](../../src/ForgeMission.Cli/ProviderClientBuilder.cs).
>
> **Done when (phase):** an MCL reasoning chain can call `IWebSearch`, receive current web data as a
> provider-neutral, source-attributed result, and synthesize over it — with the backend swappable without
> touching a single caller.

---

## 1. Why this, why now

Every bare LLM handle in Forge Rooms (`@openai`, `@claude`, `@grok`) hits the same wall: ask *"who's
playing in the World Cup today?"* and it correctly refuses — *"I don't have access to real-time data."*
That is not a bug and not fixable by prompting; the model genuinely has no data past its training cutoff.
**Retrieval is a missing capability, not a missing instruction.**

The generalization: **almost every useful conversation eventually needs data past the cutoff.** So
"reach the internet, get a shape back, feed it to reasoning" is a *foundational* capability, not a
feature. This phase adds it.

## 2. The load-bearing principle — retrieval ≠ reasoning

This is the MCL thesis applied to search: **the search returns raw data; the *reasoning chain*
synthesizes.** Keeping synthesis in MCL (not in the search provider) is what makes it:

- **structured** — you author the steps (cluster → weigh → conclude),
- **inspectable** — you can see and change how sources were fused,
- **composable** — optimist / skeptic / judge can reason over the *same* evidence differently,
- **checkable** — a `rule` / `exec` expert can verify dates or a source whitelist *before* synthesis.

**Synthesis, defined (this phase's vocabulary):** the step that *fuses multiple sources into one authored
answer*, making editorial judgments — inclusion/omission, weighting conflicting sources, reconciliation,
framing/ordering, conclusion. It is lossy and opinionated; once done, those judgments are sealed. An LLM
synthesizer performs all of it *invisibly*. Our principle is to keep that editorial act inside MCL.

## 3. Verified API landscape (checked against live docs 2026-07-12)

The verify-don't-hallucinate pass that grounds every decision below. **The chat models' hosted search are
*answer engines*, not *search engines* — they synthesize by design.** Only raw search APIs return raw
evidence with no synthesis.

| Backend | Default output | Raw hits exposed? | Turn synthesis off? | Notes |
|---|---|---|---|---|
| **Grok** `web_search` / `x_search` | synthesized answer | **none** (citations = `url` + label + char-offsets, no content) | no | server-side autonomous loop; `x_search` is X-only, handle-filterable (`allowed_x_handles`, max 20) |
| **OpenAI** `web_search` | synthesized answer | **yes** via `include:["web_search_call.results"]` (snippets; reasoning-models; byproduct of forced synthesis) + `sources` (all consulted URLs) | no | citation annotation = `type/url/title/start_index/end_index` |
| **Tavily / Exa / Brave** | **raw hits only** | **yes, full** (Exa returns page text) | n/a (never synthesizes) | the only family that cleanly honors "MCL synthesizes" |

Sources: [xAI web_search](https://docs.x.ai/developers/tools/web-search) ·
[xAI x_search](https://docs.x.ai/developers/tools/x-search) ·
[xAI citations](https://docs.x.ai/developers/tools/citations) ·
[xAI models](https://docs.x.ai/developers/models) ·
[OpenAI web search](https://developers.openai.com/api/docs/guides/tools-web-search).

**Ranking for the principle (raw → MCL synthesizes):** raw API (cleanest) > OpenAI (raw available, but a
byproduct of forced synthesis, reasoning-only, snippet-depth) > Grok (no raw at all).

## 4. Locked decisions

1. **`IWebSearch` is the single swap point.** Callers depend only on it +
   provider-neutral `WebSearchRequest` / `WebSearchResult` — **never** on Grok/OpenAI/Tavily types. Adding
   a backend = one new class, zero caller changes (same shape as `ProviderClientBuilder`'s switch).
2. **One result shape spans both backend families** (answer-engines *and* raw APIs):
   ```csharp
   record WebSearchResult(string Provider, string? Answer, IReadOnlyList<SourceRef> Sources);
   record SourceRef(string Url, string? Title, string Provider, double? ImpartialityRating = null);
   ```
   `Answer` is populated by answer-engines, **null for raw APIs**. **`Sources` is the contract; `Answer`
   is optional** — downstream MCL anchors on `Sources` so swapping in a raw backend never surprises a
   chain. Grok → `Answer` + URL-only `Sources`; OpenAI → `Answer` *and* real `Sources`; Tavily/Exa →
   `Sources` only.
3. **Source attribution over source-selection.** Every result + every `SourceRef` carries a `Provider`
   tag; `ImpartialityRating` is a null stub for a *future hosted per-domain rating*. We **do not** chase
   "the best source" (an infinite quest) — we label provenance and let policy sort it out downstream.
   This is why the raw-vs-synthesized purity debate is a non-blocker: it's managed as metadata.
4. **Synthesis policy is per-domain, not absolute.** Open web → prefer raw → MCL synthesizes (a raw API
   honors it). **X walled garden → accept Grok's synthesis, because access *is* the value** (X has no raw
   API; scrapers are blocked; Grok owns the firehose). `x_search` is a *privileged-access answer engine*,
   not a raw-search backend it structurally can't be.
5. **AOT-first wire path.** Direct `HttpClient` + **STJ source-generation** for request/response DTOs.
   **No OpenAI SDK** for Grok — it can't emit a `{"type":"web_search"}` server-side tool. No bare
   `JsonSerializerOptions`. (Follows [CLAUDE.md](../../CLAUDE.md) AOT rules.)
6. **Model = `grok-4.5`** (current flagship; xAI says "use for everything," billed most-intelligent *and*
   fastest). The old `grok-4` / `grok-4-fast` line is retiring (`grok-4-fast-reasoning` → `grok-4.3`,
   **migrate by 2026-08-15**). **The API has no "auto"** (that's a consumer-UI router only) — pin a model
   id, use the `<model>` alias (→ latest stable) to avoid chasing version strings.
7. **Project = `ForgeMission.Scout`**, root namespace `Scout`, added to
   [`ForgeMission.slnx`](../../src/ForgeMission.slnx) (a curated subset — add it explicitly).

## 5. Spokes (dependency-ordered)

| Spoke | Scope | Status |
|---|---|---|
| **[41.1 — Grok web_search POC](phase-41.1-grok-web-search.md)** | `ForgeMission.Scout` project + `IWebSearch` + `GrokWebSearch` (grok-4.5, `web_search` tool, direct HTTP + STJ, source-tagged results) + a smoke test that summarizes YouTube news. **The build now.** | **Design → building** |
| **[41.2 — `kind: search` expert + search-fronted vanilla missions](phase-41.2-search-expert-kind.md)** | New native `kind: search` primitive (an `IExpertRunner` wrapping `IWebSearch`) + a **pure-MCL** *classify → conditionally-search → answer* front-end on the vanilla missions, so every agent gains transparent, gated live-search (search backend implicitly Grok). Uses `when()` guards on a stable `search_needed` key + `when(else)`. | **Design (spec written)** |
| 41.3 — Raw-API backend (Tavily/Exa) | The backend that actually exercises "raw → MCL synthesizes"; proves the interface spans both families. | Planned |
| 41.4 — Grok `x_search` (X access) | The X walled-garden answer-engine, handle + date scoped. Second backend. | Planned |
| 41.5 — OpenAI backend | `web_search` + raw `web_search_call.results`; fills both `Answer` and `Sources`. | Planned |
| 41.6 — Hosted impartiality ratings | Populate `SourceRef.ImpartialityRating` from a managed per-domain table. | Planned |

## 6. Out of scope (POC)

- Any backend other than Grok `web_search` (41.3–41.5).
- MCL mission wiring (41.2) — the POC is the library + a standalone smoke test, callable in isolation.
- Impartiality scoring beyond the null stub (41.6).
- A runtime browse-loop / agentic `fetch(url)` tool (the "paste any link, it traverses" UX) — a raw API
  covers the open web; YouTube-shaped sources have RSS; the browse-loop only earns its keep on the
  feedless long tail. Explicitly deferred.
