# Phase 76.3 — OCI implementer plan

**Status:** Historical approved plan, executed and accepted on 2026-10-09. Full design/plan/code
reviews, normal merged-main publication and restored-package acceptance passed;
[completion evidence](phase-76.3-oci-integrity-auth_completed.md). The plan below records the
approved sequence and assumptions at handoff; it is not outstanding work.
[Locked design](phase-76.3-oci-integrity-auth.md).

## Files

All product paths are relative to `/Users/ameerdeen/progs/oci-client-dotnet`.

| File | Change and purpose |
|---|---|
| `src/Katasec.OciClient/OciClient.cs` | Add exact PullMissionWithDigestAsync and VerifyRegistryCredentialAsync APIs. Existing mission pull delegates to verified retrieval. Verify shared manifest bytes, requested digest/header before parsing; validate mission schema/descriptors and bounded bundle bytes; dispose requests/responses consistently. |
| `src/Katasec.OciClient/OciModels.cs` | Add PulledMission(byte[] Bundle, string ManifestDigest, string LayerDigest, long LayerByteLength). Extend TokenResponse with expiry metadata; preserve existing models/signatures. |
| `src/Katasec.OciClient/BearerAuth.cs` | Repair shared authority/challenge/query parsing, bounded token exchange, scoped/expiring cache, retry and download-redirect handling. Verification uses this owner and constructor credential. |
| `src/Katasec.OciClient/BoundedResponseBody.cs` | One internal bounded stream reader shared by manifest, verified bundle and token reads; no public transport config or new network client. |
| `tests/Katasec.OciClient.Tests/OciPullDigestTests.cs` | Replace incorrect-header-wins expectations; retain single-request/expert-wrapper coverage; add requested/header mismatch regressions. |
| `tests/Katasec.OciClient.Tests/OciMissionPullTests.cs` | Verify exact API, schema/descriptors, integrity/bounds, cancellation and redirects using existing internal handler seam. |
| `tests/Katasec.OciClient.Tests/BearerAuthTests.cs` | Verify credentials, realm/query authority, scope/lifetime, bounded retries, redirect secrecy and request/content ownership; ordinary push regressions. |
| `README.md` | Document verified mission retrieval, verification, bounds and explicit auth limitations. |

No MissionBundle, other Forge repo, extraction, public handler config, workflow or project-version
property changes. UI N/A.

## Reuse

| Need | Existing thing checked | Choice |
|---|---|---|
| Mission result | PulledExpert | Same immutable result-record pattern, plus required bundle digest/length |
| Manifest identity | PullManifestWithDigestAsync, ManifestDigest, ComputeDigest, IsSha256Digest | Correct the shared path; compute once, compare requested/header identity, no separate mission fetch |
| Mission retrieval | PullMissionAsync, Forge constants/models/STJ context | Delegate old API; validate schema/kind/artifact/config media types and one bundle layer |
| Bounded content | ReadAsByteArrayAsync/ReadAsStringAsync | Existing calls buffer without a cap. One internal reader rejects excessive declared length, stops at limit plus one, preserves cancellation and returns no partial result |
| Authentication | BearerAuth.SendAsync/FetchTokenAsync/ParseChallenge/Clone | Repair current owner; comma splitting and one unscoped token are insufficient |
| Expiry serialization | TokenResponse/OciJsonContext | Extend existing generated model, retain source generation |
| Controlled tests | Internal OciClient(HttpMessageHandler, credential) | Reuse; test handlers capture headers/authority and streams/disposal, no public injection knob |
| Publication | Existing publish.yml, workflow ID 296477711 | Normal merged-main workflow_dispatch; latest observed 0.4.0, new 0.5.0 after final collision check |

## Sequence

1. After explicit approval, reconfirm clean main at
   `565d3e2a7a87d95b05c7dd8261834c843f467f78`, create `codex/phase76-oci-integrity-auth`,
   and record managed baseline.
