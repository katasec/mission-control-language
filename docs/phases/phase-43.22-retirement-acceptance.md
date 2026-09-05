# Phase 43.22 task 5 — Retire old writers and prove the product

> **Design locked; Codex implementation/rollout pending, after tasks 1–4.**
> Parent: [reconstruction hub](phase-43.22-project-mission-reconstruction.md).

## Delete, preserve and correct

| Area | Required final disposition |
|---|---|
| Client Runtime | Delete `Services/ProjectControlRuntimeSession.cs`, its exception, MissionControl session slot/disposal, old open/submit handlers and dead setters. Delete unused ProjectMission SelectMission wrapper and unused default helper. |
| Transport | Delete OpenProjectMissionControl/SubmitProjectMissionControlTurn DTOs, channel methods, JSON roots and route strings. Keep unrelated `/transport/prompt`, capability and launcher contracts. |
| Host | Delete Project Control create/message HTTP handlers, grain methods/input DTOs and dedicated duplicate helpers. New Project Mission routes are the only Project write path. Retain read/deserialization of old conversation records. |
| Worker | Delete MissionControl resolver alias/kind, successful control-turn dispatch/completion/failure branches and control-only executor/assets. Keep shared Janus/Naive start/progress/recovery mechanisms. Historical queue bodies remain parseable; an unexpected old command is Unsupported and follows existing observable dead-letter handling, never executes as Naive. |
| Historic data | Preserve ConversationPurpose.ProjectControl and ConversationParticipant.MissionControl serialized values/ordinals, legacy IDs and old event/checkpoint decoding. Historical names in readers/tests/docs are not evidence of a writable duplicate. |
| Presentation | Delete MissionRunThread, Home submission buffers/focus flags and all legacy write entry points. Keep focused renderer/picker/rail. No hidden dual mode/fallback. |
| Naive | Keep `Janus/NaiveMissionExecutor.cs` small wrapper, Worker registration, packaging and `missions/naive/forge.toml` provider profile. Replace Controller policy below and regenerate only its lock with the current main lock format. |
| Tests | Replace obsolete successful-legacy-write assertions with write rejection, old-data read and drain evidence. Do not delete unrelated tests or weaken assertions to get green. |

Final absence search covers routes, DTOs, grain methods, worker branches, session registrations and
callers—not just visible strings. Old POSTs return 404/405 after removal (a generic GET route may
produce 405); during deliberate freeze they return typed 410. No old POST can create a record.
Direct old-purpose mutation through generic command/progress APIs is rejected after drain.

### Naive's role policy — implementation text

Retain necessary existing frontmatter/provider binding. Controller receives `projectGoal` and
`task` using the existing mission parameters. Replace the old body with this policy, expressed
using the current expert template's parameter syntax:

> You are Naive, a single-expert mission. Address the user's task in the context of the Project
> goal. Produce the requested answer, plan, explanation, or code directly when the task is clear.
> If essential information is missing, state what is missing and ask a concise clarification;
> do not invent a previous task or conversation. You have no filesystem, terminal, browser, or
> other tool access in this mission. Do not claim to have created files, changed a project, run
> tests, or verified external effects. When providing code or commands, distinguish proposed
> content from actions actually performed. The Project goal provides context; it does not replace
> the user's task with a goal-refinement exercise.

Verify with the actual checked-in asset and live configured provider: an explicit small coding
request yields relevant proposed code, and “another one” without a referenced prior task yields
clarification. Fake-expert executor tests cannot prove prompt behaviour. Janus remains the existing
mission composition; no new provider/model parameters enter UI or Client Runtime.

## Safe retirement — admission first, execution removal second

Old Project Control commands have no child run ID and no ordinary run-terminal lifecycle. Queue
depth alone cannot prove their in-flight work is drained. Do not silently abandon accepted commands
or rewrite them into Project Mission runs. This deployment obligation is separate from source deletion.

