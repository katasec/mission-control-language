namespace ForgeMission.Tests.Architecture;

// Any future Presentation project opts in with <ClientRuntimePresentation>true</...>. The
// contract test then rejects raw HTTP use, leaving IClientRuntimeChannel as its only Client Runtime path.
public sealed class ClientRuntimePresentationBoundaryTests
{
    [Fact]
    public void MarkedPresentationProjects_CannotUseHttpClientDirectly()
    {
        var root = RepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(project => File.ReadAllText(project).Contains("<ClientRuntimePresentation>true</ClientRuntimePresentation>", StringComparison.Ordinal))
            .ToList();

        foreach (var project in projects)
            AssertNoDirectHttpClient(Directory.GetParent(project)!.FullName);
    }

    [Fact]
    public void BoundaryRule_RejectsRawHttpClientUsage()
    {
        var source = "using System.Net.Http; class Ui { HttpClient Client = new(); }";
        Assert.Throws<InvalidOperationException>(() => AssertNoDirectHttpClient(source, "Ui.cs"));
    }

    private static void AssertNoDirectHttpClient(string directory)
    {
        foreach (var file in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                     .Where(file => Path.GetExtension(file) is ".cs" or ".razor"))
            AssertNoDirectHttpClient(File.ReadAllText(file), file);
    }

    private static void AssertNoDirectHttpClient(string source, string sourceName)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(source, @"\b(HttpClient|IHttpClientFactory)\b"))
            throw new InvalidOperationException($"Presentation must use IClientRuntimeChannel, not direct HTTP: {sourceName}");
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "ForgeMission.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
