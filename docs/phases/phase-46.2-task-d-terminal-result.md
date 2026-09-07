# Phase 46.2 Task D — terminal mission result projection

> **Status:** approved for implementation 2026-09-08. Finding: Q46.1-03.
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
