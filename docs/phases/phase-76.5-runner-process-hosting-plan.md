# Phase 76.5 — Implementer hosting plan

**Status: PLAN APPROVED** 2026-10-10 00:06:49 UTC after fresh full simplicity11 and ownership18
PASS and supervisor independent scope/security/failure/dependency/default review. Complete read-only plan returned by the same implementer,
2026-10-10 00:00:19–00:03:08 UTC. No product file/branch changed. Supervisor transcription
preserves the submitted scope, sequence and verification. [Locked design](phase-76.5-runner-process-hosting.md).
Explicit approval covers only the six-file plan below; merge/publication/deployment/default gates remain required.

## 1. Files

Runner `/Users/ameerdeen/progs/forge-runner` clean main
`95d677cb6b74f34f1911af5d3866c8ff5e484688`; infra `/Users/ameerdeen/progs/forge-infra` clean main
`7d849c92b035866061418daf779a2be8fde29e96`.

| Repository / file | Change |
|---|---|
| Runner `Dockerfile.runner` | Add apt tini; fixed `["/usr/bin/tini", "--", "dotnet", "ForgeMission.Runner.dll"]` entrypoint; preserve other behavior |
| Runner `.github/workflows/forge-runner-image.yml` | Native amd64/arm64 premerge build/test/image proof without publication/Azure; require it before normal merged-main publication; retain registries/architectures/tags |
| Runner `scripts/verify-process-hosting.py` — new | Small Python-stdlib Docker verification, image/process facts, unique-container cleanup; `python3 scripts/verify-process-hosting.py <image> <evidence-directory>` |
| Runner `README.md` | Fixed init ownership, normal verification/publication and retained evidence |
| Runner `src/ForgeMission.Runner/README.md` | Separate container init, execution and durable Host ownership |
| Infra `dev/500-app/main.bicepparam` | Only runnerImage0.20.5 → independently verified new version |

No Core, Runner application/contracts/package, consumer, permission, identity, queue, provider or
deployment-template change. Preserve all four uncommitted Core corrections/HOLD. UI/reference,
responsive/theme/browser layout gates N/A: internal process hosting only.

## 2. Reuse

| Need | Existing equivalent / choice |
|---|---|
| Container lifecycle | Distribution tini through existing apt stage; no supervisor/shell wrapper/subreaper/signal-handler replacement |
| Build/publication | Existing image workflow, Dockerfile, BuildKit restore secret, SOURCE_REVISION, ACR/GHCR and both architectures |
| Managed verification | Existing src/ForgeMission.Runner.slnx and Runner.Tests; no new project |
| Container observation | Docker CLI, runtime's existing Python, /proc and /health; no HTTP test server/provider/image dependency |
| Maintained probe | No existing hosting probe; one bounded stdlib script owns Docker observations/evidence; never copied into image |
| Deployment | Existing pin and make500-app-what-if / make500-app; no wrapper/direct resource mutation |
| Provenance | Existing source build argument; same OCI revision label on verify/publish; local image ID and published index/platform digests |
| Independent publication | Image-only workflow_dispatch from merged main; no forge-runner-v tag that also triggers unrelated NuGet publication |

Init forwards only to direct .NET child, no -g. Host durable/Runner ephemeral and all authority,
public/API contracts and credentials remain unchanged.

## 3. Sequence

1. After approval, recheck clean intended bases; isolated Runner task branch, infra's nearest
   AGENTS-required codex/ branch. Do not change unfinished Core branch.
2. Apply package/entrypoint and concise owner documentation.
3. Extend existing image workflow:
   - pull_request paths: Dockerfile, image workflow, hosting script, relevant READMEs.
   - Native verification matrix ubuntu-latest/linux-amd64 and ubuntu-24.04-arm/linux-arm64.
   - Verification contents:read/packages:read only; no Azure environment, id-token, registry login or push.
   - Normal restore/build/tests, actual Dockerfile local --load build with matrix platform,
     source revision and BuildKit secret; probe; upload evidence even on failure.
   - Existing publication job depends on both legs. Dispatch publishes only refs/heads/main;
     existing tag route proves source commit belongs to origin/main before auth/publication.
   - Write permissions only publication job; retain AzureOIDC/ACR/GHCR/multiarch build.
     Record both registries' index and platform manifests.
4. New probe: finite subprocess timeouts, fixed readiness/process deadlines, visible nonzero
   failures; finally removes only unique owned containers. Cases:
   - Normal image entrypoint, no --init/replacement/runtime credentials/provider override.
     Inspect exact entrypoint, /proc/1/comm=tini and direct dotnet child running Runner DLL;
     existing internal HTTP /health ready.
   - Controlled Python docker-exec child forks an orphan, records adoption by PID1, closes
     inherited standard handles. Terminate that test child, observe process entry disappear
     within fixed deadline and delayed sentinel absent. Init proof, independent of Core.
   - docker kill --signal=TERM: normal .NET child logs graceful shutdown, container bounded
     termination and observed exit propagation.
   - Labelled controlled component: installed tini with Python direct child exits37; container37.
   - Labelled negative: same fixed entrypoint, empty tmpfs over /usr/bin; startup fails naming
     /usr/bin/tini, no running container/PID or .NET fallback.
