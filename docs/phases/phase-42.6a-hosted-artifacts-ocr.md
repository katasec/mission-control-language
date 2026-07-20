# Phase 42.6a — Hosted artifacts + OCR demo

> **Status: DONE + LIVE (2026-07-20).** Hosted dev runs `crforgeroomsdev.azurecr.io/forge-api:0.2.1`
> + `crforgeroomsdev.azurecr.io/forge-runner:0.10.4`. Full build narrative, live evidence, and
> task-by-task design detail: [completed doc](phase-42.6a-hosted-artifacts-ocr_completed.md).
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:**
> [42.6](phase-42.6-hosted-endpoint-ttfa.md) task 5a. **Sequenced before:** 42.6 task 5b
> (`forge claude @websearch` hosted chat-wire adapter). **AOT rules:** [AGENTS.md](../../AGENTS.md).
>
> **Done when (met):**
> ```
> forge exec ocr --input <path-or-url>
> forge exec ocr --mode=pdf --input <path-or-url> --out ./scan.ocr.pdf
> forge exec summarize --input <path-or-url>
> ```
> all run against the hosted endpoint with progress streaming, a 100 MB upload cap, artifact
> capability validation, and no binary bytes embedded in answer text/jsonb/base64 JSON.

## What shipped

- A first-class binary-artifact channel on the message-based hosted API (`UploadArtifact`,
  `GetArtifact`, additive `ExecuteMission`/`ExecuteMissionResponse` fields) — see **API Design**
  below for the still-current contract.
- **`@ocr`** — deterministic OCR demo: upload an image/PDF, get back extracted text or a searchable
  PDF (`--mode=text|pdf`).
- **`@summarize`** — OCR + verified LLM synthesis: upload a document, get back a grounded summary
  with a `✓ verified` badge (`Extract -> Answerer -> Verifier` pipeline).
- **URL input**: `--input` accepts an `http(s)://` URL as well as a local path on both missions —
  fetched client-side in the CLI, never server-side.
- Per-mission artifact capability validation (content-type/size) enforced before anything reaches
  the runner.
- Single-serve `GetArtifact` downloads (deleted after a completed response copy) and best-effort
  runner-side cleanup after each artifact is copied to the API — bounds ephemeral-scratch growth
  without a retention/TTL system.

## Patterns established here (reuse these, don't re-derive)

- **A step named exactly `Answerer` gets the "Verified" badge.** `MissionRunHandler.
  BuildAgentText` (`src/ForgeMission.Runner/MissionRunHandler.cs`) picks the verified answer text
  from a step literally named `Answerer` — any mission wanting the standard verified-answer
  treatment (not a bespoke output path) must name its answer-producing step that.
- **URL input is always fetched client-side, never server-side.** ForgeAPI/the runner must never
  fetch a caller-supplied URL — that's an SSRF surface (internal IPs, metadata endpoints, redirect
  chains). The CLI fetching a URL is the same trust boundary as the user running `curl` themselves.
- **A new hosted mission needs two hardcoded registrations, not just a mission directory:**
  `src/ForgeMission.Cli/BuiltinMissions.cs` (`BuiltinMissions.All` — so the runner discovers the
  package) and `src/ForgeMission.Api/MissionCatalog.cs` (`StaticMissionCatalog`'s constructor — so
  ForgeAPI resolves the handle). Both are independently required.
- **Mission packages are self-contained; small script duplication beats cross-package sharing.**
  `missions/<name>/experts/<Expert>/` is the unit of packaging — `@summarize`'s `Extract` expert
  duplicates `@ocr`'s Tesseract/`pdftoppm` extraction logic rather than sharing it, deliberately.
- **`kind: exec` timeout is per-expert, not just mission-wide** (`timeout:` in `expert.md`
  frontmatter) — needed when one step (e.g. multi-page PDF OCR) genuinely takes longer than the
  rest of the pipeline.

## API Design

Message-based invariants (from 42.6): the message is the contract, routes are HTTP projections;
mission identity is data, never a route segment; binary bytes never enter `Answer`/jsonb/JSON;
streaming is a sequence of messages, artifact events carry metadata only.

```csharp
public sealed class UploadArtifact                  // -> UploadArtifactResponse
{
    public int Version { get; set; }
    public string ClientToken { get; set; }
    public string Name { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
    public string Sha256 { get; set; }
}

public sealed class MissionArtifact
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
    public string Sha256 { get; set; }
    public string Role { get; set; }                // input | output
}
```

`ExecuteMission` carries `InputArtifactIds`; `ExecuteMissionResponse` carries `Artifacts`;
`MissionRunEvent` carries an optional `Artifact` for the streaming form.

HTTP transport:

```text
POST /api/UploadArtifact    raw byte body + metadata headers (X-Forge-Artifact-Name/Sha256/…)
POST /api/GetArtifact       message-authenticated raw byte response
POST /api/ExecuteMission    existing; references uploaded artifacts by id
```

No multipart, no base64 — `FileStream` → `StreamContent` → `HttpClient`; only JSON responses use
STJ source generation.

## Storage

```csharp
public interface IArtifactStore
{
    Task<MissionArtifact> SaveAsync(ArtifactWriteRequest request, Stream content, PlatformKeyContext owner, CancellationToken ct);
    Task<ArtifactRead?> OpenAsync(string artifactId, PlatformKeyContext owner, CancellationToken ct);
    Task DeleteAsync(string artifactId, PlatformKeyContext owner, CancellationToken ct);
}
```

