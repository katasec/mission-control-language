# Phase 38.7 — Hosting & Deployment (Azure)

> **Status: Done (2026-07-07)** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** 38.1–38.4a (a working app) · [[project_forge_deploy_azure]] · [[project_forge_infra_auth]]
> **Done when:** Forge Rooms runs on a public HTTPS URL, backed entirely by infra-as-code, with
> no secret values in git and passwordless CI/runtime auth.

Forge Rooms was local-only (`make dev-up` + dev server on `:5286`). This spoke takes it to a
hosted, publicly reachable product on Azure Container Apps — containerized, versioned, and
provisioned by committed Bicep. Identity infra (the `forgeids` Entra External ID tenant + the
`Forge Rooms (dev)` app registration) already existed from 38.4; this spoke adds everything on the
**workforce subscription** side and wires the two together.

**Live:** `https://forge.katasec.com` (custom domain) and
`https://ca-forge-ui-dev.niceground-df7fb252.uaenorth.azurecontainerapps.io` (default FQDN).

**Two repos:** app image + CI live in `mission-control-language`; all Azure infra lives in
`katasec/forge-infra` (parameterized Bicep, no secrets committed).

---

## 1. Containerization

`ForgeUI` is **Blazor Server / .NET 10 / NOT AOT** (the CLAUDE.md AOT rule is for the `forge`
CLI). It needs a normal ASP.NET runtime image, so this is a separate `Dockerfile.forgeui` (repo
root) from the CLI's root `Dockerfile`.

- **Two-stage build**: `mcr.microsoft.com/dotnet/sdk:10.0` (publish) → `aspnet:10.0` (runtime).
- Bakes in `missions/` and sets `MissionDir=/app/missions` — the mission registry + the OpenAI key
  (`env("MCL_API_KEY")`) are read from `missions/*/forge.toml` at startup. `MCL_API_KEY` is injected
  at runtime from Key Vault; **the image carries no secrets**.
- Also builds a **self-contained EF migration bundle** (`/app/migrate`) — prod never auto-migrates
  (that path is `IsDevelopment()`-only), so `dev/500-app` runs the bundle as a one-shot job.
- **Three build gotchas fixed** (see §8): private NuGet feed via BuildKit secret mount,
  `PublishAot=false` scoped to this build, EF `--context RoomsDbContext`.

## 2. Image versioning & CI

- **Semver** via git tags `forge-ui-vX.Y.Z`; image tags `X.Y.Z` + `sha-<sha>` + `dev-latest`.
- `.github/workflows/forge-ui-image.yml` (app repo): buildx → push to ACR, **Azure auth via
  GitHub → Azure OIDC federation** (no stored cloud secret), ACR push via the CI managed identity.
  The private-feed token is a **BuildKit secret** (never in a layer), sourced from `GITHUB_TOKEN`.
- **Arch note:** local `docker buildx` on Apple Silicon produces `linux/arm64`, which Container
  Apps rejects (`must be linux/amd64`). GitHub runners are amd64 (CI is fine); local builds must
  pass `--platform linux/amd64`.

## 3. Azure infrastructure (Bicep — `forge-infra`)

Layered, numbered by deploy order; each layer is `main.bicep` + `main.bicepparam` + `README.md`.

| Layer | Contents | Notes |
|---|---|---|
| `dev/100-base` | RG `rg-forge-dev`, Log Analytics, ACR `crforgeroomsdev`, **empty** Key Vault `kv-forgerooms-dev` (RBAC), app MI `id-forge-dev` + AcrPull/Secrets-User roles | Human-run once (role assignments) |
| `dev/150-ci` | Passwordless CI identity `id-forge-ci-dev` (user-assigned MI + GitHub OIDC federated creds) + Contributor/RBAC-Admin/AcrPush | Human-run once |
| `dev/400-appenv` | Container Apps managed environment `cae-forge-dev` | Base infra |
| `dev/300-data` | Postgres Flexible Server `psql-forge-dev` → connection strings to KV | Secret-bearing |
| `dev/500-app` | Container App `ca-forge-ui-dev` + EF migration job + custom domain binding | The app |

Region **uaenorth**, workforce subscription. `.github/workflows/infra.yml` deploys a layer per
dispatch (OIDC); secret-bearing layers gated behind GitHub Environments.

## 4. Secrets & credential posture

- **Key Vault is the only secret store.** No secret value is committed; Bicep uses `@secure()`
  params + Key Vault *references* only.
- **Passwordless** CI (OIDC federated managed identity — no client secret can exist) and runtime
  (app MI pulls ACR + reads KV; no creds in app config beyond KV refs).
