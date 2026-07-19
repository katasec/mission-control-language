# Room Artifacts

> **Status: built, local-volume only.** Six components shipped and verified end-to-end
> (2026-07-11, commit `d7109b0`) against the proof mission `missions/family-halaqa/`. Azure Blob
> storage (the prod-shape `IArtifactStore`) is the remaining step to ship.

## Why this exists

Some missions need to consume and produce files, not just chat text — a PDF gets edited, a report
gets generated. Room Artifacts is the file plane that lets a room upload input to an agent and
receive a produced file back, gated the same way room membership already gates everything else.

**Sequencing decision (2026-07-11):** build **one** genuinely useful, exec-heavy mission
(operator-authored, so no untrusted-code risk) to prove a consumer can *consume a real mission*,
**before** doing exec-security hardening for user-authored missions (Phase 39.5/39.7). Framing:
*build toward a use case, not security for its own sake* — there's no point securing something no
one has shown wants using yet.

## Architecture — six components

1. **`IArtifactStore` seam.** Local-volume implementation for dev. `OpenAsync` is
   authoritative-by-id — the download path never trusts URL params, only the stored id — with a
   `.meta` sidecar per artifact. Swap point for the Azure Blob implementation.
2. **`RunArtifact`** — base64 in/out on `RunContracts`, the wire shape a run's input/output file
   travels as between orchestrator and runner.
3. **`MissionRunHandler` staging.** Stages the room's uploaded input into a per-run work directory,
   sets `source_pdf`/`work_dir` variables. Ownership split (**D5**): the orchestrator sends bytes +
   the run date; the **runner** is the one that sets the actual paths — keeps the runner
   self-contained about its own filesystem layout.
4. **`RoomAgentInvoker`** stages the room's latest upload on the way in, and stores the produced
   file on the reply on the way out — gated by `AgentDescriptor.AcceptsArtifacts` so a mission that
   doesn't declare artifact support never gets bytes shoved at it.
5. **`<InputFile>` upload** — 25 MB cap, 📎/📄 chips in the room UI for attached/produced files.
6. **`GET /rooms/{id}/artifacts/{id}`** — membership-gated download; a non-member 404s, not 403 (no
   room-existence leak).

**Date-handling design (reviewer-flagged, adopted):** the Planner step emits one canonical ISO
date; the exec step derives both the cover-page display format and the dotted filename from that
one value deterministically — killed an earlier two-date design where the LLM had to keep a
cover-date and a filename-date consistent itself, which is exactly the kind of thing an LLM
silently drifts on.

## Proof mission — `missions/family-halaqa/`

`Planner(llm) → Editor(exec/pikepdf) → Verifier(exec-judge)`, `loop(2)`. Built for a real use case
(a family PDF deck editor), not a synthetic demo.

- **Planner** (gpt-4o-mini) parses a plain-English instruction ("Remove slides 2,5,7,9,15,16,29,50,
  58-62. Date 4th Jul 2026") into `{remove_pages, date}` JSON. The AI is a thin NL→struct
  translator; *which* pages to remove is the human's editorial choice, never LLM-inferred.
- **Editor** (`pikepdf` + `reportlab`) losslessly deletes the pages and prepends a generated cover.
- **Verifier** (`role: judge`, exec) content-hashes every **kept** page against the source to prove
  byte-identity — load-bearing for this use case (nothing may be silently rewritten) — plus checks
  removals were applied, the cover is present, and the page count is right. A negative control
  (a deliberately tampered kept page) confirmed the verifier actually fails it — no false-green.
- Reproduces a real hand-edited reference PDF page-for-page, zero text diffs, on real input (63
  pages in, 13 removed, +1 cover, 51 out).

**Verified (2026-07-11):** 216 tests pass; the mission runs *inside* the built runner container
image (which bundles `pikepdf`+`reportlab` via apt) and returns a byte-identical, re-verified PDF
over `/run` — not just locally.

**Interim registration:** `family-halaqa` is a local-only built-in (`BuiltinMissions`, no OCI ref)
bound to the reserved handle `@quran-class-helper`, purely to prove the UX end-to-end. Its real home
is the Phase 39.5 custom mission registry, once that exists — this binding is scaffolding, not the
final shape.

## What's left to ship

- **Azure Blob `IArtifactStore`** — the prod swap behind the seam; dev stays local-volume-only.
- An `Artifacts:LocalRoot`-equivalent volume/config for whichever environment runs it.
- Roll a runner image that has the PDF libs, and ForgeUI, to Azure.
- Confirm `<InputFile>` streaming over Blazor Server's SignalR circuit for multi-MB PDFs — may need
  to bump `MaximumReceiveMessageSize`.
- The live UX drive itself: add `@quran-class-helper` to a room, upload a PDF, download the result,
  with a real (non-operator) user driving it.

**Also agreed, separate track:** input validation (missing file, out-of-range page numbers) should
be deterministic detection with the LLM only *phrasing* the pushback — and the conversational
"talk back and wait for a corrected upload" loop belongs at the **agent/room layer** (using room
history as memory), not inside the run-once mission itself. Keeps the mission pure: clean inputs in,
verified artifact out. This needs the artifact plane already in place (to open the uploaded file for
range-checking before the mission runs).

## Related

- [Phase 38 — Forge Rooms](../phases/phase-38-forge-rooms.md) — the room/membership model this
  plane's access gating (`AcceptsArtifacts`, membership-gated download) sits inside.
- [Phase 39 — Metered Runtime & Marketplace](../phases/phase-39-metered-runtime-marketplace.md) —
  39.5 is where `family-halaqa`'s interim binding gets a real home (custom mission registry).
