# Phase 76.9 — Host input admission

> **Status: implementation already merged; this design records its original admission boundary.**
> Source audit on 2026-10-11 confirmed Host admission (`3537011`), canonical binary reads
> (`48eaa1b`), Platform upload (`85469dd`) and Runner materialisation (`07ca039`) on their owning
> main branches. Later package-asset and output transport increments are also merged. The interim
> Runner guard below was removed by materialisation and must not be restored. This source audit
> is not new deployment/default-path acceptance. Next cloud gap:
> [outputs across execution segments](phase-76.10-output-continuations.md).

## Scope

The operator asked: “Please check the next item on the plan which should be Phase 76 for unified
cloud execution. Please kick of the documented supervisor workflow in order to complete the tasks
in that phase autonomously.” They then locked these product decisions: arbitrary user-vetted OCI
code may run; execution permissions come later; the Conversation Host is persistent; and the
Runner is ephemeral. The latest instruction is: “please continue.”

This increment borrows the established client attachment model: an authenticated client uploads an
input attachment, receives a durable typed reference, then includes that reference in its turn.
ForgeAPI keeps chunking, hashes, slots and broker metadata internal and makes `SubmitMissionTurn`
admit complete, validated named inputs before
the existing grain records the command and outbox together. This is **not** a cross-store ACID
transaction: Blob staging can precede rejection. The guarantee is that an incomplete or invalid
input never becomes a canonical command or outbox dispatch. The affected repositories are
[`/Users/ameerdeen/progs/forge-conversations`](/Users/ameerdeen/progs/forge-conversations),
[`/Users/ameerdeen/progs/forge-platform`](/Users/ameerdeen/progs/forge-platform), and
[`/Users/ameerdeen/progs/forge-runner`](/Users/ameerdeen/progs/forge-runner). The Host's locally
referenced Contracts project is released first; Runner and ForgeAPI then consume that published
package. There are no sibling project references.

It excludes Runner execution/materialisation, output upload, public binary download, Client or
Desktop UI, package claim-check migration, deployment-configuration changes, AOT, permission
policy, and the later staged-content expiry sweep. The required normal deployment and default-path
observation use the existing forge-infra route; they do not expand the product design. The sweep
is a technical lifecycle decision in the parent design, not an operator-requested
execution-permission feature; it is deliberately not represented as complete by this increment.

**Done when:** an authenticated default ForgeAPI path can stage an exact binary artifact and
submit it as a named mission input; Host accepts only a fully staged, package-declared input and
commits its canonical reference in the same grain transition that records and dispatches the
turn. Exact retries return the same turn; malformed, foreign, incomplete, conflicting, duplicate
or undeclared inputs create no command/outbox dispatch. The resulting Runner command preserves
the typed reference; the deployed interim Runner explicitly fails that new command shape before
Core rather than incorrectly executing its history JSON. Artifact materialisation/execution is the
next Phase 76 increment.

## Locked design

