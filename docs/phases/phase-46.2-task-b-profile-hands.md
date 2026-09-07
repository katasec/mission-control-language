# Phase 46.2 Task B — immutable profiles and durable hands

> **Status:** implementation accepted 2026-09-07. Finding: D46.1-01/D46.1-02.
> Parent: [Phase 46.2](phase-46.2-codex-supervised-remediation.md).

## Scope card

This task establishes the smallest Phase 45 prerequisite for the generic durable hands path:
an immutable approved launch/profile, Application's one live attachment grant, Bob's bounded
enforcement, and Host-owned generic ordered hands facts. It advances Application's Project/session
ownership, ClientRuntime's local-authority boundary, and Conversation Host's sole durable ordering
responsibility. It deliberately does not make Worker execution generic; that remains Task C after
these facts are accepted.

The profile is closed and immutable: \`NoHands\`, \`ProjectWorkspace\`, or
\`ProjectWorkspaceAndTerminal\`. A new profile means a new launch/version. Application grants a
fresh attachment only after acknowledgement of the exact resolved profile. Bob alone decides,
executes, confirms, audits, cancels, drains, and cleans up local work. Host owns one durable
correlated request/result and attachment liveness; missing hands becomes \`AwaitingHands\`, never a
fallback or grant. Worker receives no Bob, workspace, credential, filesystem, terminal, network,
or attachment authority.

## Approved boundaries and non-goals

| Included | Explicitly excluded |
|---|---|
| Schema-4 read/validation of a pre-existing approved launch and immutable profile. | Authoring/editor, draft/candidate/evaluation lifecycle, promotion/publishing, mission lists, or evaluation APIs. |
| Application acknowledgement, exact launch re-resolution, fresh attachment/reconnect and disposal. | Presentation, Desktop, TUI, CSS, rail/view state, or visual-asset implementation. |
| Bob profile-derived declarations and typed outcomes, including structural terminal containment. | Worker catalog/resolver/executor migration, Janus mapper/progress migration, or Worker pause persistence. |
| Additive durable Host contracts/state for generic request/result, confirmation and \`AwaitingHands\`. | Legacy Janus \`/conversations\` routing, adapters, protocol clients, contracts, tests, or documentation removal. |
| Narrow typed Application transport for the hands lifecycle. | New public datastore ingress, a generic dispatcher/registry, \`FullAccess\`, escalation, arbitrary host/path access, credentials, network, or publish/push authority. |

The operator has decided that the legacy Janus transport has no external-client compatibility
obligation. It remains untouched here only because the generic Worker and normal hands route do
not yet exist. Once Task C and the generic Desktop/TUI route are accepted, a separate bounded
removal task will delete that transport and prove every current surface uses the generic path.

## Contract, failure, and security gate

\`MissionVersionLaunch\` carries an approved version identity/number, definition hash, bounded
definition content or reference, and its one profile. Schema-3 remains readable without inventing
a profile, launch, or remote conversation. Application re-resolves this immutable launch inside
the Project transaction, requires a boolean acknowledgement, and binds a fresh \`(conversation,
version, profile, application session, attachment)\` to Bob. It never accepts a caller-selected
profile, tool declaration, hash, policy result, or attachment ID.

\`NoHands\` exposes no tools and produces \`DeniedOutOfProfile\`. \`ProjectWorkspace\` permits only
bounded workspace-file operations; root/symlink escape, terminal, network, credential, and unknown
capability attempts are typed denials. \`ProjectWorkspaceAndTerminal\` may expose a terminal only
through a Bob-owned OS-enforced filesystem, environment, process, and network containment boundary.
A working directory or command filter is not containment. If that structural proof is absent, the
profile is not dispatchable and this task cannot be accepted.

Host appends \`ToolRequested\` then \`AwaitingHands\` when no matching live attachment exists. It
accepts exactly one exact attached result or confirmation correlation, assigns sequence, and causes
at most one continuation command. Wrong, duplicate, late, and unknown results append no fact and
cause no continuation/effect. Attempt cancellation terminalizes the attempt and cannot resume the
Core pause. Reconnect mints a new attachment under the already-pinned profile and may redeliver the
same request once.

This is the locked D46.1-02 Type-1 design, not a new architectural decision. It is an additive
Type-2 contract/persistence implementation: Host stays Tier 2 and sole Conversation-store writer;
Worker remains queue-only; Application Host gains no data-plane credential; Project files stay
Application-owned; Bob is the only local authority. All JSON is source generated; no reflection or
new warning suppression is allowed.

| Failure | Containment / observable result | Required proof |
|---|---|---|
| Malformed, unknown, stale, or hash/profile-mismatched launch | Application rejects before conversation or Bob attachment. | Schema-3 compatibility; malformed/unknown/mismatched launch tests. |
| No live hands | Host durably records \`AwaitingHands\`; no remote/local bypass or fabricated success. | Detach-before-request then fresh reconnect/redelivery. |
| Out-of-profile or containment escape | Bob returns correlated typed denial; it is never converted into a confirmation/escalation. | NoHands, terminal/root/symlink/network/credential/unknown capability negatives. |
| Confirmation, cancellation, or Bob failure | Bob owns the decision/execution; Host records the exact nonterminal/terminal fact once. | Confirmation, cancellation, audit, drain, and failure tests. |
| Wrong, duplicate, late, or lost result | Host returns typed conflict/no-op with no second sequence, continuation, or effect. | Correlation, cancellation-race, replay, and ordering tests. |

## Approved files

| Owner | Approved paths and change |
|---|---|
| Application / Projects | \`Projects/ProjectManifest.cs\`, \`ProjectManifestFile.cs\`, \`ProjectManifestJsonContext.cs\`, and \`ProjectService.cs\`: schema-4 additive approved-launch/profile read and validation only. New \`Missions/MissionCapabilityProfile.cs\` and \`MissionVersionLaunch.cs\` define the closed internal vocabulary. |
| Application / sessions | New \`Missions/MissionHandsConversationService.cs\`, \`Sessions/ApplicationSessionService.cs\`, and \`ApplicationComposition.cs\`: exact acknowledgement, Host reconciliation, fresh Bob attachment, detach/reconnect lifecycle. Legacy \`ConversationService\` and Janus adapters stay unchanged. |
| ClientRuntime (Bob) | \`ClientExecutionSession.cs\` plus narrowly named profile-scoped adapter/provider files, including \`ProjectBoundTerminalProvider.cs\`: profile-derived declarations, typed outcomes, confirmation/cancel/audit/drain, and structural containment. Existing non-mission session behaviour remains compatible. |
| Conversation contracts | \`ConversationContracts.cs\`, \`ConversationContractsJsonContext.cs\`, and \`ConversationDeterministicIds.cs\`: append generic mission hands requests, outcomes, launch/attachment/status records, deterministic IDs, and source generation without altering historic enum values or Janus reads. The request contains only mission location/call/tool/arguments/opaque continuation — never Project roots, Bob, credentials, capability handles, or a Worker transcript. |
| Conversation Host | \`Grains/{ConversationCheckpoint,ConversationGrainResults,IConversationGrain,ConversationGrain,MissionRunCheckpoint,MissionRunGrain}.cs\`, \`Api/ConversationApiEndpoints.cs\`, and bounded persistence/Blob adapters: append fields/IDs only; own launch verification, attachment liveness, one outstanding request, ordering, recovery, and thin typed projections. Existing legacy routes stay intact. |
| Application boundary | \`Adapters/Conversations/ConversationHostClient.cs\`, \`Application.Transport/{ApplicationContracts,ApplicationJsonContext,HttpApplicationChannel}.cs\`, and \`Application.Host/Transport/ApplicationEndpoints.cs\`: the only named actions necessary to create/attach/detach/query/confirm/submit a hands result. No direct Presentation-to-Host or Bob-to-Host route. |
| Evidence | Affected component READMEs and this task record. \`docs/plan.md\` remains a one-line phase hub. |

## Surface contract and default path

Task B makes no Presentation/Desktop/TUI visual change, so implementation visual evidence is N/A.
The future surface contract is already bound by [Phase 45.3](phase-45.3-operator-missions-experience.md):
references \`4a\`–\`4c\` and
[\`mission-hands-profile-states.svg\`](../images/phase-45/mission-hands-profile-states.svg), named
\`forge-desktop-dark\` token map, read-only profile acknowledgement, correlated tool status,
\`AwaitingHands\`, confirmation, and typed denial. Desktop and TUI will use these same typed actions
with the same outcomes; neither will contact Bob or decide policy.

Default-path acceptance applies, but Task B cannot close the final zero-argument Desktop journey:
that requires Task C's generic Worker execution and Phase 45.3's surface. Task B must record this
as deferred, not passed. Its controlled proof covers the Project/Application/Bob/Host boundary;
the final published Desktop proof remains an approved immutable profile, an in-root operation,
\`AwaitingHands\` reconnect, a typed denial, ordered result/continuation, and durable reopen without
runtime overrides.

## Verification and done when

Focused tests must cover schema migration/history compatibility; launch/profile/hash validation;
acknowledgement and foreign/stale attachment denial; reconnect/disposal; all Bob profiles,
root/symlink/network/credential containment, confirmation/cancellation/audit/drain; source-generated
contract round trips; Host ordering, one outstanding request, \`AwaitingHands\`, duplicate/wrong/late
result, cancellation race, and replay; and surface-neutral Application transport routes. The
terminal profile additionally requires an attempted filesystem/environment/process/network escape
against its actual containment boundary.

Run \`dotnet build src/ForgeMission.slnx\`, \`dotnet test src/ForgeMission.slnx\`, and \`make install\`
for Native AOT. Acceptance requires zero new managed warnings, updated component ownership docs,
an independent review, and truthful deferred final default-path evidence.

## Completion evidence — 2026-09-07

Task B now stores and validates a schema-4 immutable approved launch/profile, grants one fresh
Application-to-Bob attachment, and keeps generic request, result, confirmation, cancellation,
claim, handoff, and crash-interruption facts in the canonical Conversation owner. Bob derives its
declarations from the fixed profile and its macOS terminal path uses OS sandbox containment. A
caller cannot supply mission work: Application first claims the exact Host-owned request, then
dispatches it through the attached Bob. A clean drained detach permits one redelivery; an
unprovable in-flight crash records \`Interrupted\` and never replays the unknown local effect.

An independent adversarial reviewer accepted the final code after rejecting and correcting
authority ordering, launch integrity, exact replay, containment, claim concurrency, policy,
handoff, and crash-recovery defects. It found no Worker/Bob bridge, legacy-route change, UI scope
drift, or new reflection/suppression.

| Evidence | Observation |
|---|---|
| Focused Application/profile/schema | 64 passed. |
| Focused Host/contracts | 72 passed; includes concurrent claim, clean handoff, restart, and crash-interruption coverage. |
| Build | \`dotnet build src/ForgeMission.slnx --no-restore\`: 0 warnings, 0 errors. |
| Full tests | \`dotnet test src/ForgeMission.slnx --no-restore\` with optional live-provider keys absent: exit 0; ForgeMission.Tests 621 passed / 11 intentional external-integration skips; Conversation Host 167; Worker 60; Runner 5; Rooms 97. Running those optional live tests with installed provider credentials exposed external xAI/Claude-CLI drift, which is not Task B evidence. |
| Native AOT | \`make install\` exit 0 and installed \`~/.local/bin/forge\`. macOS linker emitted existing platform-library compatibility warnings; no managed build warnings. |
| Default path | Still deferred, not passed: Task C's generic Worker execution and Phase 45.3's published Desktop/TUI surface are required before the zero-argument durable-hands journey can occur. |