- **Key Vault RBAC, least-privilege**: app MI = read-only *Secrets User*; secret writers get
  *Secrets Officer* separately.
- Identifying-but-public IDs (subscription/tenant/clientId) are injected from **GitHub variables**
  and `readEnvironmentVariable()` in bicepparam — never committed.
- Postgres admin password is **generated at deploy** and lives only inside the KV connection
  secret. Recommended hardening: passwordless Postgres (Entra/MI token auth) — deferred (needs an
  Npgsql code change).
- KV secrets in prod: `Oidc-ClientSecret`, `Mcl-ApiKey`, `ConnectionStrings-Read/WriteConnection`.
- **Provider keys (38.5 task 7, per-mission).** Each raw-model agent reads its own `env(...)` key:
  `@openai`→`MCL_API_KEY`, `@claude`→`ANTHROPIC_API_KEY`, `@grok`→`XAI_API_KEY`. On **dev** these
  map to KV secrets `Mcl-ApiKey` / `Anthropic-ApiKey` / `Xai-ApiKey` via the `id-forge-dev` managed
  identity. ⚠️ `Anthropic-ApiKey` + `Xai-ApiKey` (and their container secret refs + env vars) were
  added **manually via `az` (2026-07-08), not yet in the `dev/500-app` Bicep** — a `dev/500-app`
  redeploy would drop them until the Bicep is updated (see §9). **prod** has neither of these yet.

## 5. Custom domain & TLS

`forge.katasec.com` → CNAME to the app FQDN + `asuid.forge` TXT = the app's
`customDomainVerificationId`. Bound with a **free managed certificate** (SNI/HTTPS).

- **Ordering gotcha:** Azure requires the hostname on the app *before* a managed cert can be
  created (and cert↔app is circular), so it **can't be one Bicep pass**. Cert creation is a small
  out-of-band CLI step (`az containerapp hostname add` then `hostname bind --validation-method
  CNAME`). `500-app` Bicep is reconciled to a re-appliable form: `customDomain` +
  `customDomainCertificateId` params (empty certId → `Disabled` binding, which is what enables cert
  creation; set → `SniEnabled`).
- Entra app-registration redirect URIs extended with `https://forge.katasec.com/signin-oidc` (+
  `/signout-callback-oidc`), added via Graph PATCH using a `forgeids`-tenant token.

## 6. Prod-specific app fixes

Two issues surfaced only once running behind the platform / in prod config:

- **Forwarded headers** — Container Apps terminates TLS and forwards over http; the app emitted
  `http://` OIDC redirect URIs (rejected by Entra) and dropped the Secure correlation cookie. Fixed
  with `UseForwardedHeaders` (`XForwardedProto`/`For`) before auth in `Program.cs`.
- **Essential-agent seeding** — `RoomsSeeder` is Development-only, so the `@forge/assistant` **agent
  member row never existed in prod** → `StarterRoomService` hit an FK violation adding it to a new
  user's room, so no auto-reply. Split `RoomsSeeder.SeedEssentialAgentsAsync` (built-in agent member
  rows = **product data**) and run it in **all** environments at startup, idempotently;
  `StarterRoomService` now self-heals a solo room-of-two created before the member existed.
  **Lesson:** seed data that is product data must run in every env; only Alice/Bob/demo rooms are Dev-only.
- **Self-heal trigger placement (0.1.4)** — the self-heal lived in `EnsureStarterRoomAsync`, but
  `RoomList` only called it when `rooms.Count == 0`, so a user who already had the broken room never
  healed. Fixed to call it **unconditionally** on `/rooms` load (still only creates+redirects for a
  brand-new user). Verified live in prod logs: self-heal `INSERT INTO memberships` + agent replies.
- **Display name "unknown" (0.1.5)** — Entra's Email-OTP flow collects no name and sets the `name`
  claim to the literal `"unknown"`. `ForgeClaims.DisplayName` now treats `"unknown"`/blank as absent
  and derives the name from the email local-part (capitalized). Existing members auto-heal via
  `FindOrCreateAsync`'s profile-update on next load; room messages resolve the *current* member name,
  so past messages relabel too.
- **Consumer UI pass** — base font 14→16px, larger bubbles/avatars/inputs, wider room column (a
  "font too small" consumer note); tokens in `forge.css`.
- **Non-issue confirmed:** the `23505 ux_members_issuer_subject` duplicate-key seen in logs is the
  **already-handled** provisioning race — `FindOrCreateAsync` catches it and re-reads the winner; the
  stack trace is a logged warning, not a crash.