The parent [Phase 76.2 contract](phase-76.2-unified-cloud-run-contracts.md#named-inputs-binary-content-and-transfer)
defines content identity, limits and the admission invariant. This task applies only the input
half of that contract. The Host remains the only owner of durable bytes and canonical
conversation state; ForgeAPI authenticates the caller and sends ingress messages, but receives
neither Blob access nor a storage URL.

```mermaid
sequenceDiagram
  participant C as authenticated client
  participant A as ForgeAPI edge
  participant H as Conversation Host
  participant G as conversation grain
  participant B as Host Blob seam
  participant R as existing command outbox
  C->>A: UploadMissionInput(command, name, raw bytes)
  A->>H: internal raw ingress content chunks + trusted member
  H->>B: stage exact bytes and immutable receipt
  H-->>A: staged MissionContentReference / explicit error
  C->>A: SubmitMissionTurn(Inputs)
  A->>H: existing ingress command with body references
  H->>G: owned normalized turn
  G->>B: commit/verify every referenced binary input
  G->>G: validate names; record event and canonical command
  G->>R: owe StartMission in the same transition
```

### Transport and contract shape

Contracts adds a fixed `MissionWorkspaceDescription(string[] Roots)` value for the already-locked
future workspace body. This increment accepts no workspace input and does not add an authority
path; the type is published now only because `SubmitMissionTurn` must have its final typed shape.

`SubmitMissionTurnRequest` becomes an explicit `[JsonConstructor]`
`(ConversationId, CommandId, Text?, Inputs?, Workspace?)` record with a delegating, retained
three-argument `(ConversationId, CommandId, string Text)` constructor. This preserves the actual
published Client ABI while every source consumer moves to the generated-JSON constructor. Exactly
one of `Text` or `Inputs` is required. `MissionNamedInputs` permits string-only inputs and a
zero-byte artifact; it rejects null maps, duplicate JSON property names before deserialization,
duplicate names across maps, more than 32 **total** string/artifact names, binary bytes above the
fixed 100 MiB combined limit, invalid identifiers and reserved names. The existing 4 MiB body cap
limits serialized input JSON and the existing 32 KiB descriptor envelope limits artifact
descriptors. String values may be empty but not null. `Workspace` is rejected in this increment
rather than silently stored, because its producer and authority model belong to the later
Projects/Hands work.

`ConversationIngressBodies.SubmitMissionTurn` serializes a typed bag into the existing
`ConversationBody` field `mission-inputs`; it retains text in its existing text body. The one
ingress record is `(ConversationId, CommandId, Text?, Inputs?, Workspace?)`, with exactly one
of `Text` and `Inputs`. This is one input path with two source representations, not a legacy
route: `ConversationCommandMessages` sends both to one grain admission call. ForgeAPI never
guesses a pinned package parameter. The grain reads a text body only to normalize it to a
one-entry `MissionNamedInputs.Strings` map using the immutable pinned root input name, then follows
the same typed-bag validation/adoption path as an explicit input bag. It retains the original text
reference as `Goal` for chat history. Explicit input bags use
their submitted `mission-inputs` reference directly and use that same canonical JSON body as
`Goal` for history projection. Every resulting command therefore has one canonical named-input
reference while `Goal` remains the history-only display input.

ForgeAPI follows the attachment-client shape already used by `MissionEndpoints.UploadArtifact`:
the authenticated upload route accepts attachment metadata and raw bytes, derives the deterministic
command-bound input reference, chunks it internally, and returns that
`MissionContentReference`. The client puts the returned reference in
`SubmitMissionTurn.Inputs.Artifacts`; it never supplies chunk indexes, counts, hashes, slots, or
broker identities. This is the necessary Forge adaptation to an ordinary reusable provider file
identifier: the reference is deliberately bound to its future `CommandId` and `input/<name>` so
the existing Host adoption rule cannot attach one turn's staged bytes to another turn.

The
Contracts project owns the dependency-free `MissionContentChunkProperties` schema and
`TryRead(IReadOnlyDictionary<string, object>, ReadOnlyMemory<byte>, out MissionContentChunkEnvelope)`
reader, beside existing body chunk vocabulary; neither Contracts nor ForgeAPI references Host or
Azure types. `MissionContentChunkEnvelope` is the fixed record
`(Guid ConversationId, Guid OwnerCommandId, string Slot, MissionContentReference Reference, int Index, int Count, ReadOnlyMemory<byte> Bytes)`.
Its fixed AMQP
properties are strings for `message_kind=missionContentChunk`, `conversation_id`,
`owner_command_id`, `slot`, `content_id`, `content_sha256`, `content_type` and
`content_kind=input`; `content_bytes` is `long`, and `chunk_index`/`chunk_count` are `int`.
ForgeAPI writes those exact values internally and the Host only calls the shared reader. There is
no second JSON envelope and no base64 broker payload. Each edge send has a fresh
message/reply-session id; the Host's durable receipt is the retry identity.

### Host admission

`ConversationIngressHandler` routes the new raw content discriminator to one Host intake method.
It verifies the shared envelope and calls the existing ownership check before `StageContentAsync`;
an unknown or foreign conversation returns `404 notFound` without writing bytes. Valid stage
requests call the existing Host Blob seam with `ConversationAddress.ForMember(member,
conversation)`. Storage's existing immutable descriptor/receipt rules decide exact retry (202)
versus changed descriptor or bytes (`409 contentConflict`). Invalid identity/bounds/envelope are
`400 invalidRequest`; fixed limits are `413 contentTooLarge`. A storage/Orleans outage is left
unhandled for normal broker redelivery, as existing ingress commands do.

For an input artifact the Host requires `Kind == Input`, slot exactly `input/<name>`, and
`ContentId == ConversationDeterministicIds.Body(ownerCommandId, "mission-content/" + slot)`.
Only `SubmitMissionTurn` can later adopt it. Staging does not create a conversation event, a
canonical reference, a run or a dispatch.

`MissionConversationTurnInput` keeps `[Id(3)] string? TextBodyJson` and adds
`[Id(4)] string? InputsBodyJson` plus `[Id(5)] string? WorkspaceBodyJson`; every value is the
existing serialized `ConversationBodyReference` form and existing Ids retain their exact meaning.
The existing command-message adapter retains its ownership check, requires exactly one text/input
reference and passes those fields to the grain. The grain performs the
deterministic text normalization above, then reads the bounded named-input body and deserializes
`MissionNamedInputs`. It validates all names against the *pinned* `DurableMissionPackage` with
published Core 0.1.8's `TryValidateInputNames`. Before constructing the start command it calls
`CommitContentAsync` for each artifact using `CommandId` and its derived slot. Any `Incomplete`
or `Conflict` is `ConversationCommandOutcome.Invalid` and the command adapter returns
`400 invalidRequest` before a transition; an existing active turn remains `409 runAlreadyActive`
and a changed command receipt remains `409 commandConflict`. The byte commit is complete before
the grain's existing atomic event/receipt/outbox transition; the transition then stores the
immutable `NamedInputs` and `Workspace` body references in `ConversationCommand` (new trailing
optional members after `MissionInput`) and every continuation preserves both. The grain never
stores file bytes or a second content registry.

The grain first checks the existing receipt using only ingress-owned references: text compares its
incoming text body to the stored `Goal`; explicit input compares its incoming `mission-inputs`
body to stored `NamedInputs`; both compare command/conversation/deterministic run-turn IDs, pinned
launch and workspace. It returns the stored acceptance only on an exact match; a changed reference
is `409 commandConflict`. After no receipt exists, the grain normalizes text in memory or reads
explicit inputs, commits artifacts and builds the new command. It rejects post-serialization input
JSON above the existing 4 MiB body cap, then creates and verifies that deterministic
`mission-inputs` body **before** the grain transition through a small grain-local typed helper.
It uses the existing `ConversationBodies.Chunks`, `IConversationBodyStore.StageAsync` and
`CommitAsync` primitives rather than `WriteAsync`, whose untyped exception does not distinguish
storage incompleteness from a reused-id conflict. `BodyCommitOutcome.Conflict` maps to
`409 commandConflict`; `Incomplete` throws the existing unavailable-body failure for broker
redelivery; storage/cancellation exceptions propagate. A competing explicit input with different
bytes therefore conflicts before canonical acceptance and a matching winner is reused. This is
permitted staging before admission, not a cross-store ACID claim. It adds no outbox/state payload
or regeneration path. Existing persisted commands with
`NamedInputs == null` retain their original text-only dispatch and retry behavior. The receipt equality deliberately does not compare derived
`MissionInput`: `ComposeMissionInputAsync` reads the later event log, so that value can differ
when an old request is resent. Exact replay preserves the stored command's original
`MissionInput`; only a newly accepted command composes new history and enters the existing
event/receipt/outbox transition. `RetryMissionTurn` does not normalize or commit content: it
copies `Goal`, `NamedInputs` and workspace from the original accepted command, so a new retry
command id never tries to re-own an original artifact content id.

The current `Goal` remains the chat-history projection. A run-input turn creates its history
projection from canonical named-input JSON, where artifact entries contain only descriptor values.
That makes retries and event reconstruction compare the one normalized body reference. The
Runner command carries `NamedInputs`. Before this producer is deployed, `forge-runner` upgrades to
the published Contracts package and adds one compatibility guard at its existing mission-command
processor: `NamedInputs == Goal` identifies an explicit named-input bag and emits the existing
Error/Failed progress with fixed reason `Named mission inputs are not executable on this runner
version.` without calling Core. A normalized chat turn has `NamedInputs != Goal`, so it retains the
current text `Goal` execution path; existing persisted commands still have `NamedInputs == null`.
This guard is a temporary activation
boundary, removed in the next Runner materialisation/execution increment; it does not grant file
access or attempt a partial execution.

### Ownership, security and failure boundaries

| Gate | Decision and proof target |
|---|---|
| Bounded context/data owner | Conversation Host owns staged bytes, canonical references, receipts, event and outbox in its existing Conversation Blob/Table stores. ForgeAPI owns only HTTP authentication/routing. |
| Public entry and tiers | The existing-style authenticated attachment-upload route and existing `POST /api/SubmitMissionTurn` are Tier-1 ForgeAPI routes. The attachment route returns a command-bound `MissionContentReference`; only its internal edge adapter sends raw chunks to the existing Tier-2 ingress queue. Host is the owning Tier-2 service and Blob remains Tier 3 with no public ingress. |
| Cross-store/credentials | No service reads another context's store. ForgeAPI and Runner receive no Blob credential, SAS URL or direct-store route. Existing Host identity is reused. |
| Type | Persistent Host/ephemeral Runner and the Host ownership boundary are locked Type 1 decisions. The raw ingress envelope is a reversible Type 2 transport detail, contained behind the existing command bus and Contracts package. |
| Expected failure | Bad/foreign request is rejected at ingress; incomplete/conflicting content or undeclared names is rejected before grain transition; storage/broker failure is retried by the existing broker; accepted named inputs reach the interim Runner guard and fail before Core; no case executes a named-input history JSON as mission input. |
| Recovery/observation | Client repeats the same attachment-upload or turn-submit request after a transient failure; chunk retries belong to the internal edge adapter. Focused managed tests prove no dispatch for each rejection and one outbox command for an exact retry; normal PR CI proves the package/Host edge integration. |
| Engineering | Reuse the one body store, command bus, ownership check, deterministic id helper, grain receipt/outbox and Core validator. No new service, store, credential, runtime option, storage registry or abstraction. |
| UI/Desktop | N/A: no user-visible Desktop, ForgeUI or browser layout changes. |

### Verification and acceptance

Before CI, run non-Docker focused managed checks through the existing Platform edge harness. Run
the focused Conversation Host/Azurite functional slice in normal cloud PR CI before relying on its
unfiltered suite: a multi-chunk non-UTF8 artifact stages raw bytes,
an owned declared name is adopted and produces one command; valid string-only and zero-byte
artifact bags work; an exact lost-reply retry produces the same turn; changed bytes/descriptor,
foreign conversation, invalid identity, incomplete chunks, duplicate JSON/cross-map/undeclared/
reserved names, excess total count/size, both text and inputs, changed duplicate command content,
retry ownership, competing explicit/text body commits and active turn all prove no dispatch.
An ordinary newly submitted text turn still reaches the current Runner text path; an explicit bag
reaches the fixed pre-Core Runner failure. Restart/new-adapter evidence confirms
admission uses durable storage, not process memory. Existing text turns remain covered.

Those local harness and cloud-Azurite results are controlled evidence only. Default-path acceptance remains an
open completion gate until all product PRs are merged, the Contracts package is published, and the normal
deployed authenticated ForgeAPI endpoint uses its ordinary ingress/reply route to stage and submit
a named input with no custom URL, direct Host request, hand-prepared Blob, sibling reference or
package substitution. The observation is its accepted turn and canonical outbox command. Native
AOT remains a final whole-Phase 76 artifact gate after complete execution works; it is not a
development test for this increment.

## Build order

Historical delivery order, retained to explain the original producer/consumer boundary; do not
execute this list again against current main. The source milestones above supersede its interim
Runner instructions.

1. In `forge-conversations`, add Contracts transport values, generated JSON and ingress serializer/parser tests; update the Host intake, command adapter/grain and focused managed tests. Pin the published Core 0.1.8 package rather than copying its input validator.
2. Complete the required reviews, merge and publish the Contracts-bearing Conversations change through its normal publisher guard. Record the exact package version and successful package visibility verification.
3. In `forge-runner`, consume that exact published Contracts package and add the single named-input pre-Core failure guard. Merge and deploy it through normal forge-infra make targets before activating the new edge producer.
4. In `forge-platform`, extend the existing raw-upload endpoint pattern: add the one authenticated attachment-upload adapter that returns a command-bound `MissionContentReference` while turning its bytes into internal raw ingress chunks. Exercise that client shape through the normal command-bus route in focused tests and PR CI.
5. Merge the Platform consumer, deploy through the normal forge-infra make targets only after their required what-if, and perform the default ingress acceptance above: a new text turn succeeds through its existing Runner path, while an accepted explicit bag yields the Runner's fixed pre-Core failure, never execution. Do not run AOT until later Phase 76 execution work is complete.

Open design questions: none. The implementer must stop rather than infer a missing detail.
