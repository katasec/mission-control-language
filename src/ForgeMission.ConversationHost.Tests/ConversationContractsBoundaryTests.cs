namespace ForgeMission.ConversationHost.Tests;

/// <summary>
/// Cheap, source-level project-boundary tests (Phase 43.16 Task 2). These read csproj files as
/// plain text relative to the solution root — never load an assembly or walk a transitive
/// dependency graph — so a later dependency change fails here at review time, before it can hide
/// behind a runtime load that only fails deep in some other path.
/// </summary>
public class ConversationContractsBoundaryTests
{
    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ForgeMission.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                $"Could not locate ForgeMission.slnx by walking up from '{AppContext.BaseDirectory}'.");
        }

        return dir.FullName;
    }

    private static string ReadCsproj(string projectFolder, string csprojFileName)
    {
        var solutionRoot = FindSolutionRoot();
        var path = Path.Combine(solutionRoot, projectFolder, csprojFileName);
        Assert.True(File.Exists(path), $"expected csproj at '{path}'");
        return File.ReadAllText(path);
    }

    [Fact]
    public void Contracts_HasNoProjectOrPackageReferences()
    {
        var text = ReadCsproj("ForgeMission.Conversations.Contracts", "ForgeMission.Conversations.Contracts.csproj");

        Assert.DoesNotContain("<ProjectReference", text);
        Assert.DoesNotContain("<PackageReference", text);
    }

    // Client Runtime and CLI are the AOT-published, user-facing surfaces (Client Runtime may
    // reference Contracts from Task 7 — never Host). Contracts itself must also stay clean of
    // its own server-side dependencies, so it's included alongside them here.
    [Theory]
    [InlineData("ForgeMission.Conversations.Contracts", "ForgeMission.Conversations.Contracts.csproj")]
    [InlineData("ForgeMission.ClientRuntime", "ForgeMission.ClientRuntime.csproj")]
    [InlineData("ForgeMission.Cli", "ForgeMission.Cli.csproj")]
    public void ClientFacingProjects_DoNotNameConversationHostOrleansOrAzureSdk(string projectFolder, string csprojFileName)
    {
        var text = ReadCsproj(projectFolder, csprojFileName);

        Assert.DoesNotContain("ConversationHost", text);
        Assert.DoesNotContain("Orleans", text);
        Assert.DoesNotContain("Azure.", text); // Azure SDK package names (Azure.Storage.*, Azure.Messaging.ServiceBus, ...)
    }

    [Fact]
    public void ConversationHost_ReferencesOnlyContracts()
    {
        var text = ReadCsproj("ForgeMission.ConversationHost", "ForgeMission.ConversationHost.csproj");

        Assert.Contains("ForgeMission.Conversations.Contracts.csproj", text);
        Assert.DoesNotContain("Orleans", text);
        Assert.DoesNotContain("Azure.", text);
    }
}
