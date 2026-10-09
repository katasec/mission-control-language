# Phase 76.2 — Completed investigation and review rounds

**This is design-stage evidence, not Phase 76 completion.** Implementation, publication,
deployment and default-path acceptance have not occurred. Current decisions and next work live
in [the active contract spoke](phase-76.2-unified-cloud-run-contracts.md).

## Read-only baseline investigation

On 2026-10-09 the supervisor and fixed implementer inspected owning READMEs and source across
forge-mcl, forge-client, forge-conversations, forge-runner, forge-platform and forge-infra.
`git fetch origin` followed by HEAD/origin-main comparison found each inspected Forge checkout
at the intended main baseline with no divergence; no product files were edited.

| Repository | Observed baseline HEAD |
|---|---|
| mission-control-language | `77b6353952aaceb88fe504cf1f71fe639ccdf9af` |
| forge-mcl | `8d28dc1ff8f127facfd708b1c89369179e98cf18` |
| forge-client | `7595a72d0ded3baaf2e78f4d39485d587c15b621` |
| forge-conversations | `e1c5b5bd661ccebaf05fe316d3e5fd512ef2c278` |
| forge-runner | `95d677cb6b74f34f1911af5d3866c8ff5e484688` |
| forge-platform | `85200f9ff8c35b02b9618cb1e95752ecea4849a4` |
| forge-infra | `7d849c92b035866061418daf779a2be8fde29e96` |

| Finding | Named source observation / effect on design |
|---|---|
| Multiple-root primitive already exists | Core `LocalDiskWorkspace(IEnumerable<string>)` and `WorkspaceGuard` support a union; Bob's mission factory takes one root and Projects rejects siblings. Extend those owners rather than building a second broker. |
| Durable package transport is currently narrow | Core `DurableMissionPackageValidator` limits source/experts/count/bytes/kinds/profile; Host admission has a separate 4 KiB definition limit. Package and definition claim-checks plus explicit common budgets are required. |
| Current exec is not an isolation boundary | Core `ExecExpertRunner` inherits environment and runs package command/args. `ContextBuilder` also evaluates `env(...)` in root lets/step bindings. Server-owned execution plus structural env rejection is proposed; arbitrary code requires a separate security decision. |
| Pausable input filtering loses OCR's inputs | Core `PipelineRunner` retains only root parameters, while current OCR root is parameterless. Preserve the validated named-input set in root fingerprint/checkpoint. |
| Checkpoints retain strings and replay skips completed steps | Stable relative paths and per-segment cwd work with current OCR source; outputs must be persisted before pause and rehydrated. This was source inspection, not an executed acceptance test. |
| Existing final/artifact paths suffice | Runner emits final ParticipantMessage then RunStatus; existing ConversationArtifactReference/Artifact facts can be extended. No second terminal-response or artifact vocabulary. |
| Abrupt client death has no automatic cleanup | Host has explicit attach/recover/cancel, but new run is a fresh conversation and no heartbeat expiry closes old work. Record old pending state honestly; never promise recovery by a new invocation. |
| OCI source checkout and published packages differ | CLI/MissionRegistry csproj pin 0.2.1; PowerShell reflection of its DLL found no PullMissionWithDigestAsync. Exact cached 0.4.0 DLL exposes it and PulledMission(Bundle, ManifestDigest). Its nuspec points to `bfc88d869cabc36fbfcbbd34555f3b57e056bce9`. |
| Upgrading OCI alone is insufficient | `gh api` immutable-source reads of that commit found unchecked realm/redirect behavior and missing manifest/layer byte verification. The active spoke defines required existing-library repair; no AOT/live-registry claim is made. |

## Independent design reviews — rounds 1 and 2

Fixed role threads: `phase76_implementer`, `phase76_simplicity`, `phase76_ownership`.
Only one subagent ran at a time. Reviewers were read-only and did not author the design.
Every new round requires the full current artifact and checklist; old PASS does not carry forward.

### Simplicity

