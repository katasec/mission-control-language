using System.Xml.Linq;

namespace ForgeMission.Tests.Architecture;

// The structural half of the Desktop Supervisor/Host split: the Supervisor must stay free of any
// concrete native host, and the Host must stay free of runtime, credential, and capability
// dependencies. Replacing the native host has to be a Host-side change, so these are asserted
// against the project files and sources rather than left to review.
// See docs/design/forge-architecture.md#desktop-supervisor-and-native-host-are-separate-processes.
public sealed class DesktopSupervisorHostBoundaryTests
{
    private const string Supervisor = "ForgeMission.Desktop";
    private const string Host = "ForgeMission.Desktop.Host";
    private const string PhotinoAdapter = "ForgeMission.Desktop.Photino";
    private const string Contracts = "ForgeMission.Desktop.Contracts";

    // Runtime, credential, and capability projects: the Host owns a window, not any of these.
    private static readonly string[] RuntimeProjects =
    [
        "ForgeMission.Core",
        "ForgeMission.Orchestration",
        "ForgeMission.ClientRuntime",
        "ForgeMission.ClientRuntime.Transport",
        "ForgeMission.ClientRuntime.Presentation",
        "ForgeMission.Docker",
    ];

    [Fact]
    public void Supervisor_DoesNotReferenceAConcreteHost()
    {
        Assert.DoesNotContain(PhotinoAdapter, ProjectReferences(Supervisor));
        Assert.DoesNotContain(Host, ProjectReferences(Supervisor));
        Assert.DoesNotContain("Photino.NET", PackageReferences(Supervisor));
    }

    [Fact]
    public void Supervisor_SourceDoesNotNameTheHostContractOrAConcreteHost()
    {
        foreach (var (path, text) in SourceFiles(Supervisor))
        {
            Assert.DoesNotContain("IDesktopHost", text, StringComparison.Ordinal);
            Assert.False(text.Contains("Photino", StringComparison.Ordinal), $"{path} names Photino.");
        }
    }

    [Fact]
    public void Supervisor_DoesNotReferenceClientRuntimeImplementation()
    {
        var references = ProjectReferences(Supervisor);

        Assert.DoesNotContain("ForgeMission.ClientRuntime", references);
        Assert.DoesNotContain("ForgeMission.ClientRuntime.Transport", references);
    }

    [Theory]
    [InlineData(Host)]
    [InlineData(PhotinoAdapter)]
    public void HostAndAdapter_DoNotReferenceRuntimeProjects(string projectName)
    {
        var references = ProjectReferences(projectName);

        Assert.All(RuntimeProjects, forbidden => Assert.DoesNotContain(forbidden, references));
    }

    [Fact]
    public void Contracts_HasNoDependencies()
    {
        Assert.Empty(ProjectReferences(Contracts));
        Assert.Empty(PackageReferences(Contracts));
    }

    private static List<string> ProjectReferences(string projectName) =>
        References(projectName, "ProjectReference")
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', Path.DirectorySeparatorChar)))
            .ToList();

    private static List<string> PackageReferences(string projectName) =>
        References(projectName, "PackageReference").ToList();

    private static IEnumerable<string> References(string projectName, string elementName) =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "src", projectName, $"{projectName}.csproj"))
            .Descendants(elementName)
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(include => !string.IsNullOrEmpty(include))
            .Select(include => include!);

    private static IEnumerable<(string Path, string Text)> SourceFiles(string projectName)
    {
        var projectDirectory = Path.Combine(RepositoryRoot(), "src", projectName);
        return Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(path => (Path.GetFileName(path), File.ReadAllText(path)));
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
