using System.Text;
using System.Text.Json;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Parser;

namespace ForgeMission.Core.Runtime;

public class PipelineRunner
{
    private readonly IReadOnlyDictionary<string, IExpertRunner> _runners;

    public PipelineRunner(IReadOnlyDictionary<string, IExpertRunner> runners)
    {
        _runners = runners;
    }

    // Convenience: single default runner — keeps existing tests and callers unchanged.
    public PipelineRunner(IExpertRunner defaultRunner)
        : this(new Dictionary<string, IExpertRunner>(StringComparer.Ordinal) { ["default"] = defaultRunner }) { }

    private IExpertRunner ResolveRunner(string? profileName)
    {
        var key = profileName ?? "default";
        return _runners.TryGetValue(key, out var runner)
            ? runner
            : throw new InvalidOperationException(
                $"Provider profile '{key}' not found. " +
                $"Add [providers.{key}] to forge.toml. Available: {string.Join(", ", _runners.Keys)}");
    }

    public async Task<MissionResult> RunAsync(
        Program ast,
        Dictionary<string, ExpertDefinition> experts,
        PipelineRunOptions options,
        CancellationToken ct = default)
    {
        var mission = ast.Declarations
            .OfType<MissionDeclaration>()
            .FirstOrDefault(m => m.Name == options.MissionName)
            ?? throw new InvalidOperationException(
                $"Mission '{options.MissionName}' not found in .mcl file");

        var maxLoops = mission.MaxLoops;
        MissionResult? lastResult = null;
        string? loopFeedback = null;

        for (var attempt = 1; attempt <= maxLoops; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            if (options.StepWriter is { } sw && maxLoops > 1)
                await sw.WriteLineAsync($"(attempt {attempt}/{maxLoops})");

            var context = ContextBuilder.Seed(ast, options.Vars);
            context["attempt"]   = attempt.ToString();
            context["max_loops"] = maxLoops.ToString();
            if (loopFeedback is not null)
                context["feedback"] = loopFeedback;

            string? failReason = null;

            // Track whether any when()-guarded step matched — used for when(else) and error detection.
            var anyGuardedStepMatched = false;
            var hasGuardedSteps       = mission.Pipeline.Elements
                .OfType<StepElement>()
                .Any(e => e.Step.When is StringEqualsWhen);
            var hasElseBranch         = mission.Pipeline.Elements
                .OfType<StepElement>()
                .Any(e => e.Step.When is ElseWhen);

            foreach (var element in mission.Pipeline.Elements)
            {
                ct.ThrowIfCancellationRequested();

                if (element is ParallelElement parallel)
                {
                    if (options.StepWriter is { } psw)
                    {
                        var pnames = string.Join(", ", parallel.Steps.Select(s => s.ExpertName));
                        await psw.WriteLineAsync($"→ parallel {{ {pnames} }}");
                    }

                    // Snapshot context so all parallel steps read the same base state.
                    var snapshot = new Dictionary<string, object>(context, StringComparer.Ordinal);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var tasks = parallel.Steps
                        .Select(step => ExecuteParallelStepAsync(step, ast, experts, snapshot, options, linkedCts))
                        .ToArray();

                    try
                    {
                        var results = await Task.WhenAll(tasks);
                        foreach (var (_, pkey, pout) in results)
                            context[pkey] = pout;
                        failReason = results.Select(r => r.failReason).FirstOrDefault(r => r is not null);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // A step failed and cancelled siblings — collect completed results.
                        foreach (var ptask in tasks.Where(t => t.IsCompletedSuccessfully))
                        {
                            var (_, pkey, pout) = ptask.Result;
                            context[pkey] = pout;
                        }
                        failReason = tasks
                            .Where(t => t.IsCompletedSuccessfully)
                            .Select(t => t.Result.failReason)
                            .FirstOrDefault(r => r is not null)
                            ?? "a parallel step was cancelled";
                    }

                    if (options.StepWriter is { } psw2)
                        await psw2.WriteLineAsync();

                    if (failReason is not null) break;
                    continue;
                }

                if (element is StepElement se)
                {
                    var step = se.Step;

                    if (step.When is StringEqualsWhen sw2)
                    {
                        var matched = context.TryGetValue(sw2.Key, out var val)
                                      && val?.ToString() == sw2.Value;
                        if (!matched) continue;
                        anyGuardedStepMatched = true;
                    }
                    else if (step.When is ElseWhen)
                    {
                        if (anyGuardedStepMatched) continue;
                    }

                    failReason = await ExecuteStepAsync(step, ast, experts, context, options, ct);
                    if (failReason is not null) break;
                }
            }

            if (failReason is null && hasGuardedSteps && !anyGuardedStepMatched && !hasElseBranch)
                throw new InvalidOperationException(
                    "No when() guard matched and no when(else) branch exists in the pipeline.");

            // Carry feedback written by rule/judge experts into the next loop iteration.
            if (context.TryGetValue("feedback", out var fb))
                loopFeedback = fb?.ToString();

            var text = context.TryGetValue("output", out var last) ? last.ToString()! : string.Empty;

            if (failReason is null)
                return new MissionResult(options.MissionName, text, MissionStatus.Pass, null, attempt);

            lastResult = new MissionResult(options.MissionName, text, MissionStatus.Fail, failReason, attempt);
        }

        return lastResult!;
    }

