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

OCI product implementation/publication/acceptance subsequently passed; see the [bounded completion record](phase-76.3-oci-integrity-auth_completed.md). Earlier gates above describe their recorded stage, not current outstanding OCI work. Cloud Type-1 choices remain pending.
