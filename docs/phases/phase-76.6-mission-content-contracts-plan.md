# Phase 76.6 — Complete content contract implementer plan

**PLAN APPROVED2026-10-10 05:06:50UTC.** Both full current plan reviews PASS; supervisor
independently checked exact public shapes/seven paths, private immutable release, preserved
ABI/registry/dependency/authority and ordinary-target/default verification. Reviewed artifact SHA256
`b7908913167f5ba06b6f8850421f699d036ba3b63ec16dd5fa8aa66945cd0434`.
Seven-path implementation reviewed and merged in product PR20 on2026-10-10 05:25:03UTC.
Normal publication/default acceptance still require evidence. Native verification timing below is
historical and superseded by the operator's final-phase-delivery instruction.
Environment update: root started installed Docker Desktop05:06:23UTC; `docker info`
returned29.1.2/Docker Desktop05:06:34UTC. Full local and CI checks remain required; original
absence below records the planning baseline and grants no fixture skip.
Plan assignment05:00:36UTC,
result available05:03:39UTC; implementer observed05:01:11–05:02:31UTC. Baseline clean main
`e1c5b5bd661ccebaf05fe316d3e5fd512ef2c278`. Both released packages latest0.8.0;0.9.0 absent.
The [locked design and Done when](phase-76.6-mission-content-contracts.md) govern.

Supervisor transcription retains the complete seven-path plan. One verification clarification:
use the SDK's ordinary Native AOT target first, with no preemptive `AppleMinOSVersion` override.
The original suggested27.0 current-host override is not approved; if an actual warning/failure
occurs, preserve it and return to the supervisor before changing the verification path.
Observed host macOS27.0.1/SDK10.0.401. No warning suppression or compatibility claim.

## Files and exact public shapes

All seven product paths are in `/Users/ameerdeen/progs/forge-conversations`:

| File | Change |
|---|---|
| src/ForgeMission.Conversations.Contracts/MissionContentContracts.cs — new | Five inert values plus fixed limits, ownership comments |
| src/ForgeMission.Conversations.Contracts/ConversationContractsJsonContext.cs | Five registrations in existing context |
| src/ForgeMission.Conversations.Contracts/ForgeMission.Conversations.Contracts.csproj | Version0.8.0→0.9.0 only; no dependencies/settings change |
| src/ForgeMission.Conversations.Contracts/README.md | Values/limits/JSON/deferred authority and source link |
| tests/ForgeMission.ConversationHost.Tests/MissionContentContractsTests.cs — new | Meaningful generated-wire/negative/unchanged-shape coverage |
| eng/verify-conversations-packages.sh | Audit Contracts0.9.0/Presentation0.8.0 in existing verifier |
| .github/workflows/publish-conversations-packages.yml | Build/audit both; publish only Contracts0.9.0 |

```csharp
public enum MissionContentKind
{
    [JsonStringEnumMemberName("input")] Input = 0,
    [JsonStringEnumMemberName("output")] Output = 1,
}
public sealed record MissionContentReference(
    Guid ContentId, long ByteLength, string Sha256,
    string ContentType, MissionContentKind Kind);
public sealed record MissionNamedInputs(
    Dictionary<string, string> Strings,
    Dictionary<string, MissionContentReference> Artifacts);
public sealed record StageMissionContentChunkRequest(
    Guid ConversationId, Guid OwnerCommandId, string Slot,
    MissionContentReference Reference, int Index, int Count, byte[] Bytes);
public sealed record GetMissionContentRequest(Guid ConversationId, Guid ContentId);
public static class MissionContentLimits
{
    public const int MaxContentBytes = 100 * 1024 * 1024;
    public const int MaxInputCount = 32;
    public const int MaxInputBytes = 100 * 1024 * 1024;
    public const int MaxDescriptorBytes = 32 * 1024;
}
```

Namespace `ForgeMission.Conversations.Contracts`. No validation methods/custom constructors/side
effects. Existing camelCase/null omission/string enums unchanged; caller-authored dictionary keys;
Bytes uses normal STJ base64. No existing DTO/constructor/enum/active registry/Host Core pin/
Presentation/storage/route/consumer changes. UI/browser/visual/Desktop lifecycle N/A.

## Reuse and consumer inventory

