# Phase 76.10 — Outputs across execution segments

**Status:** corrected design supervisor-locked after full current simplicity r4 and ownership r2
PASS; Contracts/Host producer merged on PR27, normal package publication running. Runner consumer
is [scoped for its plan](phase-76.10.2-output-continuation-runner.md). Parent:
[Phase 76 contracts](phase-76.2-unified-cloud-run-contracts.md#artifact-staging-execution-and-outputs).

## Scope and acceptance

Operator instruction: `Resume Phase 76 from docs/plan.md and complete it autonomously.`

Current main already admits named inputs, stages package assets, and persists generated output
artifacts. The remaining boundary here is a pause: the current Runner deletes its segment scratch
without recording a produced-file manifest; the next segment stages only package assets and inputs.
This task preserves earlier outputs and cumulative limits through the existing server-only
continuation. It changes no Client, CLI, Desktop or ForgeUI surface. Native AOT is deferred until
the complete Phase 76 flow works, per the operator's recorded instruction.

Affected owners: `/Users/ameerdeen/progs/forge-conversations` (Contracts and Conversation Host),
`/Users/ameerdeen/progs/forge-platform` (the already-designed authenticated binary query),
`/Users/ameerdeen/progs/forge-runner` (segment execution), and `/Users/ameerdeen/progs/forge-infra`
(normal image pins/deployment). No sibling project references.

**Done when:** a real default-cloud mission produces a binary file, pauses for a correlated
Hands result, then resumes in fresh scratch and consumes exactly those bytes. Canonical artifact
queries still return the bytes after completion. Missing/corrupt/inconsistent manifests or content
fail visibly before a usable pause or resumed execution; cumulative 32-file/100 MiB limits survive
segment replacement. Required focused and full managed checks, independent reviews, publication,
normal deployment and default-path observations pass. No claim of whole-phase completion.

## Design

Use the existing claim-check text-body protocol to carry one bounded manifest on a Hands pause.
Host validates it against its own accepted Artifact facts and retains its reference alongside
the opaque continuation. The existing deterministic continuation command carries that reference.
Runner reads it through Host, verifies each binary file, restores the stable relative paths and
artifact registry, and seeds the cumulative output counts before Core resumes.

```mermaid
sequenceDiagram
  participant R as Runner segment
  participant H as Conversation Host
  participant C as Client Hands
  participant N as Fresh Runner segment
  R->>H: output chunks, Artifact facts
  R->>H: manifest body, Hands pause with reference
  H->>H: compare manifest with canonical run artifacts
  H-->>C: existing correlated Hands request
  C->>H: exact tool result
  H->>N: continuation + same manifest reference
  N->>H: read manifest and verified output bytes
  N->>N: restore paths, registry and cumulative limits; resume
```

| Behaviour | Owner and existing authority |
|---|---|
| Inert manifest and reference vocabulary | Conversations.Contracts README: stable transport-neutral values and generated JSON |
| Validate/adopt manifest, preserve reference with pause/result | ConversationHost README: canonical facts, state, receipts and one atomic transition |
| Collect, publish and rehydrate output bytes | forge-runner README: stateless execution and temporary scratch |
| Authenticated binary query projection | ForgeMission.Api README: authenticated edge and direct Host query projection |
| Pin/deploy published images | forge-infra README: normal image update/what-if/Make route |

### Types and fixed invariants

Reuse the parent contract's `MissionProducedFile(string RelativePath, MissionContentReference Content,
string ProducingStepKey, int ProducingAttempt)` and JSON `MissionProducedFile[]`. Add trailing optional
`ProducedFiles: ConversationBodyReference?` to the existing internal `ConversationProgress` and
`ConversationCommand`; register generated JSON and the fixed body field `mission-produced-files`.
No public event, snapshot, Hands work item or client continuation carries it.

The body is UTF-8 JSON, at most 32 KiB, at most 32 unique entries and 100 MiB aggregate bytes.
Paths and step keys are each at most 256 UTF-8 bytes; each filename is at most 128 UTF-8 bytes.
Each reference is output-kind, nonempty
identity, nonnegative bounded length and valid lowercase SHA-256. Its path is the existing
`outputs/<sha256(stepKey)>/<positive attempt>/<filename>` grammar, with a canonical positive
decimal attempt (no sign or leading zero). Attempt and deterministic
content ID must match that path and current run. Do not add a second output path grammar: reuse
or narrowly share the existing output validator at the Contracts boundary where both owners need
the same inert grammar. Files cannot collide with inputs or package assets, whose `outputs/`
namespace is already reserved by Core.

Pause body identity is `Body(pauseEventId, "mission-produced-files")`. A null manifest is valid
only when there are no canonical output artifacts for that run; an empty array is also valid in
that case. For a new pause with outputs, Host requires set equality with the run's accepted binary
Artifact facts: exact path and descriptor, producing attempt and step-key hash. No omitted,
duplicate, foreign-run, invented or changed output is accepted. Earlier non-binary artifact facts
do not count as produced files. Strengthen the existing outstanding-request duplicate guard to
compare the stored request and both opaque-continuation/manifest references, not ToolRequestId
alone. Only exact equality is an accepted replay; changed or missing references reject without
failing the active run. Changed content under the same body identity also conflicts through the
existing body/receipt rules. No second receipt mechanism is introduced.

Host's existing progress intake commits/verifies the manifest body before the grain. The grain
checks canonical artifacts before accepting the Hands request. Reuse the existing
`MissionHandsContinuationBodyJson` state/fact lifecycle with one adjacent manifest-reference
field: set both in the same pause transition; copy both into the one deterministic result
continuation; clear both wherever the outstanding request is cleared. Preserve Orleans field IDs
and additive JSON defaults. Store only a reference in state/outbox, never the manifest list.

Runner tracks successfully published outputs with their producing step/attempt in its existing
RunWorkspace. Publish the bounded complete manifest before the pause using the existing text-body
publisher and one-fact outbox. Restore through the same Host body/content readers before saving
the resumed provider boundary. Reuse existing descriptor/hash verification and path staging;
create files without overwrite, register relative paths for Core artifact resolution and seed
the existing output count/byte totals. A rehydrated file is not republished as a new Artifact.
Scratch disposal remains joined to the segment and never deletes Host bytes.

Expose the parent's already-defined `POST /api/GetMissionContent` query using its existing
`GetMissionContentRequest(Guid ConversationId, Guid ContentId)`. ForgeAPI's existing MapQuery,
HostReads and ConversationHostQueries forward it to
`GET /conversations/{conversationId}/contents/{contentId}` using only the authenticated member.
Reuse WireProxy for unchanged status/stream/descriptor headers; no byte store, upload, chunk API,
credential or direct Blob path is added. This minimal projection is necessary for the required
authenticated post-exit binary observation and later CLI diagnostics; the DTO already exists.
WireProxy currently strips Content-Length for streaming. Add an internal fixed binary-read marker
to ConversationHostRead and pass it through the same copy method so this query preserves the
upstream Content-Length along with Content-Type, ETag and X-Forge-Content-* headers. Other query
semantics remain unchanged; this is transport metadata, not a user setting or a second proxy.

### Failure, ownership and verification gates

| Gate | Decision / observation |
|---|---|
| Security/data | Persistent Host/ephemeral Runner ownership is operator-locked. The already-approved GetMissionContent public query uses existing platform-key authentication and Host ownership checks. No new queue, store, credentials or cross-store access. Internal manifest metadata extends the existing approved continuation contract. |
| Invalid pause | For Hands pause intake failures, pass the original pause and a trailing Host-only `IntakeFailureReason` on `ConversationProgressInput` through the grain's existing run, command and duplicate guards. Only a current correlated new pause returns the trailing `ConversationProgressOutcome.InvalidMissionPause`, without accepting the pause. Canonical manifest mismatches return the same outcome after correlation. ProgressHandler invokes the existing Error + Failed recorder only for that typed outcome. Ordinary stale, duplicate or wrong-run rejection must not fail the active run; exact accepted retries retain their existing outcome. Non-pause intake behaviour remains unchanged. Blob/Orleans outages propagate for normal broker redelivery. |
| Invalid resume | Runner's existing ConversationBodyReadException path abandons both corrupt-content and transport failures for normal broker retries before the provider boundary; after max deliveries its dead-letter handler records Error + Failed. No immediate corruption-specific terminal shortcut, silent empty workspace or provider rerun. |
| Limits | Runner counts restored files before collecting another output; Host independently enforces its existing canonical artifact totals. Over-limit output fails and earlier artifacts remain readable. |
| Recovery | Exact pending facts reuse existing outbox publication; exact continuation reads immutable Host content. A failed content read never substitutes a local file or stages new content. |
| Engineering | Reuse body intake, failure recorder, grain event store, pause/result transition, RunWorkspace and existing readers. No new coordinator, registry, runtime option or generic framework. |
| UI | N/A: this task changes no Desktop, ForgeUI or browser surface. |

Focused managed tests exercise manifest round-trip, exact retry/conflict, accepted-set equality,
foreign/omitted/duplicate/path/hash/attempt mismatches, null versus nonempty output set, dropped
body/binary chunks, cumulative count/byte limits, and state recovery. Runner's fixture must execute
produce → pause → dispose → new scratch → resume → read, independently inspect restored bytes,
and demonstrate no duplicate Artifact publication. Run focused checks locally before full CI;
Host storage fixtures use the existing Azurite route. No repeated Native AOT checks.

### Default path and delivery order

1. Contracts/Host producer: plan, sequential reviews, approved implementation, managed functional
   checks, code reviews, merge and normal Contracts publication. Verify actual package contents.
2. Runner consumer: consume that published package, plan/review/implement/review, merge and normal
   image publication. Do not substitute source projects or unpublished packages.
3. Minimal Platform query projection: its own approved plan, managed focused/full checks and code
   reviews, merge and normal API image publication.
4. Normal forge-infra image pins, required what-if and Make deployment from merged main.
5. Use the ordinary authenticated `https://api.forge.katasec.com` route and saved login with a
   dedicated disposable Project/conversation and file-only Hands attachment. Exercise the binary
   produce/pause/resume mission through ingress/progress and query durable bytes through the
   authenticated API after completion. Record exact package/image identities, absent custom URLs,
   actions, terminal result and independently inspected binary bytes. Controlled fixtures alone
   cannot close this task; installed unified CLI acceptance remains a later whole-phase gate.

