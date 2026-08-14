# Phase 43.16 Task 8a — Kind runtime build-out

> **Status: Implementation in progress (2026-08-14).** Prerequisite to
> [Task 8's live product proof](phase-43.16-janus-desktop-local-poc.md#8-product-proof-and-evidence):
> builds the real `ForgeMission.ConversationHost`/`ForgeMission.ConversationWorker` container
> images and rolls them out into the `forge-durable` Kind cluster with immutable, commit-SHA-derived
> provenance. Discovered as a blocking gap during Task 8 planning — `forge-infra`'s
> `kind/conversation-host.yaml`/`kind/mission-worker.yaml` were still explicit `image: TBD`
> placeholders, and `kind-up.sh`'s own comments confirmed this was deliberately deferred until the
> application images existed. This is genuine application/IaC build-out, kept explicitly separate
> from Task 8's evidence-only live run.

## Locked decisions

1. **Image provenance is immutable.** `forge-infra/dev/350-conversation-data/scripts/kind-up.sh`
   resolves the sibling `mission-control-language` checkout only via the documented relative
   sibling layout (`$LAYER_DIR/../../../mission-control-language`) — no environment-variable
   override. It requires that checkout to be on `main` with a clean working tree, aborting with a
   clear message otherwise. Both images are tagged with that exact commit SHA
   (`forge-conversation-host:<sha>` / `forge-conversation-worker:<sha>`), never a mutable tag.
2. **`imagePullPolicy: Never`** on both Deployments — the only way an image reaches a pod is
   `kind load docker-image`, run by the same script that resolved the SHA.
3. **Apply, then set image, then prove rollout.** The committed manifests carry a non-functional
   placeholder image string (`:unset`) and are applied for structure only (env, probes,
   resources, the Service). `kubectl set image` then pins the exact SHA-tagged image, and `kubectl
   rollout status` is the actual readiness proof — re-applying a manifest whose image string never
   changes would not by itself restart a pod already running an older build under the same mutable
   tag, which is exactly the gap this sequencing closes.
4. **Credentials fail closed, are never printed, and never appear in a manifest, Bicep file, or
   command argument.** `Dockerfile.conversationworker`'s NuGet-restore step uses
   `--mount=type=secret,id=nuget_token,required=true`, so the build fails outright — not silently —
   if the secret wasn't supplied. `kind-up.sh` additionally checks `NUGET_AUTH_TOKEN` and, before
   writing the new provider-key Secret, both `MCL_API_KEY`/`ANTHROPIC_API_KEY`, aborting with a
   clear (non-value-revealing) message if any is unset. The Secret write itself reuses the script's
   existing `printf`-into-`kubectl create secret --from-env-file=/dev/stdin` discipline — the shell
   builtin never forks, so nothing appears in `ps`, and the value is never echoed.
5. **Exact scoped configuration, matching the already-locked `ConversationStorage__*`/
   `ConversationServiceBus__*` contract** (`ConversationStorageOptions.cs`/
   `ConversationServiceBusOptions.cs`), not the stale `ConnectionStrings__*` shape the old
   placeholder manifests carried:
   - `conversation-host`: `ConversationStorage__ConnectionString`,
     `ConversationServiceBus__MissionCommandSendConnectionString`,
     `ConversationServiceBus__ProgressListenConnectionString` — from the existing
     `forge-conversation-service-cloud` Secret. `GET /health` readiness + liveness probes; one
     ClusterIP `Service` (`conversation-host:8080`).
   - `mission-worker`: `ConversationServiceBus__MissionCommandListenConnectionString`,
     `ConversationServiceBus__ProgressSendConnectionString` — from the existing
     `forge-conversation-worker-cloud` Secret; `ConversationWorker__JanusMissionDirectory`
     (baked into the image at `/app/missions/janus`); `MCL_API_KEY`/`ANTHROPIC_API_KEY` — from the
     new `forge-conversation-worker-provider-keys` Secret. No Service, no probes, no ingress —
     readiness is `kubectl rollout status` plus the Worker's own consumer-start log line, not an
     invented HTTP endpoint.
6. **No 525, no Bicep, no Azure resource change.** Only `make 350-conversation-data-what-if`
   (unchanged) and `make 350-conversation-kind-up`/`-kind-status`/`-kind-down` (extended) are used.

## Files

- `mission-control-language` (this repo):
  - `Dockerfile.conversationhost` (new) — no `NUGET_AUTH_TOKEN` needed; ConversationHost
    references only `ForgeMission.Conversations.Contracts`.
  - `Dockerfile.conversationworker` (new) — requires `NUGET_AUTH_TOKEN` (Core pulls the private
    `Katasec.AITools` package) via a required BuildKit secret mount; bakes in
    `missions/janus/`.
  - `src/ForgeMission.ConversationWorker/Messaging/AzureServiceBusMissionCommandConsumer.cs` — one
    added `LogInformation` line confirming both the mission-command and dead-letter processors
    started, immediately after `StartProcessingAsync` succeeds for both.
  - `docs/phases/phase-43.16-janus-desktop-local-poc.md` — a pointer to this doc under Task 8.
- `forge-infra`:
  - `dev/350-conversation-data/kind/conversation-host.yaml` — real Deployment + Service.
  - `dev/350-conversation-data/kind/mission-worker.yaml` — real Deployment.
  - `dev/350-conversation-data/scripts/kind-up.sh` — sibling resolution, clean-checkout/SHA
    provenance, image build/load, provider-key Secret, apply → set-image → rollout-status.
  - `dev/350-conversation-data/scripts/kind-status.sh` — reports the new Secret by name.
  - `dev/350-conversation-data/README.md` — drops the stale placeholder language; documents the
    new "Application images" contract.

## Verification

### App-repo verification (this repo, before merge)

- `dotnet build src/ForgeMission.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.slnx --no-restore`: 688 total, 677 passed, 11 pre-existing skips
  (issue #7, unrelated), 0 failed — identical counts to the pre-Task-8a baseline; only Dockerfiles
  and one log line changed, no test surface touched.
- **Negative case — `Dockerfile.conversationworker` fails closed without `NUGET_AUTH_TOKEN`:**
  ```
  ERROR: failed to build: failed to solve: secret nuget_token: not found
  ```
  confirmed via `DOCKER_BUILDKIT=1 docker build -f Dockerfile.conversationworker ...` with no
  `--secret` supplied — the build never reaches the NuGet restore step.
- **Positive builds** — both images built successfully with `NUGET_AUTH_TOKEN` pulled through the
  documented pwsh pattern:
  - `docker build -f Dockerfile.conversationhost -t forge-conversation-host:local .` — succeeded.
  - `DOCKER_BUILDKIT=1 docker build --secret id=nuget_token,env=NUGET_AUTH_TOKEN -f
    Dockerfile.conversationworker -t forge-conversation-worker:local .` — succeeded, including a
    real private-package restore of `Katasec.AITools` from the GitHub Packages feed.
  - Both verification-only `:local` tags were removed afterward; `kind-up.sh` builds its own
    SHA-tagged images independently.

### Kind rollout verification (forge-infra, after this merges to `main`)

Recorded separately in `forge-infra`'s Task 8a PR/commit, since `kind-up.sh`'s clean-`main`-
checkout requirement means the real rollout can only be exercised once these changes are live on
`mission-control-language`'s `main` — see that PR for the resolved commit SHA, `kubectl rollout
status` output for both Deployments, the Worker's consumer-start log line, the `/health` check,
and `kind-status.sh`'s secret-name-only report. This document is updated with a pointer to that
evidence once available.

## Done when

- Both Dockerfiles build; the Worker build fails closed (verified) without `NUGET_AUTH_TOKEN`.
- `make 350-conversation-kind-up` succeeds end to end against a clean `main` checkout of this
  repo: SHA resolved and logged; both images built/loaded; the existing verifier Jobs still pass;
  the provider-key Secret written only after both keys are confirmed non-empty; both Deployments
  `kubectl rollout status ... successfully rolled out`; the Worker's new log line present in
  `kubectl logs`; `/health` returns 200 on `conversation-host`.
- `make 350-conversation-kind-status` lists the new provider-key Secret by name, no values
  anywhere in captured output.
- Both repos committed, pushed, PR'd, and merged to `main`.
