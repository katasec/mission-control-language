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
| Desktop Supervisor | The published artifact is `dist/forge-desktop/ForgeMission.Desktop`, launched with **zero arguments**. It owns startup/cleanup of the Application Host and any Runtime children it starts. | Its one positional Application Host URL argument is a development/test convenience and is not the normal launch. | [`ForgeMission.Desktop/Program.cs`](https://github.com/katasec/forge-desktop/blob/main/src/ForgeMission.Desktop/Program.cs) |
| Mission Runtime resolver | With `MissionRuntime:Mode` absent, mode is `cloud`. With both `MissionRuntime:BaseUrl` and `FORGE_API_ENDPOINT` absent, the endpoint is `https://api.forge.katasec.com`. | `MissionRuntime:Mode`, `MissionRuntime:BaseUrl`, or `FORGE_API_ENDPOINT`. | [`MissionRuntimeResolver.cs`](https://github.com/katasec/forge-desktop/blob/main/src/ForgeMission.Orchestration/MissionRuntimeResolver.cs) |
| Conversation Runtime resolver | With `ConversationRuntime:BaseUrl` absent or blank, the endpoint is ForgeAPI, `https://api.forge.katasec.com/`; readiness is `GET /health` there. The Desktop calls ForgeAPI's conversation messages (`POST /api/{Name}`) with the platform key from `forge login` as `Bearer`. `FORGE_API_ENDPOINT` does not change it. | `ConversationRuntime:BaseUrl` / `ConversationRuntime__BaseUrl`. | forge-desktop `ConversationRuntimeResolver.cs` (Phase 52.1 Task 8) |
| Local Kind | **Unsupported for the durable Desktop path** since Phase 52.1: the Desktop no longer starts a Kind tunnel, the Conversation Host requires the cloud ingress namespace, and a local Host would join the cloud Orleans cluster (shared membership table). Returns with `forge dev start` / unauthenticated local mode ([backlog](../backlog.md)). | — | [Phase 52.1](../phases/phase-52.1-cloud-conversations.md) |
| Application Host, owned by Supervisor | It listens on an **OS-assigned loopback port** (`http://127.0.0.1:0`), then reports the resulting address to its Supervisor, which gives it to the native Host. The port is intentionally dynamic per launch. | No fixed public configuration; changing this ownership/ready-address protocol changes the default. | [`ForgeMission.Application.Host/Program.cs`](https://github.com/katasec/forge-desktop/blob/main/src/ForgeMission.Application.Host/Program.cs) and [`ApplicationHostProcess.cs`](https://github.com/katasec/forge-desktop/blob/main/src/ForgeMission.Desktop/ApplicationHostProcess.cs) |
| Cloud service provenance | The Conversation Host, forge-runner, Billing, and ForgeAPI run in `cae-forge-dev`, deployed only through forge-infra `make` targets from merged `main`; image versions are recorded in the phase's completion record. | Direct image loading or `kubectl`/`az` edits are troubleshooting, never default-path evidence. | [Deploy Runbook](deploy.md) |
| Project state | A task names a dedicated disposable Project or an explicitly approved existing Project. It does not mutate an unrelated Project to obtain a test result. | A different starting state must be designed and documented by that task. | This acceptance rule |

The Supervisor internally passes its resolved Runtime addresses to the Application Host. That is owned
startup wiring, not a user override. Passing evidence records the published Desktop process,
zero-argument launch, absent external configuration changes, the normal route and service
provenance, the Project action initiated through the product surface, and the durable/user-visible
result. A health check alone is insufficient.

For the MAUI Desktop on Windows and macOS, the published artifact must come from the canonical
GitHub Actions Desktop build (or its unchanged draft-release attachment). The trigger/download
procedure is owned by [Phase 48](../phases/phase-48-maui-desktop-host-spike.md#standard-bundle-steps);
do not rebuild locally and call that release-artifact acceptance. Windows and macOS acceptance is
complete; see [the Phase 48 completion record](../phases/phase-48-maui-desktop-host-spike_completed.md).

### `forge chat` and `forge chat --hands`

| Part | Default | Override (not default-path evidence) | Source |
|---|---|---|---|
| Artifact | `forge` from `make install` on forge-mcl `main`, or the complete platform ZIP from forge-mcl's published GitHub Release built at merged main. Extract all native sidecars. macOS keeps the existing Homebrew OpenSSL/Brotli prerequisites. | A `dotnet run` or a branch build proves a lower layer only. | forge-mcl `Makefile` / [CLI release README](https://github.com/katasec/forge-mcl#cli-releases) |
| Endpoint | ForgeAPI `https://api.forge.katasec.com`, platform key from `forge login` as `Bearer`. | `FORGE_API_ENDPOINT`. | forge-mcl `ForgeExec.cs:24-26`, `ForgeChat.cs` |
| Project | Current-directory `forge.project.json` with stable `projectId`, declared mission/version references and relative folders. Missing file exits 1 before login/network. No creation or ancestor search. | `--project <folder>` is an explicit open-only folder override. | [Phase 64](../phases/phase-64-portable-chat-project.md) |
| Mission and conversation | Plain selects declared `Chat@Version` / `NoHands`; `--hands` selects `ChatHands@Version` / `ProjectWorkspace` and asks fresh file consent every launch. Shared Client reconnects to the newest equivalent existing authenticated hosted pin; no match or ambiguous pin stops. No lock or authoring ledger is required. | No implicit starter creation, migration or terminal-access mode. | [How conversations work §4](how-conversations-work.md#one-conversation-per-chat-mode) |
| History projection | Full authoritative server replay from zero. Write-only profile files under platform-user-home `.forge/sessions/<projectId:N>/<conversationId:N>/`; deletion or corruption rebuilds next opening. Local write failure shows a notice and server chat continues. | No cache display, offline mode or saved-display cursor. | [Phase 64 contracts](../phases/phase-64.1-portable-chat-contracts.md#locked-contracts) |
| Mode | TUI on a terminal; piped (line) mode when input or output is redirected. Both are the shipped binary. | — | [How conversations work §4](how-conversations-work.md#4-clients-and-turns) |
| Terminal (from Phase 56 Task 2) | Ghostty, not inside tmux; the TUI needs kitty graphics. Any other terminal stops at start-up with a named message. | Kitty is supported but not the default. | [Phase 56 G8](../phases/phase-56-tui-graphics.md) |
| Theme | No `~/.forge/config.json` (or no `theme` key): **dark** (Phase 56 Task 2b, 2026-10-02). | `{ "theme": "light" }` in `~/.forge/config.json`. | [Phase 56 G10](../phases/phase-56-tui-graphics.md) |

Current defaults passed installed acceptance on 2026-10-04: missing file, portable clone,
full replay, projection rebuild/failure, line and Ghostty turns, scoped read and fresh refusal.
See [Phase 64 observations](../phases/phase-64.1-portable-chat-contracts_completed.md#installed-default-path-acceptance).
The GitHub-built v0.9.1 ZIP path is verified by native extracted-payload checks on all three hosts,
matching remote checksums and normal authenticated macOS hosted replay; see
[Phase 65 release evidence](../phases/phase-65-cli-release-copy_completed.md#published-release).

**Desktop hands are broken, upgrade deferred.** Desktop still uses Conversations.Contracts 0.4.0 and
reads the hands work item from the claim reply, which is status-only since Host 0.7 (B13). Desktop
hands therefore cannot be default-path evidence until the Desktop client upgrade
([backlog](../backlog.md)) lands.

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
