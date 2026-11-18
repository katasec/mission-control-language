# Forge — Deploy Runbook

> **Audience:** anyone (human or agent) shipping a change to the hosted Forge app. This is the
> **single authoritative deploy doc** — every other doc/memory that touches deployment should link
> here instead of re-describing the flow, so the steps don't drift into copies that disagree.
>
> For the *why* (infra design, credential posture, stand-up history), see
> Phase 38.7 — Hosting & Deployment; this doc is the
> operational how-to that sits on top of it.

## TL;DR — ship a hosted-app change

1. **Commit + merge** to `main` in the **owning repo** (table below).
2. **Build the image:** tag + push a release in that repo (CI builds and pushes to ACR), or, for
   the Conversation Host, build locally and push with crane ([gotcha 8](#gotchas)).
3. **Deploy it** — a forge-infra PR (Terraform, gitops):
   - bump the service's tag in `dev/terraform/images.tf` on a `codex/` branch and open a PR;
   - the `terraform plan` PR comment must show only that app changing;
   - merge — the `terraform apply` workflow on `main` rolls it out. Check that run passed.
4. **Verify live** (see [Verify live](#verify-live) below).

The full flow and rules are **owned by
[`forge-infra/README.md`](https://github.com/katasec/forge-infra/blob/main/README.md)** and
`forge-infra/AGENTS.md`. Never deploy with Bicep, `az deployment`, the portal, or a local
`terraform apply`; Bicep and the `make <layer>` targets are retired.

## Topology

All in Azure subscription (workforce), region **uaenorth**, resource group `rg-forge-dev`, one
Container Apps environment (`cae-forge-dev`), one registry
(`crforgeroomsdev.azurecr.io`), one Key Vault (`kv-forgerooms-dev`). Conversations are explained in
[How conversations work](how-conversations-work.md).

| Hosted app | Source repo | `images.tf` key | Terraform file | Replicas | Ingress |
|---|---|---|---|---|---|
| ForgeUI (`ca-forge-ui-dev`): account page, OIDC sign-in, Blazor Server | forge-rooms `src/ForgeUI` | `ui` | `apps.tf` | 0–3 | Public, `forge.katasec.com` |
| forge-runner (`ca-forge-runner-dev`): mission execution, provider keys | forge-runner | `runner` | `apps.tf` | 1–3 (always one, so the queue consumer runs) | Internal |
| ForgeAPI: platform-key edge for the `forge` CLI | forge-platform `src/ForgeMission.Api` | `api` | `apps.tf` | 0–3 | Public, `api.forge.katasec.com` |
| Billing service: keys, ledger, balances (`authbilling_db`) | forge-platform `src/ForgeMission.Billing.Service` | `billing` | `billing.tf` | 1–2 | Internal |
| Conversation Host: conversations (Orleans, Table/Blob) | forge-conversations `src/ForgeMission.ConversationHost` | `conversation_host` | `apps.tf` | 1 | Internal |

| Terraform file (`dev/terraform`) | Holds |
|---|---|
| `data.tf` | Postgres Flexible Server `psql-forge-dev` (`forge_rooms`, `authbilling_db`); connection strings in Key Vault |
| `data.tf`, `messaging.tf`, `base.tf` | Conversation Storage account (Tables `forgeconversationevents` and `forgeconversationindex`, Blob `forgeconversationartifacts`), the internal Service Bus queues `private-mission-command` / `private-conversation-progress`, and the Host identity |
| `messaging.tf`, `base.tf`, `billing.tf` | The financial Service Bus queue `private-run-settlement` (runner → Billing) and the Billing, runner and ForgeAPI identities |
| `messaging.tf` | The edge-facing Service Bus queues `conversation-ingress` / `conversation-reply` |
| `apps.tf` (`migrate`) | Manual migration job definition (never run by an app deploy) |

`dev/terraform` itself is the source of truth; this table is a map, not a copy.

- **DB migrations are a separate deliberate step**, not coupled to an app deploy: the `migrate` job
  in `apps.tf` is manual-trigger only, and no app rollout runs it. See `forge-infra/README.md`.
- **One live environment today (dev)**, used solely by Ameer. ForgeUI and ForgeAPI scale to zero
  (dev-only; revisit before serving other users). The runner does not: one replica always runs,
  because a replica scaled to zero has no queue consumer.

## Which image for which change

| You changed… | Build the image | Deploy (forge-infra) |
|---|---|---|
| forge-rooms `src/ForgeUI` | In forge-rooms: `git tag forge-ui-vX.Y.Z && git push origin forge-ui-vX.Y.Z` (CI) | PR bumping `ui` in `images.tf`, merge |
| forge-runner | In forge-runner: `git tag forge-runner-vX.Y.Z && git push origin forge-runner-vX.Y.Z` (CI; first CI-built release 0.19.0) | PR bumping `runner` in `images.tf`, merge |
| forge-platform `src/ForgeMission.Api` (ForgeAPI) | In forge-platform: `git tag forge-api-vX.Y.Z && git push origin forge-api-vX.Y.Z` (CI) | PR bumping `api` in `images.tf`, merge |
| forge-platform Billing service | In forge-platform: `git tag forge-billing-vX.Y.Z && git push origin forge-billing-vX.Y.Z` (CI) | PR bumping `billing` in `images.tf`, merge |
| forge-conversations Conversation Host | No image CI: build locally (`--platform linux/amd64`) and push with crane ([gotcha 8](#gotchas)) | PR bumping `conversation_host` in `images.tf`, merge |
| Infra (new secret, env var, scaling, domain, a new DB) | — | PR editing the relevant `dev/terraform/*.tf` file, merge — see `forge-infra/README.md` |
| An EF migration needs to actually run | image already has `/app/migrate` baked in | PR bumping `migrate` in `images.tf` (updates the job definition only), merge, then start the job — a separate, deliberate operator action |
| The `forge` **CLI binary** (unrelated to hosting) | forge-mcl [Release CLI workflow](https://github.com/katasec/forge-mcl#cli-releases) produces native platform ZIPs; `make install` remains the local path. | n/a — CLI release, not a hosted container |

## Verify live

- **Boot log** confirms wiring: ForgeUI logs `runner advertises N mission(s): …`; the runner logs the
  loaded missions.
  ```bash
  az containerapp logs show -n ca-forge-ui-dev -g rg-forge-dev --tail 50
  ```
  This requires `Microsoft.App/containerApps/*/action` on your Azure identity — not everyone has it by
  default; don't assume a permission denial here generalizes to other Azure APIs (Key Vault reads and
  Terraform plans are governed separately and may still work).
- **DB check**, when a migration/schema change is in play — connect to the relevant database directly
  rather than inferring state from Terraform or code. Your IP must be the `allow_operator_ip` rule
  in forge-infra `dev/terraform/data.tf` (change it with a PR; never edit the rule out of band):
  ```bash
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
cd ~/progs/forge-runner
XAI_API_KEY="$XAI_API_KEY" MissionDir="$(pwd)/missions" \
  dotnet run --project src/ForgeMission.Runner/ForgeMission.Runner.csproj
```

(If you're issuing commands *in* pwsh directly, the vars are just there — `$env:XAI_API_KEY` — no export
dance needed. The dance is only for a `bash`-backed tool.)

### Bash tool can hang indefinitely, not just miss env vars (confirmed 2026-07-27)

Beyond the missing-env-vars trap above: in at least one agent session, **every** `bash`-tool
command hung indefinitely and timed out — including a bare `echo`, and with sandboxing explicitly
disabled. The shell profile appears to launch an interactive `pwsh` session (full startup banner +
prompt visible in the captured output) that never actually runs the passed command, rather than a
plain non-interactive shell. Piping through `pwsh -NoLogo -Command "..."` explicitly did not avoid
it either. Root cause not fixed — if a session hits this, don't retry in a loop (diagnosed already);
hand any needed shell/git commands to the user to run themselves.

## Test before you ship (no prod auth needed)

Verify locally against the browser preview tooling **before** cutting an image. Full loop + gotchas:
Phase 40 hub §6 and
[UI Design System §11](ui-design-system.md#11-running-it-locally-and-two-gotchas-that-will-bite-you). In
short: `preview_start forge-ui` (config in forge-rooms [`.claude/launch.json`](https://github.com/katasec/forge-rooms/blob/main/.claude/launch.json), HTTP
`:5286`), dev sign-in `/auth/dev?user=alice`, verify at 375/768/1024 + dark. Real OIDC login needs
HTTPS (`https://localhost:7177`) — only relevant for the PWA install/login test.

## Gotchas

1. **Build ≠ deploy.** Tagging/pushing builds and publishes the image only; you still have to merge
   the `forge-infra` `images.tf` PR and see its apply run pass. Forgetting it means "I shipped" but the app still serves the old image —
   this is exactly how `authbilling_db` sat empty in prod for a day after the code merged (2026-07-18/19).
2. **amd64 only.** Container Apps rejects `linux/arm64`. CI runners are amd64 (fine by default); a
   **local** `docker buildx` on Apple Silicon must pass `--platform linux/amd64`.
3. **ForgeUI replicas.** `RoomBroadcaster` SignalR is in-proc with no backplane, so concurrent
   ForgeUI replicas can split connected clients. Scale-out needs Azure SignalR + a backplane.
4. **Secrets only via Key Vault** (`kv-forgerooms-dev`). No secret value is committed; apps use KV
   references and Terraform never holds a secret value. Passwordless throughout (CI = OIDC federation, runtime = managed identity). The image
   itself carries no runtime secrets — they're injected at run time.
5. **DB migrations never run automatically.** The `migrate` job is manual-trigger only, separate
   from the apps — an app rollout alone never touches schema. (An earlier coupled version of this
   caused a dev DB wipe in 2026-07-18; see Phase 42.6.)
6. **Provider keys live on the runner, not the app.** `@claude`/`@grok`/`@openai` bind only when the
   runner has their key; a mission whose key is empty simply isn't advertised.
7. **Don't infer Azure permissions from one denied call.** A 403 on one API (e.g. ACA log reads) says
   nothing about a different API (Key Vault, Terraform state, role assignment reads) —
   verify each capability by trying it, not by reasoning from another one's error.

8. **Docker Desktop push refused (2026-09-29).** On this Mac, the Docker Desktop daemon dials ACR
   directly ("no HTTPS proxy") and some requests were refused, while containers reach ACR through
   Docker Desktop's proxy. Working route: `docker buildx build --platform linux/amd64
   --provenance=false --load …`, then `docker save -o <tar>`, then `crane push <tar> <ref>` from a
   `crane:debug` container with an `az acr login --expose-token` token in an environment variable.
   The `docker` buildx driver cannot export `type=oci` or `type=docker` files.
   Run it from a `.ps1` file with the profile loaded: `pwsh -NoProfile` drops `NUGET_AUTH_TOKEN`
   (restore fails with "Value cannot be null … 'password'"), and an inline `pwsh -Command` string
   expands `$ACR_TOKEN` itself before `sh -c` sees it. The ACR username is
   `00000000-0000-0000-0000-000000000000`.

## Terraform gotchas (forge-infra)

Hard-won errors from importing dev into Terraform (2026-11-16) — ones that'll burn an hour if hit
cold while editing `dev/terraform`.

1. **Role assignment `scope` is ForceNew, and Bicep wrote `resourcegroups` in lowercase.** Imported
   role assignments keep that casing via `local.ra_scope` in `base.tf`; "fixing" it to
   `resourceGroups` replaces every assignment.
2. **`replace()` treats a `/…/` pattern as a regex.** `replace(id, "/resourceGroups/", …)` matched
   as a regex and produced double slashes; use a plain substring.
3. **Slow or flaky local plans.** The container apps (`listSecrets`), Service Bus auth rules and the
   storage account (`listKeys`) make POST calls on every refresh, which got connection resets from a
   local Mac. The GitHub Actions plan ran the same config in under a minute, first try — trust the
   PR plan comment over a local plan.
4. **A stale infinite lease on the state blob** blocks every plan with an empty lock ID. Break the
   lease on `stforgetfstate001/tfstate/dev/forge.tfstate`; do not plan with `-lock=false` against
   a live apply.
5. **Keep Key Vault secrets out of state.** Read them with an ephemeral `azurerm_key_vault_secret`
   and send them through write-only arguments (for example `administrator_password_wo`).
6. **Key Vault names are global**, not scoped to your subscription — `kv-forge-dev` was already
   taken by someone else, hence `kv-forgerooms-dev`.

## Reference

- **Deploy flow (source of truth):** `forge-infra/README.md` and `forge-infra/AGENTS.md` — gitops
  rules, Terraform layout, image-update recipe.
- Infra design, credential posture, full stand-up history + decision log →
  Phase 38.7 — Hosting & Deployment.
- Current authbilling_db / hosted-`/v1` work → Phase 42.6.
- Observability / OTel exporter follow-up → [Observability](observability.md).
- IaC repo: `katasec/forge-infra` (Terraform in `dev/terraform`; `terraform-plan.yml` comments the
  plan on PRs, `terraform-apply.yml` applies on merge to `main`).