| Full checklist item | Round 1 | Round 2 |
|---|---|---|
| New apps or libraries | PASS | PASS |
| Reuse | REVISE: preserve existing final-message path | REVISE: arbitrary-run launch mapping incomplete |
| Multiple code paths | PASS | PASS |
| Legacy paths | PASS: persisted historical data and functional Rooms route are real consumers | PASS |
| Knobs | PASS | PASS |
| Speculative abstractions | PASS | PASS |
| Library choice | REVISE: exact pinned API/auth probe missing | PASS: exact shortcomings and required repair recorded |
| Copy-paste | PASS | PASS |
| Redundant definitions | REVISE: reuse existing artifact DTO/event | PASS |
| Size versus requirement | REVISE: avoid chat terminal-route redesign | PASS |
| Test volume | PASS | PASS |

Round 1 also required concrete public/internal launch and input contracts, chunk ownership/IDs,
binary reads/output/continuation, explicit assets/root/hash rules, actual input semantics, OCI
grammar and producer StepKey. Supervisor revised the complete artifact before round 2.

Round 2's sole remaining simplicity defect: define arbitrary-run MissionVersionId, VersionNumber,
Definition/DefinitionHash and fixed Hands profile, including zero roots; reconcile the independent
Host definition limit. Source evidence: Host `DurableMissionPackageAdmission.cs:15–23`.

### Ownership

Round 1 failed technical completeness: shared-action contracts, single credential persistence
seam, workspace metadata DTO, trusted OCR source/output authority, binary wire/read/continuation
contracts and an honest old-run recovery outcome. Most placement choices already matched owners.
The supervisor revised those contracts before round 2.

| Behavior | Derived owner / round 2 placement | Round 2 |
|---|---|---|
| Parse inputs, text/diagnostics, clean command cut | CLI | PASS |
| Persist CLI settings | CLI ForgeConfig | PASS |
| Persist credentials | Core.Resolution.CredentialStore | PASS |
| Resolve/cache references and bundles | MissionRegistry | PASS |
| OCI HTTP/authentication/digests | Existing Katasec.OciClient | PASS |
| Package/validate language and trace identity | Core | PASS |
| Project identity/declaration | Application Projects | PASS |
| Prepare/admit/reconcile intent | Application Missions | PASS placement; composition inputs incomplete |
| Shared action vocabulary | Application Transport | PASS |
| Attachment lifetime/disposal | Application Sessions | PASS |
| File-tool authority/execution | Client Runtime and Core workspace primitives | PASS |
| Public authentication/proxy | ForgeAPI | PASS |
| Durable wire/content vocabulary | Conversations.Contracts | PASS placement; output-list contract incomplete |
| Durable state/content/receipts/reads | Conversation Host | PASS |
| Providers/trusted OCR/scratch | Runner | PASS |
| Response/step/artifact facts | Runner produces, Host persists/sequences | PASS |
| Usage/settlement | Runner reports, Billing prices/debits | PASS |
| Cancellation/process-death outcome | Missions coordinates, Host owns state, Sessions drains | PASS |
| Deploy pins/configuration | forge-infra | PASS |

Round 2 overall REVISE. No reviewed owner acquired a second unrelated job. Remaining corrections:
the same launch mapping, explicit invocation directory/registry base/credential composition, and
deterministic output IDs plus bounded produced-file continuation. Host command/state guards are
32 KiB/960 KiB (`ConversationGrain.cs:1368–1381`); relying on their late rejection is insufficient.
The supervisor revised those contracts before round 3.

Both reviewers separately kept the Type-1 operator decision open. A technically passing design
review alone cannot approve executable trust or the new binary/public cross-context boundary.

## Later full design reviews

