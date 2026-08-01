using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Resolution;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;
using ForgeMission.Parser;

namespace ForgeMission.Desktop.Services;

public sealed class VanillaMissionSessionFactory(IChatClientFactory chatClients)
{
    private static readonly string MissionDirectory = Path.Combine(AppContext.BaseDirectory, "missions", "vanilla");

    public VanillaMissionSession Create(
        LocalDiskWorkspace workspace,
        Func<ToolCallNotification, CancellationToken, Task>? notifyToolCall = null)
    {
        var missionPath = Path.Combine(MissionDirectory, "mission.mcl");
        if (!File.Exists(missionPath))
            throw new InvalidOperationException($"Bundled vanilla mission not found: {missionPath}");

        var ast = MclParser.Parse(File.ReadAllText(missionPath));
        var manifest = ForgeTomlReader.TryRead(missionPath)
            ?? throw new InvalidOperationException("Bundled vanilla mission is missing forge.toml.");
        var lockFile = LockFileIO.Read(Path.Combine(MissionDirectory, "mcl.lock"));
        var experts = ExpertResolver.ResolveAll(lockFile, MissionDirectory);

        // Validate the mission's let bindings before each independent one-shot run.
        _ = ContextBuilder.Seed(ast);

        var runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal);
        foreach (var (name, profile) in manifest.Providers)
            runners[name] = new DirectExpertRunner(chatClients.Create(profile));

        var mission = ast.Declarations.OfType<MissionDeclaration>().FirstOrDefault()
            ?? throw new InvalidOperationException("Bundled vanilla mission has no mission declaration.");
        var pipelineRunner = new PipelineRunner(runners, manifest.Execution);
        var capabilities = new CapabilityRegistry(
        [
            new WorkspaceFileProvider(workspace),
            new WorkspaceTerminalProvider(workspace),
        ]);
        var dispatcher = new CapabilityDispatcher(
            capabilities,
            new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default),
            new InMemoryCapabilityAuditLog());
        return new VanillaMissionSession(
            new AgenticSession(ast, experts, pipelineRunner, capabilities, dispatcher, notifyToolCall: notifyToolCall),
            mission.Name);
    }
}

public sealed record VanillaMissionSession(AgenticSession AgenticSession, string MissionName);