5. Stabilize local managed verification/script syntax; Docker absent locally is not a substitute
   for required native PR image proof.
6. Commit/push Runner, create attached draft PR; independent full code reviews and both image
   legs before supervisor merge.
7. Recheck exact version absence immediately before publication. Latest observed tag/ACR/GHCR
   0.20.5; 0.20.6 is only a candidate, not reserved.
8. Supervisor dispatches existing workflow from merged main:
   `gh workflow run forge-runner-image.yml --repo katasec/forge-runner --ref main -f version=0.20.6`.
   Merged-source verification precedes publication. Record workflow source/run, revision label,
   ACR/GHCR index plus both platform digests. No NuGet release.
9. After publication proof, update only infra Runner pin; normal validation, attached PR, review,
   merge. From clean merged main supervisor reviews `make 500-app-what-if` then runs
   `make 500-app`; no raw deployment/wrapper.
10. Supervisor deployed image/process + installed default plain Chat; record evidence/timing.
    Hosting closes independently. Subsequent Core guard/removal/probe needs its own revised plan.

## 4. Verification

| Gate | Route / required observation |
|---|---|
| Restore | `dotnet restore src/ForgeMission.Runner.slnx --configfile nuget.config` |
| Full managed build | `dotnet build src/ForgeMission.Runner.slnx -c Release --no-restore -warnaserror`; zero warnings/errors |
| Full tests | `env -u MCL_API_KEY dotnet test src/ForgeMission.Runner.Tests/ForgeMission.Runner.Tests.csproj -c Release --no-build -warnaserror`; unfiltered, no new skips/exclusions |
| Syntax | `python3 -m py_compile scripts/verify-process-hosting.py` |
| Premerge build | Native hosts: `docker buildx build --load --platform linux/<architecture> --secret id=nuget_token,env=NUGET_AUTH_TOKEN --build-arg SOURCE_REVISION=<checkout-sha> --label org.opencontainers.image.revision=<checkout-sha> -f Dockerfile.runner -t forge-runner:verify-<sha>-<architecture> .` |
| Actual hosting | Probe command above on both architectures; exact image ID/platform/tini version/entrypoint/PID1/child/health/signal/exit/orphan/sentinel facts |
| Failure containment | Missing init fails/no fallback; exit37; all deadlines visible; cleanup only owned containers |
| CI authority | No PR Azure login/OIDC/environment/push; normal publication after both legs, merged-main only |
| Native AOT | N/A: unchanged JIT Runner image/application; Core canonical/four-host gates remain open |
| Published artifact | Normal merged-main route; exact OCI revision/index/amd64/arm64 digests in both registries; no branch candidate published |
| Infra | Existing infra-validate PR compiles Bicep/params; only runnerImage changes |
| Deployment | Reviewed successful make500-app-what-if then make500-app from merged main; ready new revision/pin |
| Live process | Read-only az containerapp show for image/revision; exec cat /proc/1/comm then direct child name/cmdline; no env/secret reads |
| Installed default | Installed forge --version; dedicated disposable folder/project create then real piped plain Chat; saved login/normal ForgeAPI, FORGE_API_ENDPOINT/RELEASE_TAG/RID/CLI_OUTPUT absent; Project/conversation IDs, actual reply/terminal completion |

Supervisor also checks FORGE_PLATFORM_ENDPOINT absent. Prepared acceptance script
`/private/tmp/phase76-hosting-default-accept.py` syntax PASS, **not run**; asserts actual answer body
equals unique nonce (not echoed request) and records installed CLI hash, version, Project and exits.
Source/platform image provenance is separate required evidence. Controlled probe cases never
substitute for the installed/deployed default. Hosting does not establish generic Core execution.

## 5. Principles that changed a decision

| Implementer rule | Concrete choice |
|---|---|
| 1 No NIH | apt tini/Docker/existing Python/workflow |
| 2 One path | Fixed entrypoint, single probe for PR/publication |
| 3 Minimum | Six paths; no protocol/application/Core/package changes |
| 4 No speculative abstraction | Stdlib script, no framework/host project |
| 5 Scope | Core on hold, independent hosting closure |
| 6 Verified means done | Native-container/published/deployed/plain-Chat observations |
| 7 Outline | Verify/publish/lifecycle flow first |
| 8 Small functions | Named bounded start/observe/shutdown/cleanup steps |
| 9 Top-down | Command/main scenarios before private Docker helpers |
| 10 Explicit errors | Failures nonzero, no fallback |
| 11 Shallow | Sequential scenarios/early assertions |
| 12 Side effects | Named Docker/evidence functions |
| 13 Warnings | Full normal build/test; AOT explicitly N/A |
| 14 Extraction | Probe is image-verification boundary, outside payload |
| 15 Complexity | Cohesive bounded scenarios, no generic framework |

## 6. Open questions and assumptions

No unresolved hosting design question. Docker unavailable locally; both native CI probes required.
Version candidate rechecked immediately before publication. Controlled cases prove their named
layer only. Supervisor implementation handoff follows this approval; no widening is authorized.
