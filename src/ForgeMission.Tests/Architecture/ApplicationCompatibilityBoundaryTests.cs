namespace ForgeMission.Tests.Architecture;

public sealed class ApplicationCompatibilityBoundaryTests
{
    [Fact]
    public void LegacyProtocolsLiveInNamedApplicationAdapters_AndBobDoesNotNameTheirProtocol()
    {
        var root = RepositoryRoot();
        var application = Path.Combine(root, "src", "ForgeMission.Application");
        var janus = File.ReadAllText(Path.Combine(application, "Adapters", "Janus", "LegacyJanusToolDelivery.cs"));
        var local = File.ReadAllText(Path.Combine(application, "Adapters", "Missions", "LegacyMissionProtocolClient.cs"));
        var cloud = File.ReadAllText(Path.Combine(application, "Adapters", "Missions", "LegacyCloudMissionProtocolClient.cs"));

        Assert.Contains("ConversationParticipant.Implementer", janus, StringComparison.Ordinal);
        Assert.Contains("ICapabilityDispatcher", janus, StringComparison.Ordinal);
        Assert.DoesNotContain("CapabilityRegistry", janus, StringComparison.Ordinal);
        Assert.DoesNotContain("IChatClient", janus, StringComparison.Ordinal);
        Assert.Contains("ClientToken", cloud, StringComparison.Ordinal);
        Assert.Contains("ToolExecutorRegistry", local, StringComparison.Ordinal);

        foreach (var source in Directory.EnumerateFiles(Path.Combine(root, "src", "ForgeMission.ClientRuntime"), "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(source);
            Assert.DoesNotContain("ConversationParticipant", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ConversationEventKind", text, StringComparison.Ordinal);
            Assert.DoesNotContain("LegacyJanusToolDelivery", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CompatibilityExtraction_RemovesTheTransitionalCombinedOwner()
    {
        var application = Path.Combine(RepositoryRoot(), "src", "ForgeMission.Application");
        foreach (var source in Directory.EnumerateFiles(application, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(source);
            Assert.DoesNotContain("ConversationRuntimeSession", text, StringComparison.Ordinal);
            Assert.DoesNotContain("MissionRuntimeSession", text, StringComparison.Ordinal);
            Assert.DoesNotContain("CloudMissionRuntimeSession", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ApplicationInteractionServices", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ProjectMissionHistoryEndpointService", text, StringComparison.Ordinal);
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
