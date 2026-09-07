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
    private readonly HashSet<string> _locallyResumedContinuations = new(StringComparer.Ordinal);
    private readonly object _localResumeGate = new();
    private readonly Dictionary<string, LocalContinuationState> _localContinuations = new(StringComparer.Ordinal);
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

    private static string RootDefinitionFingerprint(Program ast, IReadOnlyDictionary<string, ExpertDefinition> experts, string rootMission)
    {
        var bindings = ast.Bindings.OrderBy(binding => binding.Name, StringComparer.Ordinal)
            .Select(binding => $"L:{binding.Name}:{binding.Value}");
        var missions = ast.Declarations.OfType<MissionDeclaration>().OrderBy(m => m.Name, StringComparer.Ordinal)
            .Select(m => $"M:{m.Name}:{m.MaxLoops}:{string.Join(',', m.Params)}:{string.Join(';', m.Pipeline.Elements.Select(e => e.ToString()))}");
        var definitions = experts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"E:{pair.Key}:{pair.Value.Kind}:{pair.Value.Role}:{pair.Value.SystemPrompt}");
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{rootMission}\n{string.Join('\n', bindings)}\n{string.Join('\n', missions)}\n{string.Join('\n', definitions)}")));
    }

    private PipelineFailure? ConsumeLocalContinuation(PipelineContinuationCheckpoint checkpoint)
    {
        lock (_localResumeGate)
        {
            if (!_localContinuations.TryGetValue(checkpoint.RootExecutionId, out var state))
                return _locallyResumedContinuations.Add(checkpoint.SessionId) ? null : PipelineFailure.DuplicateContinuation;
            if (checkpoint.ContinuationOrdinal < state.CurrentOrdinal) return PipelineFailure.LateContinuation;
            if (checkpoint.ContinuationOrdinal > state.CurrentOrdinal) return PipelineFailure.InvalidContinuation;
            if (!state.Consumed.Add(checkpoint.ContinuationOrdinal)) return PipelineFailure.DuplicateContinuation;
            return null;
        }
    }

    private void RegisterIssuedContinuation(string rootExecutionId, int ordinal)
    {
        lock (_localResumeGate)
        {
            if (!_localContinuations.TryGetValue(rootExecutionId, out var state))
                _localContinuations[rootExecutionId] = state = new LocalContinuationState();
            state.CurrentOrdinal = ordinal;
        }
    }

    private sealed class LocalContinuationState
    {
        public int CurrentOrdinal { get; set; }
        public HashSet<int> Consumed { get; } = [];
    }

    public async Task<MissionResult> RunAsync(
        Program ast,
        Dictionary<string, ExpertDefinition> experts,
        PipelineRunOptions options,
        CancellationToken ct = default)
    {
        // Root-scoped tools use a serializable frame interpreter.  It is intentionally a separate
        // path: legacy per-call Tools, AgenticSession, and MissionChatClient keep their established
        // caller-owned continuation semantics.
        if (options.RootTools is { Count: > 0 } && options.MissionPath is null)
            return await new RootScopedExecution(this, ast, experts, options, options.RootTools, ct).RunAsync();

        return await RunCoreAsync(ast, experts, options, ct);
    }

    /// <summary>Resumes a root-scoped Core checkpoint after a fresh runner has been composed.</summary>
    public async Task<MissionResult> ResumeAsync(
        Program ast,
        Dictionary<string, ExpertDefinition> experts,
        PipelineResumeRequest request,
        PipelineRunOptions observers,
        CancellationToken ct = default)
    {
        if (!PipelineCheckpointCodec.TryRead(request.Continuation, out var checkpoint))
            return new MissionResult(string.Empty, string.Empty, MissionStatus.Fail,
                PipelineFailure.InvalidContinuation.ToString(), Failure: PipelineFailure.InvalidContinuation);

        if (!string.Equals(checkpoint.ToolCall.CallId, request.Result.CallId, StringComparison.Ordinal)
            || !checkpoint.ToolDeclarations.Any(tool => string.Equals(tool.Name, checkpoint.ToolCall.Name, StringComparison.Ordinal)))
            return new MissionResult(checkpoint.RootMissionName, string.Empty, MissionStatus.Fail,
                PipelineFailure.InvalidContinuation.ToString(), Failure: PipelineFailure.InvalidContinuation);

        if (!string.Equals(checkpoint.RootToolScopeFingerprint,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
                    string.Join("\n", checkpoint.ToolDeclarations.Select(d => $"{d.Name}\u001f{d.Description}\u001f{d.InputSchema.GetRawText()}"))))),
                StringComparison.Ordinal))
            return new MissionResult(checkpoint.RootMissionName, string.Empty, MissionStatus.Fail,
                PipelineFailure.InvalidContinuation.ToString(), Failure: PipelineFailure.InvalidContinuation);

        if (!string.Equals(checkpoint.RootDefinitionFingerprint,
                RootDefinitionFingerprint(ast, experts, checkpoint.RootMissionName), StringComparison.Ordinal)
            || checkpoint.Frames.Count == 0
            || !string.Equals(checkpoint.Frames[0].MissionName, checkpoint.RootMissionName, StringComparison.Ordinal))
            return new MissionResult(checkpoint.RootMissionName, string.Empty, MissionStatus.Fail,
                PipelineFailure.InvalidContinuation.ToString(), Failure: PipelineFailure.InvalidContinuation);

        var localFailure = ConsumeLocalContinuation(checkpoint);
        if (localFailure is not null)
            return new MissionResult(checkpoint.RootMissionName, string.Empty, MissionStatus.Fail,
                localFailure.Value.ToString(), Failure: localFailure);

        if (checkpoint.Frames.Count == 0
            || !ast.Declarations.OfType<MissionDeclaration>().Any(m => m.Name == checkpoint.RootMissionName)
            || checkpoint.Frames.Any(frame => !ast.Declarations.OfType<MissionDeclaration>()
                .Any(mission => mission.Name == frame.MissionName)))
            return new MissionResult(checkpoint.RootMissionName, string.Empty, MissionStatus.Fail,
                PipelineFailure.InvalidContinuation.ToString(), Failure: PipelineFailure.InvalidContinuation);

        var resumedTools = checkpoint.ToolDeclarations.Select(tool => (AITool)new Katasec.AITools.DeclaredTool(
            tool.Name, tool.Description, tool.InputSchema)).ToList();
        return await new RootScopedExecution(this, ast, experts, observers with { MissionName = checkpoint.RootMissionName }, resumedTools, ct,
            checkpoint, request.Result).RunAsync();
    }

    private async Task<MissionResult> RunCoreAsync(
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
                        foreach (var ptask in tasks.Where(t => t.IsCompletedSuccessfully))
                        {
                            var (_, pkey, pout) = ptask.Result;
                            context[pkey] = pout;
                        }
                        failReason = tasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result.failReason)
                            .FirstOrDefault(r => r is not null) ?? "a parallel step was cancelled";
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

                    while (true)
                    {
                        failReason = await ExecuteStepAsync(step, ast, experts, context, options, missionPath, attempt, ct);
                        if (failReason is not null) break;

                        if (context.TryGetValue("tool_calls", out var tc)
                            && tc is IReadOnlyList<FunctionCallContent> toolCalls)
                        {
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

                        break;
                    }
                    if (failReason is not null) break;
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

    private static IDictionary<string, object?> JsonToArguments(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object) return new Dictionary<string, object?>();
        return arguments.EnumerateObject().ToDictionary(property => property.Name,
            property => JsonArgumentValue(property.Value), StringComparer.Ordinal);
    }

    private static object? JsonArgumentValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.Null => null,
        _ => value.Clone(),
    };

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

    // This interpreter is deliberately small and explicit.  A root-scoped call is a durable
    // boundary, so it cannot borrow the normal recursive call stack or a live Task while waiting
    // for a tool result.  Every parent activation and eligible-parallel branch is represented here.
    private sealed class RootScopedExecution
    {
        private const int CheckpointVersion = 1;
        private readonly PipelineRunner _owner;
        private readonly Program _ast;
        private readonly Dictionary<string, ExpertDefinition> _experts;
        private readonly PipelineRunOptions _options;
        private readonly IList<AITool> _tools;
        private readonly CancellationToken _ct;
        private readonly List<Frame> _frames;
        private readonly PipelineContinuationCheckpoint? _resumeCheckpoint;
        private readonly PipelineToolResult? _resumeResult;
        private readonly IReadOnlyList<PipelineToolDeclaration> _declarations;
        private readonly string _fingerprint;
        private readonly string _definitionFingerprint;
        private readonly IReadOnlyDictionary<string, string> _rootInputs;
        private readonly string _rootExecutionId;
        private int _nextContinuationOrdinal;

        public RootScopedExecution(PipelineRunner owner, Program ast, Dictionary<string, ExpertDefinition> experts,
            PipelineRunOptions options, IList<AITool> tools, CancellationToken ct,
            PipelineContinuationCheckpoint? resumeCheckpoint = null, PipelineToolResult? resumeResult = null)
        {
            _owner = owner; _ast = ast; _experts = experts; _options = options; _tools = tools; _ct = ct;
            _resumeCheckpoint = resumeCheckpoint; _resumeResult = resumeResult;
            _declarations = tools.Select(ToDeclaration).ToList();
            _fingerprint = ScopeFingerprint(_declarations);
            _definitionFingerprint = DefinitionFingerprint(ast, experts, options.MissionName);
            _rootInputs = resumeCheckpoint?.RootInputs ?? RootInputs(options.MissionName, options.Vars);
            _rootExecutionId = resumeCheckpoint?.RootExecutionId ?? Guid.NewGuid().ToString("N");
            _nextContinuationOrdinal = resumeCheckpoint?.ContinuationOrdinal ?? 0;
            _frames = resumeCheckpoint is null ? [NewFrame(options.MissionName, _rootInputs)]
                : resumeCheckpoint.Frames.Select(RestoreFrame).ToList();
        }

        public async Task<MissionResult> RunAsync()
        {
            try
            {
                while (_frames.Count > 0)
                {
                    _ct.ThrowIfCancellationRequested();
                    var frame = _frames[^1];
                    var mission = Mission(frame.MissionName);
                    if (frame.ElementIndex >= mission.Pipeline.Elements.Count)
                    {
                        var text = Text(frame.Context, "output");
                        _frames.RemoveAt(_frames.Count - 1);
                        if (_frames.Count == 0)
                            return new MissionResult(_options.MissionName, text, MissionStatus.Pass, Attempts: frame.Attempt);
                        CompleteChild(_frames[^1], frame, text);
                        continue;
                    }

                    var element = mission.Pipeline.Elements[frame.ElementIndex];
                    if (element is ParallelElement parallel)
                    {
                        var paused = await AdvanceParallelAsync(frame, parallel);
                        if (paused is not null) return paused;
                        continue;
                    }

                    var step = ((StepElement)element).Step;
                    if (!ShouldRun(step, frame)) { frame.ElementIndex++; continue; }
                    var pausedResult = await AdvanceStepAsync(frame, step, false);
                    if (pausedResult is not null) return pausedResult;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { return Failure(PipelineFailure.ProviderFailed); }
            return Failure(PipelineFailure.InvalidContinuation);
        }

        private async Task<MissionResult?> AdvanceParallelAsync(Frame parent, ParallelElement parallel)
        {
            if (!parallel.Steps.Any(CanReachRootToolAgent))
            {
                var snapshot = parent.Context.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.Ordinal);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_ct);
                var results = await Task.WhenAll(parallel.Steps.Select(step => _owner.ExecuteParallelStepAsync(
                    step, _ast, _experts, snapshot, _options, Path(), parent.Attempt, linked)));
                foreach (var (_, key, output) in results) parent.Context[key] = output;
                var failure = results.Select(result => result.failReason).FirstOrDefault(reason => reason is not null);
                if (failure is not null)
                    return new MissionResult(_options.MissionName, Text(parent.Context, "output"), MissionStatus.Fail, failure, parent.Attempt);
                parent.ElementIndex++;
                return null;
            }
            parent.ParallelBranchIndex ??= 0;
            parent.CompletedParallelOutputs ??= new Dictionary<string, string>(StringComparer.Ordinal);
            if (parent.ParallelBranchIndex >= parallel.Steps.Count)
            {
                foreach (var (key, value) in parent.CompletedParallelOutputs)
                    parent.Context[key] = value;
                parent.ParallelBranchIndex = null;
                parent.CompletedParallelOutputs = null;
                parent.ElementIndex++;
                return null;
            }

            // Root tools make eligible branches deterministic.  Non-agent branches can still run
            // immediately, but an eligible child mission gets its own durable frame before it runs.
            var step = parallel.Steps[parent.ParallelBranchIndex.Value];
            var childMission = FindMission(step.ExpertName);
            if (childMission is null)
            {
                var paused = await AdvanceStepAsync(parent, step, true);
                if (paused is not null) return paused;
                parent.CompletedParallelOutputs[$"{step.ExpertName}.output"] = Text(parent.Context, "output");
                parent.ParallelBranchIndex++;
                return null;
            }

            var child = NewFrame(childMission.Name, Bindings(step, parent.Context));
            RecordEnvironmentBindings(child, step);
            child.ReturnsToParallel = true;
            _frames.Add(child);
            return null;
        }

        private async Task<MissionResult?> AdvanceStepAsync(Frame frame, Step step, bool parallelDirect)
        {
            var childMission = FindMission(step.ExpertName);
            if (childMission is not null)
            {
                frame.ElementIndex++;
                _frames.Add(NewFrame(childMission.Name, Bindings(step, frame.Context)));
                RecordEnvironmentBindings(_frames[^1], step);
                return null;
            }
            if (!_experts.TryGetValue(step.ExpertName, out var expert))
                throw new InvalidOperationException($"Expert '{step.ExpertName}' not found. Run 'forge validate' to check your mission before running.");

            foreach (var (key, value) in Bindings(step, frame.Context)) frame.Context[key] = value;
            RecordEnvironmentBindings(frame, step);
            var context = frame.Context.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.Ordinal);
            if (expert.IsAgent) context["tools"] = _tools;
            if (frame.ResumePausedAgent)
            {
                if (_resumeCheckpoint is null || _resumeResult is null) return Failure(PipelineFailure.InvalidContinuation);
                context[PipelineToolContinuationInstructions.ProviderToolTurn] = ProviderTurn(_resumeCheckpoint, _resumeResult);
                frame.ResumePausedAgent = false;
            }

            await Trace(new PipelineStepStarted(_options.MissionName, Path(), step.ExpertName, expert.Kind, frame.Attempt));
            StepEnvelope envelope;
            try { envelope = await RunnerFor(expert, step).RunAsync(expert, context, _ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { return Failure(PipelineFailure.ProviderFailed); }
            await Trace(new PipelineStepCompleted(_options.MissionName, Path(), step.ExpertName, expert.Kind, frame.Attempt, envelope));
            frame.Context["output"] = envelope.Text;

            if (envelope.Status == "fail")
            {
                var mission = Mission(frame.MissionName);
                if (frame.Attempt < mission.MaxLoops)
                {
                    frame.LoopFeedback = frame.Context.TryGetValue("feedback", out var feedback) ? feedback : null;
                    frame.Attempt++;
                    frame.ElementIndex = 0;
                    frame.AnyGuardedStepMatched = false;
                    frame.Context.Clear();
                    foreach (var (key, value) in frame.InitialContext) frame.Context[key] = value;
                    frame.Context["attempt"] = frame.Attempt.ToString();
                    frame.Context["max_loops"] = mission.MaxLoops.ToString();
                    if (frame.LoopFeedback is not null) frame.Context["feedback"] = frame.LoopFeedback;
                    return null;
                }
                return new MissionResult(_options.MissionName, envelope.Text, MissionStatus.Fail,
                    $"[{step.ExpertName}] {envelope.Reason ?? "step failed"}", frame.Attempt);
            }

            if (context.TryGetValue("tool_calls", out var raw) && raw is IReadOnlyList<FunctionCallContent> calls)
            {
                if (calls.Count != 1) return Failure(PipelineFailure.MultipleOutstandingTools);
                var call = ToPipelineToolCalls(calls).Single();
                if (!_declarations.Any(d => d.Name == call.Name)) return Failure(PipelineFailure.UnsupportedTool);
                frame.ResumePausedAgent = true;
                await Trace(new PipelineRootToolCheckpointed(_options.MissionName, Path(), step.ExpertName, expert.Kind, frame.Attempt, call));
                return Pause(frame, step.ExpertName, call, context);
            }

            if (!parallelDirect) frame.ElementIndex++;
            return null;
        }

        private MissionResult Pause(Frame frame, string expertName, PipelineToolCall call, Dictionary<string, object> context)
        {
            var snapshot = _frames.Select(frame => frame.ToCheckpoint()).ToList();
            var ordinal = ++_nextContinuationOrdinal;
            _owner.RegisterIssuedContinuation(_rootExecutionId, ordinal);
            var checkpoint = new PipelineContinuationCheckpoint(CheckpointVersion, Guid.NewGuid().ToString("N"), _rootExecutionId, ordinal,
                _options.MissionName, _definitionFingerprint, _fingerprint, _declarations, Path(), expertName,
                frame.Attempt, call, _rootInputs, snapshot);
            var payload = JsonSerializer.Serialize(checkpoint, PipelineContinuationJsonContext.Default.PipelineContinuationCheckpoint);
            var pause = new PipelineToolPause(_options.MissionName, Path(), expertName, frame.Attempt, call,
                new PipelineContinuation(CheckpointVersion, payload));
            return new MissionResult(_options.MissionName, string.Empty, MissionStatus.Pass, Attempts: frame.Attempt, Pause: pause);
        }

        private void CompleteChild(Frame parent, Frame child, string text)
        {
            if (child.ReturnsToParallel)
            {
                parent.CompletedParallelOutputs![$"{child.MissionName}.output"] = text;
                parent.ParallelBranchIndex++;
            }
            else parent.Context["output"] = text;
        }

        private Frame NewFrame(string missionName, IReadOnlyDictionary<string, string>? vars)
        {
            var context = InitialContext(missionName, vars);
            var mission = Mission(missionName);
            context["attempt"] = "1"; context["max_loops"] = mission.MaxLoops.ToString();
            return new Frame(missionName, 0, 1, context, new Dictionary<string, string>(context, StringComparer.Ordinal));
        }

        private Frame RestoreFrame(PipelineExecutionFrame checkpoint)
        {
            var initial = InitialContext(checkpoint.MissionName,
                checkpoint.MissionName == _options.MissionName ? _rootInputs : null);
            var context = new Dictionary<string, string>(initial, StringComparer.Ordinal);
            foreach (var (key, value) in checkpoint.RuntimeDelta) context[key] = value;
            foreach (var (key, binding) in checkpoint.EnvironmentBindings)
                context[key] = ContextBuilder.ResolveEnv(binding.VariableName, binding.DefaultValue, key);
            return Frame.FromCheckpoint(checkpoint, context, initial);
        }

        private Dictionary<string, string> InitialContext(string missionName, IReadOnlyDictionary<string, string>? vars)
        {
            var allowed = RootInputs(missionName, vars);
            return StringSnapshot(ContextBuilder.Seed(_ast, allowed, null));
        }

        private IReadOnlyDictionary<string, string> RootInputs(string missionName, IReadOnlyDictionary<string, string>? vars)
        {
            var parameters = Mission(missionName).Params;
            return (vars ?? new Dictionary<string, string>()).Where(pair => parameters.Contains(pair.Key, StringComparer.Ordinal)
                && !IsSensitiveKey(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        private static bool IsSensitiveKey(string key) => key.Equals("apiKey", StringComparison.OrdinalIgnoreCase)
            || key.Equals("provider", StringComparison.OrdinalIgnoreCase)
            || key.Equals("endpoint", StringComparison.OrdinalIgnoreCase)
            || key.Equals("model", StringComparison.OrdinalIgnoreCase)
            || key.Contains("credential", StringComparison.OrdinalIgnoreCase)
            || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Equals("authorization", StringComparison.OrdinalIgnoreCase);

        private static string DefinitionFingerprint(Program ast, IReadOnlyDictionary<string, ExpertDefinition> experts, string rootMission)
        {
            var bindings = ast.Bindings.OrderBy(binding => binding.Name, StringComparer.Ordinal)
                .Select(binding => $"L:{binding.Name}:{binding.Value}");
            var missions = ast.Declarations.OfType<MissionDeclaration>().OrderBy(m => m.Name, StringComparer.Ordinal)
                .Select(m => $"M:{m.Name}:{m.MaxLoops}:{string.Join(',', m.Params)}:{string.Join(';', m.Pipeline.Elements.Select(e => e.ToString()))}");
            var definitions = experts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"E:{pair.Key}:{pair.Value.Kind}:{pair.Value.Role}:{pair.Value.SystemPrompt}");
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{rootMission}\n{string.Join('\n', bindings)}\n{string.Join('\n', missions)}\n{string.Join('\n', definitions)}")));
        }

        private Dictionary<string, string> Bindings(Step step, Dictionary<string, string> context)
        {
            var objects = context.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.Ordinal);
            return step.Context.ToDictionary(binding => binding.Key,
                binding => ContextBuilder.ResolveBindingValue(binding.Value, objects), StringComparer.Ordinal);
        }

        private static void RecordEnvironmentBindings(Frame frame, Step step)
        {
            foreach (var binding in step.Context)
            {
                if (binding.Value is EnvBindingValue environment)
                    frame.EnvironmentBindings[binding.Key] = new PipelineEnvironmentBinding(environment.VarName, environment.DefaultValue);
            }
        }

        private IExpertRunner RunnerFor(ExpertDefinition expert, Step step) => expert.Kind switch
        {
            "http" => new HttpExpertRunner(), "rule" => new RuleExpertRunner(), "onnx" => new OnnxExpertRunner(),
            "json_extract" => new JsonExtractExpertRunner(), "exec" => new ExecExpertRunner(_owner._execution.DefaultTimeout),
            "search" => new SearchExpertRunner(_owner._webSearch ?? throw new InvalidOperationException("kind: search requires a configured IWebSearch (Scout). Pass one to the PipelineRunner constructor."), _options.OnSearchProgress),
            _ => _owner.ResolveRunner(step.Using),
        };

        private MissionDeclaration Mission(string name) => FindMission(name)
            ?? throw new InvalidOperationException($"Mission '{name}' not found in .mcl file");
        private MissionDeclaration? FindMission(string name) => _ast.Declarations.OfType<MissionDeclaration>().FirstOrDefault(m => m.Name == name);
        private bool CanReachRootToolAgent(Step step)
        {
            if (_experts.TryGetValue(step.ExpertName, out var expert)) return expert.IsAgent;
            var mission = FindMission(step.ExpertName);
            return mission is not null && mission.Pipeline.Elements.Any(element => element switch
            {
                StepElement child => CanReachRootToolAgent(child.Step),
                ParallelElement child => child.Steps.Any(CanReachRootToolAgent),
                _ => false,
            });
        }
        private IReadOnlyList<string> Path() => _frames.Select(frame => frame.MissionName).ToList();
        private async Task Trace(PipelineTraceEvent trace)
        { if (_options.OnTrace is not null) await _options.OnTrace(trace, _ct); }
        private MissionResult Failure(PipelineFailure failure) => new(_options.MissionName, string.Empty, MissionStatus.Fail, failure.ToString(), Failure: failure);
        private static string Text(IReadOnlyDictionary<string, string> context, string key) => context.TryGetValue(key, out var value) ? value : string.Empty;
        private static bool ShouldRun(Step step, Frame frame)
        {
            var matched = step.When switch
            {
                null => true,
                StringEqualsWhen equals => frame.Context.TryGetValue(equals.Key, out var value) && value == equals.Value,
                NumericCompareWhen numeric => frame.Context.TryGetValue(numeric.Key, out var raw) && TryParseDouble(raw, out var value) && EvaluateNumericOp(value, numeric.Op, numeric.Threshold),
                ElseWhen => !frame.AnyGuardedStepMatched,
                _ => false,
            };
            if (matched && step.When is StringEqualsWhen or NumericCompareWhen)
                frame.AnyGuardedStepMatched = true;
            return matched;
        }
        private static PipelineToolDeclaration ToDeclaration(AITool tool)
        {
            if (tool is not AIFunction function) throw new InvalidOperationException($"Root tool '{tool.Name}' must be an AIFunction declaration.");
            return new PipelineToolDeclaration(function.Name, function.Description ?? string.Empty, function.JsonSchema.Clone());
        }
        private static string ScopeFingerprint(IEnumerable<PipelineToolDeclaration> declarations) => Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", declarations.Select(d => $"{d.Name}\u001f{d.Description}\u001f{d.InputSchema.GetRawText()}")))));
        private static PipelineProviderToolTurn ProviderTurn(PipelineContinuationCheckpoint checkpoint, PipelineToolResult result) => new(
            new FunctionCallContent(checkpoint.ToolCall.CallId,
                checkpoint.ToolCall.Name, JsonToArguments(checkpoint.ToolCall.Arguments)), result);

        private sealed class Frame(string missionName, int elementIndex, int attempt, Dictionary<string, string> context,
            Dictionary<string, string> initialContext)
        {
            public string MissionName { get; } = missionName;
            public int ElementIndex { get; set; } = elementIndex;
            public int Attempt { get; set; } = attempt;
            public Dictionary<string, string> Context { get; } = context;
            public Dictionary<string, string> InitialContext { get; } = initialContext;
            public Dictionary<string, PipelineEnvironmentBinding> EnvironmentBindings { get; set; } = new(StringComparer.Ordinal);
            public string? LoopFeedback { get; set; }
            public bool AnyGuardedStepMatched { get; set; }
            public bool ResumePausedAgent { get; set; }
            public int? ParallelBranchIndex { get; set; }
            public Dictionary<string, string>? CompletedParallelOutputs { get; set; }
            public bool ReturnsToParallel { get; set; }
            public PipelineExecutionFrame ToCheckpoint() => new(MissionName, ElementIndex, Attempt,
                Context.Where(pair => !InitialContext.TryGetValue(pair.Key, out var initial) || initial != pair.Value)
                    .Where(pair => !EnvironmentBindings.ContainsKey(pair.Key))
                    .Where(pair => !IsSensitiveKey(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                EnvironmentBindings, LoopFeedback, AnyGuardedStepMatched, ResumePausedAgent,
                ParallelBranchIndex, CompletedParallelOutputs, ReturnsToParallel);
            public static Frame FromCheckpoint(PipelineExecutionFrame checkpoint, Dictionary<string, string> context,
                Dictionary<string, string> initial) => new(checkpoint.MissionName,
                checkpoint.ElementIndex, checkpoint.Attempt, context, initial)
            { ResumePausedAgent = checkpoint.ResumePausedAgent, ParallelBranchIndex = checkpoint.ParallelBranchIndex,
              CompletedParallelOutputs = checkpoint.CompletedParallelOutputs is null ? null : new Dictionary<string, string>(checkpoint.CompletedParallelOutputs, StringComparer.Ordinal), EnvironmentBindings = new Dictionary<string, PipelineEnvironmentBinding>(checkpoint.EnvironmentBindings, StringComparer.Ordinal), ReturnsToParallel = checkpoint.ReturnsToParallel, LoopFeedback = checkpoint.LoopFeedback, AnyGuardedStepMatched = checkpoint.AnyGuardedStepMatched };
        }
    }
}
