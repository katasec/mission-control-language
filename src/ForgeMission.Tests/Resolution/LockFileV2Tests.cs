using ForgeMission.Core.Resolution;

namespace ForgeMission.Tests.Resolution;

/// <summary>
/// Phase 43.20 Task 3 — mcl.lock v2: one canonical source URI plus one content digest, and no
/// machine-local path anywhere in the file.
///
/// The compatibility boundary is the interesting part. A v1 LOCAL entry can be read forward
/// honestly, because a mission-relative path and a hash are exactly what the v2 shape needs. A v1
/// OCI entry cannot: it records a tag and a cache path but never the manifest digest, so its
/// immutable identity is simply absent from the file. Reconstructing one from the cache path would
/// invent provenance, so it is refused by name instead — the fixtures below are the record of that
/// boundary, and they are why no tracked v1 lock file needs to survive to prove it.
/// </summary>
public class LockFileV2Tests
{
    private const string Digest =
        "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    // --- v2 ---------------------------------------------------------------------------------------

    [Fact]
    public void A_v2_lock_file_round_trips_through_write_and_read()
    {
        var written = new LockFile
        {
            Experts =
            {
                ["Answerer"] = new LockFileExpert
                {
                    Source = "project:///experts/Answerer/expert.md",
                    ContentDigest = Digest,
                },
                ["Architect"] = new LockFileExpert
                {
                    Source = $"oci://ghcr.io/katasec/forge-architect@{Digest}",
                    ContentDigest = Digest,
                },
            },
        };

        var path = TempLockPath();
        LockFileIO.Write(path, written);
        var read = LockFileIO.Read(path);

        Assert.Equal(LockFile.CurrentVersion, read.Version);
        Assert.Equal("project:///experts/Answerer/expert.md", read.Experts["Answerer"].Source);
        Assert.Equal($"oci://ghcr.io/katasec/forge-architect@{Digest}", read.Experts["Architect"].Source);
        Assert.Equal(Digest, read.Experts["Architect"].ContentDigest);
    }

