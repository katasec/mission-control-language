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

    // Client Runtime, CLI, and Presentation are the AOT-published/user-facing surfaces (Client
    // Runtime may reference Contracts from Task 7 — never Host). Contracts itself must also stay
    // clean of its own server-side dependencies, so it's included alongside them here. Host and
    // its own test project are the only projects allowed to name Orleans/Azure SDK packages
    // (checked positively below) — everything else in this list must not.
    [Theory]
    [InlineData("ForgeMission.Conversations.Contracts", "ForgeMission.Conversations.Contracts.csproj")]
    [InlineData("ForgeMission.ClientRuntime", "ForgeMission.ClientRuntime.csproj")]
    [InlineData("ForgeMission.Cli", "ForgeMission.Cli.csproj")]
    [InlineData("ForgeMission.ClientRuntime.Presentation", "ForgeMission.ClientRuntime.Presentation.csproj")]
    public void ClientFacingProjects_DoNotNameConversationHostOrleansOrAzureSdk(string projectFolder, string csprojFileName)
    {
        var text = ReadCsproj(projectFolder, csprojFileName);

        Assert.DoesNotContain("ConversationHost", text);
        Assert.DoesNotContain("Orleans", text);
        Assert.DoesNotContain("Azure.", text); // Azure SDK package names (Azure.Storage.*, Azure.Messaging.ServiceBus, ...)
    }

    [Fact]
    public void ConversationHost_ReferencesOnlyContracts_AndNamesTheApprovedOrleansAzurePackages()
    {
        var text = ReadCsproj("ForgeMission.ConversationHost", "ForgeMission.ConversationHost.csproj");

        // Exactly one ProjectReference — Contracts. A second project reference here would be an
        // undocumented dependency Task 4/5 did not add.
        var projectReferenceCount = System.Text.RegularExpressions.Regex.Matches(text, "<ProjectReference").Count;
        Assert.Equal(1, projectReferenceCount);
        Assert.Contains("ForgeMission.Conversations.Contracts.csproj", text);

        // The Task-4-approved packages plus Task 5's reminder/Service Bus additions, at their
        // pinned versions — no other Orleans/Azure package, no serializer package/codec of any kind.
        Assert.Contains("""<PackageReference Include="Microsoft.Orleans.Server" Version="10.0.0" />""", text);
        Assert.Contains("""<PackageReference Include="Microsoft.Orleans.Clustering.AzureStorage" Version="10.0.0" />""", text);
        Assert.Contains("""<PackageReference Include="Microsoft.Orleans.Persistence.AzureStorage" Version="10.0.0" />""", text);
        Assert.Contains("""<PackageReference Include="Microsoft.Orleans.Reminders.AzureStorage" Version="10.0.0" />""", text);
        Assert.Contains("""<PackageReference Include="Azure.Data.Tables" Version="12.11.0" />""", text);
        Assert.Contains("""<PackageReference Include="Azure.Storage.Blobs" Version="12.29.1" />""", text);
        Assert.Contains("""<PackageReference Include="Azure.Identity" Version="1.21.0" />""", text);
        Assert.Contains("""<PackageReference Include="Azure.Messaging.ServiceBus" Version="7.20.2" />""", text);
        Assert.DoesNotContain("Serialization", text);
    }

    [Fact]
    public void ConversationWorker_ReferencesOnlyContractsCoreChatClients_AndNamesOnlyTheTwoApprovedAzurePackages()
    {
        var text = ReadCsproj("ForgeMission.ConversationWorker", "ForgeMission.ConversationWorker.csproj");

        Assert.Contains("ForgeMission.Conversations.Contracts.csproj", text);
        Assert.Contains("ForgeMission.Core.csproj", text);
        Assert.Contains("ForgeMission.ChatClients.csproj", text);
        var projectReferenceCount = System.Text.RegularExpressions.Regex.Matches(text, "<ProjectReference").Count;
        Assert.Equal(3, projectReferenceCount);

        // Worker constructs the mission-command Listen and progress Send directions itself and
        // needs Azure.Identity for DefaultAzureCredential — nothing else Azure-named, and no
        // Orleans/Host/Storage/ClientRuntime dependency at all.
        Assert.Contains("""<PackageReference Include="Azure.Identity" Version="1.21.0" />""", text);
        Assert.Contains("""<PackageReference Include="Azure.Messaging.ServiceBus" Version="7.20.2" />""", text);
        Assert.DoesNotContain("ConversationHost", text);
        Assert.DoesNotContain("Orleans", text);
        Assert.DoesNotContain("Azure.Data.Tables", text);
        Assert.DoesNotContain("Azure.Storage", text);
        Assert.DoesNotContain("ClientRuntime", text);
    }

    [Fact]
    public void ConversationHostTests_NamesTestcontainersAzuriteAndDirectSshNetPinOnly_NoAzureOrleansOrAuditSuppression()
    {
        var text = ReadCsproj("ForgeMission.ConversationHost.Tests", "ForgeMission.ConversationHost.Tests.csproj");

        Assert.Contains("""<PackageReference Include="Testcontainers.Azurite" Version="4.13.0" />""", text);
        // Testcontainers.Azurite transitively pulls a vulnerable SSH.NET; a direct pin to the
        // fixed release is the real fix — never a NuGet-audit suppression for a high-severity
        // advisory.
        Assert.Contains("""<PackageReference Include="SSH.NET" Version="2026.0.0" />""", text);
        Assert.DoesNotContain("NU1903", text);
        Assert.DoesNotContain("Orleans", text);
        // Testcontainers.Azurite is the only allowed Azure-named package reference here — "Azure."
        // (with the trailing dot) matches an actual Azure SDK package name, not "Azurite".
        Assert.DoesNotContain("Azure.", text);
    }
}
