using ForgeMission.Core.Experts;
using ForgeMission.Parser;
using ForgeMission.Core.Runtime;
using static ForgeMission.Core.Runtime.MissionStatus;

namespace ForgeMission.Tests.Runtime;

public class PipelineRunnerTests
{
    private static ExpertDefinition Expert(string name) =>
        new(name, "Input", "Output", $"You are {name}.");

    private static Dictionary<string, ExpertDefinition> Experts(params string[] names) =>
        names.ToDictionary(n => n, Expert, StringComparer.Ordinal);

    // ── Basic pipeline ────────────────────────────────────────────────────────

    [Fact]
    public async Task SingleStep_CallsRunnerOnce_ReturnsOutput()
    {
        var ast    = MclParser.Parse("mission BuildOperator = { KubernetesArchitect }");
        var stub   = new StubExpertRunner();
        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect"), new PipelineRunOptions("BuildOperator"));

        Assert.Single(stub.Calls);
        Assert.Equal("KubernetesArchitect", stub.Calls[0].ExpertName);
        Assert.Equal("BuildOperator", result.MissionName);
    }

    [Fact]
    public async Task MultiStep_PassesOutputForward()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator = {
                KubernetesArchitect
                -> SecurityArchitect
                -> PrincipalReviewer
            }
            """);

        var stub = new StubExpertRunner((name, ctx) =>
            $"Output from {name} given [{ctx["output"]}]");

        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect", "SecurityArchitect", "PrincipalReviewer"),
                      new PipelineRunOptions("BuildOperator"));

        Assert.Equal(3, stub.Calls.Count);
        Assert.Equal(string.Empty,
            stub.Calls[0].Context["output"].ToString());
        Assert.Equal("Output from KubernetesArchitect given []",
            stub.Calls[1].Context["output"].ToString());
        Assert.Equal("Output from SecurityArchitect given [Output from KubernetesArchitect given []]",
            stub.Calls[2].Context["output"].ToString());
    }

    [Fact]
    public async Task Result_ContainsLastStepOutput()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator = {
                KubernetesArchitect
                -> PrincipalReviewer
            }
            """);

        var stub   = new StubExpertRunner((name, _) => $"Output from {name}");
        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect", "PrincipalReviewer"),
                      new PipelineRunOptions("BuildOperator"));

        Assert.Equal("Output from PrincipalReviewer", result.Text);
    }

    [Fact]
    public async Task StepWriter_ReceivesEachStepOutput()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator = {
                KubernetesArchitect
                -> SecurityArchitect
            }
            """);

        var sw     = new StringWriter();
        var stub   = new StubExpertRunner((name, _) => $"Output from {name}");
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect", "SecurityArchitect"),
                      new PipelineRunOptions("BuildOperator", StepWriter: sw));

        var written = sw.ToString();
        Assert.Contains("→ KubernetesArchitect...", written);
        Assert.Contains("→ SecurityArchitect...", written);
        Assert.Contains("Output from KubernetesArchitect", written);
        Assert.Contains("Output from SecurityArchitect", written);
    }

    [Fact]
    public async Task CancellationToken_IsRespected()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator = {
                KubernetesArchitect
                -> SecurityArchitect
            }
            """);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PipelineRunner(new StubExpertRunner())
                .RunAsync(ast, Experts("KubernetesArchitect", "SecurityArchitect"),
                          new PipelineRunOptions("BuildOperator"),
                          cts.Token));
    }

    // ── Let bindings ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LetBindings_SeedContext()
    {
        var ast = MclParser.Parse("""
            let goal = "Design a K8s operator"
            mission BuildOperator = { KubernetesArchitect }
            """);

        var stub = new StubExpertRunner();
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect"), new PipelineRunOptions("BuildOperator"));

        Assert.Equal("Design a K8s operator", stub.Calls[0].Context["goal"].ToString());
    }

    [Fact]
    public async Task VarFlag_OverridesLetBinding()
    {
        var ast = MclParser.Parse("""
            let goal = "original"
            mission BuildOperator = { KubernetesArchitect }
            """);

        var stub = new StubExpertRunner();
        var vars = new Dictionary<string, string> { ["goal"] = "overridden" };
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect"),
                      new PipelineRunOptions("BuildOperator", vars));

        Assert.Equal("overridden", stub.Calls[0].Context["goal"].ToString());
    }

    [Fact]
    public void MissingEnvVar_ThrowsClearly()
    {
        var ast = MclParser.Parse("""
            let apiKey = env("MCLTEST_MISSING_VAR_XYZ")
            mission BuildOperator = { KubernetesArchitect }
            """);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new PipelineRunner(new StubExpertRunner())
                .RunAsync(ast, Experts("KubernetesArchitect"),
                          new PipelineRunOptions("BuildOperator"))
                .GetAwaiter().GetResult());

        Assert.Contains("MCLTEST_MISSING_VAR_XYZ", ex.Message);
    }

    // ── Context clause ────────────────────────────────────────────────────────

    [Fact]
    public async Task ContextClause_OverridesContextForStep()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator = {
                KubernetesArchitect(style: "terse")
            }
            """);

        var stub = new StubExpertRunner();
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect"), new PipelineRunOptions("BuildOperator"));

        Assert.Equal("terse", stub.Calls[0].Context["style"].ToString());
    }

    // ── Provider configuration (let bindings, still valid pre-Spoke-5) ────────

    [Fact]
    public async Task ProviderBindings_AllFourSeededInContext()
    {
        var ast = MclParser.Parse("""
            let provider = env("MCL_PROVIDER", "openai")
            let apiKey   = env("MCL_API_KEY")
            let model    = env("MCL_MODEL", "gpt-4o-mini")
            let endpoint = env("MCL_ENDPOINT", "")
            mission Demo = { Worker }
            """);

        Environment.SetEnvironmentVariable("MCL_API_KEY", "sk-test");
        try
        {
            var stub = new StubExpertRunner();
            await new PipelineRunner(stub)
                .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo"));

            var ctx = stub.Calls[0].Context;
            Assert.Equal("openai",      ctx["provider"].ToString());
            Assert.Equal("sk-test",     ctx["apiKey"].ToString());
            Assert.Equal("gpt-4o-mini", ctx["model"].ToString());
            Assert.Equal("",            ctx["endpoint"].ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCL_API_KEY", null);
        }
    }

    [Fact]
    public async Task ProviderBinding_VarFlagOverridesProvider()
    {
        var ast = MclParser.Parse("""
            let provider = env("MCL_PROVIDER", "openai")
            let apiKey   = env("MCL_API_KEY")
            let model    = env("MCL_MODEL", "gpt-4o-mini")
            mission Demo = { Worker }
            """);

        Environment.SetEnvironmentVariable("MCL_API_KEY", "sk-test");
        try
        {
            var stub = new StubExpertRunner();
            var vars = new Dictionary<string, string> { ["provider"] = "azure" };
            await new PipelineRunner(stub)
                .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo", vars));

            Assert.Equal("azure", stub.Calls[0].Context["provider"].ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCL_API_KEY", null);
        }
    }

    [Fact]
    public void MissingApiKey_ThrowsClearly()
    {
        var ast = MclParser.Parse("""
            let apiKey = env("MCL_API_KEY")
            mission Demo = { Worker }
            """);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new PipelineRunner(new StubExpertRunner())
                .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo"))
                .GetAwaiter().GetResult());

        Assert.Contains("MCL_API_KEY", ex.Message);
    }

    // ── StepEnvelope / fail-fast ──────────────────────────────────────────────

    [Fact]
    public async Task StepFail_StopsImmediately_SecondStepNeverCalled()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator = {
                KubernetesArchitect
                -> SecurityArchitect
            }
            """);

        var stub = new StubExpertRunner((name, _) =>
            name == "KubernetesArchitect"
                ? new StepEnvelope("bad output", "fail", "quality too low")
                : new StepEnvelope($"Output from {name}"));

        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect", "SecurityArchitect"),
                      new PipelineRunOptions("BuildOperator"));

        Assert.Single(stub.Calls);
        Assert.Equal(MissionStatus.Fail, result.Status);
        Assert.Contains("KubernetesArchitect", result.FailReason);
        Assert.Contains("quality too low", result.FailReason);
    }

    [Fact]
    public async Task StepPass_PipelineContinues()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator = {
                KubernetesArchitect
                -> SecurityArchitect
            }
            """);

        var stub   = new StubExpertRunner();
        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect", "SecurityArchitect"),
                      new PipelineRunOptions("BuildOperator"));

        Assert.Equal(2, stub.Calls.Count);
        Assert.Equal(MissionStatus.Pass, result.Status);
    }

    // ── loop(N) ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Loop_RetriesUntilAllStepsPass()
    {
        var ast = MclParser.Parse("""
            mission RefinedPitch loop(3) = {
                Drafter
                -> Judge
            }
            """);

        var callCount = 0;
        var stub = new StubExpertRunner((name, _) =>
        {
            callCount++;
            if (name == "Judge" && callCount <= 2)
                return new StepEnvelope("fail output", "fail", "not good enough");
            return new StepEnvelope($"Output from {name}");
        });

        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Drafter", "Judge"),
                      new PipelineRunOptions("RefinedPitch"));

        Assert.Equal(MissionStatus.Pass, result.Status);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task Loop_ExhaustedFails_SurfacesLastFailReason()
    {
        var ast = MclParser.Parse("""
            mission RefinedPitch loop(3) = {
                Drafter
                -> Judge
            }
            """);

        var stub = new StubExpertRunner((name, _) =>
            name == "Judge"
                ? new StepEnvelope("bad output", "fail", "never good enough")
                : new StepEnvelope($"Output from {name}"));

        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Drafter", "Judge"),
                      new PipelineRunOptions("RefinedPitch"));

        Assert.Equal(MissionStatus.Fail, result.Status);
        Assert.Equal(3, result.Attempts);
        Assert.Contains("never good enough", result.FailReason);
    }

    [Fact]
    public async Task AttemptVariable_InjectedEachAttempt()
    {
        var ast = MclParser.Parse("""
            mission Demo loop(3) = {
                Worker
            }
            """);

        var attempts = new List<string>();
        var stub = new StubExpertRunner((name, ctx) =>
        {
            attempts.Add(ctx["attempt"].ToString()!);
            return new StepEnvelope("done", "fail", "retry");
        });

        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo"));

        Assert.Equal(["1", "2", "3"], attempts);
    }

    [Fact]
    public async Task MaxLoopsVariable_InjectedEachAttempt()
    {
        var ast = MclParser.Parse("""
            mission Demo loop(5) = {
                Worker
            }
            """);

        var maxLoopsValues = new List<string>();
        var stub = new StubExpertRunner((name, ctx) =>
        {
            maxLoopsValues.Add(ctx["max_loops"].ToString()!);
            return new StepEnvelope("done");
        });

        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo"));

        Assert.All(maxLoopsValues, v => Assert.Equal("5", v));
    }

    [Fact]
    public async Task NoLoop_AttemptAndMaxLoopsDefaultToOne()
    {
        var ast  = MclParser.Parse("mission Demo = { Worker }");
        var stub = new StubExpertRunner();

        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo"));

        Assert.Equal("1", stub.Calls[0].Context["attempt"].ToString());
        Assert.Equal("1", stub.Calls[0].Context["max_loops"].ToString());
        Assert.Equal(1, result.Attempts);
    }

    // ── when() routing ────────────────────────────────────────────────────────

    [Fact]
    public async Task When_MatchingGuard_StepRuns()
    {
        var ast = MclParser.Parse("""
            mission HandleRequest(input) = {
                Classifier
                -> Architect when(mode: "design")
                -> Developer when(mode: "task")
                -> Planner   when(else)
            }
            """);

        // Classifier sets mode = "design" via context — simulate with stub
        var stub = new StubExpertRunner((name, ctx) =>
        {
            if (name == "Classifier")
            {
                // Next steps read context["mode"]; return envelope that sets it
                ctx["mode"] = "design";
            }
            return new StepEnvelope($"Output from {name}");
        });

        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Classifier", "Architect", "Developer", "Planner"),
                      new PipelineRunOptions("HandleRequest"));

        Assert.Equal(MissionStatus.Pass, result.Status);
        var calledNames = stub.Calls.Select(c => c.ExpertName).ToList();
        Assert.Contains("Classifier", calledNames);
        Assert.Contains("Architect", calledNames);
        Assert.DoesNotContain("Developer", calledNames);
        Assert.DoesNotContain("Planner", calledNames);
    }

    [Fact]
    public async Task When_NoMatch_ElseBranchRuns()
    {
        var ast = MclParser.Parse("""
            mission HandleRequest(input) = {
                Classifier
                -> Architect when(mode: "design")
                -> Developer when(mode: "task")
                -> Planner   when(else)
            }
            """);

        var stub = new StubExpertRunner((name, ctx) =>
        {
            if (name == "Classifier") ctx["mode"] = "unknown";
            return new StepEnvelope($"Output from {name}");
        });

        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Classifier", "Architect", "Developer", "Planner"),
                      new PipelineRunOptions("HandleRequest"));

        var calledNames = stub.Calls.Select(c => c.ExpertName).ToList();
        Assert.DoesNotContain("Architect", calledNames);
        Assert.DoesNotContain("Developer", calledNames);
        Assert.Contains("Planner", calledNames);
    }

    [Fact]
    public async Task When_NoMatchAndNoElse_Throws()
    {
        var ast = MclParser.Parse("""
            mission HandleRequest(input) = {
                Classifier
                -> Architect when(mode: "design")
                -> Developer when(mode: "task")
            }
            """);

        var stub = new StubExpertRunner((name, ctx) =>
        {
            if (name == "Classifier") ctx["mode"] = "unknown";
            return new StepEnvelope($"Output from {name}");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PipelineRunner(stub)
                .RunAsync(ast, Experts("Classifier", "Architect", "Developer"),
                          new PipelineRunOptions("HandleRequest")));
    }

    // ── provider profiles (using <profile>) ───────────────────────────────────

    [Fact]
    public async Task UsingClause_RoutesStepToNamedRunner()
    {
        var ast = MclParser.Parse("""
            mission M = {
                Drafter using architect
                -> Reviewer
            }
            """);

        var defaultStub  = new StubExpertRunner((_, _) => "default output");
        var architectStub = new StubExpertRunner((_, _) => "architect output");

        var runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal)
        {
            ["default"]   = defaultStub,
            ["architect"] = architectStub,
        };

        await new PipelineRunner(runners)
            .RunAsync(ast, Experts("Drafter", "Reviewer"), new PipelineRunOptions("M"));

        Assert.Single(architectStub.Calls);
        Assert.Equal("Drafter", architectStub.Calls[0].ExpertName);
        Assert.Single(defaultStub.Calls);
        Assert.Equal("Reviewer", defaultStub.Calls[0].ExpertName);
    }

    [Fact]
    public async Task NoUsingClause_UsesDefaultRunner()
    {
        var ast = MclParser.Parse("mission M = { KubernetesArchitect }");

        var defaultStub  = new StubExpertRunner();
        var architectStub = new StubExpertRunner();

        var runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal)
        {
            ["default"]   = defaultStub,
            ["architect"] = architectStub,
        };

        await new PipelineRunner(runners)
            .RunAsync(ast, Experts("KubernetesArchitect"), new PipelineRunOptions("M"));

        Assert.Single(defaultStub.Calls);
        Assert.Empty(architectStub.Calls);
    }

    [Fact]
    public async Task UsingClause_UnknownProfile_Throws()
    {
        var ast = MclParser.Parse("mission M = { Drafter using ghost }");

        var runner = new PipelineRunner(new Dictionary<string, IExpertRunner>(StringComparer.Ordinal)
        {
            ["default"] = new StubExpertRunner()
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(ast, Experts("Drafter"), new PipelineRunOptions("M")));
    }

    [Fact]
    public async Task SingleRunnerConstructor_WrapsAsDefault()
    {
        var ast  = MclParser.Parse("mission M = { KubernetesArchitect }");
        var stub = new StubExpertRunner();

        // Convenience constructor — backward compat
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect"), new PipelineRunOptions("M"));

        Assert.Single(stub.Calls);
    }

    // ── parallel {} ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ParallelBlock_AllStepsExecute()
    {
        var ast = MclParser.Parse("""
            mission Analysis(input) = {
                DataExtractor
                -> parallel {
                    Summariser
                    FactChecker
                }
                -> Synthesiser
            }
            """);

        var stub   = new StubExpertRunner((name, _) => $"Output from {name}");
        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("DataExtractor", "Summariser", "FactChecker", "Synthesiser"),
                      new PipelineRunOptions("Analysis"));

        var names = stub.Calls.Select(c => c.ExpertName).ToList();
        Assert.Equal(["DataExtractor", "Summariser", "FactChecker", "Synthesiser"], names);
        Assert.Equal(MissionStatus.Pass, result.Status);
    }

    [Fact]
    public async Task ParallelBlock_NamedOutputsAvailableToDownstreamStep()
    {
        var ast = MclParser.Parse("""
            mission Analysis(input) = {
                parallel {
                    Summariser
                    FactChecker
                }
                -> Synthesiser
            }
            """);

        var stub = new StubExpertRunner((name, _) => $"Output from {name}");
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Summariser", "FactChecker", "Synthesiser"),
                      new PipelineRunOptions("Analysis"));

        // Synthesiser should see named outputs from both parallel steps.
        var synthCtx = stub.Calls.First(c => c.ExpertName == "Synthesiser").Context;
        Assert.Equal("Output from Summariser",  synthCtx["Summariser.output"].ToString());
        Assert.Equal("Output from FactChecker", synthCtx["FactChecker.output"].ToString());
    }

    // ── loop feedback (kind:rule / kind:judge) ────────────────────────────────

    [Fact]
    public async Task LoopFeedback_PropagatedFromRuleExpertToNextIteration()
    {
        // Simulate the pattern: Drafter → WordCheck (kind:rule)
        // First attempt: WordCheck writes feedback to context["feedback"] and returns fail.
        // Second attempt: Drafter sees context["feedback"] from prior attempt.
        var ast = MclParser.Parse("""
            mission Draft loop(3) = {
                Drafter
                -> WordCheck
            }
            """);

        var feedbackSeenByDrafter = new List<string?>();
        var attempt = 0;

        var stub = new StubExpertRunner((name, ctx) =>
        {
            if (name == "Drafter")
            {
                feedbackSeenByDrafter.Add(
                    ctx.TryGetValue("feedback", out var fb) ? fb?.ToString() : null);
            }

            if (name == "WordCheck")
            {
                attempt++;
                if (attempt == 1)
                {
                    // Simulate RuleExpertRunner behaviour: write feedback then fail.
                    ctx["feedback"] = "Write at least 50 words.";
                    return new StepEnvelope("short output", "fail", "Write at least 50 words.");
                }
            }
            return new StepEnvelope($"Output from {name}");
        });

        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Drafter", "WordCheck"), new PipelineRunOptions("Draft"));

        Assert.Equal(MissionStatus.Pass, result.Status);
        // First attempt: no feedback yet.
        Assert.Null(feedbackSeenByDrafter[0]);
        // Second attempt: feedback propagated from first attempt's rule failure.
        Assert.Equal("Write at least 50 words.", feedbackSeenByDrafter[1]);
    }

    [Fact]
    public async Task ParallelBlock_OneFails_PipelineFails()
    {
        var ast = MclParser.Parse("""
            mission Analysis(input) = {
                parallel {
                    ExpertA
                    ExpertB
                }
                -> Synthesiser
            }
            """);

        var stub = new StubExpertRunner((name, _) => name == "ExpertB"
            ? new StepEnvelope("bad output", "fail", "ExpertB rejected the input")
            : new StepEnvelope($"Output from {name}"));

        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("ExpertA", "ExpertB", "Synthesiser"),
                      new PipelineRunOptions("Analysis"));

        Assert.Equal(MissionStatus.Fail, result.Status);
        Assert.Contains("ExpertB", result.FailReason);
        // Synthesiser must not run when a parallel step fails.
        Assert.DoesNotContain(stub.Calls, c => c.ExpertName == "Synthesiser");
    }
}
