# Phase 42.6a — Hosted artifacts + OCR demo

> **Status: Done and live on hosted dev (2026-07-20).** Inserted before the hosted `forge claude @websearch` adapter because 42.6's
> one-shot API has already proven the hosted path, and binary artifacts are the next useful
> capability unlocked by `forge exec`: upload a file, run a mission once, and receive a produced
> file back.
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:**
> [42.6](phase-42.6-hosted-endpoint-ttfa.md) task 5a (`ExecuteMission`, streaming progress,
> platform-key billing live). **Sequences before:** 42.6 task 5b (`forge claude @websearch`
> hosted chat-wire adapter). **AOT rules:** [AGENTS.md](../../AGENTS.md).
>
> **Done when:** a signed-in user can run both:
> ```
> forge exec ocr --input ./scan.jpg
>   → ./scan.txt written to disk, debited, no provider account
>
> forge exec ocr --mode=pdf --input ./scan.jpg --out ./scan.ocr.pdf
>   → searchable PDF written to disk, debited, no provider account
> ```
> against the hosted endpoint, with progress streaming, a 100 MB upload cap enforced, and
> no binary bytes embedded in answer text, jsonb, or base64 JSON.

## Why This Exists

`websearch` proved the hosted one-shot shape:

```bash
forge exec websearch "what shipped in the Claude API this week?"
```

OCR is the next natural proof because it is a **file-native one-shot capability**. A user does not
want a chat session; they want to hand Forge an image/PDF and get back text or a searchable PDF.
That makes it the right forcing function for first-class artifacts:

```bash
forge exec ocr --input ./invoice.png
forge exec ocr --mode=pdf --input ./scan.jpg --out ./scan.ocr.pdf
```

The goal is not "PaddleOCR integration" by itself. The goal is proving the hosted API can carry
real binary inputs and outputs while preserving the message-based API: contracts carry ids and
metadata; raw bytes travel on an authenticated byte projection.

## Current State

What exists now:

- `ForgeAPI` API A is message-based JSON/NDJSON only:
  `ExecuteMission`, `SearchMissions`, `GetMission`, `GetAccount`, `GetRun`.
- Streaming is already correctly modeled as a sequence of `MissionRunEvent` messages.
- `UploadArtifact` and `GetArtifact` are message-name HTTP projections for raw bytes.
- `ExecuteMission` accepts `InputArtifactIds`; `ExecuteMissionResponse` returns artifact metadata.
- The runner contract carries `RunArtifact` inputs/outputs without embedding bytes in JSON.
- The runner stages inputs under per-run scratch, exports `FORGE_*` env vars to `kind: exec`, and
  collects output artifacts from `FORGE_OUTPUT_DIR`.
- `forge exec ocr --input ...` and `forge exec ocr --mode=pdf --input ...` have CLI-side artifact
  upload/download/output-path inference.
- `missions/ocr` writes text/PDF artifacts. It uses Tesseract when the runner image provides it and
  falls back to deterministic placeholder output in local environments without OCR tools.
- Hosted dev is deployed with `crforgeroomsdev.azurecr.io/forge-api:0.2.0` and
  `crforgeroomsdev.azurecr.io/forge-runner:0.10.1`. Runner `0.10.1` includes the OCR mission
  recursion fix (`mission Ocr` now runs expert `OcrExec`, not a same-named sub-mission).
- `docs/design/room-artifacts.md` describes a room artifact plane, but the current `src/` tree has
  no base64 artifact DTOs. Treat that doc as design/history for room artifacts, not as this shipped
  one-shot binary channel.
- `MessagePayload` already records the important storage invariant: large binary belongs in blob
  storage, never jsonb.

## Live Evidence

Verified against hosted dev on 2026-07-20:

- `forge exec ocr --input /private/tmp/forge-ocr-scan.jpg --out /private/tmp/forge-ocr-output.txt`
  wrote a 60-byte ASCII text artifact containing `FORGE OCR LIVE TEST`, `Invoice 12345 total
  67.89`, and `July 20 2026`; API log: `Debited 14µ$ ... Ocr 0+0 tok / 0.90s`; `GetArtifact`
  returned `200 60 text/plain`.
