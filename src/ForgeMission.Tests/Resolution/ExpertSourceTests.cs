using ForgeMission.Core.Resolution;

namespace ForgeMission.Tests.Resolution;

/// <summary>
/// Phase 43.20 Task 3 — one canonical URI is an expert's whole identity, and its SYNTAX selects
/// the resolver. These tests are mostly about what is REFUSED: a lock file is untrusted input that
/// a person can hand-edit, and every shape below either resolves to a file outside where it claims
/// to live, or is ambiguous about which artifact it names. A source that does not parse is never
/// guessed at.
/// </summary>
public class ExpertSourceTests
{
    private const string Digest =
        "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    // --- accepted -------------------------------------------------------------------------------

    [Fact]
    public void A_project_source_carries_its_normalized_relative_path()
    {
        var source = ExpertSource.Parse("project:///experts/Answerer/expert.md", "Answerer");

        Assert.Equal(ExpertSourceKind.Project, source.Kind);
        Assert.Equal("experts/Answerer/expert.md", source.ProjectRelativePath);
        Assert.Equal("project:///experts/Answerer/expert.md", source.Value);
        Assert.Equal("", source.ManifestDigest);
    }

    [Fact]
    public void An_oci_source_splits_into_registry_repository_and_immutable_digest()
    {
        var source = ExpertSource.Parse($"oci://ghcr.io/katasec/forge-architect@{Digest}", "Architect");

        Assert.Equal(ExpertSourceKind.Oci, source.Kind);
        Assert.Equal("ghcr.io", source.Registry);
        Assert.Equal("katasec/forge-architect", source.Repository);
        Assert.Equal(Digest, source.ManifestDigest);
        Assert.Equal("", source.ProjectRelativePath);
    }

    // A repository legitimately contains slashes; only the '@' separates it from the digest.
    [Fact]
    public void A_deeply_nested_repository_path_is_kept_whole()
    {
        var source = ExpertSource.Parse($"oci://registry.example:5000/a/b/c/d@{Digest}", "Deep");

        Assert.Equal("registry.example:5000", source.Registry);
        Assert.Equal("a/b/c/d", source.Repository);
    }

    // --- refused: project sources ---------------------------------------------------------------

    [Theory]
    [InlineData("project:///../secrets/expert.md")]            // traversal
    [InlineData("project:///experts/../../etc/passwd")]        // traversal, mid-path
    [InlineData("project:////experts/A/expert.md")]            // rooted after the scheme slash
    [InlineData("project:///")]                                // empty path
    [InlineData("project:///experts//A/expert.md")]            // empty segment
    [InlineData("project:///./expert.md")]                     // dot segment
    [InlineData("project:///C:/Users/me/expert.md")]           // drive root
    [InlineData("project://authority/experts/A/expert.md")]    // project has no authority
    public void An_unsafe_or_malformed_project_source_is_refused(string source)
    {
        Assert.False(ExpertSource.TryParse(source, out _));
    }

    // --- refused: OCI sources -------------------------------------------------------------------

    [Theory]
    [InlineData("oci://ghcr.io/katasec/expert:0.1.0")]                        // a tag, not a digest
    [InlineData("oci://ghcr.io/katasec/expert")]                              // no digest at all
    [InlineData("oci://ghcr.io/katasec/expert@sha256:abc")]                   // digest too short
    [InlineData("oci://ghcr.io/katasec/expert@sha512:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("oci://ghcr.io/katasec/expert@sha256:0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("oci://ghcr.io/katasec/expert@sha256:zzzz456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("oci://ghcr.io@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("oci:///katasec/expert@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("oci://ghcr.io/@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("oci://ghcr.io/katasec/../../expert@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("oci://ghcr.io/a@b/expert@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void An_ambiguous_or_mutable_oci_source_is_refused(string source)
    {
        Assert.False(ExpertSource.TryParse(source, out _));
    }

    // --- refused: neither ------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("experts")]                                    // the v1 local marker
    [InlineData("ghcr.io/katasec/expert:0.1.0")]               // the v1 OCI marker
    [InlineData("experts/Answerer/expert.md")]                 // a bare relative path
    [InlineData("/absolute/experts/A/expert.md")]
    [InlineData("~/.forge/experts/ghcr.io/a/0.1.0/expert.md")] // a v1 cache path
    [InlineData("file:///experts/A/expert.md")]
    [InlineData(" project:///experts/A/expert.md")]            // untrimmed
    public void A_source_in_no_known_scheme_is_refused(string? source)
    {
        Assert.False(ExpertSource.TryParse(source, out _));
    }

    [Fact]
    public void Parse_names_the_expert_and_points_at_the_fix()
    {
        var failure = Assert.Throws<MclException>(() => ExpertSource.Parse("experts", "Answerer"));

        Assert.Equal(MclErrorCode.InvalidLockSource, failure.Code);
        Assert.Contains("Answerer", failure.Message, StringComparison.Ordinal);
        Assert.Contains("forge init", failure.Message, StringComparison.Ordinal);
    }

    // --- construction ---------------------------------------------------------------------------

    // A writer that could emit a source its own reader refuses would produce a lock file that only
    // fails later, on someone else's machine.
    [Fact]
    public void Every_constructed_source_parses_back_to_the_same_value()
    {
        var project = ExpertSource.ForProjectPath("experts/Answerer/expert.md");
        var oci = ExpertSource.ForOci("ghcr.io", "katasec/forge-architect", Digest);

        Assert.Equal("project:///experts/Answerer/expert.md", project);
        Assert.Equal($"oci://ghcr.io/katasec/forge-architect@{Digest}", oci);
        Assert.Equal(project, ExpertSource.Parse(project, "Answerer").Value);
        Assert.Equal(oci, ExpertSource.Parse(oci, "Architect").Value);
    }

    // Path.GetRelativePath yields backslashes on Windows; a lock file is portable, so the writer
    // normalizes rather than recording one platform's separator.
    [Fact]
    public void A_windows_style_relative_path_is_recorded_as_posix()
    {
        Assert.Equal(
            "project:///experts/Answerer/expert.md",
            ExpertSource.ForProjectPath(@"experts\Answerer\expert.md"));
    }

    [Theory]
    [InlineData("../outside/expert.md")]
    [InlineData("/absolute/expert.md")]
    [InlineData("")]
    public void An_unsafe_relative_path_cannot_be_constructed_into_a_source(string relativePath)
    {
        Assert.Throws<MclException>(() => ExpertSource.ForProjectPath(relativePath));
    }

    [Fact]
    public void A_tag_cannot_be_constructed_into_an_oci_source()
    {
        Assert.Throws<MclException>(() => ExpertSource.ForOci("ghcr.io", "katasec/expert", "0.1.0"));
    }

    // --- content digests -------------------------------------------------------------------------

    [Theory]
    [InlineData("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", true)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", false)] // bare hex
    [InlineData("sha256:0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef", false)]
    [InlineData("sha256:", false)]
    [InlineData(null, false)]
    public void A_content_digest_is_always_the_full_lower_case_sha256_form(string? digest, bool expected)
    {
        Assert.Equal(expected, ExpertSource.IsContentDigest(digest));
    }
}
