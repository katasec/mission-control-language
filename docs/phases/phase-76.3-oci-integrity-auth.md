# Phase 76.3 — OCI integrity and authentication prerequisite

**Status:** Product accepted 2026-10-09: full design/plan/code reviews passed, OCI PR 5 merged,
Katasec.OciClient 0.5.0 published normally and accepted from a fresh remote-package Native AOT
consumer. [Completion evidence and timing](phase-76.3-oci-integrity-auth_completed.md).
The contract remains here for future consumer work; cloud Type-1 decisions remain open.
Parent: [Phase 76](phase-76-unified-cloud-run.md).
Contract: [source and authentication design](phase-76.2-unified-cloud-run-contracts.md).

## Scope and gates

Operator instruction, verbatim:

> Please check the next item on the plan which should be Phase 76 for unified cloud execution. Please kick of  the documented supervisor workflow in order to complete the tasks in that phase autonomously.

Bounded prerequisite: make existing OCI retrieval return verified immutable mission content and
verify supplied registry credentials before a caller saves them. The exact dependency probe
found these missing from the pinned/published library. This is necessary for local/OCI convergence
and successful-login semantics; no new registry framework, cloud execution or settings persistence.

Owner: `/Users/ameerdeen/progs/oci-client-dotnet/src/Katasec.OciClient`, per its root README.
Its AGENTS.md requires a new `codex/` branch and reviewed PR integration; use that repository's
branch convention. Tests live in its existing test project. Read-only source baseline is
`565d3e2a7a87d95b05c7dd8261834c843f467f78`, clean main equal to origin/main after fetch.

| Gate | Answer |
|---|---|
| Security ownership/tier/data | Existing local OCI client talks to the operator-selected registry and its delegated token realm. No context/store/queue owner or service entry point changes. |
| Identity/secrets | Existing constructor credential remains local; HTTPS realm validation, scoped caching and redirect handling narrow current authority. No new provider/platform keys or cloud credential grants. |
| Type 1/2 | Reversible local-library API and hardening behind the existing registry contract; no new cloud Type-1 boundary. Cloud executable/content decisions remain blocked independently. |
| Engineering failure boundaries | Library owns HTTP/auth/integrity/bounds; callers own settings/cache/package policy. Fail with existing OciException/OciAuthException, preserve cancellation, never report unverifiable content/auth as success. |
| UI | N/A: library behavior only, no visual surface. |
| Default-path acceptance | Required for this behavior change: publish from merged main through existing workflow, restore exact package in a disposable Native AOT consumer using its public constructor against real GHCR. No endpoint override or internal handler can close it. CLI consumer adoption and full cloud acceptance remain later tasks. |

## Locked technical design

### Public API and immutable retrieval

Add `Task<PulledMission> PullMissionWithDigestAsync(string registry, string name,
string reference, CancellationToken ct = default, long maxBundleBytes = 33554432)`.
`PulledMission` exposes `Bundle: byte[]`, `ManifestDigest: string`, `LayerDigest: string`,
`LayerByteLength: long`; all digests are normalized lowercase `sha256:<64 hex>` over received bytes.
Existing PullMissionAsync delegates to the same verified retrieval and returns Bundle. Preserve
existing public call signatures; add no parallel network path. Caller-specified bounds must be
positive; the default 32 MiB is the Forge bundle retrieval budget, not a configurable CLI knob.

Shared manifest retrieval bounds decoded bytes to 1 MiB, computes their SHA-256 before parsing,
and checks a requested digest and any declared Docker-Content-Digest against it. A malformed or
mismatched header is an OciException. A missing header uses the computed identity. A tag is never
an immutable identity. Update existing expert digest tests that currently expect a deliberately
wrong header to win; that behavior is the defect being fixed.

Mission retrieval validates existing Forge schema/kind/config/layer media types, requires its
one bundle layer, rejects malformed SHA-256/negative or over-budget descriptor sizes before fetch,
uses bounded streaming reads, and checks actual size/hash against the descriptor before return.
Retain source-generation metadata. Do not unpack/extract the tar in this task; MissionRegistry
owns safe package selection/extraction later. Do not add ZIP/tar/schema abstractions or a cache.

### Authentication and HTTP authority

Add `Task VerifyRegistryCredentialAsync(string registry, CancellationToken ct = default)` using
the existing constructor credential; empty/missing credential fails OciAuthException. This is
verification only; it never writes credentials or settings. One client is one credential context.

1. Request `https://<registry>/v2/` without Authorization. Require a supported Bearer challenge;
   anonymous 200 alone cannot verify a supplied credential and fails explicitly.