2. Introduce bounded response reads and correct shared manifest digest handling. Malformed or
   mismatched requested/header digests fail before blob retrieval; absent header uses computed
   digest. Malformed manifest becomes bounded OciException without response-body echo.
3. Add exact mission result/API. Validate positive budget/schema/descriptors before fetch; bound
   selected layer by declared size/caller budget and verify actual size/SHA-256. Old API returns
   this API's Bundle.
4. Repair existing authentication owner: normalize host/port and reject scheme/userinfo/path/
   query/fragment. Production HttpClient disables auto redirects, uses ResponseHeadersRead.
   Retain origin/repository context; cache the latest challenged token and expiry per origin/repository,
   with fresh exchange for every new realm/service/scope challenge (code-review clarification). No
   token crosses repository/origin; changed challenge exchanges again. Parse quoted challenge
   parameters, reject duplicates/ambiguity, require HTTPS realm without userinfo/fragment.
   Preserve unrelated realm query entries; existing service/scope entries agree exactly and occur
   once. Basic token:<constructor credential> goes only to the validated realm. Token response
   max 64 KiB, nonempty token/access_token, reported expiry or default 60 seconds. One auth retry
   per logical request. Unsupported/malformed challenge, rejected credential or repeated 401
   yields OciAuthException without bodies/secrets; never anonymous fallback after rejection.
5. Verify credential via anonymous `/v2/`, bypass cached authorization, require supported Bearer
   challenge, exchange constructor credential, retry same registry request and require success.
   Anonymous 200, missing credential, Basic challenge or either redirect fails explicitly.
6. Downloads follow at most five HTTPS redirects, disposing intermediate responses, stripping
   Authorization across origins, never negotiating foreign-origin authentication, preserving
   correctly scoped authorization only at original origin. Token/verify exchanges follow none.
7. Preserve push through same owner. Relative upload locations resolve as before; foreign upload
   URLs receive no cached bearer/constructor credential and cannot trigger exchange there.
   Meaningful HEAD/POST/PUT authentication/body regressions; real push deviation returns to Design.
8. Run focused/full managed checks; return diff/evidence for sequential simplicity/style and
   ownership review; fix approved findings.
9. After supervisor readiness approval, commit/push/PR/merge, publish 0.5.0 through existing
   workflow from exact merged main revision.
10. Supervisor restores exact package into disposable public-API Native AOT consumer, checks
    package/source identity and real GHCR acceptance, records timing/evidence, closes docs and
    ends every touched repo on clean/current main.

## Verification

| Layer | Commands / observations |
|---|---|
| Baseline/build | `dotnet restore Katasec.OciClient.slnx`; `dotnet build Katasec.OciClient.slnx -c Release --no-restore -warnaserror` |
| Focused managed | `dotnet test Katasec.OciClient.slnx -c Release --no-build --filter "FullyQualifiedName~OciPullDigestTests\|FullyQualifiedName~OciMissionPullTests\|FullyQualifiedName~BearerAuthTests"` |
| Full deterministic | `dotnet test Katasec.OciClient.slnx -c Release --no-build --filter "Category!=Integration"`; preserve classification/schema/bundling/expert behavior |
| Existing real integration | Existing Integration category using exported credential without printing it; includes expert fixtures, digest re-resolution and PushAndPull_RoundTrip_ContentMatches. Missing authorization recorded as observed limitation; unexpected regression stays open |
| Integrity negatives | Independently wrong header/request/same-size layer; malformed digests, wrong/missing schema/kind/media types, null/multiple layers, negative/oversized descriptors, truncated/excessive streams. Reject descriptor before blob request |
| Streaming negatives | Unknown-length cap, dishonest content length, cancellation/disposal/no partial result; 1 MiB manifest, caller/default bundle, 64 KiB token |
| Authority negatives | Unsafe registry/realm/query/challenge duplicates; optional scope; empty/malformed token, valid/invalid credential, anonymous 200 verify refusal, expiry, two repositories/hosts/scopes, repeated 401. Exact request counts/no credentials at wrong origins |
| Redirect/content negatives | Signed HTTPS CDN, same-origin auth, foreign stripping/no challenge exchange, HTTP downgrade, missing Location, sixth redirect, token/verify redirects, intermediate disposal. Push retry preserves bytes without premature disposal |
| Branch Native AOT | Disposable consumer referencing branch-built package; `dotnet publish -c Release -r osx-arm64 -p:PublishAot=true -warnaserror`; public-constructor/API smoke. Controlled evidence, not default acceptance |
| Normal publication | `gh workflow run publish.yml --repo katasec/oci-client-dotnet --ref main -f version=0.5.0`; record run/head/package/release. Inspect restored nuspec repository commit and actual API |
| Restored-package default | Separate disposable consumer restores exact 0.5.0 from normal GitHub Packages feed; no project reference/substituted DLL. Publish/run Native AOT zero warnings. Public constructor pulls `ghcr.io/katasec/forge-mission-assistant:0.1.0`, then returned digest; independently verify bundle hash/length and identical bytes |
| Real auth/private | Existing credential verifies GHCR; explicit invalid credential fails. Attempt authorized private expert fixture such as katasec/kubernetes-architect:0.1.0; actual limitations recorded without claiming private compatibility |

