# Default-Path Acceptance

> **Status: mandatory start-of-task reading; governing design, handoff, and completion gate.** It
> applies to every task that changes user-visible, runtime, integration, or deployment behaviour.
> Documentation-only work records N/A explicitly.

## The rule

The configuration a person receives by launching the published product normally is a product fact.
It is the acceptance path. A passing unit test, a stubbed service, an alternate URL, an injected
environment value, or a manually swapped dependency may prove a narrower layer; none proves that
the normal product works.

Every applicable plan records the default facts before implementation. Every completion record
then names the same facts, the actual action performed, and its observed outcome. If the default
path fails, the task fails: repair the default route or its real dependency before marking the work
complete. Do not replace it with a custom configuration and describe that result as parity.

## Evidence layers

| Layer | Purpose | Can close the task? |
|---|---|---|
| Unit / contract | Proves deterministic rules and public message shapes. | No, unless the task itself has no runtime or user path. |
| Controlled component | Isolates one client/service boundary with a fake or test double. | No. State the replacement explicitly. |
| Browser / visual | Compares the running surface with its binding reference. | No by itself; it must use the default route for full acceptance. |
| Default path | Runs the published artifact with normal configuration and normal dependencies through the task's real action. | Yes, together with the task's other required checks. |

This is additive to—not a replacement for—automated, contract, security, visual, or deployment
verification.

## Current default facts

### Forge Desktop — local durable conversations

The Desktop default is an explicit process and endpoint map. A value that is deliberately assigned
by the operating system is a default too; it is not an invitation to invent a stable port.

| Component / owner | Default fact | Configuration that would change it | Defining authority |
|---|---|---|---|
| Desktop Supervisor | The published artifact is `dist/forge-desktop/ForgeMission.Desktop`, launched with **zero arguments**. It owns startup/cleanup of the Runtime children. | Its one positional Client Runtime URL argument is a development/test convenience and is not the normal launch. | [`ForgeMission.Desktop/Program.cs`](../../src/ForgeMission.Desktop/Program.cs) |
| Mission Runtime resolver | With `MissionRuntime:Mode` absent, mode is `cloud`. With both `MissionRuntime:BaseUrl` and `FORGE_API_ENDPOINT` absent, the endpoint is `https://api.forge.katasec.com`. | `MissionRuntime:Mode`, `MissionRuntime:BaseUrl`, or `FORGE_API_ENDPOINT`. | [`MissionRuntimeResolver.cs`](../../src/ForgeMission.Orchestration/MissionRuntimeResolver.cs) |
| Conversation Runtime resolver | With `ConversationRuntime:BaseUrl` absent or blank, the endpoint is `http://127.0.0.1:18080/`; readiness is `GET /health` at that address. | `ConversationRuntime:BaseUrl` / `ConversationRuntime__BaseUrl`. | [`ConversationRuntimeResolver.cs`](../../src/ForgeMission.Orchestration/ConversationRuntimeResolver.cs) |
| Local Kind bridge, owned by Supervisor | For the default Conversation address only, the Supervisor starts `kubectl port-forward --address 127.0.0.1 --namespace forge-durable service/conversation-host 18080:8080` when the endpoint is not already healthy. | A non-default Conversation Runtime endpoint disables this bridge. | [`LocalKindConversationRuntimeTunnel.cs`](../../src/ForgeMission.Orchestration/LocalKindConversationRuntimeTunnel.cs) |
| Conversation Host service | The Kind `conversation-host` service target listens on container port `8080`; the Supervisor exposes it only at loopback `127.0.0.1:18080`. | A service/port topology change must revise this row before code is written. | [`LocalKindConversationRuntimeTunnel.cs`](../../src/ForgeMission.Orchestration/LocalKindConversationRuntimeTunnel.cs) |
| Client Runtime, owned by Supervisor | It listens on an **OS-assigned loopback port** (`http://127.0.0.1:0`), then reports the resulting address to its Supervisor, which gives it to the native Host. The port is intentionally dynamic per launch. | No fixed public configuration; changing this ownership/ready-address protocol changes the default. | [`ForgeMission.ClientRuntime/Program.cs`](../../src/ForgeMission.ClientRuntime/Program.cs) and [`ClientRuntimeProcess.cs`](../../src/ForgeMission.Desktop/ClientRuntimeProcess.cs) |
| Local service provenance | `make -C ~/progs/forge-infra 350-conversation-kind-up` builds and rolls Host and Worker only from a clean `main` checkout. | Direct image loading or `kubectl set image` is controlled troubleshooting, never default-path evidence. | [Deploy Runbook](deploy.md) |
| Project state | A task names a dedicated disposable Project or an explicitly approved existing Project. It does not mutate an unrelated Project to obtain a test result. | A different starting state must be designed and documented by that task. | This acceptance rule |

The Supervisor internally passes its resolved Runtime addresses to the Client Runtime. That is owned
startup wiring, not a user override. Passing evidence records the published Desktop process,
zero-argument launch, absent external configuration changes, the normal route and service
provenance, the Project action initiated through the product surface, and the durable/user-visible
result. A health check alone is insufficient.

## New or changed defaults

Before a task changes a supported path—or introduces a new one—the active spoke must add or revise
its default facts: artifact, configuration that must be absent/present, dependency route and
provenance, safe starting state, action, and expected observable result. A missing row is a
build-readiness failure. Do not infer a default from a developer shell or an agent's temporary
environment.

If a default changes intentionally, the task owns migration and acceptance for the new default; it
does not keep accepting the old one accidentally.

## Exceptions

An exceptional test configuration may be used only to investigate a component or unblock a
non-acceptance check. The active spoke must state its exact scope, why the normal path cannot be
used at that point, its reversal path, and removal condition. It is a Type-2 operational exception
and cannot supply the task's default-path PASS. A normal-path failure remains open until a later
default-path observation passes.

## Required completion record

Use this compact record in the task completion evidence:

| Fact | Observation |
|---|---|
| Artifact | Exact published artifact/build exercised. |
| Defaults | Relevant overrides confirmed absent and normal configuration named. |
| Dependency | Normal route plus deployed/local dependency provenance. |
| Starting state | Dedicated safe Project/account/data state. |
| Action | The actual user action exercised end to end. |
| Outcome | Named durable, process, or user-visible result; PASS or FAIL. |
| Controlled tests | Any stub/override used elsewhere, labelled non-acceptance. |