    // The whole point of the format change: nothing in the file depends on which machine wrote it.
    [Fact]
    public void A_written_lock_file_contains_no_machine_local_path()
    {
        var path = TempLockPath();
        LockFileIO.Write(path, new LockFile
        {
            Experts =
            {
                ["Architect"] = new LockFileExpert
                {
                    Source = $"oci://ghcr.io/katasec/forge-architect@{Digest}",
                    ContentDigest = Digest,
                },
            },
        });

        var text = File.ReadAllText(path);

        Assert.Equal(2, ParseVersion(text));
        Assert.DoesNotContain("path:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("~/.forge", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetTempPath(), text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_records_project_sources_and_content_digests_for_local_experts()
    {
        var missionDir = Directory.CreateTempSubdirectory("forge-lock-build-").FullName;
        var expertPath = WriteExpert(missionDir, "Answerer");

        var built = LockFileIO.Build(
            new Dictionary<string, ResolvedExpert>(StringComparer.Ordinal)
            {
                ["Answerer"] = new("Answerer", "experts", expertPath),
            },
            missionDir);

        var entry = built.Experts["Answerer"];
        Assert.Equal(LockFile.CurrentVersion, built.Version);
        Assert.Equal("project:///experts/Answerer/expert.md", entry.Source);
        Assert.Equal(LockFileIO.ComputeContentDigest(expertPath), entry.ContentDigest);
        Assert.StartsWith("sha256:", entry.ContentDigest);
    }

    // --- v1 migration ------------------------------------------------------------------------------

    [Fact]
    public void A_v1_local_entry_is_read_forward_as_a_project_source()
    {
        var read = Parse("""
            version: 1
            experts:
              Answerer:
                source: experts
                path: experts/Answerer/expert.md
                hash: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
            """);

        var entry = read.Experts["Answerer"];
        Assert.Equal(LockFile.CurrentVersion, read.Version);
        Assert.Equal("project:///experts/Answerer/expert.md", entry.Source);
        Assert.Equal(Digest, entry.ContentDigest);
    }

    // A v1 file could omit the hash entirely. The migration must not invent one: the entry arrives
    // unverified, exactly as it was before, and the next 'forge init' records a real digest.
    [Fact]
    public void A_v1_local_entry_with_no_hash_migrates_without_inventing_a_digest()
    {
        var read = Parse("""
            version: 1
            experts:
              Answerer:
                source: experts
                path: experts/Answerer/expert.md
            """);

        Assert.Equal("project:///experts/Answerer/expert.md", read.Experts["Answerer"].Source);
        Assert.Null(read.Experts["Answerer"].ContentDigest);
    }

    [Fact]
    public void A_v1_windows_style_path_migrates_to_a_posix_source()
    {
        var read = Parse("""
            version: 1
            experts:
              Answerer:
                source: experts
                path: experts\Answerer\expert.md
            """);

        Assert.Equal("project:///experts/Answerer/expert.md", read.Experts["Answerer"].Source);
    }

    // The named boundary: a v1 OCI entry has a tag and a cache path, never a manifest digest.
    [Fact]
    public void A_v1_oci_entry_is_refused_with_a_reinitialization_diagnostic()
    {
        var failure = Assert.Throws<MclException>(() => Parse("""
            version: 1
            experts:
              KubernetesArchitect:
                source: ghcr.io/katasec/forge-kubernetes-architect:0.1.0
                path: ~/.forge/experts/ghcr.io/katasec/forge-kubernetes-architect/0.1.0/expert.md
            """));

        Assert.Equal(MclErrorCode.LockFileNeedsReinit, failure.Code);
        Assert.Contains("KubernetesArchitect", failure.Message, StringComparison.Ordinal);
        Assert.Contains("forge init", failure.Message, StringComparison.Ordinal);
    }

    // It is refused rather than reinterpreted: the cache path in the fixture above contains a
    // resolvable-looking registry, repository and version, and none of it may become a source.
    [Fact]
    public void A_v1_oci_entry_is_never_reconstructed_from_its_cache_path()
    {
        var failure = Assert.Throws<MclException>(() => Parse("""
            version: 1
            experts:
              KubernetesArchitect:
                source: ghcr.io/katasec/forge-kubernetes-architect:0.1.0
                path: ~/.forge/experts/ghcr.io/katasec/forge-kubernetes-architect/0.1.0/expert.md
            """));

        Assert.DoesNotContain("oci://", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_v1_local_entry_with_no_path_is_refused()
    {
        var failure = Assert.Throws<MclException>(() => Parse("""
            version: 1
            experts:
              Answerer:
                source: experts
            """));

        Assert.Equal(MclErrorCode.LockFileNeedsReinit, failure.Code);
    }

    [Fact]
    public void A_v1_local_entry_whose_path_escapes_the_project_is_refused()
    {
        Assert.Throws<MclException>(() => Parse("""
            version: 1
            experts:
              Answerer:
                source: experts
                path: ../../elsewhere/expert.md
            """));
    }

    // --- refused shapes -----------------------------------------------------------------------------

    // No mixed record: a v2 file must carry a real digest, so an entry that kept the v1 fields is
    // not quietly accepted as a v2 one with a missing digest.
    [Fact]
    public void A_v2_entry_without_a_content_digest_is_refused()
    {
        var failure = Assert.Throws<MclException>(() => Parse("""
            version: 2
            experts:
              Answerer:
                source: project:///experts/Answerer/expert.md
                path: experts/Answerer/expert.md
                hash: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
            """));

        Assert.Equal(MclErrorCode.InvalidLockSource, failure.Code);
        Assert.Contains("Answerer", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_v2_entry_with_a_v1_style_source_is_refused()
    {
        var failure = Assert.Throws<MclException>(() => Parse($"""
            version: 2
            experts:
              Answerer:
                source: experts
                contentDigest: {Digest}
            """));

        Assert.Equal(MclErrorCode.InvalidLockSource, failure.Code);
    }

    [Fact]
    public void A_v2_entry_with_a_bare_hex_content_digest_is_refused()
    {
        Assert.Throws<MclException>(() => Parse("""
            version: 2
            experts:
              Answerer:
                source: project:///experts/Answerer/expert.md
                contentDigest: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
            """));
    }

    [Fact]
    public void A_lock_file_from_a_newer_forge_is_refused_rather_than_guessed_at()
    {
        var failure = Assert.Throws<MclException>(() => Parse("""
            version: 3
            experts: {}
            """));

        Assert.Equal(MclErrorCode.LockFileNeedsReinit, failure.Code);
    }

    // --- helpers -------------------------------------------------------------------------------------

    private static LockFile Parse(string yaml) => LockFileIO.Parse(yaml, "mcl.lock");

    private static string TempLockPath() =>
        Path.Combine(Directory.CreateTempSubdirectory("forge-lock-").FullName, "mcl.lock");

    private static int ParseVersion(string yaml) =>
        int.Parse(yaml.Split('\n').First(line => line.StartsWith("version:", StringComparison.Ordinal))["version:".Length..].Trim());

    private static string WriteExpert(string missionDir, string name)
    {
        var expertDir = Path.Combine(missionDir, "experts", name);
        Directory.CreateDirectory(expertDir);
        var path = Path.Combine(expertDir, "expert.md");
        File.WriteAllText(path, $"""
            ---
            name: {name}
            input: A question
            output: An answer
            ---

            You are {name}.
            """);
        return path;
    }
}
