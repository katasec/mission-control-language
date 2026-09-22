# Phase 49.5 — Private package foundation

> **Status:** 49.5a final independent review. Package publication and repository extraction are
> deliberately coupled to the first real private consumer; prose alone cannot close this card.

> **Default-path acceptance:** N/A for 49.5a: this is a documentation/policy card and changes no
> artifact, runtime, integration, or deployment default. Each later producer/consumer cutover must
> define and exercise its own applicable default path before acceptance.

## Locked decisions

| Decision | Policy |
|---|---|
| Repository disposition (D49-02) | Create private `katasec/forge-mcl`. Import only the MCL owner map from a recorded mono commit; tag provenance in the private repository. The public monorepo becomes read-only coordination/rollback source after cutover and never consumes private packages. |
| MCL packages (D49-03, partial) | Private v1 packages are published only from `forge-mcl` immutable release tags: `Katasec.Forge.Mcl.Parser`, `.Core`, `.ChatClients`, `.Scout`, `.MissionRegistry`, and `.Serve` start at `1.0.0`; direct and intra-MCL dependencies use exact `[1.0.0]` ranges. Packable library `ProjectReference` edges retain same-repository builds and set `TreatAsPackageReference=true` with exact `Version=[1.0.0]`, so packing emits the range without restoring an unpublished sibling package. This applies Core → Parser and ChatClients/Scout/MissionRegistry → Core. Every library declares `PackageId`, `Version`, `IsPackable=true`, `RepositoryUrl`, and the existing proprietary license policy; MissionRegistry becomes packable only in `forge-mcl`. CLI is an executable, not a package, so no CLI nuspec dependency exists. `RestorePackagesWithLockFile=true`, committed lock files, and locked-mode restore are required. One major line is supported; additive v1 changes only, breaking public/wire/persistent changes require v2 and a migration card. Runner/Conversation contract policy remains to be locked in their extraction cards. |
| Docker support (D49-06) | `ForgeMission.Docker` stays the sole Docker socket/prerequisite owner, hosted by `forge-mcl` as private `Katasec.Forge.Docker` **0.1.0** with exact `[0.1.0]` consumer range, unchanged assembly/namespace, and `net10.0` AOT-safe leaf surface. Its package metadata matches the MCL libraries and explicitly sets `IsAotCompatible=true`. CLI uses source there; Desktop later consumes only the exact package. It owns no lifecycle policy, image choice, runtime mode, or process authority. |
| Access | Publisher release workflow alone has `packages: write`. Every producer/consumer PR workflow has exactly `contents: read` and `packages: read`, and provides `NUGET_AUTH_TOKEN: ${{ secrets.GITHUB_TOKEN }}` only to restore. On 2026-09-22 GitHub API observed current upstream build dependencies `Katasec.AITools`, `Katasec.OciClient`, `Katasec.OaiServer`, and `Katasec.AnthropicServer` as public, so no grant is required for the bootstrap; if any becomes private, `forge-mcl` receives an explicit package-level read grant before CI is enabled. Later consumer repositories receive explicit access only to the named private MCL packages they restore. No PAT, producer token, or package credential reaches product artifacts or containers. |
| Source mapping | Each private repo maps `Katasec.*` exclusively to GitHub Packages and unrelated packages to nuget.org; no persisted token is committed. |

## Chronological proof

1. **49.5a — policy/design:** this card locks repository/package/access/version/rollback policy and
   the exact MCL import manifest. It is reviewable now but not package-accepted.
2. **49.6 — `forge-mcl`:** bootstrap from immutable mono commit
   `d2c0c121bbe4180b60ddd44fa5f18872e2402771`. The checked-in
   `eng/import/forge-mcl-v1.json` is the authority: it enumerates exactly the 186 source blobs
   selected below. Its canonical input is UTF-8, LF-terminated, ordinal-sorted `git ls-tree -r`
   records split at the first tab, ordinal case-sensitive sorted by their source-path suffix, then
   joined as UTF-8 with LF and one final LF (`<mode> <type> <blob>\t<path>\n`). Its SHA-256 is
   `a5a6aa83cf32add694708b51296f865787c7a1d5c611444b70ba94c6e9502940`.
   Its parallel source-path check uses those same ordinal-sorted suffixes, UTF-8/LF plus final LF,
   and is
   `4936c7a94cde0b8df13948a087b235fcf45622a0d85406490c35e6de255cd21a`.
   Import Parser, Core, ChatClients, Scout, MissionRegistry, Serve, Docker, CLI, their
   source-adjacent READMEs, owned unit tests/fixtures, lowercase `nuget.config`, and the two
   `src/Directory.Build.*` files. Exclude every `missions/**` content file (including
   `missions/vanilla`), Application*, Desktop*,
   Runner*, Rooms*, Api/Billing, Conversations*, ForgeUI, the aggregate solution, root Makefile,
   and the existing `ForgeMission.Tests.csproj`. No dual write.
3. Release from a protected immutable tag only. PR CI restores/builds/tests/packs but never
   publishes. Release validates packed nuspec exact dependencies, private visibility, private
   repository metadata/commit, secret-free package/logs, hashes, and no delete/re-publish.
