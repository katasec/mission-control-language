# Room Artifacts — File Upload / Download Plane

> **Status: Design spec (written 2026-07-11), not yet built.** The end-to-end path a *file* takes
> through Forge Rooms: upload → store → hand to the runner → return the produced file → download,
> gated by room membership. Referred to in-session as the **"artifact plane"** (my shorthand — it is
> not a codebase term). **Disambiguation:** these are *user files* (a PDF a person uploads / a mission
> produces), distinct from the OCI *artifacts* of [oci-artifact-schema.md](oci-artifact-schema.md)
> (packaged experts/missions). Same word, different layer.
>
> **Parent:** [Phase 38 — Forge Rooms](../phases/phase-38-forge-rooms.md) §12 Q4 (artifact storage) +
> §11 S7 (artifact in/out, deferred "files second"). **Consumes:** the runner transport contract
> ([RunContracts.cs](../../src/ForgeMission.Runner.Contracts/RunContracts.cs)) from Phase 39.1.
> **Driven by:** the `family-halaqa` mission (built + verified 2026-07-11 — see below).

---

## 1. Why this exists

Forge Rooms is text-in / text-out today. [`RunRequest`/`RunResponse`](../../src/ForgeMission.Runner.Contracts/RunContracts.cs)
carry only strings; there is **no `IArtifactStore`, no file upload, and no binary in the runner
contract**. But the first real consumer use case — a mission that trims a slide deck and adds a cover
page — is fundamentally *file-in / file-out*. So the differentiated value (a **verified** PDF
transformation) is blocked not by the engine but by the absence of a way to move a file in and out.

