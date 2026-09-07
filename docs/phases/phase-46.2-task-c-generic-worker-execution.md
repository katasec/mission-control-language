# Phase 46.2 Task C — generic durable Worker execution and progress

> **Status:** implementation accepted 2026-09-07. Findings: F46.1-02/F46.1-03.
> Parent: [Phase 46.2](phase-46.2-codex-supervised-remediation.md). Prerequisites: Task A and Task B accepted.

## Scope card

Replace the Worker’s compiled Janus/Naive catalog, concrete executors, provider-transcript reconstruction,
and persona progress mapper with one generic durable execution path. This advances the Conversation
Worker’s purpose: independently restartable mission reasoning that publishes semantic facts while the
Host retains ordering and recovery authority.

Every admitted generic Mission Conversation is durable. There is no `using durable` grammar, execution-mode
field, ephemeral fallback, or TOML durability switch. This changes the durable conversation path only; local
CLI and Runner execution remain separate generic Core consumers.

## Locked boundary

| Owner | Responsibility |
|---|---|
| Application | Re-resolves one immutable approved package/profile, obtains acknowledgement, creates the one live Bob attachment, and starts the typed generic conversation action. |
| Conversation Host | Validates and persists the supplied immutable package, allocates every sequence, owns generic trace/pause/result facts, and emits exactly one continuation after one accepted result. |
| Worker | Receives verified value content, constructs one fixed deployment-owned runner, runs/resumes Core once, and publishes through its existing restartable outbox. |
| Core | Parses/resolves supplied declaration content in memory and owns the opaque root-scoped pause/continuation semantics. |
| Bob | Remains the only local capability authority; it receives work only through Application’s attached Host correlation. |

The Worker deployment owns exactly one `default` provider binding:

```text
ConversationWorker__DefaultProvider
ConversationWorker__DefaultModel
ConversationWorker__DefaultEndpoint       # optional
ConversationWorker__DefaultApiKey         # scoped secret; optional only where the provider permits
```

Startup validation builds one `ProviderProfile`/`IExpertRunner` under `default`. A durable package may use
only omitted `using` for its LLM steps. Named `using <profile>` is rejected before any provider call. Package,
launch, queue, Host checkpoint, trace, and Core continuation contain no provider/model/endpoint/credential,
Project root, Bob handle, local-capability authority, or provider-message transcript. The opaque continuation retains
only declaration/runtime checkpoint data plus the exact provider-issued tool-call ID/name/arguments required to submit
the one correlated tool result; Host and Worker do not interpret, project, replay into a later run, or retain it after
terminal/cancel/interruption.

## Immutable package and admission

`DurableMissionLaunch` gains an optional, additive `Package`. The canonical package is bounded value content:

```text
format_version, package_hash, mission_source, root_mission_name, root_input_name
resolved_experts[]: name, lock_source, lock_path, lock_hash, expert_markdown
```

The package hash covers its canonical content. `root_input_name` names an existing input on the declared root;
Application binds the submitted text to that input only. This is a durable package convention, not an MCL
grammar feature. The package deliberately excludes `forge.toml`: local package configuration never gets
authority over the Worker’s provider, model, endpoint, or credentials.

Host and Worker independently reject malformed, oversized, tampered, non-canonical, unparsable, or inconsistent
package content; a missing root/input; named provider selection; or any `http`, `exec`, `search`, or `onnx`
expert. Initial generic durable packages allow only `llm`, `rule`, and `json_extract`. Existing schema-4
launches without a package remain readable/attachable but cannot begin a generic durable execution.

## Approved paths and migration order

| Owner | Paths and required change |
|---|---|
| Contracts | `src/ForgeMission.Conversations.Contracts/{MissionHandsContracts.cs,ConversationContracts.cs,ConversationContractsJsonContext.cs,ConversationDeterministicIds.cs,README.md}`: additive package/start/trace/pause/continuation vocabulary and source generation. Preserve historic positional fields and enum values. |
| Core | `src/ForgeMission.Core/Experts/ExpertLoader.cs` plus focused `src/ForgeMission.Tests/Runtime/` coverage: one in-memory resolved-expert parsing/validation seam. Do not duplicate YAML/package parsing in Worker. |
| Worker | `src/ForgeMission.ConversationWorker/{Program.cs,Messaging/MissionCommandProcessor.cs,Messaging/AzureServiceBusMissionCommandConsumer.cs,Messaging/WorkerSessionState.cs}` plus narrowly named generic package/executor/provider-options files: one PipelineRunner path, generic facts, opaque Core pause persistence, and existing outbox/redelivery. |
| Worker deletion | Delete `Messaging/WorkerMissionResolver.cs` and `Janus/{JanusMissionExecutor.cs,NaiveMissionExecutor.cs,JanusPipelineProgressMapper.cs,WorkerMissionLoader.cs}` once generic tests prove their replacements. Rename/move Janus-named session state to the generic owner. |
| Host/Application | The narrow package/start changes required in `ConversationHost/Grains`, `ConversationHost/Api`, `ConversationHost/Messaging`, `Application/{Missions,Projects,Adapters/Conversations,ApplicationComposition.cs}`, `Application.Transport`, and `Application.Host/Transport`. No direct Presentation/Bob-to-Host route. |
| Image/infrastructure | `Dockerfile.conversationworker` removes copied Janus/Naive assets and directory settings. The matching Worker deployment manifests in `/Users/ameerdeen/progs/forge-infra` configure only one scoped provider secret/model and retain least-privilege queue-only Worker RBAC. |
| Documentation | This card, the Phase 46 ledger/inventory/evidence, durable-conversation design, and nearest Core/Worker/Host/Contracts/Application READMEs. `docs/plan.md` remains the light phase hub. |