| Checklist | Simplicity round 3 | Simplicity round 4 |
|---|---|---|
| New apps or libraries | PASS | PASS for both contracts and OCI prerequisite |
| Reuse | PASS | PASS for both |
| Multiple code paths | PASS | PASS for both |
| Legacy paths | PASS | PASS for both |
| Knobs | PASS | PASS for both |
| Speculative abstractions | PASS | PASS for both |
| Library choice | PASS | Main technically PASS with stale source wording correction; OCI prerequisite PASS |
| Copy-paste | PASS | PASS for both |
| Redundant definitions | PASS | PASS for both |
| Size versus requirement | PASS | PASS for both |
| Test volume | PASS | PASS for both |

Ownership round 3 independently rechecked all 19 behavior/owner rows and found placement PASS
with no owner gaining a second job. Technical REVISE was one concrete OCR mismatch: a staged
`content.<ext>` makes the existing script produce `content.txt`/`content.pdf`, while the draft
expected `output.txt`/`output.pdf`. The supervisor changed the fixed output contract to the actual
script names without changing trusted code.

Simplicity round 4 independently reviewed the entire current contract and new bounded OCI
prerequisite. It confirmed the prerequisite is necessary irrespective of executable policy and
can proceed independently through Plan/reviews. Its only source-description correction was that
current OCI main lacks the previously published/reverted mission-digest API; the supervisor
changed the active contract from “extend existing” to “add missing API, reuse retrieval.”

Ownership round 4 independently rechecked the complete revised contract and OCI prerequisite:
technical PASS for both. All prior behavior-owner rows passed, plus these prerequisite behaviors:

| Behavior | Owner / round 4 verdict |
|---|---|
| Verified mission bundle/identity retrieval | Existing OCI client / PASS |
| Bounded manifest/blob reads and descriptor/hash verification | Existing OCI client / PASS |
| Challenge exchange, credential handling and scoped token cache | Existing BearerAuth / PASS |
| Credential verification without persistence | Existing OCI client / PASS |
| HTTPS and redirect authority | Existing OCI HTTP/auth boundary / PASS |
| Normal dependency publication and public Native AOT acceptance | Existing publish workflow, supervisor acceptance / PASS |

No competing implementation/new component or second unrelated owner job was found. The reviewer
explicitly accepted independent OCI Plan while cloud Type-1 decisions remain pending; library
acceptance cannot close the full cloud/CLI task. Supervisor locked only the bounded OCI design
at 2026-10-09 19:52:51 UTC and assigned its read-only implementer Plan.

## OCI prerequisite plan reviews

Implementer Plan r1 was recorded before code. Simplicity independently checked all 11 checklist
items: PASS for each (existing owners/dependency, reuse, one path, no insecure legacy path,
bounded caller parameter, one shared byte reader, source generation, no duplicated exchange,
necessary result, bounded scope and meaningful tests). No correction required.

Ownership independently derived all behaviors: verified mission result, shared manifest identity,
schema/layer validation, bounded reads/cancellation, generated models, challenge/token/cache,
verification without persistence, redirect authority, preserved push/expert operations, tests and
publication. Every placement PASS within existing OCI/models/auth/test/workflow owners; no new
component or second job. No correction required.

At 2026-10-09 19:58:50 UTC the supervisor checked scope, security/credential containment, Native
AOT, normal publication and restored-package default evidence, then explicitly approved only
the bounded OCI Plan and assigned the same implementer. Commit/merge/publication remain gated
on current code reviews and evidence. This approval does not settle either cloud Type-1 choice.

OCI product implementation/publication/acceptance subsequently passed; see the [bounded completion record](phase-76.3-oci-integrity-auth_completed.md). Earlier gates above describe their recorded stage, not current outstanding OCI work. Both cloud Type-1 choices were subsequently settled by the operator; see the current contracts.

## Superseded executable proposal — 2026-10-10

The operator rejected this proposed whitelist and chose user-vetted arbitrary code. This text and
the matching image-owned adapter below are historical, never current implementation instructions.
The replacement policy and open redesign work are in the active contract.

## Executable trust — Type-1 proposal requiring operator decision

