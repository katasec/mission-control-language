# Phase 43.2a — Client Runtime capability boundary

> **Status: Boundary proven; transient archive implementation superseded (2026-07-31).** The
> archive-based proof in the [_completed record](phase-43.2a-client-runtime-capability-boundary_completed.md)
> established that the runner needs no host mount. [43.2b](phase-43.2b-oci-mission-delivery.md)
> replaces that transitional bootstrap with runner-owned OCI mission resolution before review.
>
> **Parent:** [Phase 43 — Forge Desktop](phase-43-forge-desktop.md) · **Depends on:**
> [43.2 — Electron Forge Desktop shell](phase-43.2-electron-forge-desktop-shell.md) Task 2b ·
> **Architecture:** [Client Runtime / Mission Runtime split](../design/forge-desktop-client-runtime.md#capability-boundary--the-client-runtime-is-the-hands-locked-2026-07-30)

## Outcome

The Client Runtime is the user's **hands**. It alone holds the opened workspace and executes local
`Read` / `Edit` / `Write` / `Bash` calls through `IWorkspace` and `ToolExecutorRegistry`. The
Mission Runtime is the **brain**. It may run in Forge Cloud or in the local
`ghcr.io/katasec/forge-runner` Docker image, and it decides whether each `/v1/messages` turn is a
final response or a tool request.

Both targets must be indistinguishable to the Client Runtime's conversation/tool loop: the only
loop-level difference is the configured base URL. Neither target may access the user's opened
workspace directly. A cloud OCR mission therefore receives only deliberately relayed bytes or
metadata; a local Docker brain receives only an explicitly delivered mission package/source, never
the general workspace.

## Current gap

Task 2b starts the local runner with the repository bind-mounted at `/workspace` and sets
`MissionFile` beneath it. That proved the real image, the local provider-key mechanism, SSE, and
the visible `tool_use` → local execution → `tool_result` round-trip. It also gives the Docker brain
read access to the repository, which contradicts this spoke's boundary. The implementation must be
hardened before that path is presented as the intended private/local architecture.

## Locked invariants

- **The Client Runtime owns local authority.** Only it receives an `IWorkspace` and invokes local
  tools. The Mission Runtime never gets workspace-root configuration, a workspace bind mount, or a
  host path that lets it discover the opened folder.
- **The brain receives explicit inputs only.** Conversation/tool-result data uses `/v1/messages`.
  Mission content is supplied through a separately chosen delivery mechanism. Future file/artifact
  features must transfer selected bytes through an explicit protocol, not grant path access.
- **Cloud and Docker are interchangeable brains.** The existing `MissionRuntimeSession` and its
  tool loop stay unchanged. Target selection changes its base URL; local-Docker lifecycle remains a
  target adapter concern, not loop logic.
- **The Docker `/v1` listener is local-only.** Its dynamic host port binds to `127.0.0.1`, not all
  host interfaces. The Client Runtime continues to call that loopback URL.
- **Provider credentials are not workspace authority.** The approved Client Runtime-owned
  `ApplicationData/Forge/provider.env` mechanism may pass the selected provider values to the local
  runner; it does not authorize a host filesystem mount. Hosted credentials/auth remain hosting
  concerns outside this spoke.
- **This is not a `ContainerWorkspace` feature.** The Client Runtime still executes tools on the
  host through `LocalDiskWorkspace`; the Docker Mission Runtime is not a tool sandbox.

## Task 1 decision — local mission delivery (approved 2026-07-31)

The archive mechanism was a verified transitional implementation, not the retained normal path.
[43.2b](phase-43.2b-oci-mission-delivery.md) now makes the runner pull and load its own OCI mission
from a digest-pinned reference. The lasting decision here is the boundary: no host directory is
mounted and the brain cannot discover the workspace.

Mission OCI artifacts/images remain the future distribution mechanism for published consumer
missions. Explicit client-to-brain file/artifact transfer (for example OCR uploads) is a separate
follow-up; this spoke establishes the boundary only. The local Docker target retains one configured
mission until [43.3](phase-43.3-mission-attach-point.md) designs the consumer/author mission
picker and cross-target mission identity.

## Tasks (chronological)

1. ✅ **Historical proof 2026-07-31.** The archive upload proved a host mount is unnecessary. It
   was intentionally replaced before review by 43.2b's runner-owned OCI pull. [Evidence](phase-43.2a-client-runtime-capability-boundary_completed.md#task-1--approved-mission-delivery-contract).

2. **Make Docker port publication loopback-only.** Extend the shared `ForgeMission.Docker`
   Docker-Engine request shape so `RunContainerAsync` can set `HostIp: "127.0.0.1"` for the Client
   Runtime runner. Preserve existing CLI callers' intended behavior explicitly rather than changing
   them accidentally. Add focused serialization/request-shape coverage for the binding.
   ✅ Done 2026-07-31 — see [completed evidence](phase-43.2a-client-runtime-capability-boundary_completed.md#tasks-2-5--implementation-and-regression-coverage).

3. **Replace the repository workspace bind with the approved mission-delivery adapter.** Change
   `DockerMissionRuntime.StartAsync` to validate the selected mission according to Task 1, construct
   the approved explicit mission input, and set `MissionFile` to its container-visible location.
   It must neither derive a repository root for Docker nor pass that root into `DockerCli`. Retain
   the existing provider allow-list, image pull/prerequisite checks, health wait, cancellation, and
   cleanup semantics.
   ✅ Done 2026-07-31 — see [completed evidence](phase-43.2a-client-runtime-capability-boundary_completed.md#tasks-2-5--implementation-and-regression-coverage).

4. **Keep target selection outside the loop.** Wire the hardened local adapter into the Client
   Runtime composition root so a local Docker target yields only its loopback base URL to
   `MissionRuntimeSession`; a hosted target remains an externally configured URL and starts no
   container. Confirm no code in the conversation/tool loop branches on Docker versus hosted.
   ✅ Done 2026-07-31 — see [completed evidence](phase-43.2a-client-runtime-capability-boundary_completed.md#tasks-2-5--implementation-and-regression-coverage).

5. **Add boundary regression coverage.** Add unit tests for the Docker create request and the local
   adapter that prove: no host bind is present, including the repository, opened workspace, or
   selected mission source; `MissionFile` is beneath the approved container location; and the
   published port has `HostIp` loopback. Reject a symbolic link in the mission package so it cannot
   become an implicit path escape.
   ✅ Done 2026-07-31 — see [completed evidence](phase-43.2a-client-runtime-capability-boundary_completed.md#tasks-2-5--implementation-and-regression-coverage).

6. **Run the real integration proof.** With Docker available and real local provider credentials,
   run the unchanged Client Runtime conversation/tool loop against `ghcr.io/katasec/forge-runner`.
   Demonstrate prompt → visible tool call → local tool execution → final answer. Inspect the running
   container to prove its mounts and published-port address meet the invariants, and retain the
   named command/output as evidence. This check must also show the runner, not an in-process/demo
   runtime, handled `/v1/messages`.
   ✅ Done 2026-07-31 — see [real Docker evidence](phase-43.2a-client-runtime-capability-boundary_completed.md#task-6--real-docker-proof).

7. **Reconcile documentation and seek review.** Move completed implementation evidence to this
   spoke's `_completed` sibling, leave a one-line verified status here and in the Phase 43 hub, and
   submit the completion summary for architecture review against the outcome above. Do not mark the
   local Docker architecture complete merely because builds or unit tests pass.
   ✅ **Done — superseded, not separately reviewed.** This spoke's archive-based implementation was
   itself replaced by [43.2b](phase-43.2b-oci-mission-delivery.md)'s runner-owned OCI resolution
   before this review step ran (see the status note at the top of this doc), so there is nothing of
   this spoke's own implementation left to review independently. The boundary properties this spoke
   locked (no host mount, loopback-only port, Client Runtime never grants workspace access) were
   re-verified as part of 43.2b's architecture review instead — see
   [43.2b Task 7](phase-43.2b-oci-mission-delivery.md#tasks-chronological). No separate "architecture
   review pending" remains open here.

## Done when

Against the real local `ghcr.io/katasec/forge-runner` image, the unchanged Client Runtime loop
performs the same visible prompt → tool call → final answer flow as the hosted target. `docker
inspect` shows no host bind or other filesystem exposure, the mission exists only through the
approved explicit-delivery contract, and the `/v1` port is bound only to loopback. Tests enforce
those properties, the full suite passes, and the live evidence identifies the Docker container and
its `/v1/messages` requests.