| Need | Checked / choice |
|---|---|
| Binary descriptor | Existing ConversationBodyReference has int UTF-8 semantics; preserve it and add locked long/media/kind binary value |
| Named bag | No content/named-input types in owning source/tests; exact two-map record only |
| Chunk/query values | Existing text claim-check ingress differs; add prescribed binary DTOs without active registration/transport |
| JSON | Same generated context/JsonSerializable/JsonStringEnumMemberName as MissionHistoryRole; no second context/converter/reflection |
| Chunk size/IDs | Existing ConversationBodyLimits.ChunkBytes196608/ConversationDeterministicIds.Body; no new algorithm/constant/helper |
| Tests | Existing xUnit/generated metadata/assertions; coherent dedicated file, no framework/helper layer |
| Audit | Existing verify_package, private args become (package_id, version, assembly_name, expected_dependency?); external script interface stays two args |
| Publication | Existing workflow/artifact/merged guard/immutable/private visibility; no new publisher |

| Existing consumer | Pin; no upgrade for unused additive vocabulary |
|---|---|
| Runner | Contracts0.8.0 |
| Client Application/Transport/tests | Contracts0.7.0 |
| Platform API | Contracts0.7.0 |
| Desktop Presentation/tests | Contracts0.4.0; Presentation0.2.0 |
| Rooms ForgeUI | Presentation0.1.0 |
| Owning Host/tests | Contracts project reference; Core0.1.1 package stays |
| forge-mcl | No direct Conversations package references |

No active writer emits new values or supports new routes. Presentation stays0.8.0 and its single
Microsoft.AspNetCore.Components.Web10.0.0 dependency. All other source remains byte-identical.

## Sequence

1. Only after PLAN APPROVED: recheck clean intended main and create
   `adeen/phase-76-mission-content-contracts`; unique scratch evidence directory; inventory no secrets.
2. Add exact declarations and five generated registrations. Add focused tests described below.
3. Set Contracts0.9.0; README states descriptors grant no authority and do not validate integrity;
   later Host admission owns semantic validation/adoption.
4. Existing verifier resolves nupkg/snupkg/nuspec version from explicit expected-version private
   arg; call for Contracts0.9.0 and Presentation0.8.0. Retain README/license/DLL/PDB/forbidden
   payload/repository URL+commit/dependency audits.
5. Existing publisher tag trigger/exact guard becomes `forge-conversations-v0.9.0`; job names
   Contracts0.9.0. Keep dispatch/default-branch ancestry, pack/upload/audit both packages. Limit
   immutable check/push/private repository+version verification to Contracts0.9.0; remove Presentation
   push. Fail on existing version/non404 inventory/push/visibility errors. No overwrite/skip-duplicate.
6. Managed development/focused/full available/package/script checks; retain failures/environment.
7. Freeze stable commit; pack Release with ContinuousIntegrationBuild and exact RepositoryCommit.
   Scratch PackageReference-only consumer of branch-built normal0.9.0 in new isolated cache/local
   feed, no global cache mutation/prerelease propagation. This is controlled, not remote proof.
8. Fresh managed consumer; package/restored-byte hashes/source identity. The original per-increment
   native step is superseded by the operator's final-delivery-only AOT instruction.
9. Push/create/attach draft PR under the approved implementation assignment; frozen full diff/evidence
   for sequential full code reviews. Supervisor owns merge/normal publication/remote acceptance.

## Verification

Normal PowerShell profile inherits keys; existing nuget.config; no credential values in logs.

```text
dotnet restore ForgeMission.Conversations.slnx --configfile nuget.config
dotnet build ForgeMission.Conversations.slnx -c Debug --no-restore -warnaserror
dotnet test tests/ForgeMission.ConversationHost.Tests/ForgeMission.ConversationHost.Tests.csproj -c Debug --no-build --no-restore --filter "FullyQualifiedName~MissionContentContractsTests|FullyQualifiedName~ConversationContractsRoundTripTests|FullyQualifiedName~ConversationBodyContractsTests|FullyQualifiedName~ConversationContractsBoundaryTests"
dotnet test ForgeMission.Conversations.slnx -c Debug --no-build --no-restore
dotnet build ForgeMission.Conversations.slnx -c Release --no-restore -warnaserror
dotnet test ForgeMission.Conversations.slnx -c Release --no-build --no-restore
```

The original local Docker absence was recovered before implementation by starting the installed
Docker Desktop. Full local Debug/Release and normal Ubuntu PR verification use real Azurite;
never skip/replace the fixture. No tests/fixtures change beyond the approved new file.

