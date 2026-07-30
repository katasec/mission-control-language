# Phase 43.2a — Client Runtime capability boundary

> **Status: Design — decision gate.** Do not implement this spoke until Task 1's mission-delivery
> decision is approved. The preceding 43.2 Task 2b proof established that the real Docker runner
> can drive the existing `/v1` conversation/tool loop; it did **not** establish the final local
> filesystem boundary.
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

## Tasks (chronological)

1. **Resolve and record the local mission-delivery contract — design approval required.** Choose
   one v1 mechanism for presenting an explicitly selected mission to the runner: a Client
   Runtime-created immutable archive uploaded through the Docker Engine API into container-owned
   storage before the runner starts, or a mission-containing image/OCI artifact. A host bind mount,
   including a read-only selected-mission mount, is not an option: Docker is still reading the host
   filesystem. Define the exact source, container path, `MissionFile` value, lifecycle, and how
   `mission.mcl`, `forge.toml`, `mcl.lock`, and local experts resolve. State whether the selected
   mission may originate inside the opened workspace and what the brain can read in that case.
   Update the architecture doc and this spoke with the approved choice. **Do not begin Task 2 until
   this is resolved.**

2. **Make Docker port publication loopback-only.** Extend the shared `ForgeMission.Docker`
   Docker-Engine request shape so `RunContainerAsync` can set `HostIp: "127.0.0.1"` for the Client
   Runtime runner. Preserve existing CLI callers' intended behavior explicitly rather than changing
   them accidentally. Add focused serialization/request-shape coverage for the binding.

3. **Replace the repository workspace bind with the approved mission-delivery adapter.** Change
   `DockerMissionRuntime.StartAsync` to validate the selected mission according to Task 1, construct
   the approved explicit mission input, and set `MissionFile` to its container-visible location.
   It must neither derive a repository root for Docker nor pass that root into `DockerCli`. Retain
   the existing provider allow-list, image pull/prerequisite checks, health wait, cancellation, and
   cleanup semantics.

4. **Keep target selection outside the loop.** Wire the hardened local adapter into the Client
   Runtime composition root so a local Docker target yields only its loopback base URL to
   `MissionRuntimeSession`; a hosted target remains an externally configured URL and starts no
   container. Confirm no code in the conversation/tool loop branches on Docker versus hosted.

5. **Add boundary regression coverage.** Add unit tests for the Docker create request and the local
   adapter that prove: no host bind is present, including the repository, opened workspace, or
   selected mission source; `MissionFile` is beneath the approved container location; and the
   published port has `HostIp` loopback. Add a test that a mission outside the approved delivery
   scope is rejected before Docker starts.

6. **Run the real integration proof.** With Docker available and real local provider credentials,
   run the unchanged Client Runtime conversation/tool loop against `ghcr.io/katasec/forge-runner`.
   Demonstrate prompt → visible tool call → local tool execution → final answer. Inspect the running
   container to prove its mounts and published-port address meet the invariants, and retain the
   named command/output as evidence. This check must also show the runner, not an in-process/demo
   runtime, handled `/v1/messages`.

7. **Reconcile documentation and seek review.** Move completed implementation evidence to this
   spoke's `_completed` sibling, leave a one-line verified status here and in the Phase 43 hub, and
   submit the completion summary for architecture review against the outcome above. Do not mark the
   local Docker architecture complete merely because builds or unit tests pass.

## Done when

Against the real local `ghcr.io/katasec/forge-runner` image, the unchanged Client Runtime loop
performs the same visible prompt → tool call → final answer flow as the hosted target. `docker
inspect` shows no host bind or other filesystem exposure, the mission exists only through the
approved explicit-delivery contract, and the `/v1` port is bound only to loopback. Tests enforce
those properties, the full suite passes, and the live evidence identifies the Docker container and
its `/v1/messages` requests.

## Questions requiring product/design decisions

1. **What is the v1 local mission-delivery mechanism?** The boundary excludes every host bind,
   including a read-only mission-folder mount. The practical author workflow is for the Client
   Runtime to package the selected mission and upload it through the Docker Engine API into
   container-owned storage before start. A mission OCI artifact/image is the strongest distribution
   story for consumers, but adds packaging/pull/version-selection work. Which should 43.2a build
   first?
2. **May an author run a mission whose source is inside the opened workspace?** If yes, a mission-
   Runtime can copy only the mission package into the container, so the brain can read the copy but
   not the workspace. If no, authored missions need a separate project/package location before they
   can be run. Which author workflow is intended for v1?
3. **What is the first explicit artifact handoff?** The architecture allows the Client Runtime to
   relay selected bytes (such as a file chosen for OCR), but the current chat/tool loop transports
   text tool results only. Should this spoke only establish the boundary and leave an artifact
   upload/download contract to its own next spoke, or is a minimal file handoff required here?
4. **How is the selected mission identified across targets?** The current Docker proof serves one
   configured `MissionFile`; Cloud can route a published mission by its own runtime configuration.
   Should 43.2a retain that one-configured-mission shape while 43.3 designs the consumer/author
   picker, or must the delivery contract already carry a mission identity that is identical for
   Docker and Cloud?
