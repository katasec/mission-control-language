using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using ForgeMission.Application.Transport;
using ForgeMission.Core.Resolution;

namespace ForgeMission.Application;

// The single owner of local Project state (43.20 task 1): title/slug/home derivation, the
// collision-safe write, manifest reading and validation, and every named failure. No surface
// derives a Project value or touches the filesystem — Desktop today and a TUI later both reach
// this class only through the project transport contracts, so they get identical results.
//
// Expected domain failures throw ProjectOperationException and are mapped to a typed
// ProjectOperationError once, at the transport endpoint. The exception never leaves Application.
internal sealed class ProjectService : IProjectService
{
    public const string ManifestFileName = "forge.project.json";

    private const int MaxTitleLength = 60;
    private const int MaxSlugLength = 40;
    private const int MaxCollisionAttempts = 100;
    private const string SlugFallback = "project";

    private readonly string _projectsRoot;
    private readonly ProjectManifestFile _manifestFile;
    private readonly ApplicationSessionService? _sessions;

    public ProjectService(string? projectsRoot = null, ProjectManifestFile? manifestFile = null)
    {
        _projectsRoot = projectsRoot ?? DefaultProjectsRoot();
        _manifestFile = manifestFile ?? new ProjectManifestFile();
    }

    public ProjectService(ApplicationSessionService sessions)
        : this(sessions, DefaultProjectsRoot())
    {
    }

    // The same owner with its Projects root stated rather than derived. Production composition uses
    // the derived root above; this seam exists so the Project-opening paths can be exercised without
    // writing into the real user profile.
    internal ProjectService(ApplicationSessionService sessions, string projectsRoot)
    {
        _projectsRoot = projectsRoot;
        _manifestFile = new ProjectManifestFile();
        _sessions = sessions;
    }

    public Task<ProjectDraftResponse> DraftAsync(ProjectDraftRequest request, CancellationToken ct)
    {
        try { return Task.FromResult(new ProjectDraftResponse(Draft(request.Goal, request.TitleOverride, request.HomeOverride), null)); }
        catch (ProjectOperationException exception) { return Task.FromResult(new ProjectDraftResponse(null, ToError(exception))); }
    }

    public Task<ProjectOperationResponse> CreateAsync(ProjectCreateRequest request, CancellationToken ct)
    {
        try { return Task.FromResult(OpenForApplication(Create(request.Goal, request.Title, request.HomePath), request.Mission, request.Runtime, ProjectOperationOutcome.Created)); }
        catch (ProjectOperationException exception) { return Task.FromResult(new ProjectOperationResponse(ProjectOperationOutcome.Failed, Error: ToError(exception))); }
    }

    public Task<ProjectOperationResponse> OpenAsync(ProjectOpenRequest request, CancellationToken ct)
    {
        try
        {
            var opened = Open(request.HomePath);
            return Task.FromResult(opened.Project is { } project
                ? OpenForApplication(project, request.Mission, request.Runtime, ProjectOperationOutcome.Opened)
                : new ProjectOperationResponse(ProjectOperationOutcome.GoalRequired, Proposal: opened.GoalRequired));
        }
        catch (ProjectOperationException exception) { return Task.FromResult(new ProjectOperationResponse(ProjectOperationOutcome.Failed, Error: ToError(exception))); }
    }

    public async Task<SelectProjectMissionResponse> SelectMissionAsync(SelectProjectMissionRequest request, CancellationToken ct)
    {
        try
        {
            var session = RequiredSession(request.SessionId);
            var project = await SelectMissionAsync(session.ProjectHome, request.Mission, ct);
            return new SelectProjectMissionResponse(Missions(project.Manifest), null);
        }
        catch (ProjectOperationException exception) { return new SelectProjectMissionResponse(null, ToError(exception)); }
    }

    /// <summary>Re-reads the Project-owned manifest before granting hands; no caller value selects
    /// a profile or substitutes a definition hash.</summary>
    internal MissionVersionLaunch? ResolveApprovedLaunch(
        ApplicationSession session, Guid missionVersionId, int versionNumber, string definitionHash)
    {
        if (missionVersionId == Guid.Empty || versionNumber <= 0 || string.IsNullOrWhiteSpace(definitionHash))
            return null;

        try
        {
            var manifest = Read(_manifestFile.Read(session.ProjectHome), session.ProjectHome);
            return manifest.ApprovedMissionLaunches?.SingleOrDefault(launch =>
                launch.MissionVersionId == missionVersionId &&
                launch.VersionNumber == versionNumber &&
                string.Equals(launch.DefinitionHash, definitionHash, StringComparison.Ordinal))
                ?? ApprovedDefinitionLaunch(manifest, missionVersionId, versionNumber, definitionHash);
        }
        catch (ProjectOperationException)
        {
            return null;
        }
    }

    /// <summary>The schema-5 half of the same question. <c>ApprovedMissionLaunches</c> is the
    /// schema-4 compatibility lane and has no writer in the authored lifecycle, so a version
    /// published through <c>MissionVersionService.PublishAsync</c> lives only in
    /// <c>MissionDefinitions</c> — without this it could never be acknowledged at all. The bar is
    /// the same one the legacy lane sets and no lower: exact version ID, version number and
    /// definition hash, genuinely Approved, and still the definition's active approved version.
    /// Nothing here writes, migrates, or changes a schema.</summary>
    private static MissionVersionLaunch? ApprovedDefinitionLaunch(
        ProjectManifest manifest, Guid missionVersionId, int versionNumber, string definitionHash) =>
        (manifest.MissionDefinitions ?? [])
            .Where(definition => definition.ActiveApprovedVersionId == missionVersionId)
            .SelectMany(definition => definition.Versions ?? [])
            .Where(version =>
                version.MissionVersionId == missionVersionId &&
                version.VersionNumber == versionNumber &&
                version.State == MissionVersionState.Approved &&
                string.Equals(version.DefinitionHash, definitionHash, StringComparison.Ordinal))
            .Select(version => new MissionVersionLaunch(version.MissionVersionId, version.VersionNumber,
                version.DefinitionHash, version.DefinitionText, version.CapabilityProfile,
                version.ApprovedAtUtc ?? version.CreatedAtUtc, version.Package))
            .SingleOrDefault();

