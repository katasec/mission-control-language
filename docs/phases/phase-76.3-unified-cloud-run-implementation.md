# Phase 76.3 — Unified cloud run: implementation plan

**Status:** Implementer plan approved after sequential simplicity and ownership reviews on
2026-10-09. Parent: [Phase 76](phase-76-unified-cloud-run.md). The locked contract is
[Phase 76.2](phase-76.2-unified-cloud-run-contracts.md). Implementation is authorized.

## Delivery slices

| Order | Repository | Bounded change | Depends on |
|---|---|---|---|
| 0 | `oci-client-dotnet` | Add AOT-safe `PulledMission` and `PullMissionWithDigestAsync`, reusing its existing authenticated manifest-digest pull; publish the package. | Locked contracts |
| 1 | `forge-mcl` | Core canonical package/input manifest, deterministic package bytes/hash and OCI digest resolution. Move `Core/Resolution/CredentialStore` persistence/lookup into CLI composition; CLI alone passes one registry pull token and Mission Registry removes its duplicate credential-store path. Publish Core. | 0 |
| 1a | `forge-desktop` | Move the existing Desktop boot platform-credential read into Desktop composition before consuming the Core package that removes Core's credential store. Preserve the same saved-login failure and Application Host handoff. | 1 |
| 2 | `forge-conversations` | Additive package/input-body, immutable-manifest and start-response contracts; publish contracts. | 1–1a |
| 3 | `forge-client` | `project.json` migration, labelled-root Hands, typed Application submission/follow service; publish Client/Hands/Transport. | 1–2 |
| 4 | `forge-platform` | ForgeAPI stages typed package/input bodies through existing authenticated ingress. | 2–3 |
| 5 | `forge-conversations` | Host admission, body persistence, matching-retry checks, dispatch and run projection. | 2, 4 |
| 6 | `forge-runner` | Read admitted bytes, materialize read-only staging, bind named inputs, execute and resume. | 1–2, 5 |
| 7 | `forge-mcl` | CLI command/config/login/project migration, stream/cancellation projection; remove legacy surface. | 1–3, 6 |
| 8 | `forge-infra` | Deploy changed Host/Runner/API images and verify the existing ingress/private queue and least-privilege RBAC contract through the required Make layers. | 4–7 |
| 9 | All affected repos | Compatibility, Native AOT, release, installed default-path acceptance and closure. | 1–8 |

## Files and owners

| Repository | Intended files/areas | Why |
|---|---|---|
| `forge-mcl` | CLI `Program`, `ForgeRun`, `ForgeExec` deletion, `ForgeConfig`, `ForgeProject`, `ForgeChat`; Core durable package validator; Mission Registry OCI reference/puller; tests/READMEs | Command composition, immutable package/input syntax, source resolution and terminal projection. |
| `forge-client` | Application Projects, Missions, ApplicationComposition, Transport contracts; ClientRuntime session; application/client-runtime tests | Local Project declaration, durable submission/recovery and Hands authority. |
| `forge-conversations` | Conversations Contracts body/launch/ingress/JSON types; Conversation Host API, grain/state/transition/persistence; tests | Versioned durable vocabulary, immutable admission, body ownership and canonical run state. |
| `forge-platform` | ForgeAPI Conversation endpoints/edge wiring and tests | Authenticated ingress projection only. |
| `forge-runner` | Conversation body reader, command processor, generic executor and tests | Hosted materialization and reasoning. |

## Reuse and restrictions

Use `DurableMissionPackageValidator`, OCI puller/reference, Client Application/Transport,
Conversation body intake/Blob store/ingress queue, existing Hands continuation and
`WorkspaceGuard`, and existing conversation SSE. Do not create a service, datastore, route family,
registry credential transfer, CLI policy, sibling-project reference, or compatibility path.

## Implementation sequence

1. Complete each delivery slice in order, releasing its package boundary before downstream work.
2. The Client Application service submits Core's validated immutable package/input manifest; CLI
   composes only. Host compares a retried command byte-for-byte before dispatch.
3. The Runner reads Host-owned bytes and maps the Core manifest to explicit inputs. Client Runtime
   maps `rootN` labels to the canonical declared folders and calls Core `WorkspaceGuard`.
4. The final CLI slice removes `exec`, `--var`, `--mode`, endpoint environment selection and the
   old declaration default, then follows durable events until final text/cancellation.

## Verification

Focused tests cover input grammar, leading-`@` refusal, input-file/dir/symlink/outside-root
rejection, OCI identity, config/login atomicity, body ownership and retry mismatch, staging,
root containment, exactly-once tool-result recovery, stream reconnection, cancellation and
stdout/stderr separation. Run each repository's full tests and final warning-free Native AOT
publish. After merged package/image releases, use an installed binary, saved normal config, real
authentication/registry/provider/Host/Runner and a disposable two-root Project for local and OCI
runs. Observe cross-root writes, final stdout, diagnostic stderr IDs, durable history, negative
containment and removal of legacy commands/flags.

## Assumptions

The current text-oriented body records are generalized to opaque bytes without weakening existing
hash/size checks. The changed packages are published before consumers update and consumers retain
only package dependencies. No open contract/design question remains.
