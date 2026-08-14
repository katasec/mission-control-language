using System.Buffers;
using System.Text;
using System.Text.Json;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Parser;
using Microsoft.Extensions.AI;
using Scout;

namespace ForgeMission.Core.Runtime;

public class PipelineRunner
{
    private readonly IReadOnlyDictionary<string, IExpertRunner> _runners;
    private readonly ExecutionConfig _execution;
    // Optional live-retrieval backend for kind:search experts (Scout). Null ⇒ kind:search fails clearly.
    // Injected here (not on ExecutionConfig, a TOML POCO) because it is a runtime service like _runners.
    private readonly IWebSearch? _webSearch;

    public PipelineRunner(
        IReadOnlyDictionary<string, IExpertRunner> runners,
        ExecutionConfig? execution = null,
        IWebSearch? webSearch = null)
    {
        _runners   = runners;
        _execution = execution ?? new ExecutionConfig();
        _webSearch = webSearch;
    }

    // Convenience: single default runner — keeps existing tests and callers unchanged.
    public PipelineRunner(IExpertRunner defaultRunner, IWebSearch? webSearch = null)
        : this(new Dictionary<string, IExpertRunner>(StringComparer.Ordinal) { ["default"] = defaultRunner },
               webSearch: webSearch) { }

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
        var history = IsNegotiationEligible(mission, experts) ? new SpeakerTranscript() : null;

        // Fresh array, root mission first, ending with this mission (Phase 43.16 Task 3). A
        // top-level call leaves options.MissionPath null; CreateChildOptions supplies it for a
        // nested sub-mission invocation.
        IReadOnlyList<string> missionPath = options.MissionPath ?? [options.MissionName];

        for (var attempt = 1; attempt <= maxLoops; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            if (options.StepWriter is { } sw && maxLoops > 1)
                await sw.WriteLineAsync($"(attempt {attempt}/{maxLoops})");

            var context = ContextBuilder.Seed(ast, options.Vars, options.ContextObjects);
            context["attempt"]   = attempt.ToString();
            context["max_loops"] = maxLoops.ToString();
            if (loopFeedback is not null)
                context["feedback"] = loopFeedback;
            if (history is not null)
                context["history"] = history;

            string? failReason = null;

            // Track whether any when()-guarded step matched — used for when(else) and error detection.
            var anyGuardedStepMatched = false;
            var hasGuardedSteps       = mission.Pipeline.Elements
                .OfType<StepElement>()
                .Any(e => e.Step.When is StringEqualsWhen or NumericCompareWhen);
            var hasElseBranch         = mission.Pipeline.Elements
                .OfType<StepElement>()
                .Any(e => e.Step.When is ElseWhen);

            // Tool continuation (42.3): pre-agent already ran on the original user turn and its
            // outputs were restored from the enrichment cache — resume at the agent step.
            var skipPreAgent = options.StartAtAgent;

            foreach (var element in mission.Pipeline.Elements)
            {
                ct.ThrowIfCancellationRequested();

                if (skipPreAgent)
                {
                    if (element is StepElement pre
                        && experts.TryGetValue(pre.Step.ExpertName, out var preExpert)
                        && preExpert.IsAgent)
                        skipPreAgent = false;   // reached the agent segment — run from here
                    else
                        continue;               // pre-agent element: skip (cache restored its outputs)
                }

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
                        .Select(step => ExecuteParallelStepAsync(step, ast, experts, snapshot, options, missionPath, attempt, linkedCts))
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
                    else if (step.When is NumericCompareWhen nw)
                    {
                        var matched = context.TryGetValue(nw.Key, out var raw)
                                      && TryParseDouble(raw, out var actual)
                                      && EvaluateNumericOp(actual, nw.Op, nw.Threshold);
                        if (!matched) continue;
                        anyGuardedStepMatched = true;
                    }
                    else if (step.When is ElseWhen)
                    {
                        if (anyGuardedStepMatched) continue;
                    }

                    failReason = await ExecuteStepAsync(step, ast, experts, context, options, missionPath, attempt, ct);
                    if (failReason is not null) break;

                    // Agent expert asked for a tool (42.3): return the tool_use to the client NOW.
                    // Post-agent steps (verify/judge) run on the final continuation — the request
                    // whose agent segment terminates without a tool call — never on this one.
                    if (context.TryGetValue("tool_calls", out var tc)
                        && tc is IReadOnlyList<FunctionCallContent> toolCalls)
                    {
                        // Emitted after that step's completed fact (already awaited inside
                        // ExecuteStepAsync above), once for the non-empty tool-call set that makes
                        // the pipeline return early (Phase 43.16 Task 3).
                        if (options.OnTrace is { } onToolRequested
                            && experts.TryGetValue(step.ExpertName, out var toolExpert))
                        {
                            await onToolRequested(new PipelineToolRequested(
                                options.MissionName, missionPath, step.ExpertName, toolExpert.Kind,
                                attempt, ToPipelineToolCalls(toolCalls)), ct);
                        }

                        var toolText = context.TryGetValue("output", out var o) ? o?.ToString() ?? string.Empty : string.Empty;
                        return new MissionResult(options.MissionName, toolText, MissionStatus.Pass, null, attempt, toolCalls);
                    }
                }
            }