### Reuse and decisions

| Need | Existing implementation / decision |
|---|---|
| Durable manifest | Claim-check bodies and generated JSON; no inline growing list |
| Canonical membership | Existing Artifact event history and output query; no new content registry |
| Continuation lifetime | Existing Hands pause/result facts and deterministic owed command |
| Segment files | Existing RunWorkspace and PipelineExecutionWorkspace artifact registry |
| Simplicity | One complete manifest on pause; no client cache or alternate recovery mode |

Rejected: keep scratch alive across workers (breaks ephemeral ownership); encode output bytes in
Core checkpoint (duplicates Host content); trust arbitrary manifest entries (bypasses canonical
artifact membership); rebuild counters from the new scratch alone (loses cumulative limits).

Open design questions: none. Parent execution/storage policy is already locked; this task adds
no permission policy. The intake correction closes a gap found during planning: the existing
recorder receives only RunId and cannot itself distinguish a stale pause in the current run.
Correlation therefore remains grain-owned and precedes any manifest read or terminal failure.
Corrected-artifact reviews passed; the bounded Contracts/Host plan is approved. Other consumer
plans remain subject to their own reviews and approval.

## Review and timing

[Contracts/Host producer plan](phase-76.10.1-output-continuation-producer.md) — r2 approved after
full current simplicity and ownership PASS; PR27 implementation and current review evidence there.

