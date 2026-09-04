using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ForgeMission.Core.Resolution;

public class LockFile
{
    /// <summary>v2 (43.20 task 3): one canonical <c>source</c> URI plus one content digest per
    /// expert. v1's <c>{ source, path, hash }</c> is read only to migrate a local entry in
    /// memory — every lock file this writes is v2.</summary>
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;
    public Dictionary<string, LockFileExpert> Experts { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// One resolved expert. <see cref="Source"/> is the whole identity — a
/// <c>project:///…</c> or immutable <c>oci://…@sha256:…</c> URI whose syntax selects the resolver
/// (see <see cref="ExpertSource"/>). No machine-local materialization path is ever stored: an OCI
/// expert's cache location is derived from the source by <see cref="ForgeCache"/>, so a lock file
/// means the same thing on every machine.
/// </summary>
public class LockFileExpert
{
    public string Source { get; set; } = "";

    /// <summary>Full <c>sha256:…</c> of the resolved <c>expert.md</c> — deliberately distinct from
    /// the manifest digest embedded in an OCI source, which identifies the artifact rather than
    /// its content. Null only for a migrated legacy v1 entry that recorded no hash; a lock file
    /// this writes always carries one.</summary>
    public string? ContentDigest { get; set; }
}

public static class LockFileIO
{
    // The on-disk shape: every field either version may carry, read into one POCO so a v1 file can
    // be recognised and migrated rather than silently losing its path/hash to
    // IgnoreUnmatchedProperties and arriving as a v2 record with an empty source.
    private class LockFileDocument
    {
        public int Version { get; set; }
        public Dictionary<string, LockFileEntryDocument> Experts { get; set; } = new(StringComparer.Ordinal);
    }

    private class LockFileEntryDocument
    {
        public string? Source { get; set; }
        public string? ContentDigest { get; set; }

        // v1 only.
        public string? Path { get; set; }
        public string? Hash { get; set; }
    }

    // LockFile/LockFileExpert are public POCOs directly instantiated here, so the trimmer
    // preserves them. The IL3050 on DeserializerBuilder is conservative — reflection works
    // in AOT for preserved types.
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LockFile))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LockFileExpert))]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Types preserved via DynamicDependency")]
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LockFileDocument))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LockFileEntryDocument))]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Types preserved via DynamicDependency")]
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static void Write(string path, LockFile lockFile)
        => File.WriteAllText(path, Serializer.Serialize(lockFile));

    public static LockFile Read(string path)
        => Parse(File.ReadAllText(path), path);

    /// <summary>
    /// Reads a lock file forward into the v2 model. A v1 LOCAL entry migrates in memory; a v1 OCI
    /// entry cannot, because it records a cache path and a tag but no manifest digest — its
    /// immutable identity is simply not in the file, and reconstructing one from the cache path
    /// would invent provenance. That case is named and refused so the next <c>forge init</c> can
    /// resolve it honestly.
    /// </summary>
    public static LockFile Parse(string yaml, string path)
    {
        LockFileDocument? document;
        try
        {
            document = Deserializer.Deserialize<LockFileDocument>(yaml);
        }
        catch (YamlDotNet.Core.YamlException exception)
        {
            // The YAML parser's own message names a line and column but not what the file was
            // supposed to be, so every caller would otherwise have to guess. One named diagnostic
            // here serves the CLI, the Runner, the Worker, and the Explorer alike.
            throw new MclException(
                MclErrorCode.LockFileNeedsReinit,
                $"{path} is not readable as a Forge lock file: {exception.Message}",
                "Run 'forge init' to regenerate it.");
        }

        if (document is null)
            throw new MclException(MclErrorCode.StaleLockFile, $"{path} is empty.");

        if (document.Version > LockFile.CurrentVersion)
            throw new MclException(
                MclErrorCode.LockFileNeedsReinit,
                $"{path} was written by a newer version of Forge (lock file version {document.Version}).");

        var lockFile = new LockFile { Version = LockFile.CurrentVersion };
        foreach (var (name, entry) in document.Experts)
            lockFile.Experts[name] = document.Version >= LockFile.CurrentVersion
                ? ReadCurrent(name, entry, path)
                : MigrateV1(name, entry, path);

        return lockFile;
    }

    public static LockFile Build(
        Dictionary<string, ResolvedExpert> catalog,
        string missionDirectory)
    {
        var lf = new LockFile();
        foreach (var (name, expert) in catalog.OrderBy(k => k.Key))
        {
            var relativePath = Path.GetRelativePath(missionDirectory, expert.ExpertMdPath);
            lf.Experts[name] = new LockFileExpert
            {
                Source = ExpertSource.ForProjectPath(relativePath),
                ContentDigest = ComputeContentDigest(expert.ExpertMdPath),
            };
        }
        return lf;
    }

    /// <summary>Bare lower-case hex SHA-256 of a file.</summary>
    public static string ComputeHash(string filePath)
    {
        var bytes = SHA256.HashData(File.ReadAllBytes(filePath));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>The prefixed <c>sha256:…</c> form a lock file records.</summary>
    public static string ComputeContentDigest(string filePath) => "sha256:" + ComputeHash(filePath);

    // --- reading ---------------------------------------------------------------------------------

    private static LockFileExpert ReadCurrent(string name, LockFileEntryDocument entry, string path)
    {
        // Parse for validation only: an unparseable source is refused here rather than deeper in,
        // where it would surface as a confusing missing-file error.
        ExpertSource.Parse(entry.Source, name);

        if (!ExpertSource.IsContentDigest(entry.ContentDigest))
            throw new MclException(
                MclErrorCode.InvalidLockSource,
                $"Expert '{name}' in {path} has no usable content digest: '{entry.ContentDigest}'.",
                "A v2 lock file records the full lower-case sha256:… of the resolved expert.md. " +
                "Run 'forge init' to regenerate the lock file.");

        return new LockFileExpert { Source = entry.Source!, ContentDigest = entry.ContentDigest };
    }

    private static LockFileExpert MigrateV1(string name, LockFileEntryDocument entry, string path)
    {
        if (!string.Equals(entry.Source, SourceResolver.DefaultExpertsDir, StringComparison.Ordinal))
            throw new MclException(
                MclErrorCode.LockFileNeedsReinit,
                $"Expert '{name}' in {path} is a version 1 OCI entry and cannot be read forward.",
                "A version 1 lock file records a registry tag and a machine-local cache path, but not the " +
                "immutable manifest digest a portable source needs. Run 'forge init' to resolve it again.");

        if (string.IsNullOrWhiteSpace(entry.Path))
            throw new MclException(
                MclErrorCode.LockFileNeedsReinit,
                $"Expert '{name}' in {path} is a version 1 local entry with no path.",
                "Run 'forge init' to regenerate the lock file.");

        return new LockFileExpert
        {
            Source = ExpertSource.ForProjectPath(entry.Path!),
            // A legacy v1 entry could omit its hash entirely. That stays absent rather than being
            // invented; verification is skipped for it exactly as it was before, and the next
            // 'forge init' records a real digest.
            ContentDigest = entry.Hash is { Length: > 0 } hash ? "sha256:" + hash.ToLowerInvariant() : null,
        };
    }
}
