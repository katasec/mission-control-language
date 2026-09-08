using System.Security.Cryptography;
using System.Text;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Resolution;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;

namespace ForgeMission.Application;

// Project owns authored mission identity and its one manifest transaction.  This service has no
// transport, provider, conversation, or capability dependency: 45.2 supplies execution later.
internal interface IMissionVersionService
{
    Task<ProjectMissionDefinition> CreateDraftAsync(string home, string name, string definitionText, MissionHandsProfile profile, CancellationToken ct);
    Task<MissionDraft> CreateNextDraftAsync(string home, Guid missionId, string definitionText, MissionHandsProfile profile, CancellationToken ct);
    Task<ProjectMissionDefinition> SaveDraftAsync(string home, Guid missionId, Guid draftId, int revision, string definitionText, MissionHandsProfile profile, CancellationToken ct);
    Task<MissionVersion> PromoteCandidateAsync(string home, Guid missionId, Guid draftId, int revision, CancellationToken ct);
    Task<MissionVersion> SaveCandidateAsync(string home, Guid missionId, Guid missionVersionId, int candidateRevision, string definitionText, CancellationToken ct);
    Task<MissionVersion> SaveCaseAsync(string home, Guid missionId, Guid missionVersionId, EvaluationCase evaluationCase, CancellationToken ct);
    Task<MissionVersion> DeleteCaseAsync(string home, Guid missionId, Guid missionVersionId, Guid evaluationCaseId, CancellationToken ct);
    Task<IReadOnlyList<EvaluationResult>> ListResultsAsync(string home, Guid missionId, Guid missionVersionId, CancellationToken ct);
    Task<EvaluationResult> RecordCompletionAsync(string home, Guid missionId, Guid missionVersionId, Guid evaluationCaseId,
        int candidateRevision, string definitionHash, EvaluationOutcome observedOutcome, string observedOutputSummary,
        EvaluationTraceOrigin? traceOrigin, CancellationToken ct);
    Task<MissionVersion> PublishAsync(string home, Guid missionId, Guid missionVersionId, CancellationToken ct);
}

internal sealed class MissionVersionService(ProjectService projects) : IMissionVersionService
{
    internal IReadOnlyList<ApprovedMissionVersion> ListApproved(string home, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Definitions(projects.ReadForHome(home).Manifest)
            .Where(definition => definition.ActiveApprovedVersionId is not null)
            .Select(definition => new { Definition = definition, Version = RequireVersion(definition, definition.ActiveApprovedVersionId!.Value) })
            .Where(item => item.Version.State == MissionVersionState.Approved)
            .Select(item => new ApprovedMissionVersion(item.Definition.MissionId, item.Definition.Name,
                new DurableMissionLaunch(item.Version.MissionVersionId, item.Version.VersionNumber, item.Version.DefinitionHash,
                    item.Version.DefinitionText, item.Version.CapabilityProfile, item.Version.Package)))
            .ToArray();
    }

