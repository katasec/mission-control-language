# Phase 49.4c — Runner and Orchestration CLI dependency cut

> **Status:** Final review. Evidence is in the
> [completion record](phase-49.4c-runner-cli-dependency-cut_completed.md). This card created no
> package, repository, workflow, deployment, identity, or store change. Acceptance requires merge.

## Purpose and locked owner

Create the internal, source-only `ForgeMission.MissionRegistry` component as the sole owner of
existing OCI cache/pull and pinned built-in mission-catalogue behavior used by CLI and Runner.
It is AOT-compatible and references Core plus the existing OciClient package; it is not a NuGet
package or a generic catalogue framework. This avoids pulling credentialed OCI/network/filesystem
side effects into provider-neutral Core and creates a genuine future extraction seam.

Its surface is limited to the existing `BuiltinMissions`, `BuiltinMissionReferences`,
`OciMissionPuller`, and `OciExpertPuller` behavior plus the canonical `vanilla.oci-ref` resource.
Its README, solution entry, and Component Atlas entry are required. OciExpertPuller moves only as
part of this one OCI cache/pull owner; no unrelated CLI behavior moves.

## Compatibility and boundaries

- Runner replaces its CLI edge with MissionRegistry plus Scout. It owns direct Grok composition and
  preserves the static three-minute client, `XAI_API_KEY` then `GROK_API_KEY` precedence, null when
  no key exists, and all existing error/provider behavior.
- CLI keeps command UX and any remaining OciClient use for `MissionBundle.Pack`.
- Orchestration owns its Docker-launch default and copies the same pinned reference as a source-local
  resource. It takes no runtime MissionRegistry/OciClient dependency. A test compares the two values.
  This is a Type-2 compatibility copy, removed/re-homed through a versioned contract before
  Desktop/MCL extraction.
- Preserve MissionRef/MissionFile rules, digest validation, per-item OCI fallback to baked content,
  required-MissionRef startup failure, routes, Docker behavior, keys, endpoints, stores, identities,
  and provider defaults. Do not edit Docker/workflows/deployment.

## Gates

| Gate | Decision |
|---|---|
| Security | Type-2 source ownership only: no tier, public entry, store, queue, identity, credential grant, or cross-context call changes. |
| Engineering | One concrete owner for shared OCI side effects; Runner retains host composition; no new knob, generic abstraction, or Core transport dependency. |
| Failure boundary | Preserve OCI pull propagation/baked fallback, stable missing-search-backend error, and local-resource validation. Add focused absent-key and default-resource negative observations. |
| Default path | N/A: no runtime/default artifact, endpoint, startup, provider, Docker, or user action changes. |
| AOT | Registry is AOT-compatible; Desktop must not reference it. Compare CLI, Application Host, Desktop Supervisor, and MAUI output/warnings. |

## Done when

- Runner has no CLI project/source reference; Orchestration has no linked CLI resource;
- canonical built-in/default behavior and Runner search-key behavior are deterministic-test proven;
- Registry/Runner/Orchestration/CLI docs, solution, atlas, focused tests, full build/test, AOT/MAUI
  checks, graph/deps audit, independent review, and revert rollback evidence pass; and
- no package/repository/extraction action occurs in this card.