Supervisor checks all Done when conditions against actual observations. Cloud execution/CLI
acceptance stays open.

## Principles that changed a decision

| Implementer rule | Choice |
|---|---|
| 1 No NIH | Shared manifest/auth paths, internal handler seam and source generation |
| 2 No duplicate paths | Old pull delegates; verification shares exchange ownership |
| 3 Minimum needed | Leave extraction, CLI/config/cloud/workflow unchanged |
| 4 No speculative abstractions | One bounded reader; no framework/interface/public transport knob |
| 6 Verified means done | Restored-package public-constructor AOT/real GHCR after publication |
| 7–9 Progressive disclosure | Public flow first; coherent validation/read/exchange helpers below |
| 10 Explicit errors | Named OCI/auth failures, cancellation preserved, no anonymous fallback |
| 11–12 Shallow flow/effects | Validation separate from network, early refusal, explicit disposal |
| 13 Zero warnings | Managed/final AOT warnings as errors |
| 14–15 Real extraction/complexity | Extract bounds/authority/challenge/redirect boundaries; check changed functions |

## Open questions and assumptions

Implementer identified no unresolved design question. Verify rather than assume: 0.5.0 remains
unused at publication; public fixture/tag remains available; existing credentials permit auth
and some private fixtures. A collision returns to supervisor. Missing private permissions are an
allowed observed limitation. No workflow/source changes outside bounded OCI repo authorized.

## Supervisor source/dependency preflight

These are lower-layer protocol/metadata probes, not new-library acceptance:

| Observation on 2026-10-09 | Result |
|---|---|
| PowerShell Invoke-WebRequest `https://ghcr.io/v2/` | 401 Bearer realm=https://ghcr.io/token, service=ghcr.io, scope=repository:user/image:pull |
| Deliberately invalid credential at token realm | 403; no credential/default persistence performed |
| Anonymous manifest GET for forge-mission-assistant:0.1.0 | 200; manifest sha256:4ba3278af7b9400e28ff20c559a4274b6546c03571e3e248a67ec03eabcddbf9; Forge mission artifact/config media types, exactly one bundle layer |
| That bundle descriptor | 17,408 bytes; sha256:30dba78b8bbf3e13b6aea645a969de8eab3664de8e65d3ddb14b2f108eb17c78 |
| Bounded PowerShell blob GET and independent ComputeHash | 200, 17,408 bytes; computed SHA-256 matches the descriptor. HEAD 200, no redirect. Earlier unbounded probe was canceled without a result; no continuing outage established |
| Local runtime / release metadata | dotnet 10.0.401; gh release list latest 0.4.0 |