    internal ApprovedMissionVersion FindApproved(string home, Guid missionId, Guid versionId, CancellationToken ct) =>
        ListApproved(home, ct).SingleOrDefault(item => item.MissionId == missionId && item.Launch.MissionVersionId == versionId)
        ?? throw Conflict("That mission version is no longer approved.");
    public async Task<ProjectMissionDefinition> CreateDraftAsync(string home, string name, string definitionText, MissionHandsProfile profile, CancellationToken ct)
    {
        var created = await projects.UpdateMissionDefinitionsAsync(home, (manifest, _) =>
        {
            var definitions = Definitions(manifest);
            if (definitions.Any(item => string.Equals(item.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw Conflict("A Project mission with that name already exists.");
            var now = DateTimeOffset.UtcNow;
            var draft = NewDraft(definitionText, profile, now);
            var definition = new ProjectMissionDefinition(Guid.NewGuid(), RequiredName(name), null, draft, []);
            return (manifest with { MissionDefinitions = [.. definitions, definition] }, definition);
        }, ct);
        return created.Value;
    }

    public async Task<ProjectMissionDefinition> SaveDraftAsync(string home, Guid missionId, Guid draftId, int revision, string definitionText, MissionHandsProfile profile, CancellationToken ct)
    {
        var saved = await projects.UpdateMissionDefinitionsAsync(home, (manifest, _) =>
        {
            var definition = RequireDefinition(manifest, missionId);
            if (definition.Draft is not { } draft || draft.DraftId != draftId || draft.Revision != revision)
                throw Changed("The mission draft changed. Refresh before saving.");
            var replacement = NewDraft(definitionText, profile, DateTimeOffset.UtcNow, draft.DraftId, draft.Revision + 1);
            var updated = definition with { Draft = replacement };
            return (Replace(manifest, updated), updated);
        }, ct);
        return saved.Value;
    }

    public async Task<MissionDraft> CreateNextDraftAsync(string home, Guid missionId, string definitionText, MissionHandsProfile profile, CancellationToken ct)
    {
        var created = await projects.UpdateMissionDefinitionsAsync(home, (manifest, _) =>
        {
            var definition = RequireDefinition(manifest, missionId);
            if (definition.Draft is not null) throw Conflict("This mission already has a draft.");
            EnsureLatestVersionIsActiveApproved(definition);
            var draft = NewDraft(definitionText, profile, DateTimeOffset.UtcNow);
            return (Replace(manifest, definition with { Draft = draft }), draft);
        }, ct);
        return created.Value;
    }

    public async Task<MissionVersion> PromoteCandidateAsync(string home, Guid missionId, Guid draftId, int revision, CancellationToken ct)
    {
        var promoted = await projects.UpdateMissionDefinitionsAsync(home, (manifest, root) =>
        {
            var definition = RequireDefinition(manifest, missionId);
            if (definition.Draft is not { } draft || draft.DraftId != draftId || draft.Revision != revision)
                throw Changed("The mission draft changed. Refresh before promoting.");
            EnsureLatestVersionIsActiveApproved(definition);
            var package = MissionPackageBuilder.Build(root, manifest, draft.DefinitionText);
            var versions = Versions(definition);
            var version = new MissionVersion(Guid.NewGuid(), versions.Length + 1, MissionVersionState.Candidate,
                draft.DefinitionText, draft.DefinitionHash, draft.CapabilityProfile, package, definition.ActiveApprovedVersionId, 1,
                DateTimeOffset.UtcNow, null, null, [], []);
            var updated = definition with { Draft = null, Versions = [.. versions, version] };
            return (Replace(manifest, updated), version);
        }, ct);
        return promoted.Value;
    }

    public async Task<MissionVersion> SaveCandidateAsync(string home, Guid missionId, Guid missionVersionId, int candidateRevision, string definitionText, CancellationToken ct)
    {
        var saved = await projects.UpdateMissionDefinitionsAsync(home, (manifest, root) =>
        {
            var definition = RequireDefinition(manifest, missionId);
            var version = RequireVersion(definition, missionVersionId);
            if (version.State != MissionVersionState.Candidate || version.CandidateRevision != candidateRevision)
                throw Changed("The candidate changed. Refresh before saving.");
            var hash = DefinitionHash(definitionText);
            var package = MissionPackageBuilder.Build(root, manifest, definitionText);
            var updated = version with
            {
                DefinitionText = RequiredDefinition(definitionText), DefinitionHash = hash, Package = package,
                CandidateRevision = version.CandidateRevision + 1, EvaluationResults = [], EvaluatedAtUtc = null,
            };
            return (Replace(manifest, definition with { Versions = Replace(Versions(definition), updated) }), updated);
        }, ct);
        return saved.Value;
    }

    public async Task<MissionVersion> SaveCaseAsync(string home, Guid missionId, Guid missionVersionId, EvaluationCase evaluationCase, CancellationToken ct)
    {
        var saved = await projects.UpdateMissionDefinitionsAsync(home, (manifest, _) =>
        {
            var definition = RequireDefinition(manifest, missionId);
            var version = RequireCandidate(definition, missionVersionId);
            ValidateCase(evaluationCase);
            var cases = Cases(version);
            if (cases.Any(item => item.EvaluationCaseId == evaluationCase.EvaluationCaseId))
                throw Conflict("That evaluation case already exists.");
            var updated = version with { EvaluationCases = [.. cases, evaluationCase], EvaluationResults = [], EvaluatedAtUtc = null };
            return (Replace(manifest, definition with { Versions = Replace(Versions(definition), updated) }), updated);
        }, ct);
        return saved.Value;
    }

    public async Task<MissionVersion> DeleteCaseAsync(string home, Guid missionId, Guid missionVersionId, Guid evaluationCaseId, CancellationToken ct)
    {
        var saved = await projects.UpdateMissionDefinitionsAsync(home, (manifest, _) =>
        {
            var definition = RequireDefinition(manifest, missionId);
            var version = RequireCandidate(definition, missionVersionId);
            var remaining = Cases(version).Where(item => item.EvaluationCaseId != evaluationCaseId).ToArray();
            if (remaining.Length == Cases(version).Length) throw Conflict("That evaluation case no longer exists.");
            var updated = version with { EvaluationCases = remaining, EvaluationResults = [], EvaluatedAtUtc = null };
            return (Replace(manifest, definition with { Versions = Replace(Versions(definition), updated) }), updated);
        }, ct);
        return saved.Value;
    }

    public Task<IReadOnlyList<EvaluationResult>> ListResultsAsync(string home, Guid missionId, Guid missionVersionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var version = RequireVersion(RequireDefinition(projects.ReadForHome(home).Manifest, missionId), missionVersionId);
        return Task.FromResult<IReadOnlyList<EvaluationResult>>(Results(version));
    }

    /// <summary>Resolves exactly the current Approved version for a Mission conversation. This is
    /// a Project read only; Missions owns the later Host command and no Project path leaves here.</summary>
    public Task<MissionLaunchProvenance> ResolveActiveApprovedLaunchAsync(string home, Guid missionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var manifest = projects.ReadForHome(home).Manifest;
        var definition = RequireDefinition(manifest, missionId);
        if (definition.ActiveApprovedVersionId is not { } approvedId)
            throw Conflict("This mission has no active approved version.");
        var version = RequireVersion(definition, approvedId);
        if (version.State != MissionVersionState.Approved)
            throw Conflict("This mission's active version is not approved.");
        var launch = new DurableMissionLaunch(version.MissionVersionId, version.VersionNumber, version.DefinitionHash,
            version.DefinitionText, version.CapabilityProfile, version.Package);
        return Task.FromResult(new MissionLaunchProvenance(manifest.ProjectId, missionId, launch));
    }

    public Guid ReadProjectId(string home, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return projects.ReadForHome(home).Manifest.ProjectId;
    }

    public async Task<EvaluationResult> RecordCompletionAsync(string home, Guid missionId, Guid missionVersionId, Guid evaluationCaseId,
        int candidateRevision, string definitionHash, EvaluationOutcome observedOutcome, string observedOutputSummary,
        EvaluationTraceOrigin? traceOrigin, CancellationToken ct)
    {
        var recorded = await projects.UpdateMissionDefinitionsAsync(home, (manifest, _) =>
        {
            var definition = RequireDefinition(manifest, missionId);
            var version = RequireCandidate(definition, missionVersionId);
            var evaluationCase = Cases(version).SingleOrDefault(item => item.EvaluationCaseId == evaluationCaseId)
                ?? throw Conflict("That evaluation case no longer exists.");
            if (version.CandidateRevision != candidateRevision || !string.Equals(version.DefinitionHash, definitionHash, StringComparison.Ordinal))
                throw Changed("The candidate changed before evaluation completed.");
            var state = Matches(evaluationCase, observedOutcome, observedOutputSummary) ? EvaluationResultState.Passed : EvaluationResultState.Failed;
            var result = new EvaluationResult(Guid.NewGuid(), evaluationCaseId, missionVersionId, candidateRevision, definitionHash,
                observedOutcome, RequiredSummary(observedOutputSummary), state, traceOrigin, DateTimeOffset.UtcNow);
            var results = Results(version).Where(item => item.EvaluationCaseId != evaluationCaseId).Append(result).ToArray();
            var allPass = Cases(version).Length > 0 && Cases(version).All(item => results.Any(result =>
                result.EvaluationCaseId == item.EvaluationCaseId && result.CandidateRevision == version.CandidateRevision &&
                string.Equals(result.DefinitionHash, version.DefinitionHash, StringComparison.Ordinal) && result.State == EvaluationResultState.Passed));
            var updated = version with { EvaluationResults = results, State = allPass ? MissionVersionState.Evaluated : MissionVersionState.Candidate,
                EvaluatedAtUtc = allPass ? DateTimeOffset.UtcNow : null };
            return (Replace(manifest, definition with { Versions = Replace(Versions(definition), updated) }), result);
        }, ct);
        return recorded.Value;
    }

    /// <summary>Writes the sole mutable Pending fact before the Missions owner contacts Host.
    /// Repeating an already-pending case returns that same identity; it never creates a second
    /// evaluation intent.</summary>
    public async Task<PendingEvaluationAdmission> CreatePendingEvaluationAsync(string home, Guid missionId, Guid missionVersionId,
        Guid evaluationCaseId, CancellationToken ct)
    {
        var pending = await projects.UpdateMissionDefinitionsAsync(home, (manifest, _) =>
        {
            var definition = RequireDefinition(manifest, missionId);
            var version = RequireCandidate(definition, missionVersionId);
            var evaluationCase = Cases(version).SingleOrDefault(item => item.EvaluationCaseId == evaluationCaseId)
                ?? throw Conflict("That evaluation case no longer exists.");
            var existing = Results(version).SingleOrDefault(item => item.EvaluationCaseId == evaluationCaseId &&
                item.CandidateRevision == version.CandidateRevision && string.Equals(item.DefinitionHash, version.DefinitionHash, StringComparison.Ordinal));
            var result = existing is { State: EvaluationResultState.Pending }
                ? existing
                : new EvaluationResult(Guid.NewGuid(), evaluationCaseId, missionVersionId, version.CandidateRevision,
                    version.DefinitionHash, null, null, EvaluationResultState.Pending, null, null);
            var results = existing is null ? [.. Results(version), result] : Results(version).Select(item =>
                item.EvaluationCaseId == evaluationCaseId ? result : item).ToArray();
            var updated = version with { EvaluationResults = results, EvaluatedAtUtc = null };
            var admission = new PendingEvaluationAdmission(manifest.ProjectId, missionId, updated, evaluationCase, result);
            return (Replace(manifest, definition with { Versions = Replace(Versions(definition), updated) }), admission);
        }, ct);
        return pending.Value;
    }

    public Task<PendingEvaluationAdmission> ReadPendingEvaluationAsync(string home, Guid missionId, Guid missionVersionId,
        Guid evaluationCaseId, Guid evaluationResultId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var manifest = projects.ReadForHome(home).Manifest;
        var definition = RequireDefinition(manifest, missionId);
        var version = RequireCandidate(definition, missionVersionId);
        var evaluationCase = Cases(version).SingleOrDefault(item => item.EvaluationCaseId == evaluationCaseId)
            ?? throw Conflict("That evaluation case no longer exists.");
        var result = Results(version).SingleOrDefault(item => item.EvaluationResultId == evaluationResultId && item.State == EvaluationResultState.Pending)
            ?? throw Changed("The pending evaluation result no longer exists.");
        return Task.FromResult(new PendingEvaluationAdmission(manifest.ProjectId, missionId, version, evaluationCase, result));
    }

    /// <summary>Reconciles exactly the pending identity created before Host admission. A stale
    /// candidate or a duplicate/foreign completion is a typed no-write conflict.</summary>
    public async Task<EvaluationResult> ReconcilePendingCompletionAsync(string home, Guid missionId, Guid missionVersionId,
        Guid evaluationCaseId, Guid evaluationResultId, int candidateRevision, string definitionHash,
        EvaluationOutcome observedOutcome, string observedOutputSummary, EvaluationTraceOrigin? traceOrigin, CancellationToken ct)
    {
        var recorded = await projects.UpdateMissionDefinitionsAsync(home, (manifest, _) =>
        {
            var definition = RequireDefinition(manifest, missionId);
            var version = RequireCandidate(definition, missionVersionId);
            var evaluationCase = Cases(version).SingleOrDefault(item => item.EvaluationCaseId == evaluationCaseId)
                ?? throw Conflict("That evaluation case no longer exists.");
            if (version.CandidateRevision != candidateRevision || !string.Equals(version.DefinitionHash, definitionHash, StringComparison.Ordinal))
                throw Changed("The candidate changed before evaluation completed.");
            var pending = Results(version).SingleOrDefault(item => item.EvaluationResultId == evaluationResultId)
                ?? throw Changed("The evaluation result no longer exists.");
            if (pending.State != EvaluationResultState.Pending || pending.EvaluationCaseId != evaluationCaseId ||
                pending.CandidateRevision != candidateRevision || !string.Equals(pending.DefinitionHash, definitionHash, StringComparison.Ordinal))
                throw Changed("The evaluation result changed before completion.");
            var state = Matches(evaluationCase, observedOutcome, observedOutputSummary) ? EvaluationResultState.Passed : EvaluationResultState.Failed;
            var result = pending with { ObservedOutcome = observedOutcome, ObservedOutputSummary = RequiredSummary(observedOutputSummary),
                State = state, TraceOrigin = traceOrigin, CompletedAtUtc = DateTimeOffset.UtcNow };
            var results = Replace(Results(version), pending, result);
            var allPass = Cases(version).Length > 0 && Cases(version).All(item => results.Any(candidate =>
                candidate.EvaluationCaseId == item.EvaluationCaseId && candidate.CandidateRevision == version.CandidateRevision &&
                string.Equals(candidate.DefinitionHash, version.DefinitionHash, StringComparison.Ordinal) && candidate.State == EvaluationResultState.Passed));
            var updated = version with { EvaluationResults = results, State = allPass ? MissionVersionState.Evaluated : MissionVersionState.Candidate,
                EvaluatedAtUtc = allPass ? DateTimeOffset.UtcNow : null };
            return (Replace(manifest, definition with { Versions = Replace(Versions(definition), updated) }), result);
        }, ct);
        return recorded.Value;
    }

    public async Task<MissionVersion> PublishAsync(string home, Guid missionId, Guid missionVersionId, CancellationToken ct)
    {
        var published = await projects.UpdateMissionDefinitionsAsync(home, (manifest, _) =>
        {
            var definition = RequireDefinition(manifest, missionId);
            var version = RequireVersion(definition, missionVersionId);
            if (version.State != MissionVersionState.Evaluated || !AllPassed(version))
                throw new ProjectOperationException(ProjectOperationErrorCode.PublishConflict, "This candidate has not passed its current evaluation cases.");
            var now = DateTimeOffset.UtcNow;
            var approved = version with { State = MissionVersionState.Approved, ApprovedAtUtc = now };
            var versions = Versions(definition).Select(item => item.MissionVersionId == approved.MissionVersionId ? approved :
                item.State == MissionVersionState.Approved ? item with { State = MissionVersionState.Superseded } : item).ToArray();
            var updated = definition with { ActiveApprovedVersionId = approved.MissionVersionId, Versions = versions };
            return (Replace(manifest, updated), approved);
        }, ct);
        return published.Value;
    }

    private static MissionDraft NewDraft(string text, MissionHandsProfile profile, DateTimeOffset now, Guid? id = null, int revision = 1) =>
        new(id ?? Guid.NewGuid(), RequiredDefinition(text), DefinitionHash(text), RequiredProfile(profile), revision, now);
    private static ProjectMissionDefinition RequireDefinition(ProjectManifest manifest, Guid id) =>
        Definitions(manifest).SingleOrDefault(item => item.MissionId == id) ?? throw Conflict("That Project mission no longer exists.");
    private static MissionVersion RequireVersion(ProjectMissionDefinition definition, Guid id) =>
        Versions(definition).SingleOrDefault(item => item.MissionVersionId == id) ?? throw Conflict("That mission version no longer exists.");
    private static MissionVersion RequireCandidate(ProjectMissionDefinition definition, Guid id)
    {
        var version = RequireVersion(definition, id);
        if (version.State != MissionVersionState.Candidate) throw Conflict("Only a candidate version can be changed.");
        return version;
    }
    private static void EnsureLatestVersionIsActiveApproved(ProjectMissionDefinition definition)
    {
        var versions = Versions(definition);
        if (versions.Length == 0) return;
        var latest = versions.MaxBy(version => version.VersionNumber)!;
        if (latest.State != MissionVersionState.Approved || definition.ActiveApprovedVersionId != latest.MissionVersionId)
            throw Conflict("A new draft can only follow this mission's current active approved version.");
    }
    private static ProjectManifest Replace(ProjectManifest manifest, ProjectMissionDefinition replacement) => manifest with
    {
        MissionDefinitions = Definitions(manifest).Select(item => item.MissionId == replacement.MissionId ? replacement : item).ToArray(),
    };
    private static MissionVersion[] Replace(MissionVersion[] versions, MissionVersion replacement) =>
        versions.Select(item => item.MissionVersionId == replacement.MissionVersionId ? replacement : item).ToArray();
    private static EvaluationResult[] Replace(EvaluationResult[] results, EvaluationResult original, EvaluationResult replacement) =>
        results.Select(item => item.EvaluationResultId == original.EvaluationResultId ? replacement : item).ToArray();
    private static ProjectMissionDefinition[] Definitions(ProjectManifest manifest) => manifest.MissionDefinitions ?? [];
    private static MissionVersion[] Versions(ProjectMissionDefinition definition) => definition.Versions ?? [];
    private static EvaluationCase[] Cases(MissionVersion version) => version.EvaluationCases ?? [];
    private static EvaluationResult[] Results(MissionVersion version) => version.EvaluationResults ?? [];
    private static ProjectOperationException Conflict(string message) => new(ProjectOperationErrorCode.PublishConflict, message);
    private static ProjectOperationException Changed(string message) => new(ProjectOperationErrorCode.VersionChanged, message);
    private static string RequiredName(string name) => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120
        ? throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest, "A mission name must contain 1 to 120 characters.") : name.Trim();
    private static string RequiredDefinition(string text) => string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > 262_144
        ? throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest, "Mission definition text must be nonblank and within 262144 bytes.") : text;
    private static string RequiredSummary(string text) => text is null || Encoding.UTF8.GetByteCount(text) > 4096
        ? throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest, "Evaluation output is too large.") : text;
    private static MissionHandsProfile RequiredProfile(MissionHandsProfile profile) => Enum.IsDefined(profile) ? profile : throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest, "The mission hands profile is invalid.");
    private static string DefinitionHash(string text) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RequiredDefinition(text)))).ToLowerInvariant();
    private static bool Matches(EvaluationCase item, EvaluationOutcome outcome, string output) => outcome == item.ExpectedOutcome &&
        RequiredFragmentsMatch(item.RequiredOutputFragments, output) && !ForbiddenFragmentMatches(item.ForbiddenOutputFragments, output);
    private static bool RequiredFragmentsMatch(string[]? fragments, string output) => (fragments ?? []).All(fragment => output.Contains(fragment, StringComparison.Ordinal));
    private static bool ForbiddenFragmentMatches(string[]? fragments, string output) => (fragments ?? []).Any(fragment => output.Contains(fragment, StringComparison.Ordinal));
    private static bool AllPassed(MissionVersion version) => Cases(version).Length > 0 && Cases(version).All(item => Results(version).Any(result =>
        result.EvaluationCaseId == item.EvaluationCaseId && result.CandidateRevision == version.CandidateRevision &&
        result.DefinitionHash == version.DefinitionHash && result.State == EvaluationResultState.Passed));

    internal static void ValidateCase(EvaluationCase item)
    {
        var fragments = (item.RequiredOutputFragments ?? []).Concat(item.ForbiddenOutputFragments ?? []).ToArray();
        if (item.EvaluationCaseId == Guid.Empty || item.Revision <= 0 || string.IsNullOrWhiteSpace(item.Input) ||
            Encoding.UTF8.GetByteCount(item.Input) > 32768 || Encoding.UTF8.GetByteCount(item.ExpectedSuccess ?? "") > 4096 ||
            Encoding.UTF8.GetByteCount(item.ExpectedFailure ?? "") > 4096 || !Enum.IsDefined(item.ExpectedOutcome) ||
            fragments.Length > 32 || fragments.Any(fragment => string.IsNullOrWhiteSpace(fragment) || Encoding.UTF8.GetByteCount(fragment) > 1024))
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest, "The evaluation case is invalid.");
    }
}