- `forge exec ocr --mode=pdf --input /private/tmp/forge-ocr-scan.jpg --out
  /private/tmp/forge-ocr-output.pdf` wrote a 52 KB one-page PDF
  (`sha256=4bb5eba9caeb845243c5f04b1e625ba004051f2b2b003da6d514e88b03059b29`); API log:
  `Debited 12µ$ ... Ocr 0+0 tok / 0.79s`; `GetArtifact` returned `200 53602 application/pdf`.
- Runner log for both runs: `Ran 'Ocr' [trusted] — verified=True steps=1`, and startup advertised
  `loaded 7 mission(s): ChatGPT, Forge, Assistant, Claude, Grok, WebSearch, Ocr`.

## UX

Text extraction (`mode=text`, the default):

```bash
$ forge exec ocr --input ./invoice.png
… Uploading invoice.png
… Loading OCR model
… Detecting text
… Recognizing 34 text regions
… Assembling reading order

Created: ./invoice.txt

✓ extracted · 34 regions · avg confidence 0.97
```

Searchable PDF:

```bash
$ forge exec ocr --mode=pdf --input ./scan.jpg --out ./scan.ocr.pdf
… Uploading scan.jpg
… Loading OCR model
… Detecting text
… Building searchable PDF

Created: ./scan.ocr.pdf

✓ searchable PDF · 1 page · 42 regions · avg confidence 0.96
```

Structured extraction remains an ordinary downstream mission behavior:

```bash
forge exec ocr --mode=text --input ./invoice.png "extract vendor, invoice number, due date, and total"
```

The natural primary input is the file path; the optional prompt is an instruction layered on top.
`mode=text` is the default because OCR most naturally means text extraction. `mode=pdf` is explicit
because it creates a searchable PDF.

Output path inference:

| Input | Mode | Inferred output |
|---|---|---|
| `scan.jpg` | `text` | `scan.txt` |
| `scan.jpg` | `pdf` | `scan.pdf` |
| `scan.pdf` | `text` | `scan.txt` |
| `scan.pdf` | `pdf` | `scan.ocr.pdf` |

`--out` always wins.

## API Design

Keep the existing message-based invariants from 42.6:

- The message is the contract; routes are HTTP projections.
- Mission identity stays data, not a route segment.
- Binary bytes are not stuffed into `Answer`, jsonb, or long-lived JSON fields.
- Streaming remains a sequence of messages; artifact events carry metadata, not whole PDFs.

Additive message shapes:

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

public sealed class UploadArtifactResponse
{
    public MissionArtifact Artifact { get; set; }
    public ResponseStatus ResponseStatus { get; set; }
}

