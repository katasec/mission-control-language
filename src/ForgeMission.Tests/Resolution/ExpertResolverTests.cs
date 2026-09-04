using ForgeMission.Core.Experts;
using ForgeMission.Core.Resolution;

namespace ForgeMission.Tests.Resolution;

public class ExpertResolverTests
{
    private static readonly string ExpertMd = """
        ---
        name: TestExpert
        input: A question
        output: An answer
        ---

        You are a test expert. Answer: {{question}}
        """;

    private static string CreateMissionDir(string? expertContent = null)
    {
        var dir        = Directory.CreateTempSubdirectory("forge-resolver-test-").FullName;
        var expertDir  = Path.Combine(dir, "experts", "TestExpert");
        Directory.CreateDirectory(expertDir);
        File.WriteAllText(Path.Combine(expertDir, "expert.md"), expertContent ?? ExpertMd);
        File.WriteAllText(Path.Combine(dir, "mission.mcl"), "");
        return dir;
    }

    private static LockFile BuildLockFile(string missionDir, string? contentDigest = null)
    {
        var expertPath = Path.Combine(missionDir, "experts", "TestExpert", "expert.md");
        return new LockFile
        {
            Experts =
            {
                ["TestExpert"] = new LockFileExpert
                {
                    Source        = "project:///experts/TestExpert/expert.md",
                    ContentDigest = contentDigest ?? LockFileIO.ComputeContentDigest(expertPath),
                }
            }
        };
    }

    [Fact]
    public void LocalExpert_ResolvedCorrectly()
    {
        var dir      = CreateMissionDir();
        var lockFile = BuildLockFile(dir);

        var result = ExpertResolver.ResolveAll(lockFile, dir);

        Assert.True(result.ContainsKey("TestExpert"));
        Assert.Equal("TestExpert", result["TestExpert"].Name);
    }

    [Fact]
    public void HashMatch_DoesNotThrow()
    {
        var dir      = CreateMissionDir();
        var lockFile = BuildLockFile(dir); // correct hash

        var ex = Record.Exception(() => ExpertResolver.ResolveAll(lockFile, dir));

        Assert.Null(ex);
    }

    [Fact]
    public void HashMismatch_ThrowsExpertLoadException()
    {
        var dir      = CreateMissionDir();
        var lockFile = BuildLockFile(dir,
            contentDigest: "sha256:0000000000000000000000000000000000000000000000000000000000000000");

        var ex = Assert.Throws<ExpertLoadException>(() => ExpertResolver.ResolveAll(lockFile, dir));

        Assert.Contains("MCL009", ex.Message);
        Assert.Contains("forge init", ex.Message);
    }

    [Fact]
    public void ExpertFileMissing_ThrowsExpertLoadException()
    {
        var dir      = Directory.CreateTempSubdirectory("forge-resolver-missing-").FullName;
        var lockFile = new LockFile
        {
            Experts =
            {
                ["TestExpert"] = new LockFileExpert
                {
                    Source        = "project:///experts/TestExpert/expert.md",
                    ContentDigest = "sha256:" + new string('a', 64),
                }
            }
        };

        var ex = Assert.Throws<ExpertLoadException>(() => ExpertResolver.ResolveAll(lockFile, dir));

        Assert.Contains("MCL008", ex.Message);
        Assert.Contains("forge init", ex.Message);
    }

    [Fact]
    public void LegacyLockFileWithoutHash_SkipsVerification()
    {
        var dir      = CreateMissionDir();
        var lockFile = new LockFile
        {
            Experts =
            {
                ["TestExpert"] = new LockFileExpert
                {
                    Source        = "project:///experts/TestExpert/expert.md",
                    ContentDigest = null, // migrated legacy entry — no digest was ever recorded
                }
            }
        };

        var ex = Record.Exception(() => ExpertResolver.ResolveAll(lockFile, dir));

        Assert.Null(ex);
    }

    [Fact]
    public void VerboseMode_WritesResolutionInfo()
    {
        var dir     = CreateMissionDir();
        var lockFile = BuildLockFile(dir);
        var writer  = new StringWriter();

        ExpertResolver.ResolveAll(lockFile, dir, verbose: writer);

        var output = writer.ToString();
        Assert.Contains("TestExpert", output);
        Assert.Contains("local", output);
    }

