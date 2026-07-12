# Phase 41.2 — `kind: search` expert + search-fronted vanilla missions

> **Status: Design (2026-07-12)** · **Parent:** [Phase 41 — Live Retrieval](phase-41-live-retrieval-scout.md) ·
> **Depends on:** [41.1](phase-41.1-grok-web-search.md) (`ForgeMission.Scout` / `IWebSearch` — ✅ built) ·
> **AOT rules:** [CLAUDE.md](../../CLAUDE.md) §"AOT-first".
>
> **Done when:** a new native `kind: search` expert (an `IExpertRunner` wrapping Scout's `IWebSearch`)
> exists, and a **pure-MCL** vanilla mission fronts its answer with a *classify → conditionally-search →
> answer* pipeline — so `@`-mentioning that agent in a room answers current-events questions (that a bare
> LLM refuses) using Grok-sourced live data, while non-search questions pass straight through. Verified
> live in a room.

This spoke makes retrieval a **transparent capability of every vanilla agent**, not a special handle.
The whole front-end is expressed **in MCL** (composing existing `llm` + `json_extract` primitives with
`when()` guards); the *only* new native code is one primitive — `kind: search` — exactly as `kind: exec`
was added in Phase 32. The search backend is **implicitly Grok** for the POC (Scout's `GrokWebSearch`),
not yet configurable.

## Context an implementer needs (verified against the code 2026-07-12)

- **Expert kinds are native `IExpertRunner`s dispatched by string** in
  [`PipelineRunner`](../../src/ForgeMission.Core/Runtime/PipelineRunner.cs) — the switch appears **twice**
  (`ExecuteStepAsync` *and* `ExecuteParallelStepAsync`):
  ```csharp
  var runner = expert.Kind switch {
      "http" => new HttpExpertRunner(), "rule" => new RuleExpertRunner(),
      "onnx" => new OnnxExpertRunner(), "json_extract" => new JsonExtractExpertRunner(),
      "exec" => new ExecExpertRunner(_execution.DefaultTimeout),
      _ => ResolveRunner(step.Using) };   // ← unknown kinds fall through to an LLM provider call
  ```
  So an unrecognised `kind: search` would be mis-dispatched as an LLM call — **adding the switch case is
  the core wiring.** No kind whitelist exists in the parser/loader, so `kind: search` frontmatter already
  parses (`ExpertDefinition.Kind` is a free string, default `"llm"`).
- **`IExpertRunner`** ([iface](../../src/ForgeMission.Core/Runtime/IExpertRunner.cs)):
  `Task<StepEnvelope> RunAsync(ExpertDefinition, Dictionary<string,object> context, ct)` + `StreamAsync`.
  A runner **may mutate `context`** to publish named keys — `JsonExtractExpertRunner` does exactly this
  (`context[prop.Name] = …`). `PipelineRunner` then sets `context["output"] = envelope.Text` after each step.
- **`when()` guards** ([grammar](../../src/ForgeMission.Parser/MclGrammar.g4): `step : UPPER_ID contextClause? usingClause? whenClause?`)
  read a **context key**: `StringEqualsWhen` ⇒ `context[key]?.ToString() == value`. Step bindings are
  `Expert(key: value)`; the `when(...)` clause follows. **Two load-bearing behaviours:**
  1. Every step overwrites `context["output"]`, so **a guard must key off a *stable* context key** (one no
     later step rewrites), not `output`, when multiple steps share a branch.
  2. `PipelineRunner` **throws** if any guarded step exists, none matched, and there is **no `when(else)`**.
     ⇒ the template **must** include a `when(else)` branch.
- **The reference pattern already in-repo:** [`missions/when-routing`](../../missions/when-routing/mission.mcl)
  — a classifier emits a keyword, `when(output: "x")` guards route, `when(else)` catches the rest. We
  extend it from *mutually-exclusive terminal branches* to *classify → augment → answer*.
- **Vanilla mission shape today** ([`missions/vanilla`](../../missions/vanilla/mission.mcl)):
  `mission Vanilla(goal) = { Answerer }` where `Answerer` is `kind: llm` with `{{goal}}`. Per-provider
  clones exist: `missions/{vanilla,grok,openai,claude,assistant}`.

## The pure-MCL front-end (chosen shape)

Guards key off the **stable `search_needed`** flag (set once by the classifier, never overwritten), and a
`when(else)` direct branch satisfies the no-guard-match rule:

```mcl
mission Grok(goal) = {
    SearchRouter                                        // kind: llm — emits ```json {"search_needed":"yes|no","search_query":"…"} ```
    -> ExtractRoute                                     // kind: json_extract — publishes search_needed + search_query keys
    -> WebSearch(query: search_query) when(search_needed: "yes")   // kind: search — publishes search_results + search_sources
    -> GroundedAnswer when(search_needed: "yes")        // kind: llm — {{goal}} + {{search_results}} (+ {{search_sources}})
    -> DirectAnswer   when(else)                        // kind: llm — {{goal}} only  (fast passthrough)
}
output(Grok)
```

- **Only `WebSearch` is the new kind.** `SearchRouter`/`GroundedAnswer`/`DirectAnswer` are `kind: llm`;
  `ExtractRoute` is `kind: json_extract` — all existing.
- **Why classify via `json_extract`:** a structured `search_needed` boolean is a reliable guard signal, and
  the same step yields an **optimized `search_query`** (better than the raw conversational goal).
- **Cost/latency of the gate:** non-search turns still pay one small classify call → use a cheap model for
  `SearchRouter`. Search turns take Grok's server-side loop (**~41s measured in 41.1**) — see caveats.

## Tasks (chronological)

### Task 1 — `SearchExpertRunner` (the native primitive)
Create `src/ForgeMission.Core/Adapters/SearchExpertRunner.cs`:
```csharp
public sealed class SearchExpertRunner(Scout.IWebSearch search) : IExpertRunner
```
1. Add `ProjectReference` to `ForgeMission.Scout` in
   [`ForgeMission.Core.csproj`](../../src/ForgeMission.Core/ForgeMission.Core.csproj) (Scout is AOT-clean).
2. `RunAsync`: resolve the query from `context["query"]` (set by the `(query: …)` binding), falling back to
   `context["output"]`; call `search.SearchAsync(new WebSearchRequest(query), ct)`; **mutate context** (the
   `json_extract` pattern) to publish `context["search_results"]` = `result.Answer ?? ""` and
   `context["search_sources"]` = a newline list of `result.Sources[].Url` (attribution visible to the LLM);
   return `new StepEnvelope(result.Answer ?? "")`. `StreamAsync` delegates to `RunAsync` (like Http/JsonExtract).
3. Depends **only** on the `Scout.IWebSearch` abstraction — no provider types, no `HttpClient` here.
- **Done when:** unit test — a stub `IWebSearch` drives `SearchExpertRunner`; `search_results`/`search_sources`
  land in context and `envelope.Text` is the answer.

### Task 2 — Dispatch + wiring
1. Add `IsSearch => Kind.Equals("search", …)` to [`ExpertDefinition`](../../src/ForgeMission.Core/Experts/ExpertDefinition.cs).
2. Carry an optional `IWebSearch` on [`ExecutionConfig`](../../src/ForgeMission.Core/Runtime) (the config already
   passed to `PipelineRunner`); add `"search" => new SearchExpertRunner(_execution.WebSearch ?? throw new
   InvalidOperationException("kind: search requires a configured IWebSearch (Scout)."))` to **both** dispatch
   switches in `PipelineRunner`.
- **Done when:** a mission with `kind: search` dispatches to `SearchExpertRunner`; a clear error fires if no
  `IWebSearch` was configured.

### Task 3 — Construct the Grok search backend where runners are built
The vanilla missions execute on the containerized runner ([39.1](phase-39-metered-runtime-marketplace.md)).
1. Where the runner assembles its `IExpertRunner`s / `ExecutionConfig`, construct
   `new Scout.Grok.GrokWebSearch(httpClient, Environment.GetEnvironmentVariable("XAI_API_KEY")!)` and set it on
   `ExecutionConfig.WebSearch`. Operator key stays on the runner (39.1 decision). If `XAI_API_KEY` is absent,
   leave it null → `kind: search` fails clearly (missions without search are unaffected).
2. Give that `HttpClient` a generous timeout (≥ 2 min) — Grok's search loop is slow.
- **Done when:** the runner boots with search configured; a `kind: search` step reaches xAI live.

### Task 4 — The search-fronted vanilla mission (pure MCL) + experts
Rewrite one mission first — recommend **`missions/vanilla`** (or `missions/assistant`, the default room agent):
1. Replace `mission.mcl` with the 5-step shape above.
2. Add the experts under `experts/<Name>/expert.md` (self-contained per the 39.3 bundle rule):
   `SearchRouter` (emits the JSON fence; cheap model), `ExtractRoute` (`kind: json_extract`), `WebSearch`
   (`kind: search`), `GroundedAnswer` (`{{goal}}` + `{{search_results}}` + `{{search_sources}}`),
   `DirectAnswer` (`{{goal}}`). Refresh `mcl.lock` via `forge init`/lock.
- **Done when:** `forge run` on the mission answers a current-events question using live data and answers a
  static question via the fast `when(else)` path — both from the CLI.

### Task 5 — End-to-end pipeline test (**the verified guarantee**)
The unit test (Task 1) proves the component; this proves the **capability** — that `kind: search` works
*through MCL*, so `forge run mission.mcl` is a tested fact, not a theoretical claim. Add to
`src/ForgeMission.Tests` (follow the existing mission-through-pipeline pattern, e.g.
`Runtime/MissionCompositionTests.cs` + the `Missions/**` test fixtures already copied by the csproj):
1. Add a test mission (`.mcl` + experts) using the Task-4 shape under the Tests project's `Missions/`.
2. **Stub-backed variant (always-on, no network):** build `PipelineRunner` with a fake `IWebSearch` on
   `ExecutionConfig`; run the mission through the full parse→load→dispatch→`when()`→answer path. Assert:
   the `kind: search` step dispatched to `SearchExpertRunner`, the `search_needed:"yes"` branch searched
   and produced a grounded answer, and a `search_needed:"no"` input took the `when(else)` passthrough
   (no search call). This is the CI-enforced capability proof.
3. **Live-backed variant (`[SkippableFact]`, gated on `XAI_API_KEY`):** same mission, real `GrokWebSearch`
   — proves the external path end-to-end.
- **Done when:** the stub-backed e2e passes in CI with no network; the live-backed e2e passes with the key
  set. "`forge run` over a `kind: search` mission works" is now backed by a test.

### Task 6 — Verify live in a room
1. Point a room agent handle at the rewritten mission (the runner already resolves built-ins).
2. Ask a current-events question that a bare handle refuses (the World-Cup/Mario-Nawfal class); confirm a
   grounded answer with sources. Ask a static question; confirm the fast passthrough (no search call).
- **Done when:** both behaviours are verified in `forge.katasec.com` (or dev), screenshot/log captured.

### Task 7 — Roll out to the other vanilla providers
Apply the same template to `missions/{grok,openai,claude,assistant}` (search backend stays Grok; the
*answering* provider differs per mission — this is the cross-provider retrieval payoff). One mission per
commit; keep `vanilla` as the reference.
- **Done when:** every vanilla agent has transparent, gated search.

## Design decisions & caveats (record)

- **Verified capability, not theoretical.** A new expert kind is only "done" when a real `mission.mcl`
  exercises it **through the pipeline** (`forge run` / `PipelineRunner`), not merely when its `IExpertRunner`
  passes a unit test. The component test proves the SDK; the **end-to-end mission test (Task 5)** proves the
  *capability* — so any guarantee about `kind: search` is backed by an executed mission. This is the bar for
  every future kind, too.
- **Pure-MCL front-end, one native primitive.** Control flow (classify/gate/answer) is declared in the
  `.mcl`; `kind: search` is a language primitive like `llm`/`exec`. No runner middleware, no C# orchestration.
- **Guard on a stable key, not `output`.** Steps overwrite `output`; `search_needed` is set once and never
  rewritten, so both the `WebSearch` and `GroundedAnswer` guards evaluate correctly. `when(else)` is
  mandatory (avoids the no-guard-match throw).
- **Search provider implicitly Grok** (POC). `IWebSearch` keeps it swappable — a raw-API backend (41.3) or
  OpenAI (41.5) drops in without touching missions. When the answering provider *is* Grok, search + answer
  are two Grok calls (not optimized away — fine for POC).
- **Grok is an answer engine:** `search_results` = Grok's synthesized `Answer` (+ URL `search_sources`), not
  raw hits. So `GroundedAnswer` reasons over Grok's answer-as-a-source (the ProviderAnswer pattern). Raw-hit
  synthesis arrives with 41.3.
- **⚠ Latency:** search turns ~41s (server-side loop). The runner's synchronous-HTTP path (39.1) timeout must
  accommodate; a room "searching…" affordance is a later UX task.
- **⚠ Search cost is currently unmetered.** `SearchExpertRunner` calls xAI via `HttpClient`, not the
  `IChatClient` that `UsageTrackingChatClient` (39.2) wraps — so its tokens/cost escape the meter. Grok's
  response carries `usage.cost_in_usd_ticks`; surfacing it into the ledger is a **39.2×41.2 integration**,
  out of POC scope. Flag, don't fix here.
- **AOT:** `SearchExpertRunner` depends only on `Scout.IWebSearch`; Scout is AOT-clean; `Core → Scout` is safe
  for the AOT `forge` CLI.
