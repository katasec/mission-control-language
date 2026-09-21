namespace ForgeMission.Desktop;

// How the Supervisor finds the children it starts. Prefers the co-located native binary (the
// published, single-folder desktop app). Falls back to `dotnet <sibling project's dll>` for the
// standard bin/<Configuration>/<TFM> layout `dotnet run` produces, so the dev loop doesn't need a
// full publish for every iteration. If neither resolves, the caller gets a clear error rather than
// a silent hang.
//
// This is also why the Supervisor has no project reference to the Host: it starts a binary by path,
// which keeps the concrete-host dependency out of the Supervisor entirely.
internal static class SiblingExecutable
{
    public static (string FileName, string? DllArgument) Resolve(string projectName)
    {
        if (TryResolvePublished(projectName, out var published))
            return published;

        var devDllPath = DevelopmentBuildPath(projectName);
        if (devDllPath is not null && File.Exists(devDllPath))
            return (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet", devDllPath);

        throw new FileNotFoundException(
            $"Could not find {projectName} next to this executable, or as a sibling dev build. " +
            $"Publish the desktop app into one folder (`make desktop-publish`). " +
            (devDllPath is null ? "" : $" and {devDllPath}"));
    }

    // The MAUI Host targets a platform/RID-specific output while the AOT Supervisor's dev build
    // remains net10.0. Do not fall back to a stale, pre-MAUI net10.0 Host DLL.
    public static (string FileName, string? DllArgument) ResolveMaui(string projectName)
    {
        if (TryResolvePublishedMaui(projectName, out var published))
            return published;

        var developmentPath = MauiDevelopmentPath(projectName);
        if (developmentPath is not null && File.Exists(developmentPath))
            return OperatingSystem.IsMacOS()
                ? (developmentPath, null)
                : (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet", developmentPath);

        throw new FileNotFoundException(
            $"Could not find {projectName} next to this executable, or as a MAUI dev build. " +
            $"Publish the desktop app into one folder (`make desktop-publish`). Looked for: {developmentPath}");
    }

    private static bool TryResolvePublishedMaui(
        string projectName,
        out (string FileName, string? DllArgument) executable)
    {
        if (TryResolvePublished(projectName, out executable))
            return true;

        if (OperatingSystem.IsMacOS())
        {
            var appPath = Path.Combine(
                AppContext.BaseDirectory,
                "Forge.app",
                "Contents",
                "MacOS",
                projectName);
            executable = (appPath, null);
            return File.Exists(appPath);
        }

        executable = default;
        return false;
    }

    private static bool TryResolvePublished(string projectName, out (string FileName, string? DllArgument) executable)
    {
        var exeName = OperatingSystem.IsWindows() ? $"{projectName}.exe" : projectName;
        var nativePath = Path.Combine(AppContext.BaseDirectory, exeName);
        executable = (nativePath, null);
        return File.Exists(nativePath);
    }

    private static string? DevelopmentBuildPath(string projectName)
    {
        var tfmDir = new DirectoryInfo(AppContext.BaseDirectory);
        var srcDir = tfmDir.Parent?.Parent?.Parent?.Parent;
        return srcDir is null
            ? null
            : Path.Combine(srcDir.FullName, projectName, "bin", tfmDir.Parent!.Name, tfmDir.Name, $"{projectName}.dll");
    }

    private static string? MauiDevelopmentPath(string projectName)
    {
        var tfmDir = new DirectoryInfo(AppContext.BaseDirectory);
        var srcDir = tfmDir.Parent?.Parent?.Parent?.Parent;
        if (srcDir is null)
            return null;

        var configuration = tfmDir.Parent!.Name;
        return OperatingSystem.IsMacOS()
            ? Path.Combine(
                srcDir.FullName,
                projectName,
                "bin",
                configuration,
                "net10.0-maccatalyst27.0",
                "maccatalyst-arm64",
                $"{projectName}.app",
                "Contents",
                "MacOS",
                projectName)
            : Path.Combine(
                srcDir.FullName,
                projectName,
                "bin",
                configuration,
                "net10.0-windows10.0.19041.0",
                "win-arm64",
                $"{projectName}.dll");
    }
}
