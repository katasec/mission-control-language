using System.Xml.Linq;

namespace ForgeMission.Tests.Architecture;

public sealed class MclPackageConsumptionTests
{
    [Fact]
    public void Application_UsesReleasedCorePackage_NotTheForgeMclCheckout()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "ForgeMission.Application", "ForgeMission.Application.csproj"));

        Assert.DoesNotContain(project.Descendants("ProjectReference"), reference =>
            string.Equals(
                Path.GetFileNameWithoutExtension(((string?)reference.Attribute("Include") ?? string.Empty)
                    .Replace('\\', Path.DirectorySeparatorChar)),
                "ForgeMission.Core",
                StringComparison.Ordinal));

        var package = Assert.Single(project.Descendants("PackageReference"), reference =>
            string.Equals((string?)reference.Attribute("Include"), "Katasec.Forge.Mcl.Core", StringComparison.Ordinal));
        Assert.Equal("Katasec.Forge.Mcl.Core", (string?)package.Attribute("Include"));
        Assert.Equal("0.1.0", (string?)package.Attribute("Version"));
    }

    [Fact]
    public void Desktop_UsesReleasedCorePackage_NotTheForgeMclCheckout()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "ForgeMission.Desktop", "ForgeMission.Desktop.csproj"));

        Assert.DoesNotContain(project.Descendants("ProjectReference"), reference =>
            string.Equals(
                Path.GetFileNameWithoutExtension(((string?)reference.Attribute("Include") ?? string.Empty)
                    .Replace('\\', Path.DirectorySeparatorChar)),
                "ForgeMission.Core",
                StringComparison.Ordinal));

        var package = Assert.Single(project.Descendants("PackageReference"), reference =>
            string.Equals((string?)reference.Attribute("Include"), "Katasec.Forge.Mcl.Core", StringComparison.Ordinal));
        Assert.Equal("Katasec.Forge.Mcl.Core", (string?)package.Attribute("Include"));
        Assert.Equal("0.1.0", (string?)package.Attribute("Version"));
    }

    [Fact]
    public void MclConsumers_UseReleasedConversationPackages_NotLocalConversationProjects()
    {
        var root = RepositoryRoot();
        var contracts = "Katasec.Forge.Conversations.Contracts";
        var presentation = "Katasec.Forge.ConversationPresentation";
        var consumers = new[]
        {
            ("ForgeMission.Application", new[] { contracts }),
            ("ForgeMission.Application.Transport", new[] { contracts }),
            ("ForgeMission.Presentation", new[] { contracts, presentation }),
            ("ForgeMission.Tests", new[] { contracts, presentation }),
        };

        foreach (var (projectName, packages) in consumers)
        {
            var project = XDocument.Load(Path.Combine(root, "src", projectName, $"{projectName}.csproj"));

            Assert.DoesNotContain(project.Descendants("ProjectReference"), reference =>
                IsMovedConversationProject((string?)reference.Attribute("Include")));

            foreach (var packageName in packages)
            {
                var package = Assert.Single(project.Descendants("PackageReference"), reference =>
                    string.Equals((string?)reference.Attribute("Include"), packageName, StringComparison.Ordinal));
                Assert.Equal("0.1.0", (string?)package.Attribute("Version"));
            }
        }

        var solution = XDocument.Load(Path.Combine(root, "src", "ForgeMission.slnx"));
        Assert.DoesNotContain(solution.Descendants("Project"), project =>
            IsMovedConversationProject((string?)project.Attribute("Path")));

        foreach (var directory in new[]
                 {
                     "ForgeMission.Conversations.Contracts",
                     "ForgeMission.ConversationPresentation",
                     "ForgeMission.ConversationHost",
                     "ForgeMission.ConversationHost.Tests",
                     "ForgeMission.ConversationWorker",
                     "ForgeMission.ConversationWorker.Tests",
                 })
            Assert.False(Directory.Exists(Path.Combine(root, "src", directory)), $"Retired source directory still exists: {directory}");

        Assert.False(File.Exists(Path.Combine(root, "Dockerfile.conversationhost")));
        Assert.False(File.Exists(Path.Combine(root, "Dockerfile.conversationworker")));
    }

    private static bool IsMovedConversationProject(string? path)
        => new[]
            {
                "ForgeMission.Conversations.Contracts",
                "ForgeMission.ConversationPresentation",
                "ForgeMission.ConversationHost",
                "ForgeMission.ConversationHost.Tests",
                "ForgeMission.ConversationWorker",
                "ForgeMission.ConversationWorker.Tests",
            }
            .Contains(
                Path.GetFileNameWithoutExtension((path ?? string.Empty).Replace('\\', Path.DirectorySeparatorChar)),
                StringComparer.Ordinal);

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