    /// <summary>Pure: what a create would use, for display before confirmation. It performs no
    /// filesystem work at all — not even a collision probe, which would be both an access and an
    /// implied reservation. <see cref="Create"/> stays authoritative for the final home.</summary>
    public ProjectHomeProposal Draft(string goal, string? titleOverride, string? homeOverride)
    {
        var title = DeriveTitle(RequiredGoal(goal), titleOverride);
        var home = Blank(homeOverride) ? Path.Combine(_projectsRoot, Slugify(title)) : ValidHome(homeOverride!);
        return new ProjectHomeProposal(home, title);
    }

    /// <summary>Creates the Project home and its current manifest. Create — never a draft, and never a
    /// surface — owns the final home: inside Forge's own projects root it takes the next free
    /// -2/-3 suffix, so confirming a drafted location that has since been taken still lands
    /// somewhere valid. A home outside that root is a directory the person named themselves and is
    /// used exactly, because silently relocating it would be worse than refusing.</summary>
    public ProjectRecord Create(string goal, string? titleOverride, string? homeOverride)
    {
        var required = RequiredGoal(goal);
        var title = DeriveTitle(required, titleOverride);
        var home = Blank(homeOverride) ? null : ValidHome(homeOverride!);
        return home is null
            ? CreateInFirstFreeHome(Slugify(title), title, required)
            : IsForgeManagedHome(home)
                ? CreateInFirstFreeHome(Path.GetFileName(home), title, required)
                : CreateInExactHome(home, title, required);
    }

    /// <summary>Creates the one managed chat Project (Phase 48) at a home keyed by the Project GUID
    /// it is given, so the marker that names that GUID also names its home. Create, not a surface,
    /// still owns the final home: this refuses an existing directory rather than adopting or
    /// overwriting whatever is there.</summary>
    internal ProjectRecord CreateManaged(Guid projectId, string title, string goal)
    {
        if (projectId == Guid.Empty)
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                "A managed Project requires its Project id.");