public sealed class GetArtifact                     // -> raw-byte HTTP projection + metadata headers
{
    public int Version { get; set; }
    public string ArtifactId { get; set; }
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

Extend existing messages additively:

```csharp
public sealed class ExecuteMission
{
    // existing fields...
    public List<string>? InputArtifactIds { get; set; }
}

public sealed class ExecuteMissionResponse
{
    // existing fields...
    public List<MissionArtifact> Artifacts { get; set; }
}

public sealed class MissionRunEvent
{
    // existing fields...
    public MissionArtifact? Artifact { get; set; }  // Type == "artifact"
}
```

HTTP transport mapping:

```text
POST /api/UploadArtifact    raw byte body + metadata headers
POST /api/GetArtifact       message-authenticated raw byte response
POST /api/ExecuteMission    existing; references uploaded artifacts by id
```

Locked upload shape:

```text
POST /api/UploadArtifact
Authorization: Bearer <platform-key>
Content-Type: <file content type, or application/octet-stream>
X-Forge-Artifact-Name: scan.jpg
X-Forge-Artifact-Sha256: ...
X-Forge-Artifact-Client-Token: ...
<body = raw bytes>
```

No multipart and no base64 for the first build. This is AOT-friendly for the CLI:
`FileStream` -> `StreamContent` -> `HttpClient`; only the JSON response uses STJ source generation.

Downloads also keep message names authoritative:

```text
POST /api/GetArtifact
Authorization: Bearer <platform-key>
Content-Type: application/json
<body = { "version": 1, "artifactId": "art_..." }>
```

The response body is raw bytes with metadata headers (`Content-Type`, `Content-Disposition`,
`X-Forge-Artifact-Sha256`, `X-Forge-Artifact-Size`). A pretty `GET /artifacts/{id}` can exist later
as non-authoritative sugar, but not as the contract.

## Storage

Introduce an `IArtifactStore` seam in `ForgeMission.Api`:

```csharp
public interface IArtifactStore
{
    Task<MissionArtifact> SaveAsync(
        ArtifactWriteRequest request, Stream content, PlatformKeyContext owner, CancellationToken ct);

    Task<ArtifactRead?> OpenAsync(
        string artifactId, PlatformKeyContext owner, CancellationToken ct);
}
```

Implementation sequence:

1. Local filesystem store for tests/dev, rooted under configured temp storage.
2. Hosted dev may use the same ephemeral filesystem scratch for the demo if one ForgeAPI replica
   handles upload/execute/download. Azure Blob is the follow-up if live topology needs cross-replica
   handoff before marking the phase robust.

Access rules:

- Artifacts are owned by the platform-key principal.
- Unknown or unauthorized artifact ids return not-found semantics; do not leak existence.
- Enforce a 100 MB upload cap before reading unbounded content.
- Artifacts are **ephemeral run scratch only**. Uploaded input bytes live only long enough for the
  run; produced output bytes live only long enough for the CLI to fetch and save them. No user-facing
  retention guarantee, no document library.

## Runner Contract

Extend `ForgeMission.Runner.Contracts` additively:

```csharp
public sealed record RunArtifact(
    string Id,
    string Name,
    string ContentType,
    long Size,
    string Sha256,
    string Role);

public sealed record RunRequest(
    string MissionRef,
    string Goal,
    IReadOnlyDictionary<string, string>? Vars,
    string Policy,
    IReadOnlyList<RunArtifact>? InputArtifacts);

public sealed record RunResponse(
    string AgentText,
    bool Verified,
    int StepCount,
    int RetryCount,
    IReadOnlyList<RunTraceStep> Trace,
    RunUsage Usage,
    IReadOnlyList<RunArtifact>? OutputArtifacts);
```

The runner stages input artifacts into a per-run working directory using a GitHub Actions-inspired
contract:

```text
FORGE_WORK_DIR/
  inputs/
  outputs/
```

Environment variables passed to `kind: exec`:

```text
FORGE_WORK_DIR
FORGE_INPUT_DIR
FORGE_OUTPUT_DIR
FORGE_SOURCE_FILE
FORGE_MODE
```

Matching MCL context vars:

```text
work_dir
input_dir
output_dir
source_file
mode
```

Exec experts may write internal scratch files anywhere under `FORGE_WORK_DIR`, but Forge collects
artifacts only from `FORGE_OUTPUT_DIR`. Stdout remains for structured JSON summary/status, not
binary payloads. No hardcoded paths in mission tooling.

## Manifest Capability Metadata

Artifact capabilities live in the mission package manifest (`forge.toml`), not in `mission.mcl`
and not in expert frontmatter. The `.mcl` file stays pure pipeline structure; `forge.toml`
describes package/catalog/runtime capability metadata.

OCR manifest shape:

```toml
[mission]
name = "ocr"
description = "Extract text from images and PDFs, or produce a searchable PDF."

[capabilities.artifacts.inputs.source]
content_types = [
  "image/jpeg",
  "image/png",
  "application/pdf",
]
max_size_mb = 100

[capabilities.artifacts.modes.text]
output_content_type = "text/plain"
output_extension = ".txt"
default = true

[capabilities.artifacts.modes.pdf]
output_content_type = "application/pdf"
output_extension = ".pdf"
```

This requires manifest-reader work, not MCL grammar/parser work. Keep it SRP-shaped:
`ForgeManifest.Capabilities` owns the typed metadata; `ForgeTomlReader` parses it; CLI/catalog
consume typed capabilities rather than scraping TOML strings.

## OCR Mission Design

Use one hosted `kind: exec` mission first. For the existing multi-arch runner, the first real OCR
engine is **Tesseract**: classical ML, CPU-only, apt-packaged on amd64/arm64, and small enough to
bake into the shared runner without adding a dedicated OCR image. PaddleOCR/PP-OCRv6 remains the
likely upgrade if this becomes more than a demo: it is a fuller document OCR pipeline, but its
PaddlePaddle runtime/model packaging is heavier and better suited to a dedicated scale-to-zero OCR
runner if/when usage justifies that extra infra.

Initial mission:

```text
missions/ocr/
  forge.toml
  mission.mcl
  experts/Ocr/expert.md          kind: exec
  experts/Ocr/ocr.py
```

First implementation can use Python in the runner image. This does not violate AOT because
`kind: exec` tooling runs out-of-process and is not linked into the .NET binary.

Model/runtime choices:

- Use the existing runner for 42.6a. No dedicated OCR runner, no runner pool, no multi-runner
  routing. YAGNI until real usage justifies it.
- Use small test images for the demo. If OCR proves useful and image size/runtime becomes painful,
  split to a dedicated scale-to-zero OCR runner later.
- Bake only the minimum OCR dependencies needed for the first demo (`tesseract-ocr`,
  `poppler-utils` for PDF-to-image text extraction).
- Do not download models on every request.
- CPU-first. GPU declarations are a later `resources:` concern.

## Tasks

| # | Task | Status | Done when |
|---|---|---|---|
| 1 | Lock artifact message contract | Done locally | `UploadArtifact`, `GetArtifact`, `MissionArtifact`, and Execute/Run additive fields implemented in `Messages.cs` + runner contracts; full suite green 2026-07-20. |
| 2 | Add manifest capability metadata | Done locally | `ForgeManifest.Capabilities` + `ForgeTomlReader` parse `capabilities.artifacts` with tests; no MCL grammar changes; full suite green 2026-07-20. |
| 3 | Add `ForgeAPI` artifact store seam | Done locally | Filesystem `IArtifactStore` tested for metadata, owner isolation, and declared 100 MB cap; full suite green 2026-07-20. |
| 4 | Add upload/download HTTP projections | Done locally | `UploadArtifact`/`GetArtifact` endpoints and CLI raw upload/download client path implemented; hosted auth smoke still covered by task 9. |
| 5 | Extend runner contract + GHA-style staging | Done locally | Runner receives input artifact metadata, stages bytes under `FORGE_WORK_DIR/inputs`, sets `FORGE_*` env vars + MCL context vars, and cleans up after the run. |
| 6 | Extend output artifact collection | Done locally | Deterministic artifact path tested through `MissionExecutionService`; CLI writes returned artifacts to disk; local script fallback produced `.txt` and `.pdf` in `/private/tmp`. |
| 7 | Build hosted OCR mission (`mode=text`) | Done live | Hosted run wrote `/private/tmp/forge-ocr-output.txt` as `text/plain`; API debited 14µ$; runner verified one `Ocr` step. |
| 8 | Build hosted OCR mission (`mode=pdf`) | Done live | Hosted run wrote `/private/tmp/forge-ocr-output.pdf` as `application/pdf`; API debited 12µ$; runner verified one `Ocr` step. |
| 9 | Deploy and verify | Done live | `forge-api:0.2.0` + `forge-runner:0.10.1` deployed; `UploadArtifact`/`ExecuteMission`/`GetArtifact` all returned 200; binary output traveled via `GetArtifact`, not answer JSON. |

## Open Decisions

All launch-shaping decisions are locked above. Reopen only if implementation proves a locked choice
impossible:

- Raw byte upload, no multipart/base64.
- Message-name endpoints (`UploadArtifact`, `GetArtifact`).
- Ephemeral run scratch only, no retention promise.
- 100 MB max upload, no chunking.
- One `ocr` mission; `mode=text` default, `mode=pdf` explicit.
- Existing runner only, no dedicated OCR runner/pool.
- Tesseract-first for the shared-runner demo; PaddleOCR remains a later dedicated-runner upgrade if
  OCR usage justifies the package/model weight.
- GHA-inspired `FORGE_*` staging contract.
- Generic artifact capability metadata in `missions/ocr/forge.toml`.

## Non-Goals

- No `forge claude` hosted adapter work here; that remains 42.6 task 5b.
- No long-term document management or user file library.
- No user-authored arbitrary OCR missions.
- No GPU scheduling.
- No base64 artifact storage in Postgres/jsonb.
