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

### The `forge` CLI

| Part | Default | Override (not default-path evidence) | Source |
|---|---|---|---|
| Artifact | `forge` from `make install` on forge-mcl `main`, or the complete platform ZIP from forge-mcl's published GitHub Release built at merged main. Make invokes shared PowerShell scripts locally and in CI. Extract all native sidecars. macOS keeps the existing Homebrew OpenSSL/Brotli prerequisites. | A `dotnet run` or a branch build proves a lower layer only. | forge-mcl `Makefile` / [CLI release README](https://github.com/katasec/forge-mcl#cli-releases) |
| Build identity | Default local install derives `X.Y.Z-dev.N+<full-sha>` from the nearest CLI tag and commit distance, including `dev.0` on an exact tag. A release prints `X.Y.Z+<full-sha>` at its exact immutable tag. Merged PRs release automatically: minor by default, `release:patch` for a bug fix; major requires a separate explicit operator request/approval. | `RELEASE_TAG`, `RID`, `CLI_OUTPUT`; these must be absent for normal laptop acceptance. A dirty local tree adds `.dirty`. | Phase 75, forge-mcl [version/build/release scripts](https://github.com/katasec/forge-mcl/tree/main/scripts) |
| Endpoint | ForgeAPI `https://api.forge.katasec.com` (`api.endpoint` in `~/.forge/config.json`, written with defaults on first use), platform key from `forge login` as `Bearer`. | A changed `api.endpoint`. | [`forge run` decisions](how-conversations-work.md#forge-run-decisions) |
| Project | Current-directory `forge.project.json` with a stable `projectId` and relative `folders` (a new project lists `"."`). No ancestor search. | `--project <file>` opens that exact file. | forge-client `ProjectService.cs` |

### `forge run`

| Part | Default fact |
|---|---|
| Invocation | `forge run <mission.mcl\|oci-ref> <prompt>` in a folder with `forge.project.json`; no Hands flag and no consent prompt. |
| No project | `forge run` exits 1 with `No forge.project.json found in the current directory. Run \`forge project create\` to create one.` |
| Reasoning | The cloud: a new mission conversation in the Conversation Host for each launch, readable after the CLI exits. |
| Authority | Hands may touch only the declared `folders` (`ProjectWorkspace`: Read/Write/Edit, no terminal tools); no folders means no Hands. |
| Cloud services | ForgeAPI, the Conversation Host, forge-runner and Billing in `cae-forge-dev`, deployed only by a merged forge-infra Terraform PR whose `terraform apply` passed; image tags are in forge-infra `dev/terraform/images.tf`. |
| Action / result | A real mission writes a file whose bytes are checked independently; the conversation id is printed to stderr and the final answer to stdout. |

Last passed 2026-11-18 (Phase 79): a CLI built from forge-mcl `4a364da`, default endpoint, Hands
wrote `phase79-proof.txt` (conversation `ea6195a6-249e-5891-9ca0-269409315a49`).

### `forge project create [folder]`

| Part | Default fact |
|---|---|
| Folder | Current directory when omitted; an explicit folder must already exist. |
| Result | Writes one `forge.project.json` with a stable GUID and `folders: ["."]`; nothing hosted is created. |

## Changing a default

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
