# Phase 49.7 — Runner extraction readiness

> **Status:** Design-ready, implementation blocked on accepted MCL package publication, Runner
> contract policy, and governed identity/image prerequisites. No source move is authorized yet.

## Scope

`forge-runner` will own `ForgeMission.Runner`, `ForgeMission.Runner.Contracts`, their tests,
`Dockerfile.runner`, and a Runner-specific image workflow. It replaces MCL source edges with exact
private `[1.0.0]` packages for Core, ChatClients, MissionRegistry, Scout, and Serve. Runner
Contracts stays local until its separate Type-1 package/support policy is locked.

It does not move Desktop, Rooms, Platform, Billing, Conversations, application state, or product
mission-content lifecycle ownership.

## Locked fallback-content decision

Keep `forge-missions` deferred. Runner imports eight image-coupled built-in fallback directories
as `content/builtins/`, preserving `/app/missions` and `MissionDir`:
`vanilla`, `hallucination-guard`, `assistant`, `claude`, `grok`, `websearch`, `ocr`, and
`summarize`. At mono commit `36a8417f166668f578806913ba652b301634f98e`, their 48 source records
have canonical ordinal SHA-256 `2731c73cc6fecba7291d66787acbd9ce8d13f25ec59caa993c8edae5298d25a0`.

This preserves `BuiltinMissions`: OCI entries remain digest-pinned and fall back per item to the
baked path; `MissionRef` failures remain startup failures with **no** fallback; and the mounted
`MissionFile` CLI path remains separate. Ocr/Summarize and OCI offline fallback prove OCI cannot
replace this asset today. A content change is a Runner release change, not a standalone mission
repository release.

## Preconditions and gates

1. Merge and publish private immutable `forge-mcl` packages; record IDs, hashes, tag, visibility,
   and explicit `forge-runner` Actions read grant with clean-cache no-grant/grant proof.
2. Lock Runner Contracts v1 compatibility, consumer window, and migration order for Rooms/Platform.
3. Govern a new `forge-runner` OIDC subject and image workflow through `forge-infra`; preserve
   current image name/tags/multi-architecture/pull compatibility and never copy credentials.
4. Runner keeps only provider keys and `enrichment_cache_db`; it never receives Billing, Rooms, or
   platform-edge secrets. Remove the unused injected platform-key HMAC as a separately verified
   least-privilege infra change.

PR CI proves clean locked restore, no MCL source edge, zero-warning build, Runner tests, contract
serialization, and token-free image build. Image release uses immutable tags and BuildKit restore
secrets only. Runner remains JIT; MCL packages retain their AOT compatibility.

## Acceptance and rollback

Controlled evidence verifies manifest/image fallback integrity and invalid `MissionRef` failure.
The normal path later deploys only through `forge-infra`, keeps `MissionDir=/app/missions`, proves
internal health, `/missions`, and one existing caller execution. Roll back by restoring the prior
image and infra revision—never a cross-repo `ProjectReference`.
