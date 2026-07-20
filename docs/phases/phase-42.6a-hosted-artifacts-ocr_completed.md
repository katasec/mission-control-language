# Phase 42.6a — completed build narrative

Full build history for [phase-42.6a-hosted-artifacts-ocr.md](phase-42.6a-hosted-artifacts-ocr.md),
moved out per the hub/spoke rule (AGENTS.md): the active spoke keeps current, still-true reference
+ a one-line-per-task index; this doc keeps the *why*, the live evidence, and the task-by-task
design narrative for anyone who wants the full story.

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
- `forge exec summarize --input
  https://www.rochester.edu/ORPA/_assets/pdf/compl_ConsultingAgreementTemplate.pdf` returned a
  grounded contract summary with `✓ verified`. The sample PDF was fetched client-side from a public
  HTTPS URL, uploaded as `application/pdf` with 28,264 bytes, and summarized by
  `Extract -> Answerer -> Verifier`.
- Runner evidence for `summarize`: startup advertised
  `loaded 8 mission(s): ChatGPT, Forge, Assistant, Claude, Grok, WebSearch, Ocr, Summarize`; run
  log: `Ran 'Summarize' [trusted] — verified=True steps=3 in 9989+503 tok / 113.70s`.
- API evidence for `summarize`: `UploadArtifact` returned `200` for `application/pdf 28264`;
  `ExecuteMission` returned `200 application/x-ndjson`; billing log:
  `Debited 3506µ$ ... Summarize 9989+503 tok / 113.70s / gpt-4o-mini`.
- `forge exec ocr --input https://tesseract-ocr.github.io/tessdoc/images/eurotext.png --out
  /private/tmp/forge-url-ocr-url.txt` and the equivalent local-file run wrote identical 419-byte
  OCR text artifacts (`sha256=fbb373b86280fbaf9e671ab39c9ea7b3787b5c032b7002fb39396d57b8516184`),
  both `✓ verified`. The original example Azure blob URL was checked but returned `403 The
  specified account is disabled`, so the live proof used a public HTTPS PNG from the official
  Tesseract docs.

## UX transcripts

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

## OCR Mission Design rationale

Use one hosted `kind: exec` mission first. For the existing multi-arch runner, the first real OCR
engine is **Tesseract**: classical ML, CPU-only, apt-packaged on amd64/arm64, and small enough to
bake into the shared runner without adding a dedicated OCR image. PaddleOCR/PP-OCRv6 remains the
likely upgrade if this becomes more than a demo: it is a fuller document OCR pipeline, but its
PaddlePaddle runtime/model packaging is heavier and better suited to a dedicated scale-to-zero OCR
runner if/when usage justifies that extra infra.

Model/runtime choices locked for this phase:

- Use the existing runner. No dedicated OCR runner, no runner pool, no multi-runner routing. YAGNI
  until real usage justifies it.
- Bake only the minimum OCR dependencies needed for the first demo (`tesseract-ocr`,
  `poppler-utils` for PDF-to-image text extraction).
- Do not download models on every request. CPU-first; GPU declarations are a later `resources:`
  concern.

## Task 10 — URL input source (full design narrative)

**Why:** a copy-pasteable demo command beats "find a file on disk" — point `--input` at a public
blob URL (or any HTTP(S) source) and get the same result as a local file.

**Locked decision: the fetch happens in the CLI process, never server-side.** `ForgeAPI`/the runner
never fetch a URL on the caller's behalf — that would be a server-side SSRF surface (internal IPs,
cloud metadata endpoints, redirect chains all need blocking, none of which exists today). The CLI
fetching a URL is the same trust boundary as a user running `curl` on their own machine, so this
stays entirely client-side and the server never sees anything but bytes it already validates via
the existing capability check. No new attack surface, no new endpoint.

Design, in `ForgeExec.UploadInputAsync` (`src/ForgeMission.Cli/ForgeExec.cs`):

- Detect a URL: `Uri.TryCreate(inputPath, UriKind.Absolute, out var uri) && uri.Scheme is "http" or
  "https"`. Anything else is treated as a local path exactly as today.
- Download to a temp file first, then fall through to the **same** local-file code path (sha256,
  content-type inference, upload) — no branching past the initial byte source.
- Filename: `Path.GetFileName(uri.LocalPath)`; fall back to the post-redirect response URI's
  filename, then a content-type-derived generic name, if the URL has no filename-like segment.
- Content type: extension-based `ContentTypeFor(path)` first; HTTP response `Content-Type` only as
  a fallback when the extension is unknown — a hint, not a security boundary, since the server-side
  capability check validates content-type independently.