            if (failReason is null && hasGuardedSteps && !anyGuardedStepMatched && !hasElseBranch)
                throw new InvalidOperationException(
                    "No when() guard matched and no when(else) branch exists in the pipeline.");

            // Carry feedback written by rule/judge experts into the next loop iteration.
            if (context.TryGetValue("feedback", out var fb))
                loopFeedback = fb?.ToString();

            var text = context.TryGetValue("output", out var last) ? last?.ToString() ?? string.Empty : string.Empty;

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
        IReadOnlyList<string> missionPath,
        int attempt,
        CancellationToken ct)
    {
        // Sub-mission: step name matches a declared mission → recurse. No synthetic lifecycle fact
        // is emitted for the sub-mission invocation itself (Phase 43.16 Task 3) — the actual
        // experts inside it, at the deeper path CreateChildOptions builds, are the visible trail.
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
                CreateChildOptions(options, step.ExpertName, childVars, missionPath), ct);

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
            "http"         => (IExpertRunner)new HttpExpertRunner(),
            "rule"         => new RuleExpertRunner(),
            "onnx"         => new OnnxExpertRunner(),
            "json_extract" => new JsonExtractExpertRunner(),
            "exec"         => new ExecExpertRunner(_execution.DefaultTimeout),
            "search"       => new SearchExpertRunner(_webSearch
                                  ?? throw new InvalidOperationException(
                                      "kind: search requires a configured IWebSearch (Scout). " +
                                      "Pass one to the PipelineRunner constructor."),
                                  options.OnSearchProgress),
            _              => ResolveRunner(step.Using)
        };

        // Before invoking a real expert (Phase 43.16 Task 3): its attempt is the enclosing
        // mission's current loop attempt.
        if (options.OnTrace is { } onStarted)
            await onStarted(new PipelineStepStarted(options.MissionName, missionPath, step.ExpertName, expert.Kind, attempt), ct);

        if (options.StepWriter is { } sw)
            await sw.WriteLineAsync($"→ {step.ExpertName}...");

        // Reached the agent segment on a fresh user turn: hand the caller the pre-agent output
        // for the enrichment cache (42.3 §3) — continuations restore it instead of re-running.
        if (expert.IsAgent && !options.StartAtAgent)
            options.OnPreAgentComplete?.Invoke(StringSnapshot(context));

        // Client tools attach to the agent expert's call only (42.3) — enrichment and
        // verification experts never see them. Rides the context bag like everything else.
        if (expert.IsAgent && options.Tools is { Count: > 0 })
        {
            context["tools"] = options.Tools;
            if (options.AllowMultipleToolCalls is { } allowMultiple)
                context[PipelineRuntimeInstructions.AllowMultipleToolCalls] = allowMultiple;
        }

        StepEnvelope envelope;
        try
        {
            // Never force the streaming path merely because OnTrace is configured (Task 3): several
            // non-LLM runners expose a text-only streaming adapter that cannot preserve a failing
            // StepEnvelope. This condition is unchanged from before OnTrace existed.
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

                    if (options.OnTrace is { } onDelta && !string.IsNullOrEmpty(chunk))
                        await onDelta(new PipelineStepDelta(options.MissionName, missionPath, step.ExpertName, expert.Kind, attempt, chunk), ct);
                }
                if (options.StepWriter is { } sw3)
                    await sw3.WriteLineAsync("\n");
                envelope = ParseStreamedEnvelope(sb.ToString());
            }
            else
            {
                envelope = await runner.RunAsync(expert, context, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Step '{step.ExpertName}' failed: {ex.Message}", ex);
        }
        finally
        {
            context.Remove("tools");
            context.Remove(PipelineRuntimeInstructions.AllowMultipleToolCalls);
        }

        context["output"] = envelope.Text;

        if (context.TryGetValue("history", out var historyValue)
            && historyValue is SpeakerTranscript history)
        {
            var text = envelope.Text;
            if (!string.IsNullOrWhiteSpace(envelope.Reason)
                && !string.Equals(envelope.Reason, text, StringComparison.Ordinal))
                text = $"{text}\n\nReason: {envelope.Reason}";

            history.Add(step.ExpertName, text);
        }

        // Emitted for both pass and fail envelopes, after the output/history update above and
        // before the caller decides whether the envelope fails the mission — always awaited
        // before the next step begins (Phase 43.16 Task 3).
        if (options.OnTrace is { } onCompleted)
            await onCompleted(new PipelineStepCompleted(options.MissionName, missionPath, step.ExpertName, expert.Kind, attempt, envelope), ct);

        if (envelope.Status == "fail")
            return $"[{step.ExpertName}] {envelope.Reason ?? "step failed"}";

        return null;
    }

    // One small helper (Phase 43.16 Task 3) replacing the two ad-hoc child `new
    // PipelineRunOptions(...)` calls in ExecuteStepAsync/ExecuteParallelStepAsync. Deliberately
    // does not inherit ContextObjects, Tools, StartAtAgent, or OnPreAgentComplete — preserving
    // today's isolated sub-mission/tool semantics (the essential Janus fix: Proposer/Approver run
    // under [Janus, Negotiate], Implementer under [Janus, Implement]).
    private static PipelineRunOptions CreateChildOptions(
        PipelineRunOptions parent,
        string childMissionName,
        Dictionary<string, string> childVars,
        IReadOnlyList<string> parentPath)
        => new(
            childMissionName,
            childVars,
            parent.StepWriter,
            parent.ContentWriter,
            OnSearchProgress: parent.OnSearchProgress,
            OnTrace: parent.OnTrace,
            MissionPath: [.. parentPath, childMissionName]);

    private static bool IsNegotiationEligible(
        MissionDeclaration mission,
        IReadOnlyDictionary<string, ExpertDefinition> experts)
    {
        if (mission.MaxLoops <= 1) return false;

        foreach (var element in mission.Pipeline.Elements)
        {
            if (element is not StepElement stepElement
                || !experts.TryGetValue(stepElement.Step.ExpertName, out var expert)
                || !expert.Kind.Equals("llm", StringComparison.OrdinalIgnoreCase)
                || expert.IsAgent)
                return false;
        }

        return true;
    }

    private async Task<(string? failReason, string namedKey, string outputText)> ExecuteParallelStepAsync(
        Step step,
        Program ast,
        Dictionary<string, ExpertDefinition> experts,
        Dictionary<string, object> baseContext,
        PipelineRunOptions options,
        IReadOnlyList<string> missionPath,
        int attempt,
        CancellationTokenSource cts)
    {
        var namedKey = $"{step.ExpertName}.output";

        // Sub-mission in parallel block → recurse with isolated child context. No synthetic
        // lifecycle fact for the sub-mission invocation itself, same as the sequential path.
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
                CreateChildOptions(options, step.ExpertName, childVars, missionPath),
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
            "http"         => (IExpertRunner)new HttpExpertRunner(),
            "rule"         => new RuleExpertRunner(),
            "onnx"         => new OnnxExpertRunner(),
            "json_extract" => new JsonExtractExpertRunner(),
            "exec"         => new ExecExpertRunner(_execution.DefaultTimeout),
            "search"       => new SearchExpertRunner(_webSearch
                                  ?? throw new InvalidOperationException(
                                      "kind: search requires a configured IWebSearch (Scout). " +
                                      "Pass one to the PipelineRunner constructor."),
                                  options.OnSearchProgress),
            _              => ResolveRunner(step.Using)
        };

        // Parallel steps may call the sink concurrently and retain their own facts/path/attempt
        // (Task 3 imposes no global sequence across them). No streaming path exists here today, so
        // only started/completed are emitted — never a delta.
        if (options.OnTrace is { } onStarted)
            await onStarted(new PipelineStepStarted(options.MissionName, missionPath, step.ExpertName, expert.Kind, attempt), cts.Token);

        var envelope = await runner.RunAsync(expert, localContext, cts.Token);

        if (options.OnTrace is { } onCompleted)
            await onCompleted(new PipelineStepCompleted(options.MissionName, missionPath, step.ExpertName, expert.Kind, attempt, envelope), cts.Token);

        if (envelope.Status == "fail")
        {
            cts.Cancel(); // Signal siblings to stop.
            return ($"[{step.ExpertName}] {envelope.Reason ?? "step failed"}", namedKey, envelope.Text);
        }

        return (null, namedKey, envelope.Text);
    }

    // Converts each provider-SDK FunctionCallContent to the closed PipelineToolCall shape (Phase
    // 43.16 Task 3) — no provider SDK object crosses into the trace. Mirrors
    // ForgeMission.Runner's RunnerToolTurnMapper.ToJsonElement/WriteValue (Core cannot reference
    // Runner, so the small Utf8JsonWriter-based conversion is duplicated here rather than
    // reflection-serialized).
    private static IReadOnlyList<PipelineToolCall> ToPipelineToolCalls(IReadOnlyList<FunctionCallContent> calls)
        => calls.Select(call => new PipelineToolCall(
            call.CallId,
            call.Name,
            ToolArgumentsToJsonElement(call.Arguments))).ToList();

    private static JsonElement ToolArgumentsToJsonElement(IDictionary<string, object?>? arguments)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        foreach (var (name, value) in arguments ?? new Dictionary<string, object?>())
        {
            writer.WritePropertyName(name);
            WriteToolArgumentValue(writer, value);
        }
        writer.WriteEndObject();
        writer.Flush();

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void WriteToolArgumentValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case string text:
                writer.WriteStringValue(text);
                return;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                return;
            case long integer:
                writer.WriteNumberValue(integer);
                return;
            case double number:
                writer.WriteNumberValue(number);
                return;
            case JsonElement element:
                element.WriteTo(writer);
                return;
            default:
                throw new InvalidOperationException(
                    $"Unsupported tool argument type '{value.GetType().FullName}'.");
        }
    }

    // The restorable slice of the context bag: string values only. Structured objects
    // (conversation/system/tools) are re-derived from the request on every call.
    private static Dictionary<string, string> StringSnapshot(Dictionary<string, object> context)
        => context.Where(kv => kv.Value is string)
                  .ToDictionary(kv => kv.Key, kv => (string)kv.Value, StringComparer.Ordinal);

    private static bool TryParseDouble(object? value, out double result)
    {
        result = 0;
        return value switch
        {
            double d   => (result = d)    == d,
            float f    => (result = f)    == f,
            int i      => (result = i)    == i,
            long l     => (result = l)    == l,
            string s   => double.TryParse(s, System.Globalization.NumberStyles.Any,
                              System.Globalization.CultureInfo.InvariantCulture, out result),
            _          => false
        };
    }

    private static bool EvaluateNumericOp(double actual, CompOp op, double threshold) => op switch
    {
        CompOp.Gt  => actual >  threshold,
        CompOp.Lt  => actual <  threshold,
        CompOp.Gte => actual >= threshold,
        CompOp.Lte => actual <= threshold,
        CompOp.Eq  => Math.Abs(actual - threshold) < 1e-10,
        _          => false
    };

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
