using ForgeMission.ChatClients;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Resolution;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using MclProgram = ForgeMission.Parser.Program;

namespace ForgeMission.ConversationWorker.Janus;

/// <summary>A checked-in, read-only mission the Worker can execute — parsed AST, loaded experts,
/// and a built runner per <c>forge.toml</c> provider profile. Loaded once at Worker startup from a
/// packaged directory; that directory is not local-machine authority, because only a MissionRef the
/// <see cref="Messaging.WorkerMissionResolver"/> recognises is ever executed.</summary>
public sealed class WorkerMissionContext
{
    public required MclProgram Ast { get; init; }
    public required Dictionary<string, ExpertDefinition> Experts { get; init; }
    public required IReadOnlyDictionary<string, IExpertRunner> Runners { get; init; }
    public required ExecutionConfig Execution { get; init; }
}

/// <summary>
/// Loads one packaged mission directory into a <see cref="WorkerMissionContext"/>. Extracted from
/// <see cref="JanusMissionExecutor"/> when the Worker gained its second mission (43.20 task 2), so
/// Janus and MissionControl share one loading path instead of duplicating the AST/lock/expert/
/// provider sequence.
/// </summary>
public static class WorkerMissionLoader
{
    public static WorkerMissionContext Load(string missionDirectory)
    {
        var missionPath = Path.Combine(missionDirectory, "mission.mcl");
        var lockPath = Path.Combine(missionDirectory, "mcl.lock");

        var ast = MclParser.Parse(File.ReadAllText(missionPath));

        var lockFile = LockFileIO.Read(lockPath);
        var experts = ExpertLoader.LoadFromLockFile(lockFile, missionDirectory);
        ExpertLoader.Validate(ast, experts, warnings: null, contractErrorsAreFatal: true, missionFilePath: missionPath);

        var manifest = ForgeTomlReader.TryRead(missionPath)
            ?? throw new InvalidOperationException($"No forge.toml found alongside '{missionPath}'.");

        var runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal);
        foreach (var (name, profile) in manifest.Providers)
            runners[name] = ChatClients.ChatClients.Build(profile);

        return new WorkerMissionContext { Ast = ast, Experts = experts, Runners = runners, Execution = manifest.Execution };
    }
}
