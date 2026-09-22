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
`summarize`. Their import is pinned to mono commit
`36a8417f166668f578806913ba652b301634f98e`; it is not inferred from a later working tree.

### Built-ins integrity manifest

The future Runner import commits `content/builtins.manifest.v1`. It has exactly 49 UTF-8 (no BOM),
LF-terminated lines, including one final LF. Its first line is exactly:

```text
forge-runner-builtins-manifest-v1	36a8417f166668f578806913ba652b301634f98e
```

Each of the next 48 lines is, with literal tab separators:

```text
<lowercase-content-sha256>	<source-path>	<destination-path>
```

Records sort by `source-path` with `StringComparer.Ordinal`. `source-path` is a blob below only the
eight declared `missions/` roots, and `destination-path` is exactly
`content/builtins/` plus the source path after `missions/`. `content-sha256` is SHA-256 of the raw
blob bytes, not a Git object ID. No escape, text normalization, symlink, extra file, or mode
transform is permitted: every selected source record at the anchor is `100644 blob`. The Python
assets remain invoked through `python3`, not an executable bit.

The complete manifest bytes have 48 records, are 7,163 bytes, and SHA-256 to
`dc22ee1a48be58b69f6f76585901d2d288941fa0c67d48f0e04a9c888fa080d4`. This binds both the
source anchor and the relocated destination map; the earlier unspecified aggregate hash is not
integrity evidence and is retired.

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
serialization, and token-free image build. It regenerates the manifest bytes from a clean checkout,
byte-hashes every `content/builtins/**` file, and proves the exact expected set (no omissions or
extras), header anchor, record count, and aggregate hash. The image check proves `/app/missions`
has the same 48 content hashes after Docker `COPY`. Image release uses immutable tags and BuildKit
restore secrets only. Runner remains JIT; MCL packages retain their AOT compatibility.

## Acceptance and rollback

Controlled evidence verifies zero-override baked fallback, the digest-pinned OCI built-ins'
per-item fallback, manifest/image integrity, and invalid `MissionRef` startup failure. `MissionFile`
remains a separate mounted path. The normal path later deploys only through `forge-infra`, keeps
`MissionDir=/app/missions`, proves internal health, `/missions`, and one existing caller execution.
Roll back by restoring the prior image and infra revision—never a cross-repo `ProjectReference`.
