using ForgeMission.Core.Resolution;

namespace ForgeMission.Tests.Resolution;

/// <summary>
/// Phase 43.20 Task 3 — an OCI expert's materialization path is DERIVED from its immutable source
/// and is never recorded anywhere. These tests pin the derivation (so a lock file written on one
/// machine resolves on another) and prove the path stays inside the cache. Nothing here touches
/// the filesystem: the derivation is pure.
/// </summary>
public class ForgeCacheSourcePathTests
{
    private const string Digest =
        "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const string Hex =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void The_path_is_registry_repository_and_digest_under_the_expert_cache()
    {
        var source = ExpertSource.Parse($"oci://ghcr.io/katasec/forge-architect@{Digest}", "Architect");

        var expected = Path.Combine(
            ForgeCache.ExpertsRoot, "ghcr.io", "katasec", "forge-architect", "sha256", Hex, "expert.md");

        Assert.Equal(expected, ForgeCache.ExpertMdPath(source));
    }

    // Keyed by the manifest digest, not a tag: two artifacts that once shared a tag cannot collide
    // on one cache location, and a moved tag never silently reuses the previous artifact's file.
    [Fact]
    public void Two_digests_of_the_same_repository_materialize_separately()
    {
        var first = ExpertSource.Parse($"oci://ghcr.io/katasec/expert@{Digest}", "A");
        var second = ExpertSource.Parse(
            "oci://ghcr.io/katasec/expert@sha256:" + new string('a', 64), "A");

        Assert.NotEqual(ForgeCache.ExpertMdPath(first), ForgeCache.ExpertMdPath(second));
    }

    [Fact]
    public void The_same_source_always_derives_the_same_path()
    {
        var value = $"oci://ghcr.io/katasec/forge-architect@{Digest}";

        Assert.Equal(
            ForgeCache.ExpertMdPath(ExpertSource.Parse(value, "Architect")),
            ForgeCache.ExpertMdPath(ExpertSource.Parse(value, "Architect")));
    }

    // The registry and repository arrive from a file a person can hand-edit. ExpertSource refuses a
    // traversal segment, and this proves the outcome rather than trusting that it did: every
    // parseable source lands inside the cache.
    [Theory]
    [InlineData("oci://ghcr.io/katasec/forge-architect@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("oci://registry.example:5000/a/b/c/d@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("oci://ghcr.io/x@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void Every_parseable_oci_source_derives_a_path_inside_the_cache(string value)
    {
        var path = ForgeCache.ExpertMdPath(ExpertSource.Parse(value, "Expert"));

        Assert.StartsWith(
            Path.GetFullPath(ForgeCache.ExpertsRoot) + Path.DirectorySeparatorChar, path, StringComparison.Ordinal);
        Assert.Equal("expert.md", Path.GetFileName(path));
    }

    [Fact]
    public void A_project_source_has_no_cache_path_at_all()
    {
        var source = ExpertSource.Parse("project:///experts/Answerer/expert.md", "Answerer");

        Assert.Throws<ArgumentException>(() => ForgeCache.ExpertMdPath(source));
    }
}
