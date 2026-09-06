using System.Xml.Linq;

namespace ForgeMission.Tests.Architecture;

public sealed class ClientExecutionBoundaryTests
{
    [Fact]
    public void Bob_ReferencesOnlyCore_AndDoesNotNameApplicationTransportConversationHttpUiOrProviders()
    {
        var root = RepositoryRoot();
        var projectPath = Path.Combine(root, "src", "ForgeMission.ClientRuntime", "ForgeMission.ClientRuntime.csproj");
        var project = XDocument.Load(projectPath);
        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(((string?)reference.Attribute("Include") ?? string.Empty)
                .Replace('\\', Path.DirectorySeparatorChar)))
            .ToList();

        Assert.Equal(["ForgeMission.Core"], references);
        Assert.Empty(project.Descendants("PackageReference"));

        foreach (var source in Directory.EnumerateFiles(Path.Combine(root, "src", "ForgeMission.ClientRuntime"), "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .Where(path => !Path.GetFileName(path).StartsWith("Properties.", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(source);
            foreach (var forbidden in new[]
                     {
                         "ForgeMission.Application", "ForgeMission.Conversations", "ForgeMission.Presentation",
                         "System.Net.Http", "HttpClient", "Microsoft.Extensions.AI.IChatClient",
                         "OpenAI", "Anthropic", "ProviderClientBuilder",
                     })
            {
                Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
            }
        }
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
