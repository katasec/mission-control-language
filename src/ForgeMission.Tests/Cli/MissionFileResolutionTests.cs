namespace ForgeMission.Tests.Cli;

/// <summary>
/// Tests for the mission file defaulting behaviour in the CLI.
/// These guard against NullReferenceException when no file argument is supplied
/// and FileInfo.DirectoryName is derived from the resolved path.
/// </summary>
public class MissionFileResolutionTests
{
    // Mirrors the ResolveMission helper in Program.cs exactly
    private static FileInfo ResolveMission(FileInfo? arg)
        => new FileInfo(Path.GetFullPath(arg?.FullName ?? "mission.fms"));

    [Fact]
    public void NoArg_ResolvesToMissionFms()
    {
        var result = ResolveMission(null);
        Assert.Equal("mission.fms", result.Name);
    }

    [Fact]
    public void NoArg_DirectoryNameIsNotNull()
    {
        // This is the exact invariant that caused the NullReferenceException.
        // new FileInfo("mission.fms").DirectoryName returns null — Path.GetFullPath fixes it.
        var result = ResolveMission(null);
        Assert.NotNull(result.DirectoryName);
    }

    [Fact]
    public void NoArg_ResolvedToAbsolutePath()
    {
        var result = ResolveMission(null);
        Assert.True(Path.IsPathRooted(result.FullName));
    }

    [Fact]
    public void WithArg_UsesProvidedFile()
    {
        var provided = new FileInfo("/some/path/custom.fml");
        var result   = ResolveMission(provided);
        Assert.Equal("custom.fml", result.Name);
    }

    [Fact]
    public void WithArg_DirectoryNameIsNotNull()
    {
        var provided = new FileInfo("/some/path/custom.fml");
        var result   = ResolveMission(provided);
        Assert.NotNull(result.DirectoryName);
    }
}
