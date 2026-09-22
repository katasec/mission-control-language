---
type: software-component
title: Mission Registry
description: AOT-safe owner of the pinned built-in mission catalogue and OCI mission/expert cache pulls.
resource: src/ForgeMission.MissionRegistry
tags: [missions, oci, registry, aot]
---

# Mission Registry

## Purpose

Own the built-in mission catalogue and the OCI cache/pull behavior shared by command and mission-runtime hosts.

## Why this exists

OCI-backed mission selection has filesystem, credential, cache, and network side effects. Keeping it outside Core preserves provider-neutral MCL semantics while preventing CLI reuse from leaking into the Runner.

## Owns

- Digest-pinned built-in mission catalogue and its embedded `vanilla.oci-ref` resource.
- OCI mission and expert reference parsing, credential lookup, cache paths, pulls, and mission unpacking.

## Does not own

- Command parsing or command output.
- Provider construction, pipeline semantics, HTTP routes, Docker lifecycle, or any user/project state.

## Consumers

- Forge CLI selects and pulls built-ins for command workflows.
- Mission Runner resolves its registered mission content and retains baked-content fallback behavior.

## Constraints

- Built-in OCI references remain digest-pinned.
- The library is source-only in this monorepo for now; it publishes no NuGet package.
- Network/cache failures are exposed to the consumer, which owns any fallback policy.