- Per-download timeout via a scoped `CancellationTokenSource` on the existing shared `HttpClient`
  (which stays `Timeout.InfiniteTimeSpan` for the unrelated `ExecuteMission` streaming call) — 30s,
  plus a client-side max-download-size guard mirroring the 100 MB server-side cap.
- Temp file cleanup in a `finally`/`IDisposable`, so a failed upload doesn't leak the temp file.
- `InferOutputPath` switches to the URL's inferred filename, never the raw URL string, for output
  naming (`scan.jpg` → `scan.txt`).

Shipped in CLI `v0.9.0`.

## Task 11 — `@summarize` (full design narrative)

**Why:** `@ocr` demos raw extraction; this demos the thing people actually want — hand it a
document, get back a trustworthy synthesis of it. Motivating story: legal teams dumping contracts
into a generic chat tool for synthesis today, with no verification step and no audit trail. MCL's
whole thesis is "verified answer, not just an LLM guess" — this mission is the one-command proof of
that against a real, messy document instead of a chat prompt.

**Locked precedent, adapted from `missions/assistant/mission.mcl`:**

```
mission Assistant(goal) loop(2) = {
    Answerer
    -> Verifier
}
output(Assistant)
```

`Verifier` is `role: judge`, `kind: llm` — fails with a reason (triggering a loop retry with
feedback) or passes, echoing the answer verbatim. `MissionRunHandler.BuildAgentText`
(`src/ForgeMission.Runner/MissionRunHandler.cs`) picks the verified answer text from **a step
literally named `Answerer`** — the naming is load-bearing, not cosmetic.

**The gap that had to be closed:** `OcrExec` (`missions/ocr/experts/Ocr/ocr.py`) returns a short
metadata line as its JSON `summary`, not the full extracted text — correct for `@ocr`'s own CLI
footer, and it was **not** changed. `@summarize` ships its own sibling exec expert (`Extract`,
`missions/summarize/experts/Extract/extract.py`) doing the same Tesseract/`pdftoppm` extraction but
returning the full text as `source_text`. Decision: small duplicated script, not a shared
extraction path — MCL mission packages are intentionally self-contained
(`missions/<name>/experts/<Expert>/`); sharing code across packages would need a new
mission-tooling convention that would be premature for this demo.

**Shipped pipeline:**

```
mission Summarize(goal) loop(2) = {
    Extract          // kind: exec — OCR/PDF extraction, full text as source_text
    -> Answerer       // kind: llm — synthesizes a summary from {{source_text}}
    -> Verifier       // role: judge, kind: llm — grounding check against {{source_text}}
}
output(Summarize)
```

`Verifier`'s mandate is narrower than `Assistant`'s general-purpose fact-check: it checks the
summary doesn't state a figure, date, party name, obligation, deadline, clause, or legal effect
that isn't present in the extracted source text — a grounding check, not a general truthfulness
check (there's no external ground truth besides the document itself).

**Mission handle:** `summarize`, not `contract-summary` — a capability noun matching `ocr`/
`websearch`, not a use-case noun; the legal-contract framing is the demo's motivating story, not
the mission's scope.

**Registration** required two hardcoded entries, not just a new mission directory:

- `src/ForgeMission.Cli/BuiltinMissions.cs` — `BuiltinMissions.All` is a hardcoded list, not an
  auto-scan of `missions/`; without an entry here the runner never discovers the package.
- `src/ForgeMission.Api/MissionCatalog.cs` — `StaticMissionCatalog`'s constructor is also
  hardcoded; without a matching `CatalogEntry` here, ForgeAPI returns `MissionNotFound` even if the
  runner loads the mission fine. The two registration points are independent.

**Implementation adjustments made during live verification:**

- Runner image `forge-runner:0.10.2` failed to build — `RunnerRegistry.cs` referenced a
  `ForgeMission.Runner.Contracts` type without importing the namespace. Fixed in `b8fb30c`.
- First hosted PDF summarize run reached the mission but `Extract` timed out under the default 30s
  exec timeout (multi-page PDF OCR takes longer than a single-image OCR). Fixed by scoping
  `timeout: 120s` to `Extract` alone in its own `expert.md` frontmatter (not the mission-wide
  default), regenerating `mcl.lock`. Fixed in `6c5203e`. Deployed as `forge-runner:0.10.4`.

**Non-goal:** no chunking/map-reduce for long documents — a single-pass prompt is fine for a
demo-sized contract; a genuinely long document (hundreds of pages) would need that later.

Shipped and live-verified 2026-07-20, merged via PR #4 (`codex/summarize-mission` → `main`).