internal static class MissionPackageBuilder
{
    internal static DurableMissionPackage Build(string home, ProjectManifest manifest, string definitionText)
    {
        try
        {
            var root = Path.GetFullPath(home);
            var lockAsset = (manifest.Assets ?? []).SingleOrDefault(asset => asset.Kind == ProjectAssetKind.LockFile)
                ?? throw Invalid("The Project does not list a lock file for this mission.");
            var lockPath = Readable(root, lockAsset.RelativePath);
            if (new FileInfo(lockPath).Length > 64 * 1024) throw Invalid("The Project lock file is too large.");
            var lockFile = LockFileIO.Read(lockPath);
            var experts = (manifest.Assets ?? []).Where(asset => asset.Kind == ProjectAssetKind.Expert).Select(asset =>
            {
                var path = Readable(root, asset.RelativePath);
                var name = Path.GetFileName(Path.GetDirectoryName(path)!);
                if (!lockFile.Experts.TryGetValue(name, out var locked) || string.IsNullOrWhiteSpace(locked.Hash)) throw Invalid("The Project lock does not match its expert content.");
                var markdown = ReadBounded(path, 8 * 1024);
                var hash = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(markdown))).ToLowerInvariant();
                if (!string.Equals(locked.Hash, hash["sha256:".Length..], StringComparison.OrdinalIgnoreCase)) throw Invalid("The Project lock does not match its expert content.");
                return new DurableResolvedExpert(name, locked.Source, locked.Path, hash, markdown);
            }).ToArray();
            var program = MclParser.Parse(definitionText);
            var roots = program.Declarations.OfType<MissionDeclaration>().ToArray();
            if (roots.Length != 1)
                throw Invalid("A durable candidate must contain exactly one mission declaration.");
            var mission = roots[0];
            var input = mission.Params.FirstOrDefault() ?? throw Invalid("The durable mission must declare an input.");
            var inputs = experts.Select(expert => new DurableResolvedExpertInput(expert.Name, expert.LockSource, expert.LockPath, expert.LockHash, expert.ExpertMarkdown)).ToArray();
            var raw = new DurableMissionPackageInput(DurableMissionPackageValidator.CurrentFormatVersion, "", definitionText, mission.Name, input, inputs);
            raw = raw with { PackageHash = DurableMissionPackageValidator.ComputeHash(raw) };
            if (!DurableMissionPackageValidator.TryValidate(raw, out _, out var reason)) throw Invalid(reason ?? "The durable package is invalid.");
            return new DurableMissionPackage(raw.FormatVersion, raw.PackageHash, raw.MissionSource, raw.RootMissionName, raw.RootInputName, experts);
        }
        catch (ProjectOperationException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        { throw Invalid($"The durable package could not be created: {exception.Message}"); }
    }

    private static string Readable(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw Invalid("The Project package contains an invalid asset path.");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(path) || new FileInfo(path).LinkTarget is not null)
            throw Invalid("The Project package asset is unavailable.");
        return path;
    }
    private static string ReadBounded(string path, int maxBytes)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > maxBytes || bytes.Any(value => value == 0)) throw Invalid("The Project package asset is invalid.");
        return new UTF8Encoding(false, true).GetString(bytes);
    }
    private static ProjectOperationException Invalid(string message) => new(ProjectOperationErrorCode.InvalidManifest, message);
}
