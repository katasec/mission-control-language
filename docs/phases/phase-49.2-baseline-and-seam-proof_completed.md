# Phase 49.2 — Baseline and seam proof: evidence

> **Status:** Accepted 2026-09-22 after independent final review PASS. This record is concise and
> secret-free. Raw logs, binlogs, TRX, and publish output remain in
> `/private/tmp/phase49-source-baseline-20260922-035711`, outside Git.

## Baseline identity and toolchain

| Fact | Observation |
|---|---|
| Baseline commit | Detached clean worktree at `99eb355b1b896dd4b0b9f9c76f9060421f32b26a` (merged PR #168). |
| Worktree cleanliness | Empty porcelain status before and after observation. |
| Host/toolchain | macOS 27.0 (26A428), .NET SDK 10.0.401, PowerShell 7.6.6; `maui-maccatalyst` 10.0.20/10.0.100 installed. |

## Source and dependency graph

| Fact | Observation |
|---|---|
| Source and solution membership | 35 `src` projects; 32 `.slnx` members. |
| Evaluated direct project edges | 78 evaluated direct `ProjectReference` edges across every source project; zero MSBuild evaluation failures. Machine-readable capture is retained outside Git. |
| Package and Docker closures | 83 evaluated package edges across 58 distinct packages; 6 Dockerfiles. Hosted Docker contexts copy `src`; Runner/UI also copy `missions`. |
| Executable/AOT roots | Managed AOT roots are CLI, Application Host, and Desktop Supervisor. Desktop Host is a MAUI packaging stage, not a fourth managed AOT root. |

## Local verification and timings

| Observation | First publish / run | Immediate repeat | Exit / warnings |
|---|---:|---:|---|
| Full build | 26.417s | N/A | `dotnet build src/ForgeMission.slnx` exit 1: MSB9008 for missing local `Katasec.AnthropicServer`/`Katasec.OaiServer` project references, then 19 `ForgeMission.Tests` compile errors (1 warning). |
| Full test | 36.626s | N/A | Aggregate exit 1 at the same missing local-project and resulting `Katasec.AnthropicServer`/`Katasec.OaiServer` compile fallout. Four completed suites passed 301/301: ConversationWorker 18, Runner 6, Rooms 97, ConversationHost 180. |
| CLI AOT | 367.744s / 166,899,336 B | 1.714s / 166,899,336 B | PASS/PASS; 6/0 warning-token matches. First executable SHA-256 `9290931650cac88f638c991781a1f614e9ad5b855d24de2939b3c8df5eeeb97e`. |
| Application Host AOT | 53.519s / 87,084,685 B | 4.483s / 87,084,685 B | PASS/PASS; 5/0 warning-token matches. First standalone RID restore failed NU1102 for `Microsoft.NETCore.App.Runtime.Mono.osx-arm64` 10.0.12; following publish restore passed. |
| Desktop Supervisor AOT | 6.387s / 44,530,904 B | 1.356s / 44,530,904 B | PASS/PASS; 5/0 warning-token matches. |
| MAUI workload/publish | workload restore 1.614s; publish 35.546s / 18,710,699 B | publish 5.402s / 18,710,688 B | Direct Desktop Host publish PASS/PASS, no warnings; first `.pkg` SHA-256 `f14085d4d547e11c322bc63e6052f033a6c5f547ac578eb34b62daeb5ff8905f`. |
| `make desktop-publish` | Unavailable | N/A | Not run: it unconditionally runs workload restore and deletes `dist`, so it is not a non-mutating baseline observation. |

The macOS AOT warning records are linker compatibility warnings (`-ld_classic` and Homebrew
openssl/brotli deployment targets), not new Phase 49 warnings. `dotnet workload restore
--skip-manifest-update` reported no manifest update but still wrote workload-install records and
garbage-collected feature bands; this environmental side effect is recorded rather than described
as read-only. No repository source/configuration changed.

## GitHub, packages, and delivery

| Fact | Observation |
|---|---|
| Repository and workflow posture | `katasec/mission-control-language` is **public**, default `main`; Actions enabled, all actions permitted, no SHA pinning. No PR-validation workflow exists. |
| Latest relevant workflow evidence | Desktop run `35663967953` at checkpoint `06e11af` succeeded: macOS ARM64 publish 3m13s, Windows ARM64 6m48s; artifacts were `forge-desktop-osx-arm64` (55,899,170 B) and `forge-desktop-win-arm64` (152,908,084 B). Latest Runner/UI/API image runs succeeded at versions 0.11.5/0.6.1/0.3.1. Release `29735093610` had successful three-leg AOT builds but failed its Docker job. Latest published CLI release: v0.9.0. |
| Remote rollback tag chain | `checkpoint-pre-repo-split-2026-09-22` remote annotated object `942686f610a029e482baa5334336aa29b3a86338` peels to `06e11af30b6eb17d2a3c32e1e5b883bbe42c0d51`; GitHub tag/commit APIs agreed, and `main` contains it. |
| Referenced NuGet inventory and access | Current `Katasec.AITools` 0.1.8, `Katasec.OciClient` 0.1.0–0.3.0, `Katasec.OaiServer` 0.1.0–0.1.8, and `Katasec.AnthropicServer` 0.1.3–0.1.8 are **public**, not private. Package repository-access endpoints returned 404; that is not an access grant. New Phase 49 packages remain private with explicit grants. |
| Actions permissions/environments | Only `forge-ui-image` exists, with no protection rules and administrator bypass. Variables were observed by name only; secret-name API returned empty. Legacy branch/tag protection endpoints returned 404; these are endpoint observations only. |

## Infrastructure and harmless route observation

| Fact | Observation |
|---|---|
| `forge-infra` source baseline | Clean `main...origin/main` at `552cb13febadd0534859105f91a65ebc58ff2644`. Deployment layers and manual migration job are separate. Conversation parameters use `:pending` and fail closed. |
| Azure identity/OIDC/ACR | Read APIs succeeded for observed subscription/RG, ACR, identity, FIC, RBAC, repositories/tags, and Container Apps. `id-forge-ci-dev` alone has FICs for the current MCL `forge-ui-image` environment and `forge-infra` main, plus Contributor/RBAC Admin/AcrPush. App/conversation identities have AcrPull. Each future image-owning repo needs an explicit governed FIC before ACR push. |
| Active deployed revision/image/ingress | `ca-forge-ui-dev` rev 0000023, external `forge-ui:0.6.1`, custom `forge.katasec.com`; `ca-forge-runner-dev` rev 0000027, internal `forge-runner:0.11.5`; `ca-forge-api-dev` rev 0000007, external `forge-api:0.3.1`, custom `api.forge.katasec.com`. All were Running/Healthy, scaled to zero, 100% latest traffic. Migration job is manual, provisioned, `forge-ui:0.5.0`, not started. |
| Public route header observation | Availability-only `curl --head --location` to `https://forge.katasec.com` timed out (exit 28, HTTP 000, no bytes). This does not imply an ACR, identity, ACA, or product-path failure. |
| Unavailable capabilities | Exact Make target intentionally unobserved as above. Application logs not queried because they may contain user/runtime data and were unnecessary. No Azure API permission denial was observed. |

## Supervisor acceptance

**Accepted.** Independent final review cross-checked raw summaries, the 78-edge table, timings,
commands, secret scan, workload exception, and D49-01 fence. This evidence does not authorize
extraction. The observed full-suite dependency failure remains a compatibility constraint; D49-01
must be resolved before the first seam card; and package design must account for the
public-current/private-target fact.