2. Parse realm/service and optional scope. Realm must be absolute HTTPS, with no userinfo or
   fragment. Preserve a valid realm query when adding encoded service/scope; never duplicate or
   override challenge parameters ambiguously. Basic registry challenges remain unsupported.
3. Exchange Basic username `token` / constructor credential only with that HTTPS realm. Bound
   token response to 64 KiB, require nonempty token/access_token, and honor expiry (default 60 s).
   Invalid credentials or malformed/unsupported challenges fail OciAuthException without secrets
   or response bodies in messages. Cancellation remains OperationCanceledException.
4. Retry the original registry request with the issued Bearer token and require success.
   Verification rejects redirects on both exchanges. Successful anonymous pulls remain separate
   from login; never retry anonymously after rejecting a supplied credential.

The same BearerAuth path serves ordinary operations. Cache only within the matching registry
host, repository, challenge service/scope and unexpired lifetime; do not send a cached bearer to
another repository/origin or reuse an expired token. A changed challenge gets its own exchange.
One authentication retry per request; no infinite 401 loop. Reject HTTP registry requests and
HTTP realms before adding credentials. Registry validation permits normalized hostname/optional
port only; no URL scheme/userinfo/path/query/fragment in the `registry` argument.

Code-review clarification of that cache contract: retain the latest issued token per normalized
origin/repository and its expiry. These APIs do not independently select realm/service/scope;
every fresh 401 exchanges the exact current challenge rather than answering it from an older
cache entry. Both full reviewers found this behavior compliant. Unused challenge metadata adds
no authority check and is removed; no second cache mechanism is required.

Disable automatic redirects in production HttpClient. Manifest/blob retrieval may explicitly
follow up to five HTTPS redirects for signed CDN downloads, stripping Authorization on any origin
change. Never negotiate redirected-origin authentication or attach registry credentials there.
Same-origin redirects retain only correctly scoped bearer authorization. Bounds and digest checks
apply to the final bytes. Token/credential verification does not follow redirects. Existing push
operations keep their public behavior; shared authentication must not add a second push route or
send credentials to cross-origin upload URLs. A required push deviation returns to Design.

### Failure and verification observations

| Failure | Owner / visible result | Meaningful observation |
|---|---|---|
| Digest/header/request/layer mismatch | OCI client / OciException before returning content | Deliberately change same-size bytes, header and requested digest independently |
| Oversized descriptor or dishonest stream | OCI client / bounded read failure | Reject declared oversize before blob request; terminate unknown-length stream at cap |
| Bad credential or unsupported/unsafe challenge | BearerAuth / OciAuthException | Assert no raw credential reaches HTTP realm/redirect/wrong origin; bad token is never anonymous success |
| Token scope/expiry/repeated 401 | BearerAuth / bounded exchange or explicit refusal | Two repositories/hosts/challenges and expired token; no bearer leakage or unbounded retry |
| HTTP interruption/cancellation | Existing HTTP boundary / named error or propagated cancellation | Cancel during bounded read; no partial PulledMission result |
| Publishing/restore failure | Supervisor / task remains open | Name workflow revision/run, package version/nuspec commit and exact restored consumer artifact |

Use existing unit/handler tests for targeted boundaries, existing integration tests for normal
expert behavior, and a disposable public-API consumer for real mission retrieval, credential
verification and Native AOT. Test callbacks/forged handlers are controlled evidence only.
Public/private GHCR compatibility requires actual observations, not the library's AOT attribute.
Use existing exported credentials without printing them; no new account or permission grant.

## Acceptance

All bounded product acceptance conditions were met; see [accepted evidence](phase-76.3-oci-integrity-auth_completed.md).
Documentation closure accompanies this record. Cloud acceptance remains open.

## Lookup

| Item | State |
|---|---|
| Supervisor design | LOCKED: full simplicity/ownership review PASS; [review evidence](phase-76.2-unified-cloud-run-contracts_completed.md#later-full-design-reviews) |
| Implementer plan | [Completed approved plan](phase-76.3-oci-integrity-auth-plan_completed.md); full simplicity/ownership PASS |
| Implementation | [OCI PR 5 merged](https://github.com/katasec/oci-client-dotnet/pull/5); both full code reviews PASS |
| Product tests/AOT/default evidence | [All required observations passed](phase-76.3-oci-integrity-auth_completed.md), including exact remote 0.5.0 package/source/hash and Native AOT public/private GHCR |
| Phase-wide Type-1 decisions | Pending; this prerequisite does not settle them |

Principles that changed decisions: reuse the existing authentication/manifest path; bounded
streaming and scoped tokens contain failures structurally; exact published/source inspection
prevents relying on a reverted API; library acceptance uses restored Native AOT/public APIs.
