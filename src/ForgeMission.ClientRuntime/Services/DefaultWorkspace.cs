namespace ForgeMission.ClientRuntime.Services;

// Desktop starts in a safe, empty local workspace so chat does not wait on folder entry. The
// convention matches Visual Studio's default project root on Windows and has the same native path
// shape on macOS/Linux: <user-profile>/source/repos/0001, then 0002, and so on.
internal static class DefaultWorkspace
{
    public static string CreateNext() => CreateNext(DefaultRoot());

    internal static string CreateNext(string root)
    {
        Directory.CreateDirectory(root);

        var next = Directory.EnumerateDirectories(root)
            .Select(Path.GetFileName)
            .Select(name => int.TryParse(name, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        var workspace = Path.Combine(root, next.ToString("D4"));
        Directory.CreateDirectory(workspace);
        return workspace;
    }

    private static string DefaultRoot()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile))
            throw new InvalidOperationException("Unable to determine the current user's home directory.");

        return Path.Combine(profile, "source", "repos");
    }
}