| Generated-wire/compatibility test | Observation |
|---|---|
| Enum | Exact input/output strings and0/1 ordinals, camelCase fields |
| Reference | Exact GUID/hash/media/long4294967297 (wire capacity only, not admission eligibility) |
| Named maps | Multiple/case-preserved keys/Unicode/empty strings/exact refs; inspect contents, not record equality of maps/arrays |
| Chunk/query | Owner/slot/index/count/ref/query IDs; exact00/7F/80/FF and base64 |
| Invalid JSON | Mutate valid chunk base64/reference kind archive; JsonException, no semantic validator inferred |
| Retained shapes | Exact existing SubmitMissionTurnRequest(ConversationId,CommandId,Text), SubmitMissionTurnIngress(ConversationId,CommandId,Text body ref), ConversationBodyReference(BodyId,int Utf8Bytes,Sha256) JSON |
| Active registries/bounds | Commands17/queries9 exclude new operations; fixed limits/existing chunk size; existing enum/null/body/boundary tests unchanged |

Pack both existing projects to scratch with Release/ContinuousIntegrationBuild/exact RepositoryCommit;
run `bash eng/verify-conversations-packages.sh <directory> <SHA>`, `bash -n` and `git diff --check`.
Contracts0.9.0 has no dependencies; Presentation0.8.0 retains its one dependency and symbols/provenance.
Disposable package mutations must fail missing package/symbol, wrong version/commit or extra Contracts
dependency. Never write the real feed during controlled checks.

Parse workflow with installed Ruby/Psych (no repo dependency). Inspect PR cannot publish;
dispatch/tag requires verify+merged ref; exact new tag/old-tag rejection; both audits retained;
Contracts0.9.0 is sole immutable/push target; private repository+exactly-one-version still required;
no Presentation push/skip-duplicate/permission expansion/new workflow. Exercise embedded predicates
with scratch JSON/intercepted commands: existing/unrelated versions, non404 API failure, wrong
visibility/repository, missing/duplicate version. These remain controlled script checks.

Historical approved native commands below retain what was actually attempted; the operator's
subsequent AOT-at-final-delivery instruction supersedes repeated native checks for this increment.
Final controlled consumer uses only PackageReferenceContracts0.9.0, reflection serialization disabled,
no project/DLL reference/custom context. Branch feed plus normal feeds, NEW empty NUGET_PACKAGES.
Exercise all five generated types/bytes/long/maps/limits/invalid JSON/representative old shapes;
managed run then `dotnet publish <project> -c Release -r osx-arm64 -p:PublishAot=true -warnaserror -o <output>`
and actual executable. Retain raw compiler/linker output, no suppression. Report actual failure
before altering the target or default dependency. No old-macOS support claim. After the observed
Mac linker failures, the supervisor approved the official ordinary-target Linux ARM64 AOT image
for both native checks; [historical adjustment and operator correction](phase-76.6-mission-content-contracts_completed.md#operator-correction-native-verification-timing).

Default acceptance: supervisor rechecks0.9.0 absent, dispatches existing workflow from merged main,
then NEW empty cache/ordinary authenticated feeds only/no local feed/sibling/DLL substitution.
Inspect actual private visibility/repository/version and merged-source nuspec/bytes, run fresh
PackageReference-only managed consumer with zero warnings. Native AOT is deferred to final phase
delivery by the operator's explicit clarification. A tag is
supported but not needed. No hosted/deployment behavior claimed. All four spoke Done when apply.

## Principles that changed choices

| Implementer rule | Choice |
|---|---|
| No NIH / duplicate paths | Same context/chunk convention/ID/verifier/publisher |
| Minimum / no abstractions | Seven paths/plain values/direct tests; no consumer/Presentation release |
| Stay in scope | Host alone later validates/authorizes/stores/dispatches |
| Verified means done | Branch proof controlled; real fresh normal-feed managed consumer mandatory; AOT at final phase delivery |
| Outline / small / top-down | Coherent vocabulary/tests, entrypoints before needed helpers |
| Explicit errors | JSON/audit/immutable/private errors preserved |
| Shallow / separate effects | Pure values, release operations in existing owners |
| Zero warnings | Managed raw output now; final phase native gate after everything works |
| Real extraction / complexity | One private version arg, no selection framework; ≤15/prefer≤10 |

Open questions: none. Full integration requires existing CI Docker. Authority/data/credential/
deployment boundaries unchanged. Next bounded task is Host content storage/admission after acceptance.
