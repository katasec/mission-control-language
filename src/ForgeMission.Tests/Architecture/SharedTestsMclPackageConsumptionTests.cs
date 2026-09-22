using System.Xml.Linq;

namespace ForgeMission.Tests.Architecture;

public sealed class SharedTestsMclPackageConsumptionTests
{
    [Fact]
    public void SharedTests_ConsumeCoreFromThePrivatePackage()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "ForgeMission.Tests", "ForgeMission.Tests.csproj"));

        Assert.DoesNotContain(project.Descendants("ProjectReference"), reference =>
            string.Equals(
                Path.GetFileNameWithoutExtension(((string?)reference.Attribute("Include") ?? string.Empty)
                    .Replace('\\', Path.DirectorySeparatorChar)),
                "ForgeMission.Core",
                StringComparison.Ordinal));

        var package = Assert.Single(project.Descendants("PackageReference"), reference =>
            string.Equals((string?)reference.Attribute("Include"), "Katasec.Forge.Mcl.Core", StringComparison.Ordinal));
        Assert.Equal("0.1.0", (string?)package.Attribute("Version"));
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