| Stage | Exact boundary and evidence |
|---|---|
| A — permanently stop admission | Before deploying a Worker without the control handler, deploy a small prerequisite Host change returning `legacyReadOnly`/410 from old create/submit HTTP and grain entry methods before any append. Keep old Worker handling and progress ingestion temporarily. No config toggle to re-enable writes. Exercise HTTP and direct grain calls; record no new events/outbox writes. |
| B — account for accepted work | Keep the old Worker/progress path running. Inventory accepted legacy UserMessage command IDs from Host-owned event/idempotency data. Reconcile each against its deterministic persisted control response/error; inspect the latest Worker session recovery state as described below. Record command IDs/counts, pending outbox states and both queue/DLQ states without secrets. Each owner reads its own store; no Client Runtime cross-store access. |
| C — drain barrier | Every accepted legacy command has a durable completion or explicit error resolution; no pending legacy Host outbox, Worker in-flight checkpoint, queued/delayed/locked message, unconsumed progress or unresolved DLQ entry remains. If a command is uncertain, use the existing Worker recovery protocol to produce its explicit interrupted/error outcome; never rerun an uncertain provider call or delete its record. |
| D — replace | Deploy final Host/Worker without legacy writer/handler and the rebuilt Desktop/Runtime. Verify old APIs cannot write, old records still deserialize/read, and Janus/Naive run through the new route. |

Stage A is a separately reviewed **small prerequisite PR from current main**, containing only the
permanent admission fence and its tests. It can be prepared during this replacement branch's review;
do not merge it until the replacement is ready for the planned transition. This is the one explicit
exception to “one feature PR”: it structurally stops new old work before removing its consumer.
The replacement branch incorporates that main revision and removes the fenced methods after the
drain proof. Never merge the original candidate or create a second enabled execution mode.

Normal local deployment uses `make -C ~/progs/forge-infra 350-conversation-kind-up` from clean main,
per [Deploy Runbook](../design/deploy.md) and that repository's instructions. Any needed inspection
helper must be read-only, bounded/paged, run in its owning Host/Worker context, and output a report;
it is not a new public product API. Use existing store/checkpoint APIs and operational inspection
where available. No data-plane credential goes into Desktop or Client Runtime for this task.
No Azure data wipe, pruning, old-run deletion, or hosted deployment is part of this plan.

The inventory must match actual persisted shapes; do not assume a per-command Worker receipt
ledger exists. Host enumerates legacy-purpose conversation checkpoints in bounded storage pages,
then their accepted UserMessage IDs and `FindByEventIdAsync(address,
ConversationDeterministicIds.Progress(commandId, 0))`. Control turns start ordinal zero and emit
ParticipantMessage(MissionControl) or Error as their terminal output; a missing result remains
unresolved. Additional contiguous progress ordinals can be inspected until the first absent ID
when a recovery emitted another fact. Do not infer success from arbitrary prose. Worker session
state (`WorkerSessionState`) only retains CurrentCommandId, nullable RunId, Phase,
NextProgressOrdinal and PendingProgressJson for the latest command. Inspect it through the
existing Service Bus session-state API in the Worker context. At the final barrier, gracefully
stop old Worker consumers, acquire/check the relevant session states after their locks release,
and require Terminal with null PendingProgressJson; no unsettled command is discarded. An
ExecutingProvider state returns to the old recovery consumer for its explicit Error before
repeating the barrier. Inspect Host pending-transition/outbox fields through Host-owned checkpoint
storage. Record inaccessible/ambiguous entries as blockers, never zero counts. This is an
operational inspection report with IDs/statuses only, not a product API or a migration write path.

**Type-2 migration exception:** only the local `forge-durable` deployment retains the old Worker
handler between A and D. Scope is already-accepted legacy commands; old admission is permanently
closed. Reversal before D restores a known compatible old Worker without reopening admission.
Removal condition is stage C's per-command proof and final D image inspection. If the proof is
blocked, retain the consumer and report rollout blocked; do not deploy a deletion that loses work.
The v3 manifest is not downgraded during rollback. A previous Desktop cannot be a recovery client
for it; roll forward or retain the v3-compatible client.

## Verification matrix