**How to test without prod auth (OIDC = the user's identity):** run the image's source locally in
Development (local Postgres + `MCL_API_KEY` pulled from Key Vault at start via a `.claude/launch.json`
bash-wrapper config — never written to disk) and drive it with the `preview_*` browser tools + the
dev sign-in (`/auth/dev?user=…`). Reproduce a specific prod DB state against local Postgres with
`docker exec forge-rooms-postgres psql`. This is how the assistant reply + self-heal were verified
before shipping.

## 7. Live state (as of 2026-07-08)

- App: `ca-forge-ui-dev`, single replica (in-proc SignalR `RoomBroadcaster` — scale-out later needs
  Azure SignalR + a backplane), image **`forge-ui:0.1.9`** (revision `--0000008`). Boot log loads
  **5 missions**: `ChatGPT, Forge, Assistant, Claude, Grok` — i.e. the 38.5 raw-model trio
  (`@openai`/`@claude`/`@grok`) all resolve their keys, alongside verified `@guard`/`@assistant`.
- Image tags in ACR: `0.1.0`…`0.1.9` — `0.1.0` first live; `0.1.1` forwarded-headers; `0.1.2`
  essential-agent seed; `0.1.3` seed + UI; `0.1.4` self-heal trigger; `0.1.5` display-name fix;
  `0.1.6`/`0.1.7` (38.5 registry + bare handles + identity seal + `/agents`); `0.1.8` (38.5 add/remove
  agent + auto-reply guard); `0.1.9` (38.5 `@grok` + xAI provider fix).
- **CI proven end-to-end:** `gh workflow run forge-ui-image.yml -f version=X` builds native amd64 +
  pushes to ACR via OIDC (GITHUB_TOKEN reads the private feed) — faster than local emulated builds.
  ⚠️ **The image workflow only builds+pushes; it does NOT roll the Container App.** The intended
  release loop is: commit → run the image workflow → **redeploy `dev/500-app`** with the new
  `FORGE_UI_IMAGE` (keep `CUSTOM_DOMAIN` + `CUSTOM_DOMAIN_CERT_ID` set to preserve the HTTPS binding).
  This session rolled `0.1.7`/`0.1.8`/`0.1.9` via a **manual `az containerapp update`** instead — quick,
  but it bypasses the Bicep and leaves the running app's image + the new provider secrets/env vars as
  **drift** vs `dev/500-app` (see §9).
- GitHub Actions variables wired in both repos.

## 8. Decision log / gotchas (so they are not re-learned)

- **Bicep `BCP258`** — a `.bicepparam` must assign every required param; can't supplement with
  `-p key=val`. Use `readEnvironmentVariable('VAR')` in the param file + export at deploy.
- **Key Vault** — `enablePurgeProtection` rejects `false` (emit `true` or omit); names are global
  (`kv-forge-dev` was taken → `kv-forgerooms-dev`).
- **Role GUIDs** — always `az role definition list --name`; Contributor is
  `b24988ac-6180-42a0-ab88-20f7382dd24c` (an easy one to get wrong).
- **Federated credentials** — Azure rejects concurrent FIC writes on one identity; chain with
  `dependsOn`.
- **Container Apps** — image must be `linux/amd64`; custom-domain managed cert can't be a single
  Bicep pass (hostname-first + circular).

## 9. Follow-ups (not blocking)

- **Custom display names** — let users set a name (profile edit), decoupling it from the email.
- **Passwordless Postgres** (Entra/MI auth) + **private endpoints** for Postgres/KV (VNet-integrate
  the Container Apps env → drop the public endpoint + the `AllowAllAzureServices` firewall rule).
- **prod slice** + environment-protection reviewers on the gated infra layers.
- Exercise the **infra** deploy workflow via dispatch (the image workflow is already proven).
- **Fold the 38.5 provider keys into IaC (drift fix).** Add `Anthropic-ApiKey` + `Xai-ApiKey` KV
  secret refs + `ANTHROPIC_API_KEY` / `XAI_API_KEY` container env vars to `dev/500-app` Bicep (they
  were added via `az` this session), and set the two secrets in the **prod** Key Vault before prod
  ships `@claude`/`@grok`. Until then a `dev/500-app` redeploy reverts them.
- **Close the build↔rollout gap.** The `forge-ui-image` workflow builds+pushes but doesn't deploy;
  rollout is a separate `dev/500-app` (or `az containerapp update`) step — easy to forget (it bit us
  this session: "deploy" built `0.1.7` but the app kept serving `0.1.6`). Consider a `deploy-dev`
  step/workflow that builds *and* rolls in one action.

_Done this session (2026-07-08): 38.5 registry/handles/seals/`/agents`/add-remove-agent/raw-model
trio shipped to dev (0.1.6→0.1.9); Anthropic + xAI keys wired (dev, via az)._
