# Forge — Deploy Runbook

> **Audience:** anyone (human or agent) shipping a change to the hosted Forge app. This is the
> **operational how-to**. For the *why* (infra design, credential posture, the decision log of how it
> was stood up), see [Phase 38.7 — Hosting & Deployment](../phases/phase-38.7-hosting-deployment.md);
> this doc is the runbook that sits on top of it.

## TL;DR — ship a change

1. **Commit + push** to `main`.
2. **Build + push the image** via GitHub Actions (OIDC → ACR, native amd64):
   - ForgeUI change → `gh workflow run forge-ui-image.yml -f version=X.Y.Z`
   - Runner change → `gh workflow run forge-runner-image.yml -f version=X.Y.Z`
   - (or push a git tag `forge-ui-vX.Y.Z` / `forge-runner-vX.Y.Z` — same effect.)
3. **Roll the Container App** onto the new image — **this is a separate step the build does NOT do**
   (see [Gotcha 1](#gotchas)). Preferred: redeploy the `dev/500-app` Bicep layer with the new image
   version. Quick: `az containerapp update` (creates drift — see gotcha).
4. **Verify live** — boot log, FQDN, a real `@mention`.

**For Phase 40 (all ForgeUI, no runner change): only step 2's `forge-ui-image.yml` + step 3's roll of
`ca-forge-ui-dev` are needed.**

## Topology

Two container images, one registry, two Container Apps in one managed environment, fronted by a custom
domain. All in Azure subscription (workforce), region **uaenorth**, resource group `rg-forge-dev`.

```
 mission-control-language (this repo)          katasec/forge-infra (IaC)
   src/ForgeUI ──► Dockerfile.forgeui             dev/100-base   RG · Log Analytics · ACR · Key Vault · app MI
   src/ForgeMission.Runner ──► Dockerfile.runner  dev/150-ci     passwordless CI identity (GitHub OIDC)
        │                                         dev/300-data   Postgres Flexible Server → conn strings to KV
        │  gh workflow run *-image.yml            dev/400-appenv Container Apps env  cae-forge-dev
        ▼  (OIDC → ACR, amd64)                    dev/500-app    the Container Apps + EF migration job + domain
   ACR  crforgeroomsdev.azurecr.io
     forge-ui:X.Y.Z          ─────────────►  ca-forge-ui-dev    (orchestrator: identity, DB, SignalR, UI)
     forge-runner:X.Y.Z      ─────────────►  ca-forge-runner-dev (stateless mission execution, provider keys)
                                                     │
   Key Vault kv-forgerooms-dev  (only secret store)  │  managed identity id-forge-dev pulls ACR + reads KV
   Postgres  psql-forge-dev     (Rooms + ledger)     ▼
                                            https://forge.katasec.com  (managed TLS, SNI)
```

- **`ca-forge-ui-dev`** — the orchestrator: OIDC identity, Postgres (Rooms + `ledger_entries`),
  SignalR (`RoomBroadcaster`, in-proc → **single replica**), all UI. **Phase 40 ships here.**
- **`ca-forge-runner-dev`** — stateless mission execution (Phase 39.1). Holds the **provider keys**
  (`MCL_API_KEY`/`ANTHROPIC_API_KEY`/`XAI_API_KEY`); the orchestrator holds none. Internal ingress,
  warm (`minReplicas ≥ 1`). Only rebuild/roll this for a runner or mission-execution change.
- **One live environment today (dev), fronted by `forge.katasec.com`.** A separate prod slice is a
  documented follow-up (38.7 §9), not yet stood up.

## Which image for which change

| You changed… | Rebuild | Roll |
|---|---|---|
| `src/ForgeUI` (rooms, nav shell, pages, orchestrator) — **incl. all of Phase 40** | `forge-ui-image.yml` | `ca-forge-ui-dev` |
| `src/ForgeMission.Runner`, mission execution, provider-key wiring, `missions/` baked into the runner | `forge-runner-image.yml` | `ca-forge-runner-dev` |
| Infra (new secret, env var, scaling, domain, Postgres) | — (Bicep) | redeploy the relevant `dev/*` layer in `katasec/forge-infra` |
| The `forge` **CLI binary** (unrelated to hosting) | `release.yml` (osx/linux/win artifacts) | n/a — not a container |

## The three steps in detail

### 1 — Build + push the image (GitHub Actions)

Both image workflows ([`forge-ui-image.yml`](../../.github/workflows/forge-ui-image.yml),
[`forge-runner-image.yml`](../../.github/workflows/forge-runner-image.yml)) are identical in posture:

- **Trigger:** a git tag (`forge-ui-vX.Y.Z` / `forge-runner-vX.Y.Z`) **or** `workflow_dispatch` with a
  `version` input. Dispatch is the usual path: `gh workflow run forge-ui-image.yml -f version=0.4.0`.
- **What it does:** `buildx` → push to ACR. Azure auth is **GitHub → Azure OIDC federation** (no stored
  cloud secret); ACR push via the CI managed identity's `AcrPush` (no registry password). The private
  GitHub Packages feed token is a **BuildKit secret** (never in an image layer), sourced from
  `GITHUB_TOKEN`.
- **Output tags:** `X.Y.Z` + `sha-<sha>` + `dev-latest` on `crforgeroomsdev.azurecr.io/forge-ui` (or
  `/forge-runner`).
- **Runs native amd64** on the GitHub runner — required (see Gotcha 2) and faster than a local emulated
  build.
- Requires the GitHub Actions **variables** `AZURE_CI_CLIENT_ID`, `AZURE_TENANT_ID`,
  `AZURE_SUBSCRIPTION_ID`, `ACR_NAME`, `ACR_LOGIN_SERVER` (already wired in this repo).

### 2 — Roll the Container App onto the new image

**The image workflow does NOT deploy** — the running app keeps serving the old image until you roll it.
Two ways:

- **Preferred — redeploy the Bicep (keeps IaC the source of truth):** in `katasec/forge-infra`, redeploy
  `dev/500-app` with the new `FORGE_UI_IMAGE=…:X.Y.Z` (and keep `CUSTOM_DOMAIN` +
  `CUSTOM_DOMAIN_CERT_ID` set, so the HTTPS binding is preserved — see Gotcha 3). This is the release
  loop of record.
- **Quick — direct update (creates drift):**
  ```bash
  az containerapp update -n ca-forge-ui-dev -g rg-forge-dev \
    --image crforgeroomsdev.azurecr.io/forge-ui:X.Y.Z
  ```
  Fast, but it bypasses Bicep, so the running image no longer matches `dev/500-app`. If you use this,
  land the same version in the Bicep afterward, or the next `dev/500-app` deploy silently reverts it.
  (This exact class of drift caused the `@claude` "No mission is bound" regression — 38.7 §9.)

### 3 — Verify live

- **Boot log** confirms wiring: ForgeUI logs `runner advertises N mission(s): …`; the runner logs the
  loaded missions. A healthy dev boot loads **5**: `ChatGPT, Forge, Assistant, Claude, Grok`.
  ```bash
  az containerapp logs show -n ca-forge-ui-dev -g rg-forge-dev --tail 50
  ```
  ⚠️ The runner floods stdout with per-`/health` OTel spans (image ≥ 0.4.2) — large `--tail` on
  `ca-forge-runner-dev` may return nothing; use a small tail (38.7 §9).
- **Smoke test** `https://forge.katasec.com`: sign in, open a room, send a `@guard` or `@assistant`
  mention, confirm a verified ✓ reply. That exercises the full app → runner → provider round-trip +
  the cost meter/ledger.

## Test before you ship (no prod auth needed)

Phase 40 is UI work — verify locally against the browser preview tooling **before** cutting an image.
Full loop + gotchas: [Phase 40 hub §6](../phases/phase-40-forge-ui-shell.md#6-building-running--verifying-locally)
and [UI Design System §9](ui-design-system.md#9-running-it-locally-and-two-gotchas-that-will-bite-you).
In short: `preview_start forge-ui` (config in [`.claude/launch.json`](../../.claude/launch.json), HTTP
`:5286`), dev sign-in `/auth/dev?user=alice`, verify at 375/768/1024 + dark. Real OIDC login needs
HTTPS (`https://localhost:7177`) — only relevant for the 40.4 PWA install/login test.

## Gotchas

1. **Build ≠ deploy.** The image workflow builds + pushes only; rolling the Container App is a separate
   step (see step 2). Forgetting it means "I deployed" but the app still serves the old image. An open
   follow-up (38.7 §9) is a `deploy-dev` action that builds *and* rolls in one.
2. **amd64 only.** Container Apps rejects `linux/arm64`. GitHub runners are amd64 (CI is fine); a **local**
   `docker buildx` on Apple Silicon must pass `--platform linux/amd64`.
3. **Preserve the custom-domain cert params on redeploy.** `dev/500-app` takes `CUSTOM_DOMAIN` +
   `CUSTOM_DOMAIN_CERT_ID`; dropping them on a redeploy breaks the HTTPS binding. The managed cert can't
   be created in a single Bicep pass (hostname-first + circular) — it's a one-time out-of-band CLI step
   already done for dev.
   ⚠️ **The `infra.yml` `500-app` dispatch does NOT pass these** — its env block sets only
   `CAE_ENV_ID / ACR_LOGIN_SERVER / FORGE_UI_IMAGE / FORGE_RUNNER_IMAGE / APP_MI_ID / KEY_VAULT_URI /
   OIDC_*`, and `forge-infra` has **no** `CUSTOM_DOMAIN`/`CUSTOM_DOMAIN_CERT_ID` GitHub vars. So a
   `500-app` GHA redeploy would resolve `customDomain=''` → **strip `forge.katasec.com`** (SniEnabled →
   removed). The workflow has never actually been run (no run history). **Until it's fixed** (add the two
   vars + wire them into the 500-app env block), roll ForgeUI via the quick `az containerapp update`
   below — image-only, it preserves the domain/ingress/secrets. Verified 2026-07-12 rolling to 0.3.4.
4. **Rolling ForgeUI in practice (2026-07-12, verified).** `az containerapp update -n ca-forge-ui-dev
   -g rg-forge-dev --subscription 174c6cc1-faef-4e40-91f4-1bef3a703153 --image
   crforgeroomsdev.azurecr.io/forge-ui:X.Y.Z` — creates a new revision (Single mode → 100% traffic),
   custom domain untouched. **The app pins an explicit tag (not `dev-latest`), so it does NOT auto-pull**
   on image push — the roll is always a deliberate step. After rolling, **bump the `FORGE_UI_IMAGE`
   GitHub var** (`gh variable set FORGE_UI_IMAGE -R katasec/forge-infra --body "forge-ui:X.Y.Z"`) so the
   Bicep source-of-truth matches and the next `500-app` deploy won't silently revert it.
   Gotcha: `az account list` may **not list** the workforce sub `174c6cc1…` (it's in a different tenant),
   but `--subscription 174c6cc1…` on any command works once you've `az login`'d to that tenant.
5. **Provider keys live on the runner, not the app** (post-39.1). `@claude`/`@grok`/`@openai` bind only
   when the runner has their key; a mission whose key is empty simply isn't advertised. Keys are in
   `dev/500-app` Bicep on `ca-forge-runner-dev` (folded into IaC 2026-07-09).
6. **Single replica.** `RoomBroadcaster` SignalR is in-proc, so `ca-forge-ui-dev` runs one replica.
   Scale-out later needs Azure SignalR + a backplane.
7. **Secrets only via Key Vault** (`kv-forgerooms-dev`). No secret value is committed; Bicep uses KV
   references. Passwordless throughout (CI = OIDC federation, runtime = managed identity). The image
   itself carries no runtime secrets — they're injected at run time.
8. **DB migrations** run as the `dev/500-app` EF migration job (Dev seeds Alice/Bob/demo rooms;
   **essential built-in agent rows are product data seeded in every env** — 38.7 §6). Phase 40 needs no
   migration (no schema change).

## Reference

- Infra design, credential posture, full stand-up history + decision log →
  [Phase 38.7 — Hosting & Deployment](../phases/phase-38.7-hosting-deployment.md).
- Observability / OTel exporter follow-up → [Observability](observability.md).
- IaC repo: `katasec/forge-infra` (layered Bicep + `.github/workflows/infra.yml`).
