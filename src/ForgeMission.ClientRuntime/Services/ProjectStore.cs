using System.Text;
using System.Text.Json;
using ForgeMission.ClientRuntime.Transport;

namespace ForgeMission.ClientRuntime.Services;

// The single owner of local Project state (43.20 task 1): title/slug/home derivation, the
// collision-safe write, manifest reading and validation, and every named failure. No surface
// derives a Project value or touches the filesystem — Desktop today and a TUI later both reach
// this class only through the project transport contracts, so they get identical results.
//
// Expected domain failures throw ProjectOperationException and are mapped to a typed
// ProjectOperationError once, at the transport endpoint. The exception never leaves Client Runtime.
internal sealed class ProjectStore
{
    public const string ManifestFileName = "forge.project.json";

    private const int MaxTitleLength = 60;
    private const int MaxSlugLength = 40;
    private const int MaxCollisionAttempts = 100;
    private const string SlugFallback = "project";

    private readonly string _projectsRoot;
    private readonly ProjectManifestFile _manifestFile;

    public ProjectStore(string? projectsRoot = null, ProjectManifestFile? manifestFile = null)
    {
        _projectsRoot = projectsRoot ?? DefaultProjectsRoot();
        _manifestFile = manifestFile ?? new ProjectManifestFile();
    }

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
        if (!ProjectMissions.IsAllowed(mission))
            throw new ProjectOperationException(ProjectOperationErrorCode.UnknownMission,
                $"'{mission}' is not a mission this Project can run.");

        var selected = ProjectMissions.Reference(mission);
        return UpdateAsync(home, manifest =>
        {
            if (manifest.SelectedMission is { Origin: ProjectMissionOrigin.BuiltIn } current &&
                string.Equals(current.Reference, selected.Reference, StringComparison.Ordinal))
                return manifest;

            return manifest with { SelectedMission = selected };
        }, cancellationToken);
    }

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
                    ProjectMissions.RequireSelected(manifest.SelectedMission),
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
                ProjectMissions.RequireSelected(manifest.SelectedMission),
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
        // relative path against the Client Runtime's own working directory, which is never a
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

    private ProjectRecord? TryWriteNewManifest(string home, string title, string goal)
    {
        var manifest = new ProjectManifest(
            ProjectManifest.CurrentSchemaVersion,
            Guid.NewGuid(),
            title,
            goal,
            [],
            ProjectMissionReference.BuiltInJanus,
            [],
            ProjectMissionContainerId: null,
            [],
            LegacyProjectControlConversationId: null,
            MissionControlConversationId: null,
            Submission: null);

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
        };

        foreach (var asset in normalized.Assets)
            ValidateAssetPath(asset, manifestPath, home);
        foreach (var context in normalized.AttachedContext)
            ValidateContextReference(context, manifestPath);
        ValidateSubmission(normalized.Submission, manifestPath);

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
                            ProjectMissions.IsAllowed(submission.Mission) &&
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
