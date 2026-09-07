using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Runtime;
using Katasec.AITools;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>Executes one admitted immutable package through Core.  This owns neither a package
/// registry nor a provider profile map: every LLM step uses the Worker-composed <c>default</c>
/// runner.</summary>
internal sealed class GenericDurableMissionExecutor(IExpertRunner defaultRunner)
{
    public async Task<MissionResult> RunAsync(
        DurableMissionPackage package,
        string input,
        MissionHandsProfile profile,
        Func<PipelineTraceEvent, CancellationToken, Task> trace,
        CancellationToken ct)
    {
        if (!TryPackage(package, out var validated, out var reason))
            return new MissionResult(string.Empty, string.Empty, MissionStatus.Fail, reason);

        var runner = new PipelineRunner(defaultRunner);
        return await runner.RunAsync(validated!.Ast, validated.Experts, new PipelineRunOptions(
            validated.Input.RootMissionName,
            new Dictionary<string, string>(StringComparer.Ordinal) { [validated.Input.RootInputName] = input },
            OnTrace: trace, RootTools: RootTools(profile)), ct);
    }

    public async Task<MissionResult> ResumeAsync(DurableMissionPackage package, MissionHandsProfile profile,
        string continuation, string providerToolCallId, ConversationToolResult result, Func<PipelineTraceEvent, CancellationToken, Task> trace, CancellationToken ct)
    {
        if (!TryPackage(package, out var validated, out var reason))
            return new MissionResult(string.Empty, string.Empty, MissionStatus.Fail, reason);
        var runner = new PipelineRunner(defaultRunner);
        return await runner.ResumeAsync(validated!.Ast, validated.Experts, new PipelineResumeRequest(
            new PipelineContinuation(1, continuation), new PipelineToolResult(providerToolCallId,
                result.IsError ? PipelineToolResultStatus.Failed : PipelineToolResultStatus.Succeeded, result.Content)),
            new PipelineRunOptions(validated.Input.RootMissionName, OnTrace: trace, RootTools: RootTools(profile)), ct);
    }

    internal static bool TryPackage(DurableMissionPackage? package,
        out ValidatedDurableMissionPackage? validated, out string? reason)
    {
        validated = null;
        reason = null;
        if (package is null)
        {
            reason = "A generic durable execution requires an immutable package.";
            return false;
        }
        return DurableMissionPackageValidator.TryValidate(new DurableMissionPackageInput(
            package.FormatVersion, package.PackageHash, package.MissionSource, package.RootMissionName,
            package.RootInputName, package.ResolvedExperts.Select(expert => new DurableResolvedExpertInput(
                expert.Name, expert.LockSource, expert.LockPath, expert.LockHash, expert.ExpertMarkdown)).ToArray()),
            out validated, out reason);
    }

    private static IList<AITool>? RootTools(MissionHandsProfile profile)
    {
        var names = profile switch
        {
            MissionHandsProfile.NoHands => [],
            MissionHandsProfile.ProjectWorkspace => new[] { "Read", "Write", "Edit" },
            MissionHandsProfile.ProjectWorkspaceAndTerminal => new[] { "Read", "Write", "Edit", "Bash" },
            _ => [],
        };
        return names.Length == 0 ? null : names.Select(name => (AITool)new DeclaredTool(name,
            $"Request the bounded {name} capability from Forge.", JsonDocument.Parse("{}").RootElement.Clone())).ToList();
    }
}
