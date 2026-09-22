using ForgeMission.Runner;

namespace ForgeMission.Runner.Tests;

public sealed class RunnerWebSearchTests
{
    [Fact]
    public void Build_WithoutProviderKeys_ReturnsNull()
    {
        Assert.Null(RunnerWebSearch.Build(null, null));
    }

    [Fact]
    public void Build_UsesGrokKeyOnlyWhenXaiKeyIsAbsent()
    {
        Assert.NotNull(RunnerWebSearch.Build(null, "grok-key"));
        Assert.Null(RunnerWebSearch.Build("", "grok-key"));
    }

    [Fact]
    public void ResolveKey_PrefersNonEmptyXaiKeyOverGrokKey()
    {
        Assert.Equal("xai-key", RunnerWebSearch.ResolveKey("xai-key", "grok-key"));
    }
}
