# Forge OCI Artifact Schema (v1)

> **Phase 39.3 (B0 — blocks all publishing).** The contract for how Forge experts and missions are
> packaged as OCI artifacts. Defined **before** anything is published, because re-tagging signed,
> published artifacts later is the painful path. Implemented in `Katasec.OciClient`
> (`oci-client-dotnet`); consumed by the runner/CLI when distribution lands (39.4).

## Why a schema

The runtime must, at **pull time**, decide *what an artifact is* — an expert or a whole mission —
**before** pulling its blobs, so it can route correctly and refuse to run the wrong kind. OCI 1.1's
top-level **`artifactType`** is exactly that discriminator, and (with the annotations) it is the
surface a **cosign** signature covers — so the discriminator **is** the trust boundary.

## The discriminator: `artifactType`

| Kind | `artifactType` |
|---|---|
| Expert | `application/vnd.forge.expert.v1+json` |
| Mission | `application/vnd.forge.mission.v1+json` |

Read it from the manifest and route before fetching blobs
(`OciClient.Classify` / `ClassifyAsync` → `ForgeArtifactKind`). Legacy experts pushed before the
schema have **no** `artifactType`; classification falls back to the config `mediaType`.

## Layer / config media types

| Kind | config `mediaType` | layer `mediaType` | layer content |
|---|---|---|---|
| Expert | `application/vnd.forge.expert.config.v1+json` | `application/vnd.forge.expert.v1` | `expert.md` (markdown) |
| Mission | `application/vnd.forge.mission.config.v1+json` | `application/vnd.forge.mission.bundle.v1+tar` | self-contained mission tar |

The config blob is the well-known empty descriptor (0 bytes); the type lives in `artifactType` +
the layer `mediaType`, not the config payload.

## Self-contained missions (locked decision)

A mission artifact is **self-contained**: its single bundle layer is a tar of the whole mission
directory — `mission.mcl` + `mcl.lock` + `experts/**` — so a pull needs **no recursive expert
fetches**. Rationale: matches OCI immutability (the mission digest pins the *entire* mission),
lighter metadata, no recursion. (`MissionBundle.Pack` / `Unpack`.)

Because experts are bundled, the `dev.forge.mission.experts` annotation (pinned expert digests) is
**intentionally unused** — it is only meaningful for the *referenced*-experts variant, which we did
not choose.

## Annotations (covered by the signature)

Standard OCI keys plus Forge keys, set on every push (`OciClient.BuildAnnotations`):

- `org.opencontainers.image.{title,version,created}` (+ caller extras: `description`, `authors`, …)
- `dev.forge.schema.version` — **`1`**. Format-evolution key (the one people forget); bump on a
  breaking schema change.
- `dev.forge.kind` — human-readable mirror of `artifactType`: `"expert"` | `"mission"`.
- `dev.forge.mission.experts` — reserved; unused while missions are self-contained.

## Trust boundary (implemented in 39.4)

`artifactType` + annotations are the metadata a **cosign** signature signs. 39.3 defines *what* is
signed; the actual signing (needs the Forge signing key) and pull-side **verification** land with
distribution in **39.4**, where built-ins move from baked-into-the-image to pulled-and-verified from
the trusted Forge registry.

## Round-trip (the 39.3 gate)

`OciSchemaTests` proves it without a live registry (the HTTP push/pull plumbing is unchanged and
covered by the expert integration tests against a real registry):

1. push-shaped expert/mission manifests **classify** correctly from `artifactType`;
2. a legacy expert (no `artifactType`) still classifies via the config `mediaType`;
3. `artifactType` + annotations **survive a JSON round-trip**; a null `artifactType` is **omitted**
   from the wire (not serialized as `null`);
4. a mission bundle **packs then unpacks** with its experts intact (self-contained).

## Push/pull surface (`Katasec.OciClient`)

- `PushExpertAsync` / `PushMissionAsync(bundleTar, annotations?)` — set `artifactType` + annotations.
- `Classify(manifest)` / `ClassifyAsync(ref)` — route before pulling blobs.
- `PullMissionAsync` — pulls the bundle, **throws** if the ref is not a mission (can't run an expert
  as a mission).
- `MissionBundle.Pack(dir)` / `Unpack(tar, dir)` — the self-contained tar.
