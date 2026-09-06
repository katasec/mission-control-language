namespace ForgeMission.Tests.Architecture;

public sealed class ApplicationHostRouteBoundaryTests
{
    [Fact]
    public void ApplicationHost_RoutesInjectOnlyTheirNamedTypedOwners_WithoutAGenericDispatcher()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "ForgeMission.Application.Host",
            "Transport",
            "ApplicationEndpoints.cs"));

        Assert.Contains("IApplicationSessionService service", source, StringComparison.Ordinal);
        Assert.Contains("IProjectService service", source, StringComparison.Ordinal);
        Assert.Contains("IMissionSubmissionService service", source, StringComparison.Ordinal);
        Assert.Contains("IRunHistoryService service", source, StringComparison.Ordinal);
        Assert.Contains("IProjectContentService service", source, StringComparison.Ordinal);
        Assert.Contains("IConversationService service", source, StringComparison.Ordinal);
        Assert.Contains("ICapabilityActionService service", source, StringComparison.Ordinal);
        Assert.Contains("IInteractionService service", source, StringComparison.Ordinal);

        Assert.DoesNotContain("InvokeAsync<TRequest, TResponse>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeAsync<", source, StringComparison.Ordinal);
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
