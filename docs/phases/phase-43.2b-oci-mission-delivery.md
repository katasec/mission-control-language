# Phase 43.2b — OCI mission delivery

> **Status: Public runner image published and integration-proven; architecture-review hardening is
> implemented and awaiting architect acceptance. Literal Electron UI proof remains pending
> (2026-07-31).** The Client Runtime must not package or deploy a mission.
> It supplies a mission reference; the Mission Runtime resolves and loads that mission itself.
>
> **Parent:** [Phase 43 — Forge Desktop](phase-43-forge-desktop.md) · **Depends on:**
> [43.2a — Client Runtime capability boundary](phase-43.2a-client-runtime-capability-boundary.md) ·
> **Architecture:** [Client Runtime / Mission Runtime split](../design/forge-desktop-client-runtime.md#capability-boundary--the-client-runtime-is-the-hands-locked-2026-07-30)

## Outcome

The normal local-Docker route is:

```text
Client Runtime → MissionRef → local forge-runner → OCI pull from GHCR → provider → /v1
```

The Client Runtime starts a runner with one `MissionRef` environment value and receives its local
loopback base URL. The runner owns the rest: it pulls the OCI artifact, checks its mission type,
caches it in its own Forge cache, loads the mission, then serves `/v1/messages`. There is no bind
mount, Docker archive-copy choreography, or Client Runtime knowledge of a mission's files.

This is the same ownership model as hosted Forge: a Mission Runtime makes a referenced mission
available; a Client Runtime is the hands that sends messages and executes local tools.

## Locked decisions

- **`MissionRef` is a complete, digest-pinned OCI reference.** Example:
  `ghcr.io/katasec/forge-mission-vanilla@sha256:9663e05847676da28191f09459ce45671d624221d2d9b329ff0770cb9621dc46`.
  Tags are not accepted for this startup path.
- **The runner resolves the reference itself.** It uses the existing `OciMissionPuller` / OCI client
  and its own `~/.forge/missions` cache. A pull failure is a runner-startup failure; it does not
  silently run a different baked-in mission.
- **The Client Runtime passes, but does not interpret, the reference.** Its local-Docker adapter
  validates only that a configured value is present, forwards it as `MissionRef`, and waits for
  `/health`. `MissionRuntimeSession` remains unchanged.
- **`MissionFile` remains a legacy runner mode for `forge claude --container`.** It is mutually
  exclusive with `MissionRef` and is not used by the Client Runtime.
- **Public GHCR missions are v1.** Anonymous pull works for the public Forge mission artifacts.
  Private registry authentication and unpublished-author delivery are separate work; neither is
  improvised into this path.

## Tasks (chronological)

1. **Extract a runner-owned source resolver.** Move the runner boot selection out of top-level
   `Program.cs` into a testable component. It must reject both `MissionRef` and `MissionFile`, pull
   one digest-pinned `MissionRef` through `OciMissionPuller`, construct one registry spec from its
   returned `mission.mcl`, retain the existing `MissionFile` legacy branch, and otherwise resolve
   built-ins as today.
   ✅ Done 2026-07-31.
2. **Enforce the reference contract.** Validate the exact `registry/name@sha256:<64 lowercase hex>`
   shape before any network call. Give missing/invalid references a startup error that names the
   `MissionRef` setting; do not fall back to an arbitrary built-in mission.
   ✅ Done 2026-07-31.
3. **Simplify the Client Runtime adapter.** Remove mission-file resolution, in-memory tar packing,
   Docker archive upload, and stopped-container startup. Read
   `MissionRuntime:Docker:MissionRef`, pass it as `MissionRef` when creating the runner, set no
   binds, publish only to loopback, and use the existing create-and-start lifecycle.
   ✅ Done 2026-07-31.
4. **Set the developer default intentionally.** `scripts/desktop.ps1` supplies the public,
   digest-pinned vanilla mission reference for `make desktop`. A caller can override the Client
   Runtime configuration without modifying loop code.
   ✅ Done 2026-07-31.
5. **Test the contracts.** Unit-test reference validation and source-mode exclusivity; test the
   Client Runtime Docker request contains `MissionRef`, no `MissionFile`, and no bind. Preserve
   coverage that normal CLI callers retain their existing port behavior.
   ✅ Done 2026-07-31.
6. **Run the real local proof.** Build a runner image containing this change, run `make
   desktop`, and verify prompt → visible local tool call → final answer. Inspect the runner: no
   mounts; loopback-only port; `MissionRef` present; logs show the OCI artifact was pulled/cached
   before the `/v1/messages` requests.
   **In progress — public-image integration proof done 2026-07-31; literal Electron UI proof
   remains.** GitHub Actions run `30594193883` published
   `ghcr.io/katasec/forge-runner:0.10.5` and updated `:latest` (both resolve to
   `sha256:2f90bf592f4522f5ecf94ebf772d71740a73ecb3d44a281fba85d45459456f4a`). A Client Runtime
   started that public image, the real `Read` loop passed, runner logs showed the pinned OCI
   mission pull and `tool_calls` → `stop`, and inspection reported `Binds=null`/`Mounts=[]` with a
   loopback-only port. See [completed evidence](phase-43.2b-oci-mission-delivery_completed.md#public-runner-image--real-docker-proof-2026-07-31).
7. **Reconcile evidence and review.** Move detailed proof to a `_completed` sibling and leave only
   concise status in this active spoke and its hubs. Do not call this done without the architecture
   review. **Architecture review completed; its startup-hardening findings are implemented and
   awaiting architect acceptance.**
8. **Harden the `MissionRef` boot boundary.** A successfully pulled OCI mission that later fails
   loading (missing `mission.mcl`, validation error, or missing provider key) must exit before
   `/health`, naming its `MissionRef`; baked-in fallback keeps skip-and-continue semantics. Also
   disambiguate the per-run wire label, centralize the vanilla reference, and cover the boundary
   with unit/process tests. **Implementation verified, awaiting architect acceptance — see
   [startup-hardening evidence](phase-43.2b-oci-mission-delivery_completed.md#startup-hardening-verification-2026-07-31).**

## Done when

`make desktop` uses the real local runner, which pulls the configured, digest-pinned public Forge
mission from GHCR itself and serves the unchanged Client Runtime tool loop. The container has no
host mount, accepts no mutable reference, reports a clear startup failure for invalid/missing
references, and exposes `/v1` only on loopback. Full tests and a live Docker inspection prove these
claims.