Filesystem-backed today (rooted under configured temp storage); the interface is the swap point for
blob storage later. Access rules: artifacts owned by the platform-key principal; unknown/unauthorized
ids return not-found semantics (no existence leak); 100 MB cap enforced before reading unbounded
content; `GetArtifact` downloads are single-serve. **Known limitation:** an artifact that's never
downloaded still leaks until the process/container dies — a durable backend needs an explicit
TTL/sweeper before this becomes product storage, not a demo.

## Runner Contract

```csharp
public sealed record RunArtifact(string Id, string Name, string ContentType, long Size, string Sha256, string Role);
public sealed record RunRequest(string MissionRef, string Goal, IReadOnlyDictionary<string,string>? Vars, string Policy, IReadOnlyList<RunArtifact>? InputArtifacts);
public sealed record RunResponse(string AgentText, bool Verified, int StepCount, int RetryCount, IReadOnlyList<RunTraceStep> Trace, RunUsage Usage, IReadOnlyList<RunArtifact>? OutputArtifacts);
```

GHA-inspired per-run staging: `FORGE_WORK_DIR/{inputs,outputs}/`. Env vars passed to `kind: exec`:
`FORGE_WORK_DIR`, `FORGE_INPUT_DIR`, `FORGE_OUTPUT_DIR`, `FORGE_SOURCE_FILE`, `FORGE_MODE`. Matching
MCL context vars: `work_dir`, `input_dir`, `output_dir`, `source_file`, `mode`. Forge collects
output artifacts only from `FORGE_OUTPUT_DIR`; stdout is JSON status only, never binary.

## Manifest Capability Metadata

Artifact capabilities live in `forge.toml`, not `mission.mcl` or expert frontmatter:

```toml
[capabilities.artifacts.inputs.source]
content_types = ["image/jpeg", "image/png", "application/pdf"]
max_size_mb = 100

[capabilities.artifacts.modes.text]
output_content_type = "text/plain"
output_extension = ".txt"
default = true

[capabilities.artifacts.modes.pdf]
output_content_type = "application/pdf"
output_extension = ".pdf"
```

`ForgeManifest.Capabilities` owns the typed metadata; `ForgeTomlReader` parses it; ForgeAPI enforces
input content-type/size against it (via the runner's `GET /missions` → `StaticMissionCatalog`)
before any upload reaches the runner.

## Output path inference (`@ocr`)

| Input | Mode | Inferred output |
|---|---|---|
| `scan.jpg` | `text` | `scan.txt` |
| `scan.jpg` | `pdf` | `scan.pdf` |
| `scan.pdf` | `text` | `scan.txt` |
| `scan.pdf` | `pdf` | `scan.ocr.pdf` |

`--out` always wins. URL inputs use the inferred filename (from the URL path or content-type), never
the raw URL string, for this table's "Input" column.

## Tasks

| # | Task | Status | Done when |
|---|---|---|---|
| 1 | Lock artifact message contract | Done locally | `UploadArtifact`, `GetArtifact`, `MissionArtifact`, and Execute/Run additive fields implemented; full suite green. |
| 2 | Add manifest capability metadata | Done locally | `ForgeManifest.Capabilities` + `ForgeTomlReader` parse `capabilities.artifacts` with tests; no MCL grammar changes. |
| 3 | Add `ForgeAPI` artifact store seam | Done locally | Filesystem `IArtifactStore` tested for metadata, owner isolation, declared 100 MB cap. |
| 4 | Add upload/download HTTP projections | Done locally | `UploadArtifact`/`GetArtifact` endpoints + CLI raw upload/download client path implemented. |
| 5 | Extend runner contract + GHA-style staging | Done locally | Runner stages input artifacts, sets `FORGE_*` env vars + MCL context vars, cleans up after the run. |
| 6 | Extend output artifact collection | Done locally | Deterministic artifact path tested through `MissionExecutionService`; CLI writes returned artifacts to disk. |
| 7 | Build hosted OCR mission (`mode=text`) | Done live | Hosted run wrote a `text/plain` artifact; debited; runner verified one `Ocr` step. |
| 8 | Build hosted OCR mission (`mode=pdf`) | Done live | Hosted run wrote an `application/pdf` artifact; debited; runner verified one `Ocr` step. |
| 9 | Deploy and verify | Done live | `forge-api`/`forge-runner` deployed; `UploadArtifact`/`ExecuteMission`/`GetArtifact` all 200; binary traveled via `GetArtifact`, not answer JSON. |
| 10 | URL input source for `forge exec` | Done live | CLI downloads an `http(s)://` `--input` client-side, uploads through the existing local-file path unchanged; byte-identical output vs. local-file run. Shipped in CLI `v0.9.0`. |
| 11 | `@summarize` — OCR + verified LLM synthesis | Done live | `Extract -> Answerer -> Verifier` pipeline live-verified against a real public contract PDF; `✓ verified`, debited. Merged via PR #4. |

Full evidence, rationale, and per-task design narrative: [completed doc](phase-42.6a-hosted-artifacts-ocr_completed.md).

## Open Decisions

All launch-shaping decisions are locked; reopen only if implementation proves one impossible:

- Raw byte upload, no multipart/base64. Message-name endpoints (`UploadArtifact`, `GetArtifact`).
- Ephemeral run scratch only, no retention promise. 100 MB max upload, no chunking.
- Tesseract-first; PaddleOCR remains a later dedicated-runner upgrade if usage justifies the weight.
- URL input fetches client-side (CLI), never server-side — no SSRF surface, no allowlist infra.

## Non-Goals

- No `forge claude` hosted adapter work here; that's 42.6 task 5b.
- No long-term document management or user file library.
- No user-authored arbitrary OCR/summarize missions.
- No GPU scheduling. No base64 artifact storage in Postgres/jsonb.
- No chunking/map-reduce for long documents in `@summarize`.
