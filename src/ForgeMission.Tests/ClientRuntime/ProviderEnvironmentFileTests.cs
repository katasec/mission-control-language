using ForgeMission.ClientRuntime.Services;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ProviderEnvironmentFileTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("forge-provider-env-").FullName;

    [Fact]
    public void Load_UsesOnlyAllowedVariablesFromTheConfiguredFile()
    {
        var path = Path.Combine(directory, "provider.env");
        File.WriteAllText(path, """
            # local Docker provider configuration
            MCL_API_KEY=test-key
            MCL_MODEL=gpt-4o-mini
            """);

        var variables = ProviderEnvironmentFile.Load(path);

        Assert.Equal(["MCL_API_KEY=test-key", "MCL_MODEL=gpt-4o-mini"], variables);
    }

    [Fact]
    public void Load_RejectsAFileWithoutTheVanillaMissionKey()
    {
        var path = Path.Combine(directory, "provider.env");
        File.WriteAllText(path, "MCL_MODEL=gpt-4o-mini");

        var exception = Assert.Throws<InvalidOperationException>(() => ProviderEnvironmentFile.Load(path));

        Assert.Contains("MCL_API_KEY", exception.Message);
        Assert.Contains(path, exception.Message);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
