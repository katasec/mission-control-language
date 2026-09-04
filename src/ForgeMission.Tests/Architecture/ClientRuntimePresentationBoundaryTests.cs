namespace ForgeMission.Tests.Architecture;

// Any future Presentation project opts in with <ClientRuntimePresentation>true</...>. The
// contract test then rejects raw HTTP use, leaving IClientRuntimeChannel as its only Client Runtime path.
public sealed class ClientRuntimePresentationBoundaryTests
{
    [Fact]
    public void MarkedPresentationProjects_CannotUseHttpClientDirectly()
    {
        var root = RepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(project => File.ReadAllText(project).Contains("<ClientRuntimePresentation>true</ClientRuntimePresentation>", StringComparison.Ordinal))
            .ToList();

        foreach (var project in projects)
            AssertNoDirectHttpClient(Directory.GetParent(project)!.FullName);
    }

    [Fact]
    public void BoundaryRule_RejectsRawHttpClientUsage()
    {
        var source = "using System.Net.Http; class Ui { HttpClient Client = new(); }";
        Assert.Throws<InvalidOperationException>(() => AssertNoDirectHttpClient(source, "Ui.cs"));
    }

    // 43.20 task 1: local execution — including deriving a Project home or reading a manifest —
    // belongs to the Client Runtime. Presentation cannot hold a filesystem rule it has no API for.
    [Fact]
    public void MarkedPresentationProjects_CannotUseTheFilesystem()
    {
        var root = RepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(project => File.ReadAllText(project).Contains("<ClientRuntimePresentation>true</ClientRuntimePresentation>", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(projects); // A rule that matches no project proves nothing.
        foreach (var project in projects)
            ForEachSourceFile(Directory.GetParent(project)!.FullName, AssertNoFilesystemAccess);
    }

    // 43.20 task 2: Mission Control is the sole active conversation while a Project is open. The
    // rule is enforced by DELETION, not by routing — Presentation no longer references the Janus
    // prompt path, the mission-switch path, or the picker at all, so there is no code path to fall
    // back through. A reintroduction fails here, at review time, rather than quietly becoming a
    // second surface behaviour.
    [Fact]
    public void MarkedPresentationProjects_CannotReachTheJanusPromptOrMissionSwitchPath()
    {
        var root = RepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(project => File.ReadAllText(project).Contains("<ClientRuntimePresentation>true</ClientRuntimePresentation>", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(projects); // A rule that matches no project proves nothing.
        foreach (var project in projects)
            ForEachSourceFile(Directory.GetParent(project)!.FullName, AssertNoJanusOrMissionSwitchPath);
    }

    // 43.20 task 3: the Explorer reads a manifest and a lock file — through the Client Runtime,
    // which owns the file format, the source-URI rules, and the cache derivation. Presentation
    // gets typed entries and a text document, so it has no reason to name any of those types and
    // no way to widen an entry ID into a file read.
    [Fact]
    public void MarkedPresentationProjects_CannotReachTheLockFileOrTheExpertCache()
    {
        var root = RepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(project => File.ReadAllText(project).Contains("<ClientRuntimePresentation>true</ClientRuntimePresentation>", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(projects); // A rule that matches no project proves nothing.
        foreach (var project in projects)
            ForEachSourceFile(Directory.GetParent(project)!.FullName, AssertNoResolutionTypes);
    }

    [Fact]
    public void BoundaryRule_RejectsLockFileAndCacheAccess()
    {
        var source = "class Ui { void Read() => LockFileIO.Read(\"mcl.lock\"); }";
        Assert.Throws<InvalidOperationException>(() => AssertNoResolutionTypes(source, "Ui.cs"));
    }

    // 43.21 task 1: a person selects a named mission, never a provider, a model, or an expert.
    // Presentation therefore has no reason to name any of the execution types those choices would
    // be made with.
    [Fact]
    public void MarkedPresentationProjects_CannotNameAProviderModelOrExpert()
    {
        var root = RepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(project => File.ReadAllText(project).Contains("<ClientRuntimePresentation>true</ClientRuntimePresentation>", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(projects); // A rule that matches no project proves nothing.
        foreach (var project in projects)
            ForEachSourceFile(Directory.GetParent(project)!.FullName, AssertNoExecutionChoice);
    }

    // 43.21 task 2: the legacy Project Control route is gone from this surface, and it is gone by
    // DELETION rather than by no longer being called — there is no identifier, route, or string
    // left to reach for. The rule reads raw source text on purpose: a comment or a field name that
    // still says "Mission Control" would leave the old product name in the one place a person can
    // actually see, and the visible notice for a migrated Project says "legacy history" for
    // exactly that reason.
    [Fact]
    public void MarkedPresentationProjects_CannotReachOrNameTheLegacyControlRoute()
    {
        var root = RepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(project => File.ReadAllText(project).Contains("<ClientRuntimePresentation>true</ClientRuntimePresentation>", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(projects); // A rule that matches no project proves nothing.
        foreach (var project in projects)
            ForEachSourceFile(Directory.GetParent(project)!.FullName, AssertNoLegacyControlRoute);
    }

    [Fact]
    public void BoundaryRule_RejectsTheLegacyControlRoute()
    {
        var source = "class Ui { void Open() => Channel.SendAsync<OpenProjectMissionControlRequest, X>(null); }";
        Assert.Throws<InvalidOperationException>(() => AssertNoLegacyControlRoute(source, "Ui.cs"));
    }

    [Fact]
    public void BoundaryRule_RejectsTheLegacyProductNameInVisibleText()
    {
        var source = "<p>This Project has earlier Mission Control history.</p>";
        Assert.Throws<InvalidOperationException>(() => AssertNoLegacyControlRoute(source, "Ui.razor"));
    }

    [Fact]
    public void BoundaryRule_RejectsNamingAnExecutionChoice()
    {
        var source = "class Ui { IChatClient Client; }";
        Assert.Throws<InvalidOperationException>(() => AssertNoExecutionChoice(source, "Ui.cs"));
    }

    [Fact]
    public void BoundaryRule_RejectsTheJanusPromptPath()
    {
        var source = "class Ui { void Send() => Channel.SendAsync<PromptRequest, PromptResponse>(null); }";
        Assert.Throws<InvalidOperationException>(() => AssertNoJanusOrMissionSwitchPath(source, "Ui.cs"));
    }

    [Fact]
    public void BoundaryRule_RejectsFilesystemUsage()
    {
        var source = "class Ui { string Read() => File.ReadAllText(Path.Combine(\"a\", \"b\")); }";
        Assert.Throws<InvalidOperationException>(() => AssertNoFilesystemAccess(source, "Ui.cs"));
    }

    private static void AssertNoDirectHttpClient(string directory) =>
        ForEachSourceFile(directory, AssertNoDirectHttpClient);

    private static void ForEachSourceFile(string directory, Action<string, string> assert)
    {
        foreach (var file in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                     .Where(file => Path.GetExtension(file) is ".cs" or ".razor")
                     .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            assert(File.ReadAllText(file), file);
    }

    private static void AssertNoDirectHttpClient(string source, string sourceName)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(source, @"\b(HttpClient|IHttpClientFactory)\b"))
            throw new InvalidOperationException($"Presentation must use IClientRuntimeChannel, not direct HTTP: {sourceName}");
    }

    // Presentation also never reaches the Conversation service itself: it calls Client Runtime,
    // which owns the manifest read, the deterministic create, and the durable tail.
    private static void AssertNoJanusOrMissionSwitchPath(string source, string sourceName)
    {
        foreach (var forbidden in new[]
                 {
                     "PromptRequest", "SessionSetupRequest", "AttachableMission",
                     "ConversationHostClient", "conversations/",
                 })
        {
            if (source.Contains(forbidden, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Presentation must not reference '{forbidden}' while Mission Control is the sole " +
                    $"active conversation (43.20 task 2): {sourceName}");
        }
    }

    // Deliberately NOT the bare word "LockFile": ProjectExplorerEntryKind.LockFile is a legitimate
    // transport kind Presentation must render. What is banned is reaching for the format, the
    // resolver, the cache, or the file itself.
    private static void AssertNoResolutionTypes(string source, string sourceName)
    {
        foreach (var forbidden in new[]
                 {
                     "LockFileIO", "LockFileExpert", "ExpertSource", "ExpertResolver",
                     "ForgeCache", "ForgeMission.Core", "mcl.lock",
                 })
        {
            if (source.Contains(forbidden, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Presentation must not reference '{forbidden}': the Client Runtime owns the lock " +
                    $"format, source URIs, and the expert cache (43.20 task 3): {sourceName}");
        }
    }

    // Named types rather than the bare words "provider"/"model"/"expert": those appear in ordinary
    // prose and would make the rule fire on a comment. What is banned is the machinery a surface
    // would have to touch to CHOOSE one.
    private static void AssertNoExecutionChoice(string source, string sourceName)
    {
        foreach (var forbidden in new[]
                 {
                     "IChatClient", "ProviderClientBuilder", "ChatClients",
                     "ExpertDefinition", "IExpertRunner", "ProviderProfile",
                 })
        {
            if (source.Contains(forbidden, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Presentation must not reference '{forbidden}': a person selects a named mission, " +
                    $"and mission assets own expert and provider composition (43.21 task 1): {sourceName}");
        }
    }

    // Both spellings: "MissionControl" catches the contract types, the participant label and any
    // field named after them, "mission-control" catches the route, and "Mission Control" catches
    // the product name in prose a person reads.
    private static void AssertNoLegacyControlRoute(string source, string sourceName)
    {
        foreach (var forbidden in new[]
                 {
                     "ProjectControl", "MissionControl", "mission-control", "Mission Control",
                 })
        {
            if (source.Contains(forbidden, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Presentation must not reference '{forbidden}': the Missions page replaced the " +
                    $"Project Control route entirely (43.21 task 2): {sourceName}");
        }
    }

    private static void AssertNoFilesystemAccess(string source, string sourceName)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(source, @"\b(System\.IO|File|Directory|Path|FileStream|StreamReader|StreamWriter)\s*\."))
            throw new InvalidOperationException($"Presentation must not touch the filesystem: {sourceName}");
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
