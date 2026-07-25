using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.Tools;

public sealed class WorkspaceGuardTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-guard-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void RelativePath_InsideRoot_Resolves()
    {
        Assert.True(WorkspaceGuard.TryResolve([_root], "subdir/file.txt", out var resolved, out var error));
        Assert.Null(error);
        Assert.StartsWith(_root, resolved);
    }

    [Fact]
    public void AbsolutePath_InsideRoot_Resolves()
    {
        var absolute = Path.Combine(_root, "file.txt");
        Assert.True(WorkspaceGuard.TryResolve([_root], absolute, out var resolved, out var error));
        Assert.Null(error);
        Assert.Equal(absolute, resolved);
    }

    [Fact]
    public void AbsolutePath_InsideSecondRoot_Resolves()
    {
        var secondRoot = Directory.CreateTempSubdirectory("forge-guard-second-").FullName;
        try
        {
            var absolute = Path.Combine(secondRoot, "file.txt");
            Assert.True(WorkspaceGuard.TryResolve([_root, secondRoot], absolute, out var resolved, out var error));
            Assert.Null(error);
            Assert.Equal(absolute, resolved);
        }
        finally
        {
            Directory.Delete(secondRoot, recursive: true);
        }
    }

    [Fact]
    public void RelativePath_AlwaysResolvesAgainstFirstRoot()
    {
        var secondRoot = Directory.CreateTempSubdirectory("forge-guard-second-").FullName;
        try
        {
            var primaryFile = Path.Combine(_root, "same-name.txt");
            var secondFile = Path.Combine(secondRoot, "same-name.txt");
            File.WriteAllText(primaryFile, "from primary");
            File.WriteAllText(secondFile, "from second");

            Assert.True(WorkspaceGuard.TryResolve([_root, secondRoot], "same-name.txt", out var resolved, out var error));

            Assert.Null(error);
            Assert.Equal(primaryFile, resolved);
            Assert.Equal("from primary", File.ReadAllText(resolved));
        }
        finally
        {
            Directory.Delete(secondRoot, recursive: true);
        }
    }

    [Fact]
    public void RelativePath_TraversingOutsideRoot_Rejected()
    {
        Assert.False(WorkspaceGuard.TryResolve([_root], "../../etc/passwd", out _, out var error));
        Assert.Contains("outside the workspace roots", error);
    }

    [Fact]
    public void AbsolutePath_OutsideRoot_Rejected()
    {
        var sibling = Directory.CreateTempSubdirectory("forge-guard-sibling-").FullName;
        try
        {
            Assert.False(WorkspaceGuard.TryResolve([_root], Path.Combine(sibling, "file.txt"), out _, out var error));
            Assert.NotNull(error);
        }
        finally
        {
            Directory.Delete(sibling, recursive: true);
        }
    }

    [Fact]
    public void SiblingDirectory_SharingRootAsPrefix_Rejected()
    {
        // "/root-evil" starts with "/root" as a raw string but is NOT inside it — the
        // trailing-separator boundary check must catch this, not a naive StartsWith.
        var evilSibling = _root + "-evil";
        Directory.CreateDirectory(evilSibling);
        try
        {
            Assert.False(WorkspaceGuard.TryResolve([_root], Path.Combine(evilSibling, "file.txt"), out _, out var error));
            Assert.NotNull(error);
        }
        finally
        {
            Directory.Delete(evilSibling, recursive: true);
        }
    }

    [Fact]
    public void EmptyPath_Rejected()
    {
        Assert.False(WorkspaceGuard.TryResolve([_root], "", out _, out var error));
        Assert.Equal("file_path is required", error);
    }

    [SkippableFact]
    public void Symlink_PointingOutsideRoot_Rejected()
    {
        var outside = Directory.CreateTempSubdirectory("forge-guard-outside-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(outside, "secret.txt"), "top secret");
            var linkPath = Path.Combine(_root, "escape-link");

            var canCreateSymlinks = true;
            try
            {
                Directory.CreateSymbolicLink(linkPath, outside);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                canCreateSymlinks = false;
            }
            Skip.If(!canCreateSymlinks, "Symlink creation not permitted on this machine (needs elevation/Developer Mode on Windows)");

            Assert.False(WorkspaceGuard.TryResolve([_root], "escape-link/secret.txt", out _, out var error));
            Assert.NotNull(error);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }
}
