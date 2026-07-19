# Forge — Deploy Runbook

> **Audience:** anyone (human or agent) shipping a change to the hosted Forge app. This is the
> **single authoritative deploy doc** — every other doc/memory that touches deployment should link
> here instead of re-describing the flow, so the steps don't drift into copies that disagree.
>
> For the *why* (infra design, credential posture, stand-up history), see
> [Phase 38.7 — Hosting & Deployment](../phases/phase-38.7-hosting-deployment.md); this doc is the
> operational how-to that sits on top of it.

## TL;DR — ship a ForgeUI change

1. **Commit + push** to `main` in this repo.
2. **Tag + push a release** to build the image via CI:
   ```bash
   git tag forge-ui-v0.6.0
   git push origin forge-ui-v0.6.0
   ```
3. **Deploy it** — commands live in `forge-infra`, not duplicated here:
   ```bash
   cd /Users/ameerdeen/progs/forge-infra
   make 500-app-deploy-image VERSION=0.6.0
   ```
4. **Verify live** (see [Verify live](#verify-live) below).

The full command set (bump-only, what-if preview, runner image, migration job, firewall access) is
**owned by [`forge-infra/README.md`](https://github.com/katasec/forge-infra/blob/main/README.md)** —
that repo has the Makefile, so its README can't drift from the commands the way a copy in a second
repo would. Read it before deploying; don't rely on a paraphrase here.

## Topology

Two container images, one registry, two Container Apps in one managed environment, fronted by a
custom domain. All in Azure subscription (workforce), region **uaenorth**, resource group
`rg-forge-dev`.

```
 mission-control-language (this repo)          katasec/forge-infra (IaC, layered Bicep + Makefile)
   src/ForgeUI ──► Dockerfile.forgeui             dev/100-base    RG · Log Analytics · ACR · Key Vault · app MI
   src/ForgeMission.Runner ──► Dockerfile.runner  dev/150-ci      passwordless CI identity (GitHub OIDC)
        │                                         dev/200-entra   app registration (manual, no Bicep)
        │  git tag forge-ui-vX.Y.Z                dev/300-data    Postgres Flexible Server + authbilling_db
        │  (CI: OIDC → ACR, native amd64)         dev/400-appenv  Container Apps env  cae-forge-dev
        ▼                                         dev/450-migrate Manual migration job definition
   ACR  crforgeroomsdev.azurecr.io                dev/500-app     ForgeUI + runner Container Apps + domain
     forge-ui:X.Y.Z          ─────────────►  ca-forge-ui-dev    (orchestrator: identity, DB, SignalR, UI)
     forge-runner:X.Y.Z      ─────────────►  ca-forge-runner-dev (stateless mission execution, provider keys)
                                                     │
   Key Vault kv-forgerooms-dev  (only secret store)  │  managed identity id-forge-dev pulls ACR + reads KV
   Postgres  psql-forge-dev                          ▼    (forge_rooms + authbilling_db, same server)
                                            https://forge.katasec.com  (managed TLS, SNI)
```

- **`ca-forge-ui-dev`** — the orchestrator: OIDC identity, Postgres (`forge_rooms`), SignalR
  (`RoomBroadcaster`, in-proc → **single replica**), all UI.
- **`ca-forge-runner-dev`** — stateless mission execution (Phase 39.1). Holds the **provider keys**
  (`MCL_API_KEY`/`ANTHROPIC_API_KEY`/`XAI_API_KEY`); the orchestrator holds none. Internal ingress,
  warm (`minReplicas ≥ 1`).
- **`authbilling_db`** — a second database on the *same* Postgres server (`psql-forge-dev`), a
  separate bounded context for `platform_keys` + `ledger_entries` (Phase 42.6). ForgeUI reads/writes
  it via `ConnectionStrings__AuthBillingConnection`, wired explicitly in `dev/500-app`'s Bicep — not
  derived from `WriteConnection` anymore.
- **DB migrations are a separate deliberate step**, not coupled to an app deploy: `dev/450-migrate`
  defines the job, `dev/500-app` never runs it automatically. See `forge-infra/README.md`.
- **One live environment today (dev)**, fronted by `forge.katasec.com`. A separate prod slice is a
  documented follow-up (38.7 §9), not yet stood up.

## Which image for which change

| You changed… | Rebuild (this repo) | Deploy (forge-infra) |
|---|---|---|
| `src/ForgeUI` (rooms, nav shell, pages, orchestrator) | `git tag forge-ui-vX.Y.Z && git push origin forge-ui-vX.Y.Z` | `make 500-app-deploy-image VERSION=X.Y.Z` |
| `src/ForgeMission.Runner`, mission execution, provider-key wiring, `missions/` baked into the runner | `git tag forge-runner-vX.Y.Z && git push origin forge-runner-vX.Y.Z` | bump `runnerImage` in `dev/500-app/main.bicepparam`, `make 500-app` |
| Infra (new secret, env var, scaling, domain, Postgres, a new `authbilling_db`-style DB) | — (Bicep only) | the relevant `make <layer>` target — see `forge-infra/README.md`'s layer table |
| An EF migration needs to actually run | image already has `/app/migrate` baked in | `make 450-migrate` (updates the job definition only) then start the job — a separate, deliberate operator action |
| The `forge` **CLI binary** (unrelated to hosting) | `release.yml` (osx/linux/win artifacts) | n/a — not a container |

## Verify live

- **Boot log** confirms wiring: ForgeUI logs `runner advertises N mission(s): …`; the runner logs the
  loaded missions.
  ```bash
  az containerapp logs show -n ca-forge-ui-dev -g rg-forge-dev --tail 50
  ```
  This requires `Microsoft.App/containerApps/*/action` on your Azure identity — not everyone has it by
  default; don't assume a permission denial here generalizes to other Azure APIs (Key Vault reads and
  `az deployment group create` are governed separately and may still work).
- **DB check**, when a migration/schema change is in play — connect to the relevant database directly
  rather than inferring state from Bicep or code:
  ```bash
  make 300-data-operator-ip           # (forge-infra) opens the Postgres firewall to your current IP
  psql "host=psql-forge-dev.postgres.database.azure.com port=5432 dbname=<db> user=forge_admin sslmode=require"
  ```
- **Smoke test** `https://forge.katasec.com`: sign in, open a room, send a `@guard` or `@assistant`
  mention, confirm a verified ✓ reply.

## Local dev environment — shell + provider keys (read this before running anything locally)

> **The maintainer's default shell is PowerShell (`pwsh`), and all provider keys are already exported
> in the pwsh environment.** You do **not** need to ask for keys or set them up — they exist. But there
> is one trap that will silently waste your time:

**The keys live in pwsh, and an agent's `bash` tool does NOT inherit them.** Most agent harnesses run
Bash commands in a `bash` shell seeded from the bash profile — which never sees pwsh's exported vars. So
`echo $XAI_API_KEY` from a Bash tool prints empty, the runner loads **0 missions** ("no API key for
provider … — skipping"), and a local `@grok`/search run can't work — even though the key is right there.

Keys present in the pwsh environment:

| Env var | Used by |
|---|---|
| `XAI_API_KEY` / `GROK_API_KEY` | `@grok` + Scout web search (Phase 41) |
| `OPENAI_API_KEY` | `@openai` / OpenAI-provider missions |
| `CLAUDE_API_KEY` | `@claude` / Anthropic-provider missions (note: the code reads `MCL_API_KEY` per mission `forge.toml`; this is the raw Anthropic key) |
| `MCL_API_KEY` | the default provider key a mission's `forge.toml` resolves via `env("MCL_API_KEY")` |
| `GOOGLE_SEARCH_API_KEY` | Google Programmable Search (future raw-search backend, 41.3) |

**To use a key from a Bash tool, pull it from pwsh** (this loads the pwsh profile, so the exports are
present; the `2>/dev/null | tail -1` drops the profile's Azure-module warning banner):

```bash
export XAI_API_KEY="$(pwsh -NoLogo -Command 'Write-Output $env:XAI_API_KEY' 2>/dev/null | tail -1)"
# then, e.g., boot the runner with a real key so it actually loads the Grok/search mission:
XAI_API_KEY="$XAI_API_KEY" MissionDir="$(pwd)/missions" \
  dotnet run --project src/ForgeMission.Runner/ForgeMission.Runner.csproj
```

(If you're issuing commands *in* pwsh directly, the vars are just there — `$env:XAI_API_KEY` — no export
dance needed. The dance is only for a `bash`-backed tool.)

### pwsh mangles an unquoted leading `@` on native-command arguments

Confirmed 2026-07-19: `forge exec @websearch "..."` fails in pwsh with a confusing "Required argument
missing" from the CLI's own arg parser — pwsh consumes/mangles the bare `@websearch` token before it
reaches the process. Works fine in bash/zsh. Workarounds: quote it (`"@websearch"`) or drop the `@`
entirely (`forge exec websearch "..."` — the CLI strips a leading `@` if present, so both forms are
equivalent; `websearch` is the form documented in `forge exec --help` specifically because pwsh is the
default shell here). Not a forge bug — a native pwsh argument-passing quirk with `@`-prefixed tokens.

## Test before you ship (no prod auth needed)

Verify locally against the browser preview tooling **before** cutting an image. Full loop + gotchas:
[Phase 40 hub §6](../phases/phase-40-forge-ui-shell.md#6-building-running--verifying-locally) and
[UI Design System §9](ui-design-system.md#9-running-it-locally-and-two-gotchas-that-will-bite-you). In
short: `preview_start forge-ui` (config in [`.claude/launch.json`](../../.claude/launch.json), HTTP
`:5286`), dev sign-in `/auth/dev?user=alice`, verify at 375/768/1024 + dark. Real OIDC login needs
HTTPS (`https://localhost:7177`) — only relevant for the PWA install/login test.

## Gotchas

1. **Build ≠ deploy.** Tagging/pushing builds and publishes the image only; you still have to run the
   `forge-infra` deploy step. Forgetting it means "I shipped" but the app still serves the old image —
   this is exactly how `authbilling_db` sat empty in prod for a day after the code merged (2026-07-18/19).
2. **amd64 only.** Container Apps rejects `linux/arm64`. CI runners are amd64 (fine by default); a
   **local** `docker buildx` on Apple Silicon must pass `--platform linux/amd64`.
3. **Single replica.** `RoomBroadcaster` SignalR is in-proc, so `ca-forge-ui-dev` runs one replica.
   Scale-out later needs Azure SignalR + a backplane.
4. **Secrets only via Key Vault** (`kv-forgerooms-dev`). No secret value is committed; Bicep uses KV
   references. Passwordless throughout (CI = OIDC federation, runtime = managed identity). The image
   itself carries no runtime secrets — they're injected at run time.
5. **DB migrations never run automatically.** `dev/450-migrate` is a manual-trigger-only job, separate
   from `dev/500-app` — an app deploy alone never touches schema. (An earlier coupled version of this
   caused a dev DB wipe in 2026-07-18; see [Phase 42.6](../phases/phase-42.6-hosted-endpoint-ttfa.md).)
6. **Provider keys live on the runner, not the app.** `@claude`/`@grok`/`@openai` bind only when the
   runner has their key; a mission whose key is empty simply isn't advertised.
7. **Don't infer Azure permissions from one denied call.** A 403 on one API (e.g. ACA log reads) says
   nothing about a different API (Key Vault, `az deployment group create`, role assignment reads) —
   verify each capability by trying it, not by reasoning from another one's error.

## Bicep authoring gotchas (forge-infra)

Hard-won errors from standing up `forge-infra`'s Bicep layers — not deploy-flow issues, but ones
that'll burn an hour if hit cold while editing any layer.

1. **BCP258.** A `.bicepparam` must assign every required param; you can't supplement it with
   `-p key=val` on the command line. Use `readEnvironmentVariable('VAR')` inside the param file and
   export the var at deploy time — keeps IDs/secrets out of the repo without a hybrid param source.
2. **`enablePurgeProtection` rejects `false`.** Key Vault's Bicep resource errors if you pass it
   literally — emit `true` or omit the property entirely (`condition ? true : null`).
3. **Key Vault names are global**, not scoped to your subscription — `kv-forge-dev` was already
   taken by someone else, hence `kv-forgerooms-dev`.
4. **Contributor's role-definition GUID is `b24988ac-6180-42a0-ab88-20f7382dd24c`.** Don't hardcode
   a remembered role ID for any built-in role — confirm via `az role definition list --name "<Role>"`
   first; a wrong GUID fails silently different ways depending on the API.
5. **Concurrent federated-credential writes on one managed identity are rejected.** If a Bicep
   template creates more than one `federatedIdentityCredentials` child under the same identity,
   chain them with `dependsOn` — parallel creation 409s.

## Reference

- **Deploy commands (source of truth):** `forge-infra/README.md` — Makefile targets, layer list,
  image-update recipe.
- Infra design, credential posture, full stand-up history + decision log →
  [Phase 38.7 — Hosting & Deployment](../phases/phase-38.7-hosting-deployment.md).
- Current authbilling_db / hosted-`/v1` work → [Phase 42.6](../phases/phase-42.6-hosted-endpoint-ttfa.md).
- Observability / OTel exporter follow-up → [Observability](observability.md).
- IaC repo: `katasec/forge-infra` (layered Bicep + Makefile; `.github/workflows/infra.yml` for PR
  validation + selected manual deploys).