| Stage / role / round | Start (UTC) | End (UTC) | Evidence |
|---|---|---|---|
| Design / supervisor | Unavailable; not recorded | Unavailable; first artifact preceded review | Scoped current-main gap; no executable changes |
| Review design / simplicity / r1 | 2026-10-11 12:14:00 | 2026-10-11 12:14:51 | REVISE: query dependency, typed failure, read retries and output bounds |
| Review design / simplicity / r2 | 2026-10-11 12:15:54 | 2026-10-11 12:16:08 | Full 11-check PASS on corrected artifact |
| Review design / ownership / r1 | 2026-10-11 12:17:00 | 2026-10-11 12:18:04 | Full 11-behaviour PASS; no owner gains unrelated work |
| Review design / simplicity / r3 | 2026-10-11 12:24:23 | 2026-10-11 12:24:54 | REVISE: existing duplicate guard matches ID only |
| Review design / simplicity / r4 | 2026-10-11 12:26:07 | 2026-10-11 12:26:14 | Full11-check PASS; exact replay/reference guard explicit |
| Review design / ownership / r2 | 2026-10-11 12:26:50 | 2026-10-11 12:27:07 | Full14-behaviour PASS on corrected design |

Product/default-path observations remain pending. Tokens are N/A; unavailable timestamps are not
reconstructed.
