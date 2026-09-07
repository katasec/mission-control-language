# Phase 46.2 Task D — terminal mission result projection

> **Status:** implementation submitted for independent review 2026-09-08. Finding: Q46.1-03.
> Parent: [Phase 46.2](phase-46.2-codex-supervised-remediation.md).

## Decision and scope

The terminal declared MCL result is always the operator-facing answer. A mission expresses debate,
verification, synthesis, and any retry topology in its declaration; its final step must produce the
text it intends users to receive. `Answerer` is ordinary author vocabulary and has no Runner meaning.

This is a Type-2 Runner projection correction: no MCL grammar, output-selector syntax, TOML setting,
provider selection, tier/data owner, credential, or wire-schema change. Core already returns the terminal
`MissionResult`; Runner alone owns `RunResponse.AgentText`; API, ForgeUI, and Rooms already forward that
field and retain the full trace.

## Approved changes

| Owner | Change |
|---|---|
| Runner | Delete the verified-run lookup for an expert literally named `Answerer`; successful terminal runs project `MissionResult.Text`. Preserve current failure and tool-pause behavior. |
| Built-in content | Change hallucination-guard’s terminal verifier to return the verified original answer on pass rather than `pass`; regenerate its lock. Other missions retain their declared terminal result. |
| Built-in distribution | Publish a new immutable hallucination-guard OCI package, capture its digest, and update the Forge built-in pin so normal Runner resolution receives the changed content. |
| Documentation | Record the decision/evidence, resolve Q46.1-03, and remove stale `Answerer`-as-output guidance from Runner and hosted documentation. |

## Failure and compatibility

| Case | Required result |
|---|---|
| Terminal pass | `AgentText` equals terminal `MissionResult.Text`, irrespective of expert name. |
| Terminal failure | Preserve the existing generic verification-failure projection and `Verified: false`. |
| Pending tool call | Preserve empty `AgentText`, tool-use response, and non-terminal semantics. |
| Historic response | Read unchanged; only newly projected Runner responses change. |
| OCI resolution | Pin the new immutable digest; a registry failure retains the existing baked-in fallback. |

## Verification and done when

Focused Runner tests prove that an `Answerer` text differing from a terminal verifier text is not selected,
the two-step trace remains visible, failures stay safe, and tool continuation remains blank until terminal.
Run `dotnet build src/ForgeMission.slnx`, deterministic `dotnet test src/ForgeMission.slnx`, and `make install`.
Build/publish the immutable package and Runner image using the established workflows; use the infrastructure
repository's `what-if` then image deploy workflow for the normal hosted path.

The controlled default-path proof is a normal signed-in Room invocation of `@guard`: it renders the verified
answer (“no English month contains X”), not `pass`, while the trace remains inspectable. Existing markup,
interaction, and visual tokens are reused, so visual implementation is N/A.

## Non-goals

- No output-selection grammar, metadata field, mission registry, or Rooms-level output selector.
- No generic Worker, durable conversation, Project manifest, Desktop/TUI, Bob, identity, or legacy route work.
- No change to verification/trust semantics, trace retention, or raw model agent behavior.

## Implementation record — pending independent acceptance

| Check | Observation |
|---|---|
| Terminal projection | `MissionRunHandler` now returns terminal `MissionResult.Text` on a pass. Its focused regression uses a prior expert literally named `Answerer` with different text, and proves the terminal verifier result wins while a pending tool turn remains blank. |
| Guard content/package | `Verifier` returns the original answer as its pass verdict; `forge init` regenerated `mcl.lock`; `forge validate` reported `OK — mission is valid.` The immutable package was published as `ghcr.io/katasec/forge-mission-hallucination-guard:0.2.1` at `sha256:4020a035d14b00a76f723e1c147205316cbbba81c627e6fd6c920d0feddd3424`, and the built-in pin now uses that digest. |
| Focused and deterministic tests | Focused `MissionRunHandlerTests`: 2 passed. The terminal-failure regression proves the public response remains the generic safe projection and does not expose raw provider detail. With optional provider keys absent only in the child test environment: Core/CLI suite 622 passed, 11 intentional external skips; Conversation Host 172 passed; Worker 18 passed; Runner 5 passed; Rooms 97 passed. |
| Build and AOT | `dotnet build src/ForgeMission.slnx --no-restore`: 0 warnings, 0 errors. Final-tree `make install` completed its Native AOT compilation and updated `~/.local/bin/forge`; the final binary then validated the guard package successfully. |
| Hosted artifact and deploy | The normal `forge-runner image` workflow published `forge-runner:0.11.5` from commit `639385b` to ACR (`sha256:3f10e410c764240f011350b4cdba9864ea8e20f86c4f985a0f6b08b7ff78d0af`). On clean `forge-infra` `main`, `make 500-app-what-if` showed only the Runner image revision and normal dependent app reference refresh; `make 500-app` then succeeded. `ca-forge-runner-dev--0000024` is the latest ready healthy revision using that image. |
| Default path | Pending only the required signed-in Room `@guard` action: normal Forge Rooms login, a dedicated safe Room message, visible verified answer “no English month contains X” rather than `pass`, and an inspectable trace. No user-visible claim is made before that observation. |
