using System.Xml.Linq;

namespace ForgeMission.Tests.Architecture;

public sealed class DesktopShellBoundaryTests
{
    [Theory]
    [InlineData("ForgeMission.Desktop")]
    [InlineData("ForgeMission.Desktop.Photino")]
    public void DesktopHost_DoesNotReferenceClientRuntimeImplementation(string projectName)
    {
        var project = Path.Combine(RepositoryRoot(), "src", projectName, $"{projectName}.csproj");
        var references = XDocument.Load(project)
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(include => include is not null)
            .ToList();

        Assert.DoesNotContain(references, IsForbiddenReference);
    }

    private static bool IsForbiddenReference(string? reference) =>
        Path.GetFileNameWithoutExtension(reference) is "ForgeMission.Core" or
            "ForgeMission.ClientRuntime" or "ForgeMission.ClientRuntime.Transport";

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