    private async Task<string?> ExecuteStepAsync(
        Step step,
        Program ast,
        Dictionary<string, ExpertDefinition> experts,
        Dictionary<string, object> context,
        PipelineRunOptions options,
        CancellationToken ct)
    {
        // Sub-mission: step name matches a declared mission → recurse.
        var subMission = ast.Declarations
            .OfType<MissionDeclaration>()
            .FirstOrDefault(m => m.Name == step.ExpertName);

        if (subMission is not null)
        {
            var childVars = step.Context.ToDictionary(
                b => b.Key,
                b => ContextBuilder.ResolveBindingValue(b.Value, context),
                StringComparer.Ordinal);

            if (options.StepWriter is { } msw)
                await msw.WriteLineAsync($"→ {step.ExpertName} (mission)...");

            var subResult = await RunAsync(ast, experts,
                new PipelineRunOptions(step.ExpertName, childVars, options.StepWriter, options.ContentWriter), ct);

            context["output"] = subResult.Text;

            return subResult.Status == MissionStatus.Fail
                ? $"[{step.ExpertName}] {subResult.FailReason ?? "sub-mission failed"}"
                : null;
        }

        if (!experts.TryGetValue(step.ExpertName, out var expert))
            throw new InvalidOperationException(
                $"Expert '{step.ExpertName}' not found. " +
                "Run 'forge validate' to check your mission before running.");

        foreach (var binding in step.Context)
            context[binding.Key] = ContextBuilder.ResolveBindingValue(binding.Value, context);

        var runner = expert.Kind switch
        {
            "http" => (IExpertRunner)new HttpExpertRunner(),
            "rule" => new RuleExpertRunner(),
            _      => ResolveRunner(step.Using)
        };

        if (options.StepWriter is { } sw)
            await sw.WriteLineAsync($"→ {step.ExpertName}...");

        StepEnvelope envelope;
        if (options.StepWriter is not null || options.ContentWriter is not null)
        {
            var sb = new StringBuilder();
            await foreach (var chunk in runner.StreamAsync(expert, context, ct))
            {
                if (options.StepWriter is { } sw2)
                    await sw2.WriteAsync(chunk);
                if (options.ContentWriter is { } cw)
                    await cw.WriteAsync(chunk);
                sb.Append(chunk);
            }
            if (options.StepWriter is { } sw3)
                await sw3.WriteLineAsync("\n");
            envelope = ParseStreamedEnvelope(sb.ToString());
        }
        else
        {
            envelope = await runner.RunAsync(expert, context, ct);
        }

        context["output"] = envelope.Text;

        if (envelope.Status == "fail")
            return $"[{step.ExpertName}] {envelope.Reason ?? "step failed"}";

        return null;
    }

    private async Task<(string? failReason, string namedKey, string outputText)> ExecuteParallelStepAsync(
        Step step,
        Program ast,
        Dictionary<string, ExpertDefinition> experts,
        Dictionary<string, object> baseContext,
        PipelineRunOptions options,
        CancellationTokenSource cts)
    {
        var namedKey = $"{step.ExpertName}.output";

        // Sub-mission in parallel block → recurse with isolated child context.
        var subMission = ast.Declarations
            .OfType<MissionDeclaration>()
            .FirstOrDefault(m => m.Name == step.ExpertName);

        if (subMission is not null)
        {
            var childVars = step.Context.ToDictionary(
                b => b.Key,
                b => ContextBuilder.ResolveBindingValue(b.Value, baseContext),
                StringComparer.Ordinal);

            var subResult = await RunAsync(ast, experts,
                new PipelineRunOptions(step.ExpertName, childVars, options.StepWriter, options.ContentWriter),
                cts.Token);

            if (subResult.Status == MissionStatus.Fail)
            {
                cts.Cancel();
                return ($"[{step.ExpertName}] {subResult.FailReason ?? "sub-mission failed"}", namedKey, subResult.Text);
            }

            return (null, namedKey, subResult.Text);
        }

        if (!experts.TryGetValue(step.ExpertName, out var expert))
            throw new InvalidOperationException(
                $"Expert '{step.ExpertName}' not found. " +
                "Run 'forge validate' to check your mission before running.");

        // Each parallel step gets its own context copy so with-bindings don't interfere.
        var localContext = new Dictionary<string, object>(baseContext, StringComparer.Ordinal);
        foreach (var binding in step.Context)
            localContext[binding.Key] = ContextBuilder.ResolveBindingValue(binding.Value, localContext);

        var runner = expert.Kind switch
        {
            "http" => (IExpertRunner)new HttpExpertRunner(),
            "rule" => new RuleExpertRunner(),
            _      => ResolveRunner(step.Using)
        };

        var envelope = await runner.RunAsync(expert, localContext, cts.Token);

        if (envelope.Status == "fail")
        {
            cts.Cancel(); // Signal siblings to stop.
            return ($"[{step.ExpertName}] {envelope.Reason ?? "step failed"}", namedKey, envelope.Text);
        }

        return (null, namedKey, envelope.Text);
    }

    private static StepEnvelope ParseStreamedEnvelope(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize(raw.Trim(), StepEnvelopeContext.Default.StepEnvelope)
                ?? new StepEnvelope(raw);
        }
        catch (JsonException)
        {
            return new StepEnvelope(raw);
        }
    }
}
