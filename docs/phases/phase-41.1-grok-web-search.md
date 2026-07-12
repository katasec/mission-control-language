# Phase 41.1 — Grok web_search POC (`ForgeMission.Scout`)

> **Status: Design → building** (2026-07-12) · **Parent:** [Phase 41 — Live Retrieval](phase-41-live-retrieval-scout.md) ·
> **Depends on:** none (standalone library + smoke test) · **Regression risk:** none (net-new project) ·
> **AOT rules:** [CLAUDE.md](../../CLAUDE.md) §"AOT-first".
>
> **Done when:** `ForgeMission.Scout` builds AOT-clean, and a standalone smoke test calls
> `GrokWebSearch.SearchAsync("summarize the latest news from Mario Nawfal's YouTube channel")` and prints
> a synthesized `Answer` plus a non-empty list of `SourceRef`s each tagged `Provider = "grok"`.

This spoke is the **POC**: the swap-point interface + one Grok-backed implementation + a smoke test. No
MCL mission wiring (that's 41.2), no second backend. Scoped so an agent can build it from this doc alone.

## Context an implementer needs

- **Grok is an answer engine** (verified — see [hub §3](phase-41-live-retrieval-scout.md#3-verified-api-landscape-checked-against-live-docs-2026-07-12)):
  it returns a synthesized answer + citations that are `url` + a numeric label + character offsets, with
  **no page content**. So for Grok, `WebSearchResult.Answer` is populated and `Sources` carry the
  citation/`sources` URLs (title when present). This is expected and correct — the POC consumes the
  answer; the "raw → MCL synthesizes" path arrives with a raw-API backend in 41.3.
- **Do not use the OpenAI SDK.** `ProviderClientBuilder` reaches Grok as OpenAI-compatible for *chat*,
  but the OpenAI SDK cannot emit a `{"type":"web_search"}` **server-side tool**. This POC uses a **direct
  `HttpClient` POST** with a hand-built body + STJ source-gen. Different path from chat — that's fine.
- **Auth/model:** `XAI_API_KEY` env var; model `grok-4.5` (or `grok-4.5` alias). Endpoint base
  `https://api.x.ai/v1`.
- **One wire detail to confirm before finalizing DTOs (Task 1):** the exact endpoint
  (`/v1/chat/completions` vs a `/v1/responses`-style endpoint), where the tool config nests, and where
  citations/annotations land in the response. Behavior + field *names* are verified; the exact JSON
  envelope is the one thing to pin from the live API reference rather than assume.

## Tasks (chronological)

### Task 1 — Confirm the exact xAI request/response envelope

Before writing DTOs, pull the current shape from the API reference so the source-gen contracts match reality.

1. Fetch [xAI web_search tool](https://docs.x.ai/developers/tools/web-search) + the chat/completions API
   reference. Confirm: (a) endpoint path, (b) request field carrying the tool
   (`tools: [{"type":"web_search", …}]` and whether `allowed_domains`/`excluded_domains` nest inside the
   tool object or under `filters`), (c) response location of the synthesized text and of citations
   (`choices[0].message.content`; `citations` vs `annotations`, and their fields).
- **Done when:** a curl against `api.x.ai` with `XAI_API_KEY` returns an answer + citations, and the exact
  JSON field names are written down for Task 3.

### Task 2 — Scaffold `ForgeMission.Scout`

1. Create `src/ForgeMission.Scout/ForgeMission.Scout.csproj` — a library, `net10.0`, `RootNamespace=Scout`,
   `Nullable=enable`, `ImplicitUsings=enable`. No `PublishAot` on the lib itself, but keep it **AOT-clean**
   (it will be referenced by the AOT CLI eventually): no reflection, no bare `JsonSerializerOptions`.
2. Add it to [`src/ForgeMission.slnx`](../../src/ForgeMission.slnx) as a `<Project>` entry.
- **Done when:** `dotnet build src/ForgeMission.Scout` succeeds and the project appears in the solution.

### Task 3 — The swap-point interface + provider-neutral DTOs

Create `src/ForgeMission.Scout/IWebSearch.cs` (namespace `Scout`). Exactly the [hub §4](phase-41-live-retrieval-scout.md#4-locked-decisions)
shape — **no xAI types leak**:

```csharp
namespace Scout;

public interface IWebSearch
{
    Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct = default);
}

public sealed record WebSearchRequest(
    string Query,
    IReadOnlyList<string>? AllowedDomains = null);   // POC: optional scoping; more knobs later

public sealed record WebSearchResult(
    string Provider,                                 // "grok"
    string? Answer,                                  // answer-engine synthesis (null for raw APIs)
    IReadOnlyList<SourceRef> Sources);

public sealed record SourceRef(
    string Url,
    string? Title,
    string Provider,                                 // "grok" — travels with each source
    double? ImpartialityRating = null);              // future hosted per-domain rating (41.6)
```

- **Done when:** the interface + records compile with zero provider-specific references.

### Task 4 — STJ source-gen contracts for the Grok wire types

Create `src/ForgeMission.Scout/Grok/GrokWire.cs` — internal DTOs matching the JSON confirmed in Task 1
(request body with the `web_search` tool; response with content + citations), plus a source-gen context
per [CLAUDE.md](../../CLAUDE.md):

```csharp
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GrokRequest))]
[JsonSerializable(typeof(GrokResponse))]
internal partial class GrokJsonContext : JsonSerializerContext { }
```

Use `GrokJsonContext.Default.<Type>` for (de)serialization — **never** a bare `JsonSerializerOptions`.

- **Done when:** request serializes and a captured sample response deserializes, both via the source-gen
  context, no reflection warnings.

### Task 5 — `GrokWebSearch : IWebSearch`

Create `src/ForgeMission.Scout/Grok/GrokWebSearch.cs`:

```csharp
public sealed class GrokWebSearch(HttpClient http, string apiKey, string model = "grok-4.5") : IWebSearch
```

1. Build `GrokRequest`: `model`, `messages = [ user(request.Query) ]`, the `web_search` tool (with
   `AllowedDomains` mapped in if present), `enable_image_understanding=false`, `enable_image_search=false`.
2. `POST {base}/v1/chat/completions` with `Authorization: Bearer {apiKey}`; serialize via
   `GrokJsonContext`.
3. Parse: `Answer = choices[0].message.content`; map each citation → `SourceRef(url, title, "grok")`
   (title null when Grok gives only a label). De-dupe by URL.
4. Return `new WebSearchResult("grok", answer, sources)`.
5. Throw a typed `WebSearchException` on non-2xx with the status + body snippet (no silent empty result).
- **Done when:** a live call returns a populated `WebSearchResult` with `Provider="grok"`, a non-empty
  `Answer`, and ≥1 `SourceRef`.

### Task 6 — Smoke test

A minimal runnable proof (a xUnit test in `src/ForgeMission.Tests`, gated on `XAI_API_KEY` being set so CI
without the key skips it, **or** a tiny `dotnet run` console under the scratchpad — implementer's choice):

1. Construct `GrokWebSearch(new HttpClient(), Environment.GetEnvironmentVariable("XAI_API_KEY")!)`.
2. `await search.SearchAsync(new("Summarize the latest news from Mario Nawfal's YouTube channel"))`.
3. Assert/print: non-empty `Answer`; `Sources` non-empty; every `SourceRef.Provider == "grok"`.
- **Done when:** the test prints a real summary + source URLs, demonstrating the "no real-time data" wall
  from Forge Rooms is broken by a source-attributed, provider-neutral result.

## Not in this spoke

- MCL wiring (`kind: search` / tool on `llm`) → **41.2**.
- Raw-API backend that exercises MCL-side synthesis → **41.3**.
- `x_search` (X access) → **41.4**. OpenAI → **41.5**. Impartiality ratings → **41.6**.
