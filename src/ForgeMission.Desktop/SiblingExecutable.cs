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
        var exeName = OperatingSystem.IsWindows() ? $"{projectName}.exe" : projectName;
        var nativePath = Path.Combine(AppContext.BaseDirectory, exeName);
        if (File.Exists(nativePath))
            return (nativePath, null);

        var devDllPath = DevelopmentBuildPath(projectName);
        if (devDllPath is not null && File.Exists(devDllPath))
            return (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet", devDllPath);

        throw new FileNotFoundException(
            $"Could not find {projectName} next to this executable, or as a sibling dev build. " +
            $"Publish the desktop app into one folder (`make desktop-publish`). Looked for: {nativePath}" +
            (devDllPath is null ? "" : $" and {devDllPath}"));
    }

    private static string? DevelopmentBuildPath(string projectName)
    {
        var tfmDir = new DirectoryInfo(AppContext.BaseDirectory);
        var srcDir = tfmDir.Parent?.Parent?.Parent?.Parent;
        return srcDir is null
            ? null
            : Path.Combine(srcDir.FullName, projectName, "bin", tfmDir.Parent!.Name, tfmDir.Name, $"{projectName}.dll");
    }
}