The only prior specification is one paragraph — phase-38 §12 Q4 (*"bytes → blob store behind an
`IArtifactStore` seam … a reference in the message payload jsonb; retrieval gated by room
membership"*) — plus the anticipating comment in [MessagePayload.cs](../../src/ForgeMission.Rooms/MessagePayload.cs).
This doc makes that paragraph concrete.

## 2. Motivating use case (already proven)

`missions/family-halaqa/` — a `Planner(llm) → Editor(exec) → Verifier(exec-judge)` mission, built and
verified end-to-end on 2026-07-11:
- **Planner** (gpt-4o-mini): parses the aunt's plain-English instruction → `{remove_pages, date}`.
- **Editor** (`pikepdf` + `reportlab`, exec): deletes the named pages losslessly, prepends a generated
  cover, writes `output.pdf`.
- **Verifier** (exec, `role: judge`): content-hash proof that every *kept* page is byte-identical to
  the source, removals applied, cover present, count correct. Catches a tampered kept page (verified
  via negative control) — **no false green**.

It reproduces a real hand-made output (`Family Halaqa 04.07.2026.pdf`) page-for-page. Run today via
`forge run … --var request=… --var source_pdf=… --var work_dir=…`. The artifact plane is what removes
the `source_pdf` / `work_dir` plumbing from the user's view: she uploads a file and types an
instruction; the plane fills the vars.

## 3. Settled decisions

| # | Decision | Choice | Why |
|---|---|---|---|
| D1 | **Access scope** | **Room-scoped, uniform** — any file in a room (uploaded *or* agent-generated) is retrievable by any room member. Same boundary as messages. | Matches chat intuition (WhatsApp: post a file → the group has it); matches the use case (the output is the *deliverable for the room*); reuses the one boundary that already gates messages; invite = grant (authorization at a deliberate moment). See §4. |
| D2 | **Storage** | `IArtifactStore` seam — **local volume in dev, Azure Blob in prod.** Bytes in blob; a *reference* (id/filename/mime/size/key) in the message jsonb. Never Postgres large objects. | Rent-a-domain-agnostic-primitive; keeps large binary out of the DB (per the MessagePayload contract). |
| D3 | **Runner transport** | **Inline base64** in `RunRequest`/`RunResponse` for the first cut. Runner materializes bytes to `/work/input.pdf`, collects `/work/output.pdf` back. | Simplest; keeps the runner **stateless + zero-egress** (a 39.1 invariant). A trimmed PDF is a few MB — fine inline. Blob-by-reference deferred (§6). |
| D4 | **Reference model** | Extend [MessagePayload](../../src/ForgeMission.Rooms/MessagePayload.cs) with an artifact reference (list). Bytes never in jsonb. | The comment already anticipates exactly this. |
| D5 | **Var wiring (ownership split)** | **Orchestrator → runner:** the input **bytes** + original filename + `today` (a non-filesystem var) in `RunRequest`. **Runner (after materializing to `/work`):** sets `source_pdf=/work/input.pdf` and `work_dir=/work` into the pipeline vars. | Locked 2026-07-11 (corrects the earlier wording, which had the orchestrator set the paths). With **inline base64** only the runner knows where it wrote the bytes; emitting `/work/...` from the orchestrator would leak the runner's filesystem layout and dent the stateless/zero-egress invariant. The user still types no path — the UX payoff is intact. **Carry (not v1):** a *generic* runner setting *mission-specific* var names (`source_pdf`/`work_dir`) is a wrinkle — long-term the **mission** should declare which var receives the staged-input path. Convention is fine for v1. |

## 4. The isolation decision (D1) — room-scoped, and why not user-scoped

The earlier requirement was phrased as "user-scoped" (others can't see her uploads/output). On
reflection that optimizes for privacy of a thing — the deliverable — that is *meant to be shared*, and
pays for it with a second authorization model.

- **Room-scoped pros:** one boundary (identical to messages), matches chat intuition and the anchor
  use case, invite-is-consent is clean/auditable, and it's already the §12 Q4 position. In a
  room-of-two it is *identical* to user-scoped, so it costs nothing today.
- **User-scoped cons:** a second ACL distinct from message access (more surface, more bugs — the exact
  per-user-within-room infra to avoid pre-value); fights the deliverable (you'd privatize the output
  then re-invent room-sharing on top); confusing UX ("I posted a file nobody can see?"); breaks the
  "reply visible to all" tenet for artifacts.
- **The one room-scoped caveat — the raw source upload is visible to the whole room — is a non-issue**
  (decided 2026-07-11): it is no different from posting a file in a WhatsApp group, which is the
  behavior users already expect. So there is **no** source-vs-output special-casing, no ephemeral
  source. One rule: *a file in a room is a room artifact.*

**Enforcement is one line:** `IArtifactStore.Open(ref, requestingMember)` checks the requester against
the artifact's room membership — the same check messages already do — before streaming a byte.

## 5. Build map — six components

| # | Component | Where it lives | Build / Rent |
|---|---|---|---|
| 1 | **Upload** — `InputFile` / drop zone → posts a message with a 📎 file chip | [RoomConversation.razor](../../src/ForgeUI/Shared/RoomConversation.razor) | Build (Blazor) |
| 2 | **`IArtifactStore`** — `Put(stream, meta) → ref`, `Open(ref, member) → stream` (membership-gated) | new seam in `ForgeMission.Rooms.Data` | Build seam / Rent impl (local volume dev, Azure Blob prod) |
| 3 | **Artifact reference** — `{id, filename, mime, size, key}` on the message | extend [MessagePayload.cs](../../src/ForgeMission.Rooms/MessagePayload.cs) | Build (jsonb field) |
| 4 | **Runner transport** — carry bytes in/out (base64), materialize to `/work` | extend [RunContracts.cs](../../src/ForgeMission.Runner.Contracts/RunContracts.cs) + [MissionRunHandler](../../src/ForgeMission.Runner/MissionRunHandler.cs) | Build |
| 5 | **Var wiring** — orchestrator sends bytes + filename + `today`; runner sets `source_pdf` / `work_dir` after materializing (D5) | `RoomAgentInvoker` (`ForgeUI`) + `MissionRunHandler` (`ForgeMission.Runner`) | Build |
| 6 | **Download** — result message renders a membership-gated download link | RoomConversation.razor + a stream endpoint | Build |

Plus a **runner-image dependency**: the exec step needs `pikepdf` + `reportlab` in the runner image
(39.1 baked only bare `python3`). Pure exec-subprocess tooling — **no AOT concern** (see the
AOT-lib-selection rule: the constraint binds on .NET in the AOT binary, not on `kind:exec` python).

## 6. How it manifests to the user (the payoff)

**Steps 3–6 — DONE (2026-07-11, compile-verified; live UX is the owner's post-checkpoint drive):**
`MessagePayload.Artifacts` (ref list, jsonb) + `ArtifactDto` on `RoomMessageDto` (metadata only, no
bytes over SignalR). `IArtifactStore.OpenAsync` refactored to **authoritative-by-id** — `(roomId,
artifactId, requester)` with a persisted `.meta` sidecar, so a download's filename/type come from the
store, never URL params (the reviewer's constraint). `RoomAgentInvoker`: stages the room's latest
upload → `RunRequest.Input`, sets `today`, stores `RunResponse.Output` → `ArtifactRef` on the reply,
and derives the human line from the actual result (removed-count, via `ComposeFileReply`) — the ✓ badge
still flows through the `VerifiesAnswers` gate (no bypass). `AgentDescriptor.AcceptsArtifacts` gates
whether a file is staged (text agents never get a stray PDF). Upload = `<InputFile>` in
`RoomConversation.razor` (25 MB ceiling + friendly rejection) with 📎/📄 download chips on messages;
download = membership-gated `GET /rooms/{roomId}/artifacts/{artifactId}` in `Program.cs`.

**Interim registration (2026-07-11, marked for revision):** `family-halaqa` is registered as a
**built-in** (`BuiltinMissions`, local-only — `OciRef=null`, loaded from the baked-in copy, not ghcr)
bound to the handle **`@quran-class-helper`** (`AgentRegistry`, `VerifiesAnswers`+`AcceptsArtifacts`,
Official seal; seeded agent member; reserved handle) **purely to prove the artifact-plane UX live**.
This is a documented shortcut — its **real home is the Phase 39.5 per-user custom-mission registry**;
move it there once the UX is reviewed/refined. All three touch-points carry an `INTERIM (2026-07-11)`
comment, and a follow-up task tracks the move.

---

All six components collapse into one gesture in the room:

> The aunt drags `Gr 14d.pdf` into the chat, types *"remove slides 2,5,7… date 4th Jul"*, and gets
> back a message with **📄 Family Halaqa 04.07.2026.pdf — Download** and a ✓ verified badge. She never
> sees `source_pdf`, `work_dir`, or a page path — component #5 fills those from the file she uploaded.

## 7. Deferred / open

- **Blob-by-reference transport** (vs inline base64) — only if PDF size makes inline hurt; would mean
  the runner reads/writes blob via a passed URL, which trades against the runner's zero-egress
  invariant. Revisit with real sizes.
- **Size ceiling** — pick a max upload (e.g. 25 MB) and a friendly rejection message.
- **Cheap-now input validation** (agreed, separate track): the mission's Editor/Planner already detect
  missing-file / out-of-range pages; surface those as plain-English messages the agent posts. The
  *conversational* "talk back and wait" (a pre-flight validator at the **agent** layer, using room
  history as memory) is downstream of this plane — it needs to open the uploaded file to page-range-
  check. Keep validation deterministic; use the LLM only to phrase the pushback. The mission stays
  pure (clean inputs → verified artifact); the dialogue is the agent's job.

## 8. Status & next step

Build order (approved 2026-07-11): `IArtifactStore` seam (#2) + runner image deps → runner transport
(#4) → var wiring (#5) → upload (#1) + download (#6).

**Step 1 — DONE (2026-07-11):** `IArtifactStore` seam + `ArtifactRef` + a dev `LocalVolumeArtifactStore`
(membership-gated via `IReadStore.IsMemberAsync`) + `AddArtifactStore` DI, wired in ForgeUI `Program.cs`
(`Artifacts:LocalRoot`, default under content root). Runner image gains `python3-pikepdf` +
`python3-reportlab`. Planner now emits `date_display` (cover) + `date_file` (dotted, download name);
`edit.py` reads `date_display` and carries `output_name` in its result. **Azure Blob impl** is the prod
swap behind the seam — deferred to the deploy pass (a new class + DI switch, no call-site change).
*(Design refinement, adopted: the Planner emits ONE canonical ISO `date`; `edit.py` derives both the
cover display and the dotted filename deterministically — avoids asking the LLM for two agreeing date
forms. Verified via real `forge run`: LLM → `2026-07-04`, filename → `Family Halaqa 04.07.2026.pdf`.)*

**Step 2 — DONE + verified (2026-07-11):** `RunContracts.cs` gains a PDF-agnostic `RunArtifact`
(FileName/ContentType/Base64); `RunRequest.Input` + `RunResponse.Output` (both optional, trailing).
`MissionRunHandler` stages `Input` bytes into a **per-run** work dir (`WORK_ROOT`, not a shared
`/work` — concurrency-safe), sets `source_pdf`/`work_dir` vars (D5 ownership split), collects
`{work_dir}/output.pdf` **only on a verified run**, and names it from a step's `output_name` (generic
key, `ExtractOutputName`) — cleaned up in `finally`. `MissionRunnerClient.RunAsync` carries `vars` +
`input`. **Verified through the real HTTP contract:** POST `/run` with a 3.2 MB PDF as base64 →
HTTP 200, `verified=true`, `output.fileName="Family Halaqa 04.07.2026.pdf"`; the returned base64
decoded **byte-identical** (2 230 655 B, 51 pp) and `verify.py` re-passed on it against the original
source (no corruption, no false-green). All 216 tests pass. Next: var wiring (#5, `RoomAgentInvoker`)
+ upload/download (#1/#6).
