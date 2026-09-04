# Phase 43.20 — Project Workbench MVP: completion record

Completed-task evidence for the active [Project Workbench MVP spoke](phase-43.20-project-workbench-mvp.md).

## Task 2 — Durable Project Mission Control

**Verified and merged 2026-09-04 — PR #90, merge commit `767c289`.**

### Delivered boundary

- A Project has one durable, zero-tool `MissionControl` conversation. Its server-issued ID is
  written atomically to the local manifest only after Host acceptance; reopening replays the same
  conversation rather than creating a run.
- `ProjectControl` remains distinct from Janus: control turns carry no capability, local path, or
  run ID, and the Worker resolves only the fixed `MissionControl` mission for them.
- Presentation reaches the Client Runtime through the named Project-control open/submit contracts;
  the obsolete picker is absent and no local transcript store was introduced.
- Invalid control input, an unopened control session, and expected Host outcomes have typed
  presentation results. A failed submit retains its text and command ID so retry is idempotent.

### Verification

| Observation | Result |
|---|---|
| Full solution suite | 925 passed, 0 failed, 11 skipped. |
| AOT Desktop publish | Completed cleanly with no new AOT warnings. |
| Browser-first visual matrix | Ready, pre-open, failed-send, busy/error, long content, continuous resize, 125/150/200% zoom, and both colour modes passed against the Task 2 references. |
| Packaged default path | Published Desktop was launched with no `ConversationRuntime__BaseUrl` override. Its own supervisor opened the standard `127.0.0.1:18080` Kind tunnel. |
| Reproducible local deployment | `make -C ~/progs/forge-infra 350-conversation-kind-up` passed the Conversation/Worker verifier probes and rolled both deployments to `767c289`, the clean `main` commit. |
| End-to-end durable result | Reopening the designated throwaway Project opened its existing control conversation; a submitted turn was accepted as sequence 3 and the real Worker appended the `MissionControl` participant reply at sequence 4. `runs` remained empty. |

### Operational correction

An earlier parity capture temporarily loaded branch-built Kind images to test before merge. That
was not an acceptable completion path because it bypassed the reproducible-main guard. After PR
#90 merged, the sanctioned Kind target above rebuilt and rolled both services from clean `main`.
Only that restored default deployment is counted as acceptance evidence.

The disposable verification Project remains at
`/Users/ameerdeen/Forge/Projects/task2-packaged-parity-throwaway`; it contains no runs or
credentials and was intentionally not deleted.

## Task 3 — Project Explorer and resolved dependencies

**Implemented; review pending.** Branch `codex/task-43-20-task3-project-explorer`. Two gaps remain
open and are named at the end of this section — neither is claimed as passing.

### Prerequisite: `katasec/oci-client-dotnet` 0.3.0

`PulledExpert { Content, ManifestDigest }` and `PullExpertWithDigestAsync` (PR #2, merged as
`565d3e2`, published as `v0.3.0`). The digest comes from the same manifest response the layer
descriptor came from: the body is read once and used for both the parse and — when the registry
omits the optional `Docker-Content-Digest` header — the computed SHA-256, so a digest can never
describe a different response than the one that was parsed. A malformed header is refused rather
than silently replaced. `PullExpertAsync` is a compatibility wrapper over the same path.

| Fact | Observation |
|---|---|
| Release test | `dotnet test --configuration Release --filter "Category!=Integration"` in the workflow: 18 passed, 0 failed, 0 skipped. |
| Live registry, read-only | 13 passed, 0 failed against real `ghcr.io/katasec`, including a pull-by-tag whose returned digest re-resolves to byte-identical content. The outward-writing push round-trip was deliberately excluded. |
| Published | Feed lists `0.3.0`; tag `v0.3.0` points at `565d3e2`; release `v0.3.0` carries `Katasec.OciClient.0.3.0.nupkg`. |

### Lock file v2

`LockFileExpert { Source, ContentDigest }`. v1's `{ source, path, hash }` made `path` mean either a
Project file or a machine-local cache location depending on `source`, so a lock file was not
portable. v2 records `project:///<path>` or `oci://<registry>/<repo>@sha256:<hex>` and its syntax
alone selects the resolver; an OCI expert's materialization is derived by `ForgeCache` and never
serialized. A v1 local entry migrates in memory; a v1 OCI entry is refused by name, because it
holds a tag and a cache path but no manifest digest, and reconstructing one from the cache path
would invent provenance. v1-local coverage lives in `LockFileV2Tests`' own fixtures.

### Default-path acceptance — CLI