Implement in this order: additive contracts; Core in-memory package validation; Host admission/order; Application
launch/attachment handoff; generic Worker; generic tests; delete all concrete Worker branches/assets/config;
then drain legacy in-flight Worker sessions before deployment. Historic Janus/Naive records remain readable but
are never resumed by the generic Worker.

## Failure, security, and compatibility gate

| Failure | Owner and observable behavior | Proof |
|---|---|---|
| Invalid/tampered package, named profile, unsupported expert kind | Host rejects before dispatch; Worker independently rejects before a provider invocation. | Package negative tests including runner-not-called assertions. |
| Provider call uncertain on redelivery | Worker marks the generic run `Interrupted`; it never replays an unknown provider call. | Session recovery test. |
| Pending progress send | Worker resends the identical durable fact and event ID before new work. | Outbox/replay test. |
| Generic Core pause/result | Host sequences one request/`AwaitingHands`, then one exact result and deterministic continuation. | Wrong/duplicate/late-result and continuation-count tests. |
| Package supplied content attempts authority escalation | Admission rejects it; Worker has no project, Bob, filesystem, terminal, browser, Host-store, or provider-selection authority. | Boundary/architecture and forbidden-kind tests. |

This applies the already-locked durable ownership topology, not a new architecture. The Host remains the sole
Conversation-store writer; Worker retains only queue directions and its scoped provider secret. All JSON stays
source-generated. No reflection, dynamic discovery, provider registry, profile map, warning suppression, or
second runtime is permitted.

## Verification and done when

Focused coverage must prove:

- historic contracts/events deserialize unchanged; new package, generic trace, pause, and continuation records
  round-trip through source-generated contexts;
- a newly authored two-expert declaration and its resolved expert content run without a Worker image, config, or
  code change;
- package hashes, lock metadata, root input, MCL/expert syntax, named `using`, and forbidden kinds fail before
  runner/provider access;
- generic trace order, root/nested pause, exact resume, typed deny/cancel/fail, and terminal result;
- outbox resend, duplicate command no-op, provider-call interruption, and pause recovery retain no provider
  transcript;
- Host ordering, `AwaitingHands`, one continuation, and rejection of wrong/duplicate/late results; and
- Application cannot accept caller-selected package/profile/tools and cleans up a failed provisional Bob session.

Run focused tests, then `dotnet build src/ForgeMission.slnx`, `dotnet test src/ForgeMission.slnx`, and
`make install`. Build the Worker image and perform the normal clean-`main` Kind provenance/rollout route.
Acceptance requires no new managed warnings, independent adversarial review, and all deletion/boundary checks.

Default-path acceptance applies. Task C’s controlled proof is a clean-main Worker image using the normal
Host → command queue → Worker → progress queue path for a disposable Project with a newly authored approved
package, default runner, bounded hands operation, reconnect through `AwaitingHands`, typed denial, and durable
ordered result. The final zero-argument Desktop/TUI proof remains honestly deferred to Phase 45.3 because this
task makes no UI change.

## Explicit non-goals

- Presentation, Desktop/TUI layout, authoring/evaluation workflow, result-selection policy, or UI work.
- Legacy Janus `/conversations` route, adapter, protocol clients, obsolete contracts/tests, or docs removal. It
  is a later bounded deletion task after generic Desktop/TUI acceptance.
- User attribution, authenticated tenant/member association, membership enforcement, or identity-bearing Host
  contracts. The current single-environment development partition remains unchanged; that future authenticated
  edge/Host feature is independent of this task.
- Heterogeneous durable provider profiles, profile maps/fallbacks, arbitrary network/process expert kinds,
  capability escalation, Worker-to-Bob access, or Worker datastore permissions.

## Completion evidence — 2026-09-07

Independent review accepted the generic path after adversarial corrections to Host admission/correlation,
launch bounds, exact profile acknowledgement, provisional Bob cleanup, opaque continuation lifetime, and
infrastructure documentation. The Worker has no compiled Janus/Naive resolver, executor, mapper, mission
asset, or directory configuration; historic records remain readable only.

| Evidence | Observation |
|---|---|
| Build | `dotnet build src/ForgeMission.slnx --no-restore`: 0 warnings, 0 errors. |
| Deterministic full tests | Provider/runtime keys absent in the child process: ForgeMission.Tests 622 passed / 11 intended skips; Host 172; Worker 18; Runner 5; Rooms 97. |
| AOT and image | Worker Native AOT publish passed; `make install` completed and installed `forge`; current Worker image built successfully. macOS linker emitted platform dylib-version warnings only. |
| Infrastructure | Changed Bicep compiled successfully; scoped Worker configuration has one deployment-owned OpenAI provider/model binding. |
| Default path | Controlled Kind rollout remains pending clean merged `main`; final zero-argument Desktop/TUI proof remains Phase 45.3 work, not claimed here. |