        var required = RequiredGoal(goal);
        var home = Path.Combine(_projectsRoot, projectId.ToString("D"));
        if (Directory.Exists(home))
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidHome,
                $"{home} already exists; a managed Project is never created over an existing directory.");

        return TryWriteNewManifest(home, DeriveTitle(required, title), required, projectId)
            ?? throw new ProjectOperationException(ProjectOperationErrorCode.InvalidHome,
                $"{home} was claimed while the managed Project was being created.");
    }

    /// <summary>Where Forge's own Projects live. Exposed so the managed-chat marker beside that root
    /// is derived in one place rather than recomputed by another owner.</summary>
    internal string ProjectsRoot => _projectsRoot;

    /// <summary>Opens an existing directory as a Project home. A directory with no manifest is not
    /// a failure and not an empty-goal Project: it returns the proposal the goal-confirmation flow
    /// needs, having created nothing.</summary>
    public ProjectOpenResult Open(string homePath)
    {
        var home = ValidHome(homePath);
        if (!Directory.Exists(home))
            throw new ProjectOperationException(ProjectOperationErrorCode.HomeNotFound,
                $"No directory exists at {home}.");

        if (!File.Exists(Path.Combine(home, ManifestFileName)))
            return new ProjectOpenResult(null, new ProjectHomeProposal(home, TitleFromDirectory(home)));

        return new ProjectOpenResult(new ProjectRecord(Read(_manifestFile.Read(home), home), home), null);
    }

    /// <summary>Reads a manifest for a Project session that is already open. It creates neither a
    /// directory nor any conversation/capability authority.</summary>
    public ProjectRecord ReadForHome(string home)
    {
        var root = ValidHome(home);
        if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, ManifestFileName)))
            throw MissingManifest(root);

        return new ProjectRecord(Read(_manifestFile.Read(root), root), root);
    }

    public ProjectRecord SetProjectMissionContainerId(string home, Guid containerId) =>
        SetProjectMissionContainerIdAsync(home, containerId, CancellationToken.None).GetAwaiter().GetResult();

    public Task<ProjectRecord> SetProjectMissionContainerIdAsync(
        string home, Guid containerId, CancellationToken cancellationToken) =>
        SetStableIdAsync(
            home,
            containerId,
            "Project Mission container",
            manifest => manifest.ProjectMissionContainerId,
            (manifest, id) => manifest with { ProjectMissionContainerId = id },
            cancellationToken);

    public Task<ProjectRecord> SelectMissionAsync(string home, string mission, CancellationToken cancellationToken)
    {
        if (!MissionCatalog.IsAllowed(mission))
            throw new ProjectOperationException(ProjectOperationErrorCode.UnknownMission,
                $"'{mission}' is not a mission this Project can run.");

        var selected = MissionCatalog.Reference(mission);
        return UpdateAsync(home, manifest =>
        {
            if (manifest.SelectedMission is { Origin: ProjectMissionOrigin.BuiltIn } current &&
                string.Equals(current.Reference, selected.Reference, StringComparison.Ordinal))
                return manifest;

            return manifest with { SelectedMission = selected };
        }, cancellationToken);
    }

    private ProjectOperationResponse OpenForApplication(ProjectRecord project, string? mission, SessionRuntimeKind runtime, ProjectOperationOutcome outcome)
    {
        var session = (_sessions ?? throw new InvalidOperationException("Project transport requires application sessions."))
            .CreateForProject(project.Home, mission, runtime);
        var summary = new ProjectSummary(project.Manifest.ProjectId, project.Manifest.Title, project.Manifest.Goal, project.Home);
        return new ProjectOperationResponse(outcome, new ProjectSession(session.Id, session.Execution.AvailableCapabilities, summary));
    }

    private ApplicationSession RequiredSession(string sessionId)
    {
        if (_sessions is null || !_sessions.TryGet(sessionId, out var session) || session is null)
            throw new KeyNotFoundException();
        return session;
    }

    private static ProjectOperationError ToError(ProjectOperationException exception) => new(exception.Code, exception.Message);

    private static ProjectMissionsView Missions(ProjectManifest manifest) => new(
        MissionCatalog.All,
        manifest.SelectedMission is { Origin: ProjectMissionOrigin.BuiltIn } selected && MissionCatalog.IsAllowed(selected.Reference)
            ? selected.Reference : null,
        manifest.LegacyProjectControlConversationId is not null || manifest.MissionControlConversationId is not null);

    public Task<ProjectRecord> PrepareSubmissionAsync(
        string home,
        Guid commandId,
        Guid? previousCommandId,
        string input,
        CancellationToken cancellationToken)
    {
        ValidateSubmissionRequest(commandId, input);
        return UpdateAsync(home, manifest =>
        {
            var prepared = PrepareSubmission(manifest, commandId, previousCommandId, input);
            EnsureJournalFitsInput(prepared.Submission);
            return prepared;
        }, cancellationToken);
    }

    public async Task<ProjectRecord> RecordSubmissionAcceptedAsync(
        string home,
        Guid commandId,
        ProjectSubmissionAcceptance acceptance,
        CancellationToken cancellationToken)
    {
        ValidateAcceptance(acceptance);
        try
        {
            return await UpdateAsync(home, manifest => RecordAcceptance(manifest, commandId, acceptance), cancellationToken);
        }
        catch (ProjectOperationException exception) when (exception.Code is
            ProjectOperationErrorCode.ManifestWriteFailed or ProjectOperationErrorCode.ProjectChanged)
        {
            throw SubmissionUncertain();
        }
    }

    public async Task<ProjectRecord> RecordSubmissionRejectedAsync(
        string home,
        Guid commandId,
        ProjectSubmissionRejection rejection,
        CancellationToken cancellationToken)
    {
        ValidateRejection(rejection);
        try
        {
            return await UpdateAsync(home, manifest => RecordRejection(manifest, commandId, rejection), cancellationToken);
        }
        catch (ProjectOperationException exception) when (exception.Code is
            ProjectOperationErrorCode.ManifestWriteFailed or ProjectOperationErrorCode.ProjectChanged)
        {
            throw SubmissionUncertain();
        }
    }

    private Task<ProjectRecord> SetStableIdAsync(
        string home,
        Guid id,
        string description,
        Func<ProjectManifest, Guid?> read,
        Func<ProjectManifest, Guid, ProjectManifest> write,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                $"A {description} id is required.");

        return UpdateAsync(home, manifest =>
        {
            if (read(manifest) is not { } existing)
                return write(manifest, id);

            if (existing == id)
                return manifest;

            throw new ProjectOperationException(ProjectOperationErrorCode.MissionRunConflict,
                $"This Project already names a different {description}.");
        }, cancellationToken);
    }

    private async Task<ProjectRecord> UpdateAsync(
        string home,
        Func<ProjectManifest, ProjectManifest> transform,
        CancellationToken cancellationToken)
    {
        var root = ValidHome(home);
        if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, ManifestFileName)))
            throw MissingManifest(root);

        return await _manifestFile.UpdateAsync(root, snapshot =>
        {
            var current = Read(snapshot, root);
            var updated = transform(current);
            if (ReferenceEquals(current, updated))
                return new ProjectManifestFileUpdate<ProjectRecord>(new ProjectRecord(current, root), null);

            Validate(updated, snapshot.Path, root);
            return new ProjectManifestFileUpdate<ProjectRecord>(
                new ProjectRecord(updated, root),
                SerializeManifest(updated));
        }, cancellationToken);
    }

    // The authored-version service is deliberately the only other Project-domain participant in
    // this lease. It receives the Project home solely to read manifest-listed package inputs while
    // the transaction is held; it cannot introduce a route, remote call, or arbitrary path.
    /// <summary>
    /// Gives a Project the two files a durable mission package is built from, once. A Project
    /// created through the launcher carries no assets at all, so <c>MissionPackageBuilder</c> has
    /// no lock file to read and promoting a candidate is impossible - authoring would dead-end at
    /// its first real step. This closes that gap at the moment the need appears rather than
    /// changing what creating a Project means.
    /// <para>
    /// The scaffold is deliberately the smallest thing that can actually be published: a Proposer
    /// and a Reviewer, which is two experts exactly (the durable package limit) and the propose-
    /// then-review shape the operator is authoring. It selects no provider profile, because a
    /// durable package cannot carry one.
    /// </para>
    /// It is idempotent and never destructive: a Project that already lists a lock file is left
    /// exactly as it is, and an existing file on disk is adopted rather than overwritten.
    /// </summary>
    internal Task<ProjectRecord> EnsureMissionAssetsAsync(string home, CancellationToken cancellationToken) =>
        EnsureExpertAssetsAsync(home, StarterExperts, cancellationToken);

    /// <summary>The managed chat Project's own shipped pair (Phase 48). Same single asset writer as
    /// the authoring scaffold above, with the shipped Proposer/Approver content: ProjectService
    /// stays the only writer of a Project's directory, assets, and manifest.</summary>
    internal Task<ProjectRecord> EnsureManagedChatAssetsAsync(string home, CancellationToken cancellationToken) =>
        EnsureExpertAssetsAsync(home, ShippedMissionCatalog.Experts, cancellationToken);

    private async Task<ProjectRecord> EnsureExpertAssetsAsync(
        string home, (string Name, string Markdown)[] pair, CancellationToken cancellationToken)
    {
        var root = ValidHome(home);
        var existing = ReadForHome(root);
        if ((existing.Manifest.Assets ?? []).Any(asset => asset.Kind == ProjectAssetKind.LockFile))
            return existing;

        var experts = new Dictionary<string, LockFileExpert>(StringComparer.Ordinal);
        var descriptors = new List<ProjectAssetDescriptor>(existing.Manifest.Assets ?? []);
        foreach (var (name, markdown) in pair)
        {
            var relative = $"experts/{name}/expert.md";
            var path = Path.Combine(root, "experts", name, "expert.md");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Adopt, never clobber: if the operator already wrote this expert, their content is
            // what gets locked.
            if (!File.Exists(path))
                await File.WriteAllTextAsync(path, markdown, cancellationToken);
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            experts[name] = new LockFileExpert
            {
                Source = "experts",
                Path = relative,
                Hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
            };
            if (!descriptors.Any(asset => asset.Kind == ProjectAssetKind.Expert && asset.RelativePath == relative))
                descriptors.Add(new ProjectAssetDescriptor(ProjectAssetKind.Expert, relative, null));
        }

        var lockPath = Path.Combine(root, "mcl.lock");
        if (!File.Exists(lockPath))
            LockFileIO.Write(lockPath, new LockFile { Version = 1, Experts = experts });
        descriptors.Add(new ProjectAssetDescriptor(ProjectAssetKind.LockFile, "mcl.lock", null));

        var updated = await UpdateMissionDefinitionsAsync(root,
            (manifest, _) => (manifest with { Assets = [.. descriptors] }, true), cancellationToken);
        return updated.Record;
    }

    /// <summary>The starter pair. Kinds are limited to what a durable package accepts, and the
    /// pair fits the package's two-expert ceiling exactly.</summary>
    private static readonly (string Name, string Markdown)[] StarterExperts =
    [
        ("Proposer", "---\nname: Proposer\nkind: llm\ninput: task\noutput: proposal\n---\nPropose a concise plan for the task below. State assumptions explicitly.\n\n{{task}}\n"),
        ("Reviewer", "---\nname: Reviewer\nkind: llm\ninput: proposal\noutput: answer\n---\nReview the proposal below. Approve it, or decline and name the specific conflict.\n\n{{proposal}}\n"),
    ];

    /// <summary>The definition a new mission starts from: one declaration, the two starter
    /// experts, no provider profile. It is publishable as written, so an operator can reach the
    /// end of the flow before they change a line of it.</summary>
    internal const string StarterMissionDefinition =
        "mission Janus(task) = {\n    Proposer\n    -> Reviewer\n}\n";

    internal async Task<(ProjectRecord Record, T Value)> UpdateMissionDefinitionsAsync<T>(
        string home,
        Func<ProjectManifest, string, (ProjectManifest Manifest, T Value)> transform,
        CancellationToken cancellationToken)
    {
        var root = ValidHome(home);
        if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, ManifestFileName)))
            throw MissingManifest(root);

        return await _manifestFile.UpdateAsync(root, snapshot =>
        {
            var current = Read(snapshot, root);
            var result = transform(current, root);
            Validate(result.Manifest, snapshot.Path, root);
            return new ProjectManifestFileUpdate<(ProjectRecord, T)>(
                (new ProjectRecord(result.Manifest, root), result.Value), SerializeManifest(result.Manifest));
        }, cancellationToken);
    }

    private static ProjectManifest PrepareSubmission(
        ProjectManifest manifest,
        Guid commandId,
        Guid? previousCommandId,
        string input)
    {
        var existing = manifest.Submission;
        if (existing is null)
        {
            if (previousCommandId is not null)
                throw SubmissionChanged();

            return manifest with
            {
                Submission = new ProjectSubmission(
                    commandId,
                    previousCommandId,
                    MissionCatalog.RequireSelected(manifest.SelectedMission),
                    input,
                    manifest.Goal,
                    ProjectSubmissionPhase.Prepared,
                    Acceptance: null,
                    Rejection: null),
            };
        }

        if (existing.CommandId == commandId)
        {
            if (existing.PreviousCommandId == previousCommandId && string.Equals(existing.Input, input, StringComparison.Ordinal))
                return manifest;

            throw new ProjectOperationException(ProjectOperationErrorCode.MissionRunConflict,
                "This command id was already prepared with different immutable content.");
        }

        if (existing.Phase == ProjectSubmissionPhase.Prepared)
            throw new ProjectOperationException(ProjectOperationErrorCode.SubmissionPending,
                "A Project Mission submission is still awaiting its acceptance result.");

        if (previousCommandId != existing.CommandId)
            throw SubmissionChanged();

        return manifest with
        {
            Submission = new ProjectSubmission(
                commandId,
                previousCommandId,
                MissionCatalog.RequireSelected(manifest.SelectedMission),
                input,
                manifest.Goal,
                ProjectSubmissionPhase.Prepared,
                Acceptance: null,
                Rejection: null),
        };
    }

    private static ProjectManifest RecordAcceptance(
        ProjectManifest manifest,
        Guid commandId,
        ProjectSubmissionAcceptance acceptance)
    {
        var submission = RequireSubmission(manifest, commandId);
        if (submission.Phase == ProjectSubmissionPhase.Accepted && Equals(submission.Acceptance, acceptance))
            return manifest;
        if (submission.Phase != ProjectSubmissionPhase.Prepared)
            throw ConflictingReceipt();

        return manifest with
        {
            Submission = submission with
            {
                Phase = ProjectSubmissionPhase.Accepted,
                Acceptance = acceptance,
                Rejection = null,
            },
        };
    }

    private static ProjectManifest RecordRejection(
        ProjectManifest manifest,
        Guid commandId,
        ProjectSubmissionRejection rejection)
    {
        var submission = RequireSubmission(manifest, commandId);
        if (submission.Phase == ProjectSubmissionPhase.Rejected && Equals(submission.Rejection, rejection))
            return manifest;
        if (submission.Phase != ProjectSubmissionPhase.Prepared)
            throw ConflictingReceipt();

        return manifest with
        {
            Submission = submission with
            {
                Phase = ProjectSubmissionPhase.Rejected,
                Acceptance = null,
                Rejection = rejection,
            },
        };
    }

    private static ProjectSubmission RequireSubmission(ProjectManifest manifest, Guid commandId)
    {
        if (commandId == Guid.Empty || manifest.Submission is not { } submission || submission.CommandId != commandId)
            throw new ProjectOperationException(ProjectOperationErrorCode.MissionRunConflict,
                "This submission receipt does not match the Project's current immutable intent.");

        return submission;
    }

    private static ProjectOperationException SubmissionChanged() =>
        new(ProjectOperationErrorCode.SubmissionChanged,
            "The Project submission changed before this request could be prepared. Refresh and retry.");

    private static ProjectOperationException ConflictingReceipt() =>
        new(ProjectOperationErrorCode.MissionRunConflict,
            "This submission already has a different terminal receipt.");

    private static ProjectOperationException SubmissionUncertain() =>
        new(ProjectOperationErrorCode.SubmissionUncertain,
            "The Host result may be durable, but Forge could not record its receipt. Retry the same command id.");

    private static void ValidateSubmissionRequest(Guid commandId, string input)
    {
        if (commandId == Guid.Empty)
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidMissionInput,
                "A Project Mission command id is required.");
        if (string.IsNullOrWhiteSpace(input) || input.Length > 32_000 || Encoding.UTF8.GetByteCount(input) > 16_384)
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidMissionInput,
                "A Project Mission instruction must be nonblank and within the supported size limit.");
    }

    private static void ValidateAcceptance(ProjectSubmissionAcceptance acceptance)
    {
        if (acceptance.ContainerId == Guid.Empty || acceptance.RunId == Guid.Empty || acceptance.AcceptedSequence <= 0)
            throw new ProjectOperationException(ProjectOperationErrorCode.MissionRunConflict,
                "A Project Mission acceptance receipt is incomplete.");
    }

    private static void ValidateRejection(ProjectSubmissionRejection rejection)
    {
        if (string.IsNullOrWhiteSpace(rejection.Code) || string.IsNullOrWhiteSpace(rejection.Message))
            throw new ProjectOperationException(ProjectOperationErrorCode.MissionRunConflict,
                "A Project Mission rejection receipt is incomplete.");
    }

    private static void EnsureJournalFitsInput(ProjectSubmission? submission)
    {
        if (submission is not null &&
            JsonSerializer.SerializeToUtf8Bytes(submission, ProjectManifestJsonContext.Default.ProjectSubmission).Length > 96 * 1024)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidMissionInput,
                "The Project Mission submission receipt exceeds its bounded size.");
        }
    }

    // --- derivation (pure) ----------------------------------------------------------------------

    // The goal gate runs before anything else, including a title override. A supplied title says
    // what to call the Project, never that the Project may exist without a goal — an override that
    // could skip this check is exactly how an empty goal would reach a persisted manifest.
    private static string RequiredGoal(string goal)
    {
        if (Blank(goal))
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidGoal,
                "A goal is required to create a Project.");

        return goal.Trim();
    }

    private static string DeriveTitle(string goal, string? titleOverride) =>
        Blank(titleOverride)
            ? Truncate(goal.Split('\n')[0].Trim(), MaxTitleLength)
            : titleOverride!.Trim();

    // Only when a non-empty title normalizes to nothing usable (for example "***", or a fully
    // non-ASCII title) does the directory name fall back — the title itself is always preserved
    // verbatim in the manifest.
    private static string Slugify(string title)
    {
        var slug = new StringBuilder(title.Length);
        foreach (var character in title.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
                slug.Append(character);
            else if (slug.Length > 0 && slug[^1] != '-')
                slug.Append('-');
        }

        var trimmed = slug.ToString().Trim('-');
        return trimmed.Length == 0 ? SlugFallback : Truncate(trimmed, MaxSlugLength).Trim('-');
    }

    // Word-boundary truncation: a cut mid-word reads like a bug in a title a person will see.
    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        var cut = value[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');
        return (lastSpace > 0 ? cut[..lastSpace] : cut).TrimEnd();
    }

    private static string TitleFromDirectory(string home)
    {
        var name = Path.GetFileName(home.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? SlugFallback : name;
    }

    // Path.GetFullPath normalizes without touching the filesystem, which is what keeps Draft pure.
    private static string ValidHome(string homePath)
    {
        if (Blank(homePath))
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidHome,
                "A Project home path is required.");

        // Rootedness is checked before normalizing: Path.GetFullPath would silently resolve a
        // relative path against the Application Host's own working directory, which is never a
        // Project home a caller meant to name.
        var trimmed = homePath.Trim();
        if (!Path.IsPathRooted(trimmed))
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidHome,
                $"A Project home must be an absolute path: {homePath}.");

        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidHome,
                $"That Project home path is not usable: {homePath}.");
        }
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    // --- creation (filesystem) ------------------------------------------------------------------

    // A home directly inside Forge's own projects root is one Forge chose, whether this call
    // derived it or a surface confirmed the draft that derived it — either way Forge owns picking
    // a free name there.
    private bool IsForgeManagedHome(string home) =>
        string.Equals(Path.GetDirectoryName(home), _projectsRoot, StringComparison.Ordinal);

    private ProjectRecord CreateInFirstFreeHome(string slug, string title, string goal)
    {
        for (var attempt = 1; attempt <= MaxCollisionAttempts; attempt++)
        {
            var home = Path.Combine(_projectsRoot, attempt == 1 ? slug : $"{slug}-{attempt}");
            if (Directory.Exists(home))
                continue;

            // CreateNew is the race guard: a second Forge instance that reached this same free
            // candidate first owns it, and this one advances rather than overwriting its manifest.
            if (TryWriteNewManifest(home, title, goal) is { } created)
                return created;
        }

        throw new ProjectOperationException(ProjectOperationErrorCode.CollisionAttemptsExhausted,
            $"Could not find a free Project directory for \"{title}\" after {MaxCollisionAttempts} attempts.");
    }

    private ProjectRecord CreateInExactHome(string home, string title, string goal)
    {
        if (File.Exists(Path.Combine(home, ManifestFileName)))
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidHome,
                $"{home} already contains a Project. Open it instead.");

        return TryWriteNewManifest(home, title, goal)
            ?? throw new ProjectOperationException(ProjectOperationErrorCode.InvalidHome,
                $"{home} was claimed by another Project while this one was being created.");
    }

    private ProjectRecord? TryWriteNewManifest(string home, string title, string goal, Guid? projectId = null)
    {
        var manifest = new ProjectManifest(
            ProjectManifest.CurrentSchemaVersion,
            projectId ?? Guid.NewGuid(),
            title,
            goal,
            [],
            ProjectMissionReference.BuiltInJanus,
            [],
            ProjectMissionContainerId: null,
            [],
            LegacyProjectControlConversationId: null,
            MissionControlConversationId: null,
            Submission: null,
            ApprovedMissionLaunches: [],
            MissionDefinitions: []);

        // The same owner used by every update also creates the initial manifest. A competing
        // process sees the manifest under the lease and moves to its next candidate; it never
        // sees a half-written file or writes around the transaction boundary.
        var created = _manifestFile.CreateIfAbsentAsync(home, SerializeManifest(manifest), CancellationToken.None)
            .GetAwaiter().GetResult();
        return created ? new ProjectRecord(manifest, home) : null;
    }

    // --- reading and validation -----------------------------------------------------------------

    private static ProjectManifest Read(ProjectManifestFileSnapshot snapshot, string home)
    {
        ProjectManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(snapshot.Bytes, ProjectManifestJsonContext.Default.ProjectManifest);
        }
        catch (JsonException exception)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                $"{snapshot.Path} is not readable as a Forge Project manifest: {exception.Message}");
        }

        if (manifest is null)
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                $"{snapshot.Path} is empty.");

        return Validate(manifest, snapshot.Path, home);
    }

    // A manifest that fails validation is refused and left exactly as it is — never overwritten or
    // repaired, because a hand-edited or newer file is the user's data, not a corrupt cache.
    private static ProjectManifest Validate(ProjectManifest manifest, string manifestPath, string home)
    {
        if (manifest.SchemaVersion > ProjectManifest.CurrentSchemaVersion)
            throw new ProjectOperationException(ProjectOperationErrorCode.UnsupportedManifestVersion,
                $"{manifestPath} was created by a newer version of Forge (schema {manifest.SchemaVersion}).");

        if (manifest.SchemaVersion < 1 || manifest.ProjectId == Guid.Empty ||
            Blank(manifest.Title) || Blank(manifest.Goal) || manifest.SelectedMission is null)
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                $"{manifestPath} is missing a required Project field.");

        // A missing collection is an older-but-valid hand edit, not a failure: the identity fields
        // above are what a Project cannot be without. Migration is in-memory only; Open does not
        // rewrite a person's manifest, and the next successful mutation publishes v3.
        var normalized = MigrateToCurrentSchema(manifest) with
        {
            Assets = OrEmpty(manifest.Assets),
            AttachedContext = OrEmpty(manifest.AttachedContext),
            Runs = OrEmpty(manifest.Runs),
            ApprovedMissionLaunches = OrEmpty(manifest.ApprovedMissionLaunches),
            MissionDefinitions = OrEmpty(manifest.MissionDefinitions),
        };

        foreach (var asset in normalized.Assets)
            ValidateAssetPath(asset, manifestPath, home);
        foreach (var context in normalized.AttachedContext)
            ValidateContextReference(context, manifestPath);
        ValidateSubmission(normalized.Submission, manifestPath);
        ValidateApprovedLaunches(normalized.ApprovedMissionLaunches, manifestPath);
        ValidateMissionDefinitions(normalized.MissionDefinitions, manifestPath);

        return normalized;
    }

    private static ProjectManifest MigrateToCurrentSchema(ProjectManifest manifest)
    {
        var legacy = MergeLegacyIds(manifest, manifest.SchemaVersion);
        return manifest with
        {
            SchemaVersion = ProjectManifest.CurrentSchemaVersion,
            ProjectMissionContainerId = manifest.SchemaVersion == 1 ? null : manifest.ProjectMissionContainerId,
            LegacyProjectControlConversationId = legacy,
            MissionControlConversationId = null,
            Submission = manifest.SchemaVersion < 3 ? null : manifest.Submission,
            ApprovedMissionLaunches = manifest.SchemaVersion < 4 ? [] : manifest.ApprovedMissionLaunches,
            MissionDefinitions = manifest.SchemaVersion < 5 ? [] : manifest.MissionDefinitions,
        };
    }

    private static Guid? MergeLegacyIds(ProjectManifest manifest, int schemaVersion)
    {
        if (manifest.LegacyProjectControlConversationId is { } legacy &&
            manifest.MissionControlConversationId is { } v1 && legacy != v1)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                "The Project manifest contains conflicting legacy Mission Control conversation ids.");
        }

        if (schemaVersion == 1 && manifest.ProjectMissionContainerId is not null)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                "A v1 Project manifest cannot name a Project Mission container.");
        }

        return manifest.LegacyProjectControlConversationId ?? manifest.MissionControlConversationId;
    }

    private static void ValidateSubmission(ProjectSubmission? submission, string manifestPath)
    {
        if (submission is null)
            return;

        var validIdentity = submission.CommandId != Guid.Empty &&
                            MissionCatalog.IsAllowed(submission.Mission) &&
                            !string.IsNullOrWhiteSpace(submission.Input) &&
                            !string.IsNullOrWhiteSpace(submission.ProjectGoal) &&
                            submission.Input.Length <= 32_000 &&
                            Encoding.UTF8.GetByteCount(submission.Input) <= 16_384 &&
                            JsonSerializer.SerializeToUtf8Bytes(submission, ProjectManifestJsonContext.Default.ProjectSubmission).Length <= 96 * 1024;
        if (!validIdentity)
            throw InvalidSubmission(manifestPath);

        var validPhase = submission.Phase switch
        {
            ProjectSubmissionPhase.Prepared => submission.Acceptance is null && submission.Rejection is null,
            ProjectSubmissionPhase.Accepted => IsCompleteAcceptance(submission.Acceptance) && submission.Rejection is null,
            ProjectSubmissionPhase.Rejected => submission.Acceptance is null && submission.Rejection is { Code.Length: > 0, Message.Length: > 0 },
            _ => false,
        };
        if (!validPhase)
            throw InvalidSubmission(manifestPath);
    }

    private static void ValidateApprovedLaunches(MissionVersionLaunch[] launches, string manifestPath)
    {
        var ids = new HashSet<Guid>();
        foreach (var launch in launches)
        {
            var valid = launch.MissionVersionId != Guid.Empty &&
                        launch.VersionNumber > 0 &&
                        IsDefinitionHash(launch.DefinitionHash, launch.Definition) &&
                        !string.IsNullOrWhiteSpace(launch.Definition) &&
                        launch.Definition.Length <= 256 * 1024 &&
                        launch.ApprovedAtUtc != default &&
                        Enum.IsDefined(launch.CapabilityProfile) &&
                        ids.Add(launch.MissionVersionId);
            if (!valid)
                throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                    $"{manifestPath} contains an invalid approved mission launch.");
        }
    }

    private static void ValidateMissionDefinitions(ProjectMissionDefinition[] definitions, string manifestPath)
    {
        var missionIds = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            if (definition.MissionId == Guid.Empty || string.IsNullOrWhiteSpace(definition.Name) || definition.Name.Trim().Length > 120 ||
                !missionIds.Add(definition.MissionId) || !names.Add(definition.Name.Trim()))
                throw InvalidDefinitions(manifestPath);
            if (definition.Draft is { } draft) ValidateDraft(draft, manifestPath);
            var versions = OrEmpty(definition.Versions);
            var ids = new HashSet<Guid>();
            var numbers = new HashSet<int>();
            foreach (var version in versions)
            {
                if (version.MissionVersionId == Guid.Empty || version.VersionNumber <= 0 || !ids.Add(version.MissionVersionId) ||
                    !numbers.Add(version.VersionNumber) || version.CandidateRevision <= 0 || version.CreatedAtUtc == default ||
                    !Enum.IsDefined(version.State) || !Enum.IsDefined(version.CapabilityProfile) ||
                    !IsDefinitionHash(version.DefinitionHash, version.DefinitionText) || version.Package is null ||
                    !ValidPackage(version.Package))
                    throw InvalidDefinitions(manifestPath);
                var cases = OrEmpty(version.EvaluationCases);
                if (cases.Select(item => item.EvaluationCaseId).Distinct().Count() != cases.Length) throw InvalidDefinitions(manifestPath);
                foreach (var evaluationCase in cases)
                {
                    try { MissionVersionService.ValidateCase(evaluationCase); }
                    catch (ProjectOperationException) { throw InvalidDefinitions(manifestPath); }
                }
                var results = OrEmpty(version.EvaluationResults);
                if (results.Select(item => item.EvaluationResultId).Distinct().Count() != results.Length ||
                    results.Any(result => !ValidResult(result, version, cases))) throw InvalidDefinitions(manifestPath);
                if (!ValidVersionState(version, cases, results)) throw InvalidDefinitions(manifestPath);
            }
            ValidateVersionLineage(versions, manifestPath);
            var approved = versions.Where(version => version.State == MissionVersionState.Approved).ToArray();
            if (approved.Length > 1 || approved.Length == 1 && definition.ActiveApprovedVersionId != approved[0].MissionVersionId ||
                approved.Length == 0 && definition.ActiveApprovedVersionId is not null)
                throw InvalidDefinitions(manifestPath);
        }
    }

    private static void ValidateDraft(MissionDraft draft, string manifestPath)
    {
        if (draft.DraftId == Guid.Empty || draft.Revision <= 0 || draft.UpdatedAtUtc == default || !Enum.IsDefined(draft.CapabilityProfile) ||
            !IsDefinitionHash(draft.DefinitionHash, draft.DefinitionText)) throw InvalidDefinitions(manifestPath);
    }

    private static bool ValidPackage(ForgeMission.Conversations.Contracts.DurableMissionPackage package)
    {
        var raw = new ForgeMission.Core.Runtime.DurableMissionPackageInput(package.FormatVersion, package.PackageHash, package.MissionSource,
            package.RootMissionName, package.RootInputName, package.ResolvedExperts.Select(expert =>
                new ForgeMission.Core.Runtime.DurableResolvedExpertInput(expert.Name, expert.LockSource, expert.LockPath, expert.LockHash, expert.ExpertMarkdown)).ToArray());
        return ForgeMission.Core.Runtime.DurableMissionPackageValidator.TryValidate(raw, out _, out _);
    }

    private static bool ValidVersionState(MissionVersion version, EvaluationCase[] cases, EvaluationResult[] results) =>
        version.ReleaseLabel is not null
            ? ValidShippedVersionState(version, cases, results)
            : version.State switch
            {
                MissionVersionState.Candidate => version.EvaluatedAtUtc is null && version.ApprovedAtUtc is null,
                MissionVersionState.Evaluated => version.EvaluatedAtUtc is not null && version.ApprovedAtUtc is null && AllCurrentCasesPassed(version, cases, results),
                MissionVersionState.Approved => version.EvaluatedAtUtc is not null && version.ApprovedAtUtc is not null && AllCurrentCasesPassed(version, cases, results),
                MissionVersionState.Superseded => version.EvaluatedAtUtc is not null && version.ApprovedAtUtc is not null && AllCurrentCasesPassed(version, cases, results),
                _ => false,
            };

    /// <summary>A version carrying a release label is shipped, release-reviewed code rather than an
    /// operator-authored candidate (Phase 48). The authored lane's bar — evaluated, with every current
    /// case passed — cannot apply to it, because Forge ran no evaluation for it and must record no
    /// outcome it did not observe. So the shipped lane asserts the opposite invariant: it is Approved
    /// (or later superseded) from the start, it was never evaluated, and it holds no case or result at
    /// all. Nothing here relaxes the authored lane above, and nothing can publish into this lane
    /// except the shipped-mission writer.</summary>
    private static bool ValidShippedVersionState(MissionVersion version, EvaluationCase[] cases, EvaluationResult[] results) =>
        cases.Length == 0 && results.Length == 0 && version.EvaluatedAtUtc is null &&
        version.State is MissionVersionState.Approved or MissionVersionState.Superseded && version.ApprovedAtUtc is not null;

    private static bool AllCurrentCasesPassed(MissionVersion version, EvaluationCase[] cases, EvaluationResult[] results) =>
        cases.Length > 0 && cases.All(evaluationCase => results.Any(result =>
            result.EvaluationCaseId == evaluationCase.EvaluationCaseId && result.CandidateRevision == version.CandidateRevision &&
            string.Equals(result.DefinitionHash, version.DefinitionHash, StringComparison.Ordinal) && result.State == EvaluationResultState.Passed));

    private static void ValidateVersionLineage(MissionVersion[] versions, string manifestPath)
    {
        var ordered = versions.OrderBy(version => version.VersionNumber).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var version = ordered[index];
            if (version.VersionNumber != index + 1)
                throw InvalidDefinitions(manifestPath);
            if (index == 0 && version.ParentVersionId is not null ||
                index > 0 && version.ParentVersionId != ordered[index - 1].MissionVersionId)
                throw InvalidDefinitions(manifestPath);
        }
    }

    private static bool ValidResult(EvaluationResult result, MissionVersion version, EvaluationCase[] cases) =>
        result.EvaluationResultId != Guid.Empty && result.EvaluationCaseId != Guid.Empty && result.MissionVersionId == version.MissionVersionId &&
        cases.Any(item => item.EvaluationCaseId == result.EvaluationCaseId) && result.CandidateRevision == version.CandidateRevision &&
        string.Equals(result.DefinitionHash, version.DefinitionHash, StringComparison.Ordinal) && Enum.IsDefined(result.State) &&
        (result.State == EvaluationResultState.Pending
            ? result.ObservedOutcome is null && result.ObservedOutputSummary is null && result.TraceOrigin is null && result.CompletedAtUtc is null
            : result.ObservedOutcome is { } outcome && Enum.IsDefined(outcome) && result.ObservedOutputSummary is not null &&
              result.CompletedAtUtc is not null && Encoding.UTF8.GetByteCount(result.ObservedOutputSummary) <= 4096 &&
              (result.TraceOrigin is null || result.TraceOrigin.ConversationId != Guid.Empty && result.TraceOrigin.TurnId != Guid.Empty && result.TraceOrigin.TurnAttemptId != Guid.Empty));

    private static ProjectOperationException InvalidDefinitions(string manifestPath) => new(ProjectOperationErrorCode.InvalidManifest,
        $"{manifestPath} contains invalid authored mission definitions.");

    internal static bool IsDefinitionHash(string hash, string definition)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(definition) || Encoding.UTF8.GetByteCount(definition) > 256 * 1024)
            return false;
        var expected = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition))).ToLowerInvariant();
        return string.Equals(hash, expected, StringComparison.Ordinal);
    }

    private static ProjectOperationException InvalidSubmission(string manifestPath) =>
        new(ProjectOperationErrorCode.InvalidManifest,
            $"{manifestPath} contains an invalid Project Mission submission record.");

    private static bool IsCompleteAcceptance(ProjectSubmissionAcceptance? acceptance) =>
        acceptance is { AcceptedSequence: > 0 } &&
        acceptance.ContainerId != Guid.Empty &&
        acceptance.RunId != Guid.Empty;

    private static byte[] SerializeManifest(ProjectManifest manifest)
    {
        EnsureJournalFitsInput(manifest.Submission);

        return JsonSerializer.SerializeToUtf8Bytes(manifest, ProjectManifestJsonContext.Default.ProjectManifest);
    }

    private static ProjectOperationException MissingManifest(string root) =>
        new(ProjectOperationErrorCode.HomeNotFound,
            $"No Forge Project manifest exists at {Path.Combine(root, ManifestFileName)}.");

    private static void ValidateAssetPath(ProjectAssetDescriptor asset, string manifestPath, string home)
    {
        var root = Path.GetFullPath(home);
        var resolved = Blank(asset.RelativePath) ? null : SafeFullPath(Path.Combine(root, asset.RelativePath));
        if (resolved is null || Path.IsPathRooted(asset.RelativePath) ||
            !resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidPath,
                $"{manifestPath} declares an asset outside the Project home: {asset.RelativePath}");
        }
    }

    private static void ValidateContextReference(ProjectContextDescriptor context, string manifestPath)
    {
        var expectsLocalPath = context.Kind is ProjectContextKind.SourceRoot or ProjectContextKind.File;
        if (Blank(context.Reference) || Path.IsPathRooted(context.Reference) != expectsLocalPath)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidPath,
                $"{manifestPath} declares a {context.Kind} context with an unusable reference: {context.Reference}");
        }
    }

    // Deserialization can leave a collection null when its key is absent, whatever the declared
    // annotation says — this is the one place that fact is handled.
    private static T[] OrEmpty<T>(T[]? items) => items ?? [];

    private static string? SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string DefaultProjectsRoot()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile))
            throw new InvalidOperationException("Unable to determine the current user's home directory.");

        return Path.Combine(profile, "Forge", "Projects");
    }
}

internal sealed record ProjectRecord(ProjectManifest Manifest, string Home);

/// <summary>Exactly one is populated: an opened Project, or the proposal a directory without a
/// manifest needs before it can become one.</summary>
internal sealed record ProjectOpenResult(ProjectRecord? Project, ProjectHomeProposal? GoalRequired);

internal sealed class ProjectOperationException(ProjectOperationErrorCode code, string message)
    : Exception(message)
{
    public ProjectOperationErrorCode Code { get; } = code;
}
