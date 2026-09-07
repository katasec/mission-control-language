# Phase 46.2 Task A — Core generic root-scoped continuation

> **Status:** implementation accepted 2026-09-07. The active status and compact evidence are in
> [the Task A record](phase-46.2-task-a-core-continuation.md).
> Finding: F46.1-01. Parent: [Phase 46.2](phase-46.2-codex-supervised-remediation.md).

## Scope card

`ForgeMission.Core` is the sole owner. Its provider-neutral pipeline execution and trace/result
contracts need a generic root-scoped tool pause and opaque continuation so a declared child mission
can request a root-approved tool without receiving a dispatcher, Bob, or implicit tool inheritance.
This removes the reason Janus manually calls `Negotiate` and `Implement` as separate top-level runs.

This task changes only Core and focused tests. It does not change Worker catalog/execution, durable
contracts or storage, Application, Client Runtime/Bob, capability profiles, Desktop/TUI, legacy
Janus routing, or Janus/Naive assets. F46.1-02/03 remove the specialized Worker path only after
this task and the Phase 45 durable profile/hands work are accepted.

## Approved contract

Core adds a transport-neutral, source-generated, versioned opaque continuation envelope. The public
pause exposes root and child-agent locations, attempt, exactly one closed `PipelineToolCall`, and
`PipelineContinuation(format_version, payload)`. Only Core decodes `payload`; Worker/Host later
persist and relay it without interpreting it.

The payload is a Core checkpoint, not a Janus transcript. It contains the root identity and
declared root-tool-scope fingerprint, frame/loop/branch position, string execution context,
completed child output, the paused provider call, and ordered pending parallel work. It has no
dispatcher, Bob/session/attachment, Project/root path, credential, policy decision, provider client,
Host/Worker identifier, or capability handle. Its closed declaration descriptors are enough for
Core to reconstruct declaration-only tools on resume; a resumer cannot substitute a new
tool/profile/scope. Core re-derives the expert system prompt and supplies the provider only the
original provider-visible turn, exact function call, and matching function result.

`PipelineRunner.ResumeAsync` accepts only the opaque continuation, one correlated generic result,
and execution observers. It restores saved frame state and returns either a terminal `MissionResult`
or another single pause. Existing non-root `ToolCalls`, `MissionChatClient`, and `AgenticSession`
behavior stays unchanged.

The additive Core result vocabulary distinguishes `UnsupportedTool`, `MultipleOutstandingTools`,
`InvalidContinuation`, `DuplicateContinuation`, `LateContinuation`, and `ProviderFailed`. An unknown
tool, multiple provider calls, malformed or wrong-version/root/call continuation, or local
duplicate/late resume performs no provider call and advances no trace. A correlated `Denied`,
`Cancelled`, or `Failed` tool result is represented to the resumed provider as an error result;
Core does not decide policy or synthesize success. Cancellation of the entire root remains
`OperationCanceledException`, invalidates its in-memory continuation session, and never retries.

One root scope exposes at most one request. For opt-in root-scoped execution, a `parallel` block
whose branch can reach a root-tool-capable agent runs those branches in source order. Core checkpoints
branch index, completed outputs, and remaining work before every eligible agent. A pause prevents a
later eligible branch from invoking a provider; resumption completes the paused branch before the
next. Parallel blocks unable to reach that scope retain current concurrent behavior. This is a fixed
Core scheduling rule, not a registry, dispatcher, or Worker queue.

## Gates and failure boundary

This implements the already-locked D46.1-02 Type-1 continuation boundary; it creates no public
ingress, datastore, tier route, credential, identity, or cross-context access. Core owns execution-
state validation only. Future Host owns durable request/result correlation and replay; Application/
Bob owns profile enforcement, confirmation, execution, cancellation, audit, and cleanup.

| Failure | Containment and observable result | Proof |
|---|---|---|
| Unsupported/multiple request or invalid/duplicate/late continuation | Core returns the typed failure before a resumed provider call or trace advance. | Focused nested and resume negative tests. |
| Denied, cancelled, or failed correlated result | Core resumes the exact paused agent with an error tool result; policy stays outside Core. | Scripted provider observes the matching error result and terminal/next pause. |
| Provider failure or root cancellation | Existing explicit provider failure / `OperationCanceledException`; no fabricated success or retry. | Focused provider-failure and cancellation tests. |