    // A local experts/<Name>/expert.md always wins over whatever the lock file recorded, and the
    // warning is what stops that from being silent. The shadowed entry here is a project source
    // pointing elsewhere in the mission rather than a registry one: an OCI source would resolve to
    // the developer's real ~/.forge cache, and a unit test must not write there.
    [Fact]
    public void LocalExpert_ShadowingAnotherRecordedLocation_EmitsWarning()
    {
        var missionDir = Directory.CreateTempSubdirectory("forge-mission-").FullName;

        var vendoredDir = Path.Combine(missionDir, "vendor", "TestExpert");
        Directory.CreateDirectory(vendoredDir);
        var vendoredPath = Path.Combine(vendoredDir, "expert.md");
        File.WriteAllText(vendoredPath, ExpertMd);

        var localExpertDir = Path.Combine(missionDir, "experts", "TestExpert");
        Directory.CreateDirectory(localExpertDir);
        File.WriteAllText(Path.Combine(localExpertDir, "expert.md"), ExpertMd.Replace("test expert", "local override"));

        var lockFile = new LockFile
        {
            Experts =
            {
                ["TestExpert"] = new LockFileExpert
                {
                    Source        = "project:///vendor/TestExpert/expert.md",
                    ContentDigest = LockFileIO.ComputeContentDigest(vendoredPath),
                }
            }
        };

        var warnings = new StringWriter();
        ExpertResolver.ResolveAll(lockFile, missionDir, warnings: warnings);

        Assert.Contains("MCL010", warnings.ToString());
        Assert.Contains("TestExpert", warnings.ToString());
        Assert.Contains("shadows", warnings.ToString());
    }

    // The recorded digest describes the recorded file, so it must not be applied to a local
    // override that deliberately differs from it.
    [Fact]
    public void AShadowedEntrysDigest_IsNotAppliedToTheLocalOverride()
    {
        var missionDir = Directory.CreateTempSubdirectory("forge-mission-shadow-digest-").FullName;

        var vendoredDir = Path.Combine(missionDir, "vendor", "TestExpert");
        Directory.CreateDirectory(vendoredDir);
        File.WriteAllText(Path.Combine(vendoredDir, "expert.md"), ExpertMd);

        var localExpertDir = Path.Combine(missionDir, "experts", "TestExpert");
        Directory.CreateDirectory(localExpertDir);
        File.WriteAllText(Path.Combine(localExpertDir, "expert.md"), ExpertMd.Replace("test expert", "local override"));

        var lockFile = new LockFile
        {
            Experts =
            {
                ["TestExpert"] = new LockFileExpert
                {
                    Source        = "project:///vendor/TestExpert/expert.md",
                    ContentDigest = LockFileIO.ComputeContentDigest(
                        Path.Combine(vendoredDir, "expert.md")),
                }
            }
        };

        Assert.Null(Record.Exception(() => ExpertResolver.ResolveAll(lockFile, missionDir)));
    }

    // An unparseable source is refused by name rather than surfacing later as a confusing
    // missing-file error.
    [Fact]
    public void AnUnparseableSource_IsRefusedByName()
    {
        var dir = CreateMissionDir();
        var lockFile = new LockFile
        {
            Experts = { ["TestExpert"] = new LockFileExpert { Source = "experts", ContentDigest = null } }
        };

        var failure = Assert.Throws<MclException>(() => ExpertResolver.ResolveAll(lockFile, dir));

        Assert.Equal(MclErrorCode.InvalidLockSource, failure.Code);
        Assert.Contains("TestExpert", failure.Message);
    }

    [Fact]
    public void LocalExpert_NoShadowing_NoWarning()
    {
        var dir      = CreateMissionDir();
        var lockFile = BuildLockFile(dir);
        var warnings = new StringWriter();

        ExpertResolver.ResolveAll(lockFile, dir, warnings: warnings);

        Assert.Empty(warnings.ToString());
    }

    [Fact]
    public void MultipleExperts_AllResolved()
    {
        var dir = Directory.CreateTempSubdirectory("forge-resolver-multi-").FullName;

        foreach (var name in new[] { "ExpertA", "ExpertB", "ExpertC" })
        {
            var expertDir = Path.Combine(dir, "experts", name);
            Directory.CreateDirectory(expertDir);
            File.WriteAllText(Path.Combine(expertDir, "expert.md"), $"""
                ---
                name: {name}
                input: input
                output: output
                ---
                You are {name}.
                """);
        }

        var lockFile = new LockFile();
        foreach (var name in new[] { "ExpertA", "ExpertB", "ExpertC" })
        {
            var path = Path.Combine(dir, "experts", name, "expert.md");
            lockFile.Experts[name] = new LockFileExpert
            {
                Source        = $"project:///experts/{name}/expert.md",
                ContentDigest = LockFileIO.ComputeContentDigest(path),
            };
        }

        var result = ExpertResolver.ResolveAll(lockFile, dir);

        Assert.Equal(3, result.Count);
        Assert.True(result.ContainsKey("ExpertA"));
        Assert.True(result.ContainsKey("ExpertB"));
        Assert.True(result.ContainsKey("ExpertC"));
    }
}