4. **49.7 — `forge-runner`:** before its cutover, observe clean-cache restore failure without a
   package grant, grant only required MCL package Actions access, then observe clean-cache locked
   restore/build/test success using its own workflow token. Replace approved MCL source edges with
   exact package references; no cross-repository ProjectReference remains.
5. The successful real Runner consumer proof closes 49.5b. Record package IDs/versions/hashes,
   producer tag/commit, import-manifest hash, consumer commit/workflow run, package visibility,
   explicit grants, and current/previous deployable image and infra revision.

### Exact imported source roots

The manifest expands the following paths only: the eight listed production project directories;
test directories `Adapters`, `Cli`, `Experts`, `Fixtures`, `Manifest`,
`MissionRegistry`, `Missions`, `Parser`, `Resolution`, `Rules`, `Runtime`, `Scout`, and `Tools`;
and individual test files `ClientRuntime/DockerCliTests.cs` and
`Runner/RunnerMissionSourceTests.cs`. The five individual root/build files are `.gitignore`,
`Dockerfile`, `nuget.config`, `src/Directory.Build.props`, and
`src/Directory.Build.targets` (with the exact files already inside the listed roots). Generated
repository bootstrap files are recorded separately and never represented as imported source.

The new `ForgeMission.Mcl.Tests` retains assembly name `ForgeMission.Tests` for the existing Docker
friend assembly. It imports the manifest test paths verbatim under `tests/ForgeMission.Mcl.Tests`
and replaces their former aggregate project with one new project that references only Parser, Core,
ChatClients, Scout, MissionRegistry, Serve, Docker, and the CLI build output. Its acceptance map is
Parser/Experts/Manifest/Resolution/Rules → Parser/Core; Adapters/Runtime/Tools → Core/ChatClients;
Scout → Scout/Core; MissionRegistry and `RunnerMissionSourceTests` → MissionRegistry/Core;
Cli → CLI/Core; and `DockerCliTests` → Docker. It retains the existing fixed test package pins and
`Microsoft.AspNetCore.App` framework reference, and uses released `Katasec.OaiServer` and
`Katasec.AnthropicServer` **0.1.7** where needed. It contains no sibling `ProjectReference` and
does not import the floating `GitHub.Copilot.SDK` reference. Live Grok coverage remains present but
is excluded from deterministic CI; it is separately recorded as external-provider evidence.

## Reproducible CI and substitution proof

Both producer and Runner set `RestorePackagesWithLockFile=true` and commit `packages.lock.json`;
each `NuGet.config` clears inherited
sources, maps `Katasec.*` only to GitHub Packages and `*` only to nuget.org, and contains no
credential other than the `%NUGET_AUTH_TOKEN%` placeholder. CI uses a fresh checkout/cache and
`dotnet restore --locked-mode`, then `dotnet build --no-restore`, `dotnet test --no-restore`, and
producer `dotnet pack --no-build`. Producer CI fails when any library nuspec lacks its expected
exact internal range (Core → Parser; ChatClients/Scout/MissionRegistry → Core), contains a CLI
dependency, or omits required repository/package metadata. Producer additionally publishes CLI
AOT; CI stores command exit/result and artifact hash. Runner replaces its `Core`, `ChatClients`, `Scout`,
`MissionRegistry`, and `Serve` ProjectReferences with exact `Katasec.Forge.Mcl.*` references;
Runner.Contracts remains Runner source. Its proof records a clean-cache no-grant restore failure,
then explicit package grant and clean-cache locked restore/build/test success with workflow run IDs.

## Security, failure, and rollback

This changes no public route, store, queue, identity, or product credential. Package source mapping
and explicit Actions grants are security controls. A missing grant fails restore; recovery owner is
the package administrator, who grants only the named consumer repository and re-runs clean CI.
The producer's PR workflow uses only `contents: read` and `packages: read`, exposes its own
`GITHUB_TOKEN` only as `NUGET_AUTH_TOKEN` for restore, and runs its missing-token clean-cache
restore as controlled negative-path evidence, never default-path evidence. Its protected-tag
release workflow alone receives `packages: write`, validates package metadata and exact internal
ranges, and never deletes or republishes a version. A bad package or grant is contained before a
consumer cutover: retain the current image/commit, correct the producer or grant, and re-run clean
CI. This is a Type-2 bootstrap: deleting the new repository branch or returning a consumer to its
prior package-pinned commit restores the old product path.
Docker is a configured local path, not Desktop's default cloud route; later Desktop default-path
acceptance must use the published zero-argument artifact with overrides absent.

Rollback never recreates an unusable cross-repo ProjectReference: deploy the recorded previous
image/infra revision or return a private consumer to its prior package-pinned commit. Preserve all
immutable package versions, source tags, and the mono rollback anchor.

## Done when

49.5 is accepted only when 49.5a is independently reviewed **and** private `forge-mcl` publishes
real private v1 packages that a separately private `forge-runner` restores with its own token under
locked mode after explicit least-privilege access proof.
