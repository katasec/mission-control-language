using System.Runtime.CompilerServices;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;

namespace ForgeMission.ConversationWorker.Tests;

/// <summary>Scripted <see cref="IExpertRunner"/> — no network/provider call, so
/// <see cref="MissionCommandProcessorTests"/> can drive a real <c>PipelineRunner</c> execution
/// hermetically. Throws if invoked when a test expects the executor path to never run (proving a
/// redelivery/mismatch really took the no-op branch, not just "happened to look the same").</summary>
internal sealed class FakeExpertRunner(Func<ExpertDefinition, Dictionary<string, object>, StepEnvelope> handler) : IExpertRunner
{
    public Task<StepEnvelope> RunAsync(ExpertDefinition expert, Dictionary<string, object> context, CancellationToken ct = default)
        => Task.FromResult(handler(expert, context));

    public async IAsyncEnumerable<string> StreamAsync(
        ExpertDefinition expert, Dictionary<string, object> context, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield return handler(expert, context).Text;
    }
}

internal sealed class ThrowingExpertRunner : IExpertRunner
{
    public Task<StepEnvelope> RunAsync(ExpertDefinition expert, Dictionary<string, object> context, CancellationToken ct = default)
        => throw new InvalidOperationException($"Executor must not run for expert '{expert.Name}' in this scenario.");

    public IAsyncEnumerable<string> StreamAsync(ExpertDefinition expert, Dictionary<string, object> context, CancellationToken ct = default)
        => throw new InvalidOperationException($"Executor must not run for expert '{expert.Name}' in this scenario.");
}
