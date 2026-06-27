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

    private static LockFile BuildLockFile(string missionDir, string? hash = null)
    {
        var expertPath = Path.Combine(missionDir, "experts", "TestExpert", "expert.md");
        var computedHash = hash ?? LockFileIO.ComputeHash(expertPath);
        return new LockFile
        {
            Experts =
            {
                ["TestExpert"] = new LockFileExpert
                {
                    Source = "experts",
                    Path   = "experts/TestExpert/expert.md",
                    Hash   = computedHash,
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
        var lockFile = BuildLockFile(dir, hash: "0000000000000000000000000000000000000000000000000000000000000000");

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
                    Source = "experts",
                    Path   = "experts/TestExpert/expert.md",
                    Hash   = "abc123",
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
                    Source = "experts",
                    Path   = "experts/TestExpert/expert.md",
                    Hash   = null, // legacy — no hash recorded
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

    [Fact]
    public void LocalExpert_ShadowingCacheEntry_EmitsWarning()
    {
        // Two dirs: "cache" holds the OCI version, "mission" holds a local override.
        var cacheDir   = Directory.CreateTempSubdirectory("forge-cache-").FullName;
        var missionDir = Directory.CreateTempSubdirectory("forge-mission-").FullName;

        // Write an expert in the cache dir
        var cacheExpertDir = Path.Combine(cacheDir, "TestExpert");
        Directory.CreateDirectory(cacheExpertDir);
        var cachePath = Path.Combine(cacheExpertDir, "expert.md");
        File.WriteAllText(cachePath, ExpertMd);

        // Write a different local expert with the same name
        var localExpertDir = Path.Combine(missionDir, "experts", "TestExpert");
        Directory.CreateDirectory(localExpertDir);
        File.WriteAllText(Path.Combine(localExpertDir, "expert.md"), ExpertMd.Replace("test expert", "local override"));

        // Lock file points to the cache path
        var lockFile = new LockFile
        {
            Experts =
            {
                ["TestExpert"] = new LockFileExpert
                {
                    Source = "cache",
                    Path   = cachePath,
                    Hash   = LockFileIO.ComputeHash(cachePath),
                }
            }
        };

        var warnings = new StringWriter();
        ExpertResolver.ResolveAll(lockFile, missionDir, warnings: warnings);

        Assert.Contains("MCL010", warnings.ToString());
        Assert.Contains("TestExpert", warnings.ToString());
        Assert.Contains("shadows", warnings.ToString());
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
                Source = "experts",
                Path   = $"experts/{name}/expert.md",
                Hash   = LockFileIO.ComputeHash(path),
            };
        }

        var result = ExpertResolver.ResolveAll(lockFile, dir);

        Assert.Equal(3, result.Count);
        Assert.True(result.ContainsKey("ExpertA"));
        Assert.True(result.ContainsKey("ExpertB"));
        Assert.True(result.ContainsKey("ExpertC"));
    }
}
