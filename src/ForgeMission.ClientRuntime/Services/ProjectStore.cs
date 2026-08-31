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
internal sealed class ProjectStore(string? projectsRoot = null)
{
    public const string ManifestFileName = "forge.project.json";

    private const int MaxTitleLength = 60;
    private const int MaxSlugLength = 40;
    private const int MaxCollisionAttempts = 100;
    private const string SlugFallback = "project";

    private readonly string _projectsRoot = projectsRoot ?? DefaultProjectsRoot();

    /// <summary>Pure: what a create would use, for display before confirmation. It performs no
    /// filesystem work at all — not even a collision probe, which would be both an access and an
    /// implied reservation. <see cref="Create"/> stays authoritative for the final home.</summary>
    public ProjectHomeProposal Draft(string goal, string? titleOverride, string? homeOverride)
    {
        var title = DeriveTitle(RequiredGoal(goal), titleOverride);
        var home = Blank(homeOverride) ? Path.Combine(_projectsRoot, Slugify(title)) : ValidHome(homeOverride!);
        return new ProjectHomeProposal(home, title);
    }

    /// <summary>Creates the Project home and its v1 manifest. Create — never a draft, and never a
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

        var manifestPath = Path.Combine(home, ManifestFileName);
        if (!File.Exists(manifestPath))
            return new ProjectOpenResult(null, new ProjectHomeProposal(home, TitleFromDirectory(home)));

        return new ProjectOpenResult(new ProjectRecord(Read(manifestPath, home), home), null);
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
            MissionControlConversationId: null,
            []);

        Directory.CreateDirectory(home);
        try
        {
            using var file = new FileStream(Path.Combine(home, ManifestFileName), FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(file, manifest, ProjectManifestJsonContext.Default.ProjectManifest);
        }
        catch (IOException) when (File.Exists(Path.Combine(home, ManifestFileName)))
        {
            // Another writer won this home between the free-candidate check and this write. Any
            // other IOException (permissions, full disk) is a real fault and must not be reported
            // as a collision, so it stays unhandled here.
            return null;
        }

        return new ProjectRecord(manifest, home);
    }

    // --- reading and validation -----------------------------------------------------------------

    private static ProjectManifest Read(string manifestPath, string home)
    {
        ProjectManifest? manifest;
        try
        {
            using var file = File.OpenRead(manifestPath);
            manifest = JsonSerializer.Deserialize(file, ProjectManifestJsonContext.Default.ProjectManifest);
        }
        catch (JsonException exception)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                $"{manifestPath} is not readable as a Forge Project manifest: {exception.Message}");
        }

        if (manifest is null)
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                $"{manifestPath} is empty.");

        return Validate(manifest, manifestPath, home);
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
        // above are what a Project cannot be without.
        var normalized = manifest with
        {
            Assets = OrEmpty(manifest.Assets),
            AttachedContext = OrEmpty(manifest.AttachedContext),
            Runs = OrEmpty(manifest.Runs),
        };

        foreach (var asset in normalized.Assets)
            ValidateAssetPath(asset, manifestPath, home);
        foreach (var context in normalized.AttachedContext)
            ValidateContextReference(context, manifestPath);

        return normalized;
    }

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