Cites the [Forge CLI — resolved OCI experts](../design/default-path-acceptance.md#forge-cli--resolved-oci-experts) row.

| Fact | Observation |
|---|---|
| Artifact | `forge` published by `make install` (native AOT, `1.0.0+637711c`). |
| Defaults | Plain `forge init`. No mission argument, no `--refresh`, no injected registry URL, no manual cache placement, no pre-written lock. `FORGE_REGISTRY_TOKEN` confirmed absent. |
| Dependency | Normal `ghcr.io` route. The machine holds a stored `ghcr.io` credential in `~/.forge/credentials.json`, so `CredentialStore` returned it and that — not anonymous negotiation — was the route used. A separate probe confirmed `ghcr.io` also issues an anonymous pull token for the same repository, so the artifact is public. |
| Starting state | A dedicated disposable directory with `mission.mcl` and a normal `forge.toml` naming one public `ghcr.io/katasec` expert **by tag**. |
| Action | `forge init`. |
| Outcome | **PASS.** A new v2 `mcl.lock` whose OCI source is `oci://ghcr.io/katasec/forge-kubernetes-architect@sha256:94df5c28…` — the digest `ghcr.io` serves for that tag, verified against the registry — and whose content digest `sha256:12e1947f…` equals the SHA-256 of the resolved cached `expert.md`. No path appears anywhere in the file. |
| Runtime proof | `forge run` on `loop-demo-naive` (project sources) and on `build-operator-oci` (three OCI sources) both complete; `--verbose` shows all three experts resolving from their digest-derived cache locations. |
| Controlled tests | The loopback OCI stub, every temp-directory lock fixture, and all unit/contract tests are non-acceptance. |

### Repository lock migration

All 38 tracked `mcl.lock` files regenerated to v2 with the published binary: 37 local-only (no
network) and `missions/build-operator-oci` (three public `ghcr.io/katasec` experts). No tracked v1
lock and no tracked v1 OCI lock remains, so the reader never has to refuse a file this repository
ships.

### Automated verification

| Check | Observation |
|---|---|
| Full solution suite | 1041 passed, 0 failed, 11 skipped (709 + 171 + 97 + 59 + 5), before and after the lock migration. |
| AOT Desktop publish | Clean, no warnings. It initially **failed**: the Explorer made `ForgeMission.ClientRuntime` root YamlDotNet's reflection builders for the first time. Fixed by mirroring the suppression `ForgeMission.Cli` already carries for the same reason — every POCO through those builders is preserved by an explicit `[DynamicDependency]`, and the AOT `forge` binary built from the same Core code demonstrably reads and writes v2 locks. |

### Browser-first acceptance

Run against the real Client Runtime process with its user profile redirected to a disposable
sandbox. The seeded Project's manifest and lock are hand-authored fixtures — a controlled test, not
default-path evidence. Observed states are recorded as
`docs/images/phase-43.20/task3-workbench-*-observed.svg` beside their binding compact frames.

| Check | Observation |
|---|---|
| Rail | Dark navy surface, wordmark, Explorer / Mission Control / Settings in that order, Settings bottom-aligned, selection carrying both the accent marker and `aria-current="page"`. |
| Interactions | Every rail switch, the document open, Back to Explorer, and opening the read-only OCI dependency were performed with **real pointer clicks**. Tab reaches the rail and the focus outline is the marker colour. |
| Four corners | 800×568, 1536×568, 800×1024, 1536×1024 — no page scrolling in either axis, no clipping of rail labels or the pinned reference. Rail 116px at the lower bound (the reference's own width) and 180px at the upper. |
| Continuous resize | 980×700 → 1180×820 → 1360×930 → 1536×600 → 820×990: rail ramps 137.19 → 165.19 → 180 → 180 → 116 with no scrolling or clipping at any point. |
| Zoom | 125/150/200% at 800×568: no horizontal page scroll, no clipping, Settings stays inside the rail. At 200% the **content region** takes the vertical scroll, which is the designed ownership. |
| Colour modes | In dark mode the document region inverts while the five `--wb-rail-*` tokens hold their light values, as the design requires. |
| Copy | Matches the binding frames, including `· OCI dependency · read-only` — a mismatch this check caught. |

This check also caught a defect no test could see: the rail was first built with `RenderTreeBuilder`,
and Blazor stamps a component's scoped-CSS attribute only onto elements written in the template, so
it rendered completely unstyled while bunit's structural assertions passed.

### Packaged Desktop

| Fact | Observation |
|---|---|
| Artifact | `dist/forge-desktop/ForgeMission.Desktop`, launched with **zero arguments**. |
| Defaults | No `ConversationRuntime__BaseUrl`, `MissionRuntime__*`, or `FORGE_API_ENDPOINT` in the environment, verified before launch. |
| Dependency | Its own Supervisor opened the standard `kubectl port-forward … 127.0.0.1:18080` Kind tunnel; `GET /health` returned 200. Both deployments run image tag `767c289…`, the clean `main` commit rolled by `make -C ~/progs/forge-infra 350-conversation-kind-up`. |
| Starting state | A new disposable Project, `~/Forge/Projects/task-3-packaged-parity-throwaway`. It holds no runs and no credentials and was intentionally not deleted. |
| Action | Create the Project from a goal, submit one Mission Control turn, then navigate Explorer → Settings → Mission Control. |
| Outcome | **PASS for behaviour.** Mission Control was the view on open and opened against the real Conversation service (composer enabled, no error). The turn was accepted and the real Worker appended a `MissionControl` reply. After navigating away and back the live transcript was intact, with no connection banner, no gap notice, and no second conversation: the manifest holds exactly one `missionControlConversationId` and zero runs. Killing the Supervisor left no orphaned Client Runtime or port-forward. |

### Two open gaps — not claimed as passing

1. **In-window packaged visual parity.** macOS Screen Recording and Accessibility permissions are
   not granted to this session, so the Photino window could neither be captured nor driven. The
   packaged evidence above was obtained through that same packaged, zero-argument app's own Client
   Runtime process and its default Conversation route — it proves the packaged runtime's behaviour,
   not the pixels inside its window. The window's **measured default usable viewport** is therefore
   still unrecorded, and this task's visual acceptance is not closed until an operator (or a
   session with those permissions) captures it.
2. **Operator visual sign-off.** Agent PASS is recorded above against the binding references; the
   operator's independent acceptance has not been given.
