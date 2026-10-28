# Phase 76 — Verified observations

These observations close their named checks only. The parent phase and installed unified CLI
acceptance remain open.

## Durable run increment

**COMPLETE (2026-10-28).** forge-mcl [PR 82](https://github.com/katasec/forge-mcl/pull/82),
merge `8cfd7955d6ccf43e1a2c16d3c45d8427f2a5417e`, added `forge run --project <file> <mission> <prompt>`.
It moves `Katasec.Forge.Client` to 0.10.1 (and `Conversations.Contracts` to 0.12.0), prepares the
package, creates a mission conversation, submits one named-input turn and prints the final reply.
The run is admitted NoHands. The turn carries no workspace body: the edge converter
(`ConversationIngressBodies.SubmitMissionTurn`) and the Host reject one.

| Check | Observation |
|---|---|
| Live cloud run | `missions/elevator-pitch` with the `fp-sample1` project: exit 0, real reply, conversation `9eae38e6-1539-5c37-8d00-95dfbee4705e` |
| Stored after exit | Separate `POST /api/GetConversation`: `completed`, `missionConversation`, project `ad427dd9-…`, `lastSequence` 6, mission `QuickPitch` |
| CI | All four platform builds and "Verify package and macOS ARM64 CLI" passed on the merged head |
| Fixed on the way | `TuiColourLiteralTests` read the deleted `ForgeConfig.cs` (replaced by `Services/ForgeSettings.cs` in `bef8f7b`), so it failed on `main`; the PR repoints it |

## Hosted catalog CLI cloud execution

**COMPLETE (2026-10-11).** forge-mcl
[PR 79](https://github.com/katasec/forge-mcl/pull/79), merge
`bcef529950b881f94426b5008eccdb86cebe00de`, changed
`forge run <handle> <prompt>` to use the configured `api.endpoint` and existing authenticated
`ExecuteMission` request/response path. The hosted API remains the endpoint default and the saved
platform key remains the credential source.

All required PR checks passed, including the four native build hosts and macOS ARM64 CLI package
verification. Before merge, a real `dotnet run ... vanilla` call returned the exact expected
sentinel. This closes the bounded hosted-catalog CLI implementation and functional check.

It does not close installed-artifact default-path acceptance or the separate unified scope for
arbitrary local/OCI mission packages, named file inputs and durable Hands execution. Those remain
open in the active phase.

## Real cloud baseline

On2026-10-11, the supervisor ran a temporary managed service-path probe using actual published
Core0.1.8 and Conversations.Contracts0.12.0. This is a lower-layer cloud observation, not installed
CLI acceptance. Saved platform login, no `FORGE_API_ENDPOINT`, normal
`https://api.forge.katasec.com/`; no provider override or fake provider.

| Fact | Observation |
|---|---|
| Safe starting state | Dedicated Project `1543461d-d914-4850-bdae-d0c02035cf51` |
| Mission | Real `CloudBaseline` → `Researcher` LLM expert; immutable package and normal hosted reasoning |
| Admission | CreateMissionConversation HTTP201; SubmitMissionTurn HTTP202 |
| Identity | Conversation `0abd6524-552f-5329-a6da-82902c2f6e3c`; turn `e81752c7-6adc-5ee6-b731-3b55e41cf12f`; attempt `36c013a6-1b38-598f-bea5-3db97b5796a5` |
| Durable result | GetConversation HTTP200, Completed, sequence6; replay contains step output and final output exactly `CLOUD_VERIFIED_OK_d87a557e8d`, followed by Completed |
| Probe result | Exit0: real default-cloud mission completed with exact durable output |
| Local evidence | `/tmp/phase76-live-cloud-verified-20261011T124400Z`: source, csproj, request replies, events.json, expected.txt and live-cloud.log |

Fresh `az containerapp list -g rg-forge-dev` observed ready images/revisions: Host0.10.2/rev17,
Runner0.21.0/rev47, API0.8.0/rev14, UI0.7.0/rev24 and Billing0.1.2/rev2. No deployment was
performed for this probe. Binary continuation, multiple roots, opaque final output and installed
unified `forge run` are not established by this observation.

## Client preparation delivery

The operator explicitly approved adding forge-client to the private MissionRegistry package's
Actions access. GitHub Settings confirmed the grant and `forge-client` Role: Read; package stayed
private. PR15 CI38137449623 attempt2 passed. PR15 merged at
`f8ed16fd763f044d14ca4b7225ac2d17434f4a75` on2026-10-11T12:37:48Z.

Normal main publisher run38139995534 passed restore/build/tests, pack/audit, immutable guard and
package push. Its final metadata check failed; that workflow is not fully green. Independent
authenticated GitHub metadata confirms exactly one Client0.10.1, version1369010166, private,
associated with `katasec/forge-client`, created2026-10-11T12:38:56Z. Repair the guard separately;
do not rerun publication of this immutable version.

Actual downloaded package nuspec records the merged commit above and dependencies Client.Contracts
0.2.1, Hands0.1.0, Conversations.Contracts0.12.0, Core0.1.8 and MissionRegistry0.1.1. Its
ForgeMission.Application.dll SHA256 is
`9d0323a3d78f2409e5465f9020cb5a76c3cc458a8784a74106e62dfbf8d3837c`.
Local binary evidence: `/tmp/phase76-client-published-0.10.1/remote`.

## Resolved local Core cache

The local Core0.1.8 cache came from an earlier scratch feed and contained an obsolete DLL
(`222d392ea13e8972a6ae9e0d799b1f38c2574ff4ae834e80c24109d21fe8acdc`) and nuspec commit03dd4dbf.
Normal restore exposed missing existing APIs. The supervisor independently downloaded the real
GitHub package: DLL SHA256
`c448f95e04af78673dfbed40196d45ef57874e52c57c44efa7a74be13c9877a5`, nuspec commit
`1ecef9aee2b6a1667c558711ea4904037d8f05c9`.

Only the stale version directory was backed up and removed. Normal solution restore using
`nuget.config --force --no-cache` then restored the identical real DLL from GitHub. No dependency
upgrade, source patch, alternate feed or DLL replacement was used. Producer Release build then
passed with zero warnings/errors and focused146/146 tests passed. Evidence and backup:
`/tmp/phase76-real-core-package-audit-20261011`.