| Layer | Required observation |
|---|---|
| Static boundary/design review | Every affected function has one concern; no duplicate coordinator, abandoned route or unused owner; all manifest writes share the transaction; no English-prose classification. Report CC hot spots with reasons, not a meaningless average. |
| Project persistence | Task 1 process contention/crash/immutable-journal/migration matrix passes, including selection changing during lost-response recovery. |
| Host/Worker | Full existing acceptance/outbox/recovery/persistence suites plus bounded index/trace/typed-error tests; duplicate ID never starts another run. Zero authority on direct Host and Worker entry points. |
| Client transport | Task 3 same actions without UI, reconnect/early events/failed refusal, two windows, stale response and disposal cases pass. |
| UI | Task 4 state/reference/browser matrix passes; repeated completion while Trace is open produces no framework banner or invalid-focus error. |
| Full repository | `dotnet build src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx` pass with zero warnings; `make desktop` publishes the actual AOT package. Required CI checks pass. A focused/rehosted test subset is never reported as the whole suite. |
| Mission policy | Actual Naive asset/live provider answers explicit task and handles missing context honestly; Janus/Naive each produce a durable completed or correctly explained rejected outcome; neither claims filesystem execution. |
| Removal/migration | Stages A–D recorded; old writer routes/methods gone, old data readable, no legacy command discarded. |
| Product | Same revision across packaged Desktop, Client Runtime, Host and Worker; default-route journey below passes; operator gives final independent visual acceptance after agent PASS. |

Prior audit environment had full-suite failures (Rooms 41 failures, Runner 1 failure, and
ForgeMission.Tests compilation due to external sibling-project paths) despite Host/Worker focused
passes. Record and repair/reproduce the actual current failures before approval; do not carry
forward an inherited “tests passed” claim. Environment failures still block full acceptance until
the real required checks run successfully. Do not prune/reset the user's containers/data to mask them.

## Default facts and journey — binding

| Fact | Required value / action |
|---|---|
| Artifact | `dist/forge-desktop/ForgeMission.Desktop`, zero arguments, exact build revision recorded; Supervisor owns Runtime/native children |
| Absent overrides | MissionRuntime Mode/BaseUrl, FORGE_API_ENDPOINT and ConversationRuntime BaseUrl overrides absent; no hand-injected endpoint or user-supplied Runtime URL |
| Dependency | Existing resolver defaults; ConversationHost loopback `http://127.0.0.1:18080/` via normal Supervisor-owned Kind bridge; Client Runtime OS-assigned loopback port |
| Provenance | Clean-main Host/Worker from sanctioned `350-conversation-kind-up`; record source SHA and deployed digests; no direct image substitution counts as default acceptance |
| Starting state | Dedicated disposable Project under normal launcher flow; additionally a copied v1/v2 fixture with historical data owned by the test, not an unrelated user Project |
| Action | Launch; create/open Project; default Janus; submit explicit instruction; open live Trace; finish while Trace shown; return; select/run Naive; restart app; reopen both traces from Missions and Explorer; page history; repeat terminal-event test in Settings |
| Expected | One run per deliberate submission, persisted selection, exact original trace, matching lists/status/counts, no focus crash, no tools/local changes, legacy notice only on migrated fixture |
| Failure journey | Controlled network loss after acceptance, concurrent windows and receipt-write fault prove same-ID recovery; label injected faults separately from the normal successful route |

The default dependency builder only accepts clean main. Therefore a **Type-2 sequencing exception**
permits merging the replacement only after code review, all branch-verifiable tests, same-SHA
controlled candidate stack, browser/packaged parity and retirement prerequisite evidence pass.
Immediately build normal dependencies from clean merged main and exercise the default journey.
Until this passes, status is **merged, default acceptance pending**, not done/release-ready. A
failure requires a corrective PR and repeated normal-route proof before release. This exception
changes no endpoint, credential or product default and expires on recorded default acceptance.

Default-path record must include artifact SHA, overrides absent, dependency build/digests, safe
Project ID, actual action, observed durable/UI outcome and PASS/FAIL. Keep controlled provider
fakes, audit scratch builds and manually rolled images explicitly separate.

## Completion report and acceptance

Supply per-task evidence pointers, actual full-suite/publish output, duplicate/dead-path searches,
reviewed CC hotspots, route inventory, migration report, browser captures and normal-route record.
Report production files/lines deleted, replaced, newly added and unchanged borrowed code separately
from tests/docs/reference frames. Explain any files outside the approved scope before approval.

Codex reviews source and evidence against these conditions; a success summary alone is not proof.
After agent visual PASS, request the operator's independent visible acceptance. Only then update
the hub to complete and move verified narrative to a completion companion. No requirement is closed
by calling the result “salvaged”, by retaining a large percentage, or by deleting a quota of lines.