No reflection or warning suppression is permitted. The checkpoint uses a source-generated JSON
context, and declaration reconstruction remains AOT-safe.

## Approved files and verification

| Path | Approved change |
|---|---|
| `src/ForgeMission.Core/Runtime/PipelineRunOptions.cs` | Add the explicit root-scope opt-in; child options stay isolated. |
| `src/ForgeMission.Core/Runtime/PipelineToolPause.cs` | Add closed pause, continuation, result/resume, declaration, and failure contracts plus JSON context. |
| `src/ForgeMission.Core/Runtime/MissionResult.cs` | Add optional pause and typed failure fields without changing legacy `ToolCalls`. |
| `src/ForgeMission.Core/Runtime/PipelineTraceEvent.cs` | Add Core-only pause/checkpoint provenance, never durable identifiers. |
| `src/ForgeMission.Core/Runtime/PipelineRunner.cs` | Add root checkpoint/pause/resume coordination and limited ordered eligible-parallel path. |
| `src/ForgeMission.Core/Adapters/DirectExpertRunner.cs` | Consume only the Core-reconstructed provider continuation. |
| `src/ForgeMission.Tests/Runtime/{PipelineTraceTests,MissionCompositionTests,AgentToolPipelineTests,PipelineRunnerTests}.cs` | Prove generic pause/resume, isolation, exact failures, no re-execution, provider continuation shape, and eligible-parallel order. |
| `src/ForgeMission.Core/README.md` | Update only if the finished public Core vocabulary needs discoverability; do not claim authority or durable ownership. |

Focused evidence must prove a newly declared `Root -> Child -> Agent` mission reaches a pause and
resumes without a Janus/Naive name or executor; unsupported tool/multiple calls/wrong and duplicate/
late continuation; denial/cancel/failure; provider failure; root cancellation; payload round-trip/
unknown version; no authority fields in the checkpoint; and two eligible parallel children executing
in source order with one exposed request. Existing direct-agent, non-tool parallel,
`MissionChatClient`, and `AgenticSession` coverage must remain green. Then run `dotnet build
src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx` with zero warnings.

Default-path acceptance applies to this runtime change. There is no current published Desktop
journey capable of invoking this generic nested-tool seam without the later Phase 45 launch, hands,
and durable protocol work. These Core tests are controlled component evidence only; the normal
zero-argument Desktop acceptance is an explicit final Phase 45/46 prerequisite and cannot be
claimed by Task A alone.

## Done when

The approved Core-only scope and checks pass, a new declared nested-tool mission proves the generic
pause/resume path with no mission-name selection, required build/test checks are clean, and an
independent review accepts the diff. Default-path completion remains open until the later integrated
task exercises the normal Desktop route.

## Completion evidence — 2026-09-07

The implementation uses a source-generated, versioned Core checkpoint and explicit execution
frames to resume nested sequential and eligible-parallel agent paths in a fresh `PipelineRunner`.
It preserves one root request, isolates children from authority, re-derives environment-bound
values and the expert system prompt rather than serializing them, and binds continuation resume to
the full mission/expert/binding and tool-scope fingerprints. Independent review accepted the final
diff after rejecting and correcting initial in-memory, secret-persistence, parallel-scheduling, and
identity shortcomings.

Observed verification: focused `AgentToolPipelineTests` 18 passed; `dotnet build
src/ForgeMission.slnx` exited successfully with 0 warnings/errors; full solution tests exited 0
(ForgeMission.Tests 617 passed, 7 skipped; Conversation Host 153; Conversation Worker 60; Runner
5; Rooms 97); and `make install` completed the Native AOT CLI publish. The macOS linker emitted
platform-library compatibility warnings during AOT linking; the managed build itself was warning
free. The normal zero-argument Desktop acceptance cannot occur until Phase 45/46 supplies the
approved durable profile, live Bob attachment, and generic Host/Worker route. It remains a required
integrated acceptance observation, not a Task A claim.