```mermaid
flowchart LR
  Package[Local or OCI package] --> Validation[Mission Runtime validation]
  Validation --> Declarative[Declarative experts through existing engine]
  Validation --> Trusted[Exact server-owned executable identity]
  Validation --> Refusal[Other executable content: explicit refusal]
```

**Proposed first implementation:** retain arbitrary local/OCI *selection*, with admission policy
owned by Mission Runtime. Expand declarative execution independently of source. For executable
experts, accept only an exact server-owned immutable implementation, initially the existing OCR
expert and its accompanying script. Runner matches the submitted executable content to a digest
of the reviewed implementation shipped in its image; it runs its own copy, never a submitted
command/script. Unknown or modified executable experts fail explicitly before process creation.
The CLI does not maintain this policy or silently substitute an OCR catalog entry for a package.

This preserves the current service's trusted-code boundary and supplies the required OCR case.
It does **not** promise cloud execution of arbitrary uploaded Python/native code. Supporting
that requires a separate isolated compute boundary with no provider keys, managed identity,
broker/store access or host filesystem authority. Merely starting a child process, removing
environment variables, or putting it in the runner container does not establish that boundary.

| Observation | Source |
|---|---|
| Durable packages currently reject `exec`/`onnx` and accept only `llm`/`rule`/`json_extract` | `forge-mcl/src/ForgeMission.Core/Runtime/DurableMissionPackageValidator.cs` |
| Exec starts the declared command with inherited process environment | `forge-mcl/src/ForgeMission.Core/Adapters/ExecExpertRunner.cs` |
| Hosted OCR executes `python3 ./ocr.py`, takes `source_file`, `output_dir`, `mode`, and can create a PDF | `forge-runner/missions/ocr/experts/Ocr/expert.md`, `forge.toml` |
| Runner holds provider credentials and internal service authority | [deployment ownership](../design/deploy.md#topology) |

The decision concerns a service credential/execution boundary, classified Type 1 by
[Security Architecture](../design/security-architecture.md#type-1-versus-type-2-decisions).
[Supervisor workflow scope](../design/supervisor-workflow.md#required-loop) says
“Type-1 decisions go to the operator.” Selection requirements do not settle this executable trust
policy. Operator choice: approve the proposed server-owned executable policy, or include isolated
arbitrary executable compute in this phase's design. The second choice changes infrastructure
scope; it must be designed before implementation rather than inferred by the implementer.


### Superseded image-owned adapter

The trusted executable adapter uses that segment scratch as explicit process cwd and invokes its
image-owned implementation by a fixed absolute script path. It does not change process-wide cwd.
Resume recreates the same relative layout from immutable content refs before Core replay. Cleanup
only disposes that segment's scratch. The read-only implementer probe confirmed relative paths
work with current OCR and Core replay; it did not execute a product acceptance test. Produced
files needed after a pause must be uploaded before that pause and rehydrated at their stable
relative output path: Core skips completed executable steps during replay. Each executable
invocation has its own output path derived from its stable step key/attempt, preventing overwrite.
Core currently hardcodes `exec` routing; a host-supplied execution adapter seam is required before
any trusted executable can run. There is no fallback to `ExecExpertRunner` for an admitted package.

Trusted OCR identity is the tuple of exact expert markdown hash, every declared executable asset's
relative path/content hash and command/args/timeout. Runner builds the allowed
tuple from its reviewed image files, never a client-provided name/digest allowlist. It selects the
fixed OCR adapter only after equality; changed/extra executable assets fail closed. `source_file`
must be a verified named artifact with supported magic bytes/media type. A literal path, package
let/step binding for `source_file`, or override of `output_dir` is rejected. Before process spawn,
the adapter constructs `FORGE_SOURCE_FILE` and `FORGE_OUTPUT_DIR` solely from its own verified
stage/output allocation; it ignores package context for those authority-bearing values. Mode is
validated `text`/`pdf`. Use an explicit environment with only required fixed process/PATH/locale
entries and those three OCR values, no inherited provider keys or managed-identity variables.
This is least privilege for reviewed code, not a sandbox for untrusted executables.

## Operator policy record — documentation validation

2026-10-10 (Dubai): executable trust is decided by the operator, while content ownership is
still an explanation request. The current contract records the verbatim instruction, archives
the rejected image-owned-only design, and makes generic execution redesign/full reviews explicit.
Earlier technical PASS does not carry over to the changed executable contract.

This bounded update is documentation-only: runtime, Native AOT, default-path and UI acceptance
are N/A; no product changes or unused subagent team. No new security/implementation approval is
claimed. Existing OCI acceptance remains valid. Local validation PASS: eight files, 62 local
links/anchors, two JSON examples, balanced fences, top-level-only hub and diff checks.

| Stage | Start (UTC) | End (UTC) | Wall | Evidence |
|---|---|---|---|---|
| Documentation / supervisor / operator policy | Unavailable; not recorded | 2026-10-09 21:13:36 UTC | Unavailable | Recorded decision, superseded proposal archived, local checks PASS |

## Generic execution redesign — investigation and library probe

Operator subsequently approved: **"Agreed - convo host - persistent. Runner ephermeral"**.
Both ownership and executable trust decisions are now settled. Current generic execution design
reuses Core's existing adapters, expert-relative cwd and root replay with a runtime-only workspace
value; it does not retain the rejected image-owned identity whitelist.

Read-only implementer investigation found existing exec stdin/output deadlock and child-lifecycle
gaps relevant to the requested generic execution. The revised design covers concurrent bounded
I/O and joined cancellation. Existing OCR reads environment only and returns a summary; its own
script must adopt generic stdin/default semantics and return recognized text in text mode. No
runtime OCR response adapter is needed. Both product repos remained clean during investigation.

Supervisor probe of existing Microsoft.ML.OnnxRuntime **1.27.0** loaded the normal macOS ARM64
native library and a minimal numeric Identity model: float input `[1,2]`, probabilities index 1
observed `0.8`; `RunOptions.Terminate=true` rejected inference; the existing
`SessionOptions.SetLoadCancellationFlag(true)` rejected session creation. Observed command result:
`PASS: ONNX Runtime 1.27.0 native macOS ARM64, [1,2] float inference, probabilities[1]=0.8,
terminated inference rejected, cancelled session load rejected.`
Evidence: `/private/tmp/phase76-onnx-probe-20261009/result.txt` and `identity.onnx`, model SHA-256
`691a2d6476544d32195258db2e889967b1b25964ba89c209363b40bc9593425a`.
This is a library/native API probe, not Linux Runner-image, Native AOT or cloud acceptance.
Local Docker daemon was unavailable (`docker version` failed to connect to its configured socket);
that observation grants no waiver for the later real image/runtime gate.

| Stage / role / round | Start (UTC) | End (UTC) | Wall | Evidence |
|---|---|---|---|---|
| Design / supervisor / generic execution | 2026-10-09 21:15:43 | 2026-10-09 21:21:35 | 5m52s | Revised contracts and approved ownership record |
| Investigation / implementer / r2 | Unavailable; not recorded | 2026-10-09 21:19:34 | Unavailable | Read-only source/API report; no product changes |

### Generic execution design review — simplicity round 5

Full current checklist; **REVISE**, with no inherited verdict from the superseded design.
Settled operator trust/ownership decisions are not reopened.

| Check | Verdict / current-artifact evidence |
|---|---|
| New apps or libraries | PASS — existing services, store, interpreter and adapters |
| Reuse | REVISE — newly produced current-segment files were not registered in the workspace artifact-path set |
| Multiple code paths | PASS — source adapters converge; existing Core adapter routing reused |
| Legacy paths | PASS — old CLI removed; HTTP path serves actual non-CLI consumers; persisted launch normalization preserves real history |
| Knobs | PASS — existing timeout/profile convention, fixed transfer/output budgets |
| Speculative abstractions | PASS — workspace value and content/manifest DTOs have named runtime/store boundaries |
| Library choice | PASS — OCI 0.5.0 and existing ONNX APIs reused; real Linux/image acceptance still required |
| Copy-paste | PASS — common validator, preparation, intake and settlement seams reused |
| Redundant definitions | PASS — existing Artifact and ParticipantMessage events extended |
| Size versus requirement | PASS — revised detail covers executable code and durable files; no product changes |
| Test volume | PASS — focused failure/default observations; add same-segment output chaining and staging-collision cases |

Combined corrections required: register validated newly produced paths before downstream execution,
define concurrent visibility and distinguish it from Host commit; reserve `inputs/` and `outputs/`
against package assets before any staging. Both belong in the existing workspace/common validator.

| Stage / role / round | Start (UTC) | End (UTC) | Wall | Evidence |
|---|---|---|---|---|
| Review design / simplicity / r5 | 2026-10-09 21:21:35 | 2026-10-09 21:23:32 | 1m57s | Full 11-check REVISE table and two concrete contract gaps |

### Generic execution design review — ownership round 5

**REVISE.** Owners independently derived from the Desktop atlas and component READMEs before
comparison; all existing-owner placements passed. No owner acquired a second unrelated job.

| Behaviour | Derived / proposed owner | Verdict |
|---|---|---|
| Command/input/diagnostic presentation | CLI | PASS |
| Saved endpoint/registry configuration | CLI ForgeConfig | PASS |
| Project identity/declaration/roots | Application Projects | PASS |
| Frozen preparation/admission/reconciliation | Application Missions | PASS |
| Attachment lifetime/joined shutdown | Application Sessions | PASS |
| Local tool authority/execution | Client Runtime Hands | PASS |
| OCI reference/retrieval/auth | MissionRegistry / OCI dependency | PASS |
| Semantic package/assets | Core | Placement PASS; metadata/layout contracts incomplete |
| Shared action and conversation DTOs | Application Transport / Conversations.Contracts | PASS |
| Authenticated public command/query routing | ForgeAPI | PASS |
| Content/admission/receipts/continuations | Conversation Host | PASS |
| Process execution/lifecycle | Existing Core exec adapter | PASS |
| ONNX/HTTP/search semantics | Core adapters; Runner deployment bindings | PASS |
| Scratch/collection/rehydration | Runner | Placement PASS; same-segment path registration incomplete |
| Profiles/usage settlement | Runner; Billing retains pricing/ledger | PASS |
| OCR behavior/files | Authored script; generic execution/collection owners | PASS |
| Trace/final production, persistence and output | Core → Runner → Host → CLI | Placement PASS; failed-step text projection incomplete |
| Publication/deployment | Owning repos / forge-infra | PASS |

Supervisor accepted four combined blockers: same-segment output visibility, reserved runtime
namespaces, failed-step output persistence distinct from reason, and provider-free distribution
metadata reads before environment evaluation. Additional clarifications: retain ordinary
`token_count` names under exact reserved-key validation; fingerprint all execution-affecting
expert fields while excluding scratch; distinguish macOS native probe from future Linux/image
acceptance; new run does not use an MCL output declaration to write local files.
The next full review covers all corrections and newly explicit Core APIs, not just these findings.

| Stage / role / round | Start (UTC) | End (UTC) | Wall | Evidence |
|---|---|---|---|---|
| Review design / ownership / r5 | 2026-10-09 21:24:07 | 2026-10-09 21:28:32 | 4m25s | Full 18-behavior table; four contract blockers |
| Design / supervisor / r6 corrections | 2026-10-09 21:29:45 | 2026-10-09 21:30:51 | 1m06s | Combined corrections and exact Core DTO/API shapes |

### Generic execution design review — round 6

Both full current reviews returned **REVISE** for one shared technical blocker: parallel Core
trace callbacks could race over Runner's captured session state, progress ordinals, delta buffer
and cumulative output checks. All earlier findings were resolved. Ownership placement passed;
no component gained a second unrelated job. No product files changed.

| Simplicity check | Verdict / evidence |
|---|---|
| New apps or libraries | PASS — existing services, store, interpreter and adapters |
| Reuse | REVISE — existing outbox lacked a concurrent callback sequencing contract |
| Multiple code paths | PASS — local/OCI converge; one metadata parser and interpreter |
| Legacy paths | PASS — actual persisted/non-CLI consumers justify retained contracts |
| Knobs | PASS — fixed bounds and existing runtime conventions |
| Speculative abstractions | PASS — pure common construction and runtime workspace have real boundaries |
| Library choice | PASS — published OCI and observed macOS ONNX probe; Linux evidence remains required |
| Copy-paste | PASS — common parser, validator, progress and settlement seams |
| Redundant definitions | PASS — existing Text/Reason and event kinds reused |
| Size versus requirement | PASS — seven docs, +366/−72 at review; no product changes |
| Test volume | PASS — add concurrent outbox/budget failure observations |

| Ownership behaviour | Existing owner / verdict |
|---|---|
| Run arguments, named spelling, diagnostics | CLI — PASS |
| Project filename, identity and roots | Projects — PASS |
| Immutable preparation and uncertain admission | Missions / Sessions — PASS |
| Local/OCI retrieval | MissionRegistry / OCI library — PASS |
| Provider-free distribution metadata | Core manifest reader — PASS |
| Package/assets/input-name validation | Core — PASS |
| Semantics and compatible replay | Core / Runner identity checks — PASS |
| Wire DTOs and generated JSON | Conversations.Contracts — PASS |
| Public authentication/content proxy | ForgeAPI — PASS |
| Canonical admission/transitions | Conversation Host — PASS |
| Content persistence/commit/sweep | Conversation Host — PASS |
| Scratch rehydration/verified paths | Runner — PASS |
| Generic adapter execution | Core; Runner host bindings — PASS |
| Output collection/downstream visibility | Runner — PASS |
| Output publication/continuation | Runner → Host — placement PASS, sequencing incomplete |
| Cumulative output limits | Runner / Host — REVISE atomic reservation |
| Profile usage and settlement | Runner / Billing — PASS |
| OCR code and assets | Authored mission — PASS |
| Local file authority | Hands — PASS |
| Attachment/retry/disposal | Sessions / Host correlation — PASS |
| Exact trace/final text and rendering | Core → Runner → Host → CLI — placement PASS, sequencing incomplete |
| Configuration and credentials | CLI / existing credential seam / OCI — PASS |
| Publication/deployment/default proof | Owning repos / infra / supervisor — PASS design, evidence pending |

Supervisor correction puts one awaited per-command asynchronous gate inside the existing Runner
processor/outbox owner, covering delta batching, ordinals, budget reservation, chunk-before-fact
publication and pending/sent captured state. Execution remains parallel outside the gate; final
publication follows joined callbacks. Publication failure keeps existing pending/redelivery failure
semantics. Source inspection confirmed UsageAccumulator already uses interlocked counters; reuse it.
Fresh full reviews are required after this correction.

| Stage / role / round | Start (UTC) | End (UTC) | Wall | Evidence |
|---|---|---|---|---|
| Review design / simplicity / r6 | 2026-10-09 21:31:15 | 2026-10-09 21:32:22 | 1m07s | Full 11-check REVISE table |
| Review design / ownership / r6 | 2026-10-09 21:32:52 | 2026-10-09 21:36:00 | 3m08s | Full 23-behaviour table; one sequencing blocker |
| Design / supervisor / r7 correction | 2026-10-09 21:36:37 | 2026-10-09 21:36:37 | <1s | Explicit existing-owner asynchronous sequencing contract |

### Generic execution design review — round 7

Full current artifact independently reviewed: **simplicity PASS; technical/ownership PASS**.
The supervisor locked the generic design at **2026-10-09 21:40:31 UTC**. No implementation plan
approval or runtime acceptance is implied. Both reviews found the one Runner-owned asynchronous
gate sufficient for concurrent trace/budget/outbox state without serializing expert execution.

| Simplicity check | Verdict / evidence |
|---|---|
| New apps or libraries | PASS — existing engine, Host store and Runner |
| Reuse | PASS — parser, guards, dispatch, claims and outbox |
| Multiple code paths | PASS — converged preparation and one metadata parser |
| Legacy paths | PASS — clean CLI cut; actual non-CLI consumers/persisted data accounted for |
| Knobs | PASS — required settings/inputs and fixed bounds |
| Speculative abstractions | PASS — runtime workspace and one existing-owner async gate |
| Library choice | PASS — OCI accepted; ONNX probe observed; Linux/image evidence required later |
| Copy-paste | PASS — shared construction, validation and publication seams |
| Redundant definitions | PASS — existing event vocabulary and interlocked usage counters |
| Size versus requirement | PASS — seven docs, +443/−73 at review; required boundaries only |
| Test volume | PASS — proportionate failure, concurrency and default observations |

| Ownership behaviour | Existing owner / verdict |
|---|---|
| Command/named spelling | CLI — PASS |
| Project identity/default filename | Projects — PASS |
| Frozen intent/admission reconciliation | Missions — PASS |
| Invocation/attachment/disposal lifetime | Sessions — PASS |
| OCI retrieval/authentication | MissionRegistry / OCI — PASS |
| Distribution metadata | Core manifest reader — PASS |
| Package/assets/input names | Core — PASS |
| Semantic/replay compatibility | Core / Runner identity — PASS |
| Shared action JSON/DTOs | Application.Transport — PASS |
| Conversation transfer vocabulary | Conversations.Contracts — PASS |
| Public authentication/query proxy | ForgeAPI — PASS |
| Canonical launch/run/receipts | Conversation Host — PASS |
| Binary persistence/adoption/sweep | Conversation Host — PASS |
| Scratch rehydration/paths | Runner — PASS |
| Generic adapter semantics | Core / Runner bindings — PASS |
| Generated file collection/visibility | Runner — PASS |
| Trace/outbox/budget sequencing | Runner command processor/outbox — PASS |
| Produced-file continuation | Runner → Host — PASS |
| Profile usage/settlement | Runner / Billing — PASS |
| OCR behavior | Authored mission — PASS |
| Local multi-root authority | Hands — PASS |
| Tool correlation/exact-result retry | Sessions/Hands / Host — PASS |
| Exact step/final text and presentation | Core → Runner → Host → CLI — PASS |
| Settings/credential persistence | CLI / existing credential seam / OCI — PASS |
| CLI clean cut | CLI — PASS |
| Normal publication/deploy/default proof | Owning repos / infra / supervisor — PASS design |

No owner acquires unrelated duties, new datastore access or duplicate execution paths. Security,
transfer/failure and required DTO/API contracts are sufficiently defined for bounded planning.
Current design diff check passed. Linux/image and unified installed-path evidence remain future.

| Stage / role / round | Start (UTC) | End (UTC) | Wall | Evidence |
|---|---|---|---|---|
| Review design / simplicity / r7 | 2026-10-09 21:36:54 | 2026-10-09 21:38:34 | 1m40s | Full 11-check PASS |
| Review design / ownership / r7 | 2026-10-09 21:38:52 | 2026-10-09 21:39:51 | 59s | Full 26-behaviour PASS |
| Design lock / supervisor | 2026-10-09 21:40:31 | 2026-10-09 21:40:31 | <1s | Locked generic contracts; no code approval |

Documentation checkpoint after lock: **2026-10-09 21:43:05 UTC**, nine docs, 68 local
links/anchors, two JSON examples, balanced fences, top-level-only hub and `git diff --check` PASS.
This snapshot changes documentation only; product tests, Native AOT and default-path runtime
acceptance are N/A for the snapshot, still required for implementation. The first bounded Core
producer task is scoped and its read-only implementer Plan is in progress; no code approval.
