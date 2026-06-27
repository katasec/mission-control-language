using ForgeMission.Parser;

namespace ForgeMission.Tests.Parser;

public class ParserTests
{
    // ── Basic mission parsing ─────────────────────────────────────────────────

    [Fact]
    public void ValidMission_SingleExpert_ParsesCorrectly()
    {
        var result = MclParser.Parse("mission BuildOperator = { KubernetesArchitect }");

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        Assert.NotNull(mission);
        Assert.Equal("BuildOperator", mission.Name);
        var names = mission.Pipeline.Elements.OfType<StepElement>().Select(e => e.Step.ExpertName);
        Assert.Equal(["KubernetesArchitect"], names);
    }

    [Fact]
    public void ValidMission_MultiStepPipeline_ParsesCorrectly()
    {
        var source = """
            mission BuildOperator = {
                KubernetesArchitect
                -> SecurityArchitect
                -> PrincipalReviewer
            }
            """;

        var result = MclParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        Assert.NotNull(mission);
        Assert.Equal("BuildOperator", mission.Name);
        Assert.Equal(
            ["KubernetesArchitect", "SecurityArchitect", "PrincipalReviewer"],
            mission.Pipeline.Elements.OfType<StepElement>().Select(e => e.Step.ExpertName));
    }

    // ── expert keyword is removed ─────────────────────────────────────────────

    [Fact]
    public void ExpertKeyword_IsParseError()
    {
        var source = "expert KubernetesArchitect = { RequirementsAnalyst }";
        Assert.Throws<ParseException>(() => MclParser.Parse(source));
    }

    // ── Params ────────────────────────────────────────────────────────────────

    [Fact]
    public void MissionParams_ParseCorrectly()
    {
        var source = """
            mission BuildOperator(goal, persona) = {
                KubernetesArchitect
            }
            """;

        var result = MclParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        Assert.NotNull(mission);
        Assert.Equal(["goal", "persona"], mission.Params);
    }

    // ── Context clause (key: value) ───────────────────────────────────────────

    [Fact]
    public void ContextClause_StringLiteral_ParsesCorrectly()
    {
        var source = """
            mission BuildOperator = {
                KubernetesArchitect
                -> PrincipalReviewer(style: "terse ADR")
            }
            """;

        var result = MclParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        Assert.NotNull(mission);
        var lastStep = mission.Pipeline.Elements.OfType<StepElement>().Last().Step;
        Assert.Equal("PrincipalReviewer", lastStep.ExpertName);
        var binding = Assert.Single(lastStep.Context);
        Assert.Equal("style", binding.Key);
        var value = Assert.IsType<StringBindingValue>(binding.Value);
        Assert.Equal("terse ADR", value.Text);
    }

    [Fact]
    public void ContextClause_VarRef_ParsesCorrectly()
    {
        var source = """
            let myStyle = "verbose"
            mission BuildOperator = {
                KubernetesArchitect(style: myStyle)
            }
            """;

        var result = MclParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        Assert.NotNull(mission);
        var step = mission.Pipeline.Elements.OfType<StepElement>().Single().Step;
        var binding = Assert.Single(step.Context);
        var value = Assert.IsType<VarRefBindingValue>(binding.Value);
        Assert.Equal("myStyle", value.Name);
    }

    [Fact]
    public void ContextClause_MultipleBindings_ParsesCorrectly()
    {
        var source = """
            mission BuildOperator = {
                DataExtractor(source: "prod", format: "json")
            }
            """;

        var result = MclParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        var step = mission!.Pipeline.Elements.OfType<StepElement>().Single().Step;
        Assert.Equal(2, step.Context.Count);
        Assert.Equal("source", step.Context[0].Key);
        Assert.Equal("format", step.Context[1].Key);
    }

    [Fact]
    public void WithClause_OldSyntax_IsParseError()
    {
        var source = """
            mission BuildOperator = {
                PrincipalReviewer with { style = "terse" }
            }
            """;
        Assert.Throws<ParseException>(() => MclParser.Parse(source));
    }

    // ── using clause ──────────────────────────────────────────────────────────

    [Fact]
    public void UsingClause_ParsesCorrectly()
    {
        var source = """
            mission BuildOperator = {
                KubernetesArchitect using architect
            }
            """;

        var result = MclParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        var step = mission!.Pipeline.Elements.OfType<StepElement>().Single().Step;
        Assert.Equal("architect", step.Using);
    }

    [Fact]
    public void UsingClause_WithContextClause_ParsesCorrectly()
    {
        var source = """
            mission BuildOperator = {
                PrincipalReviewer(style: "terse") using fast
            }
            """;

        var result = MclParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        var step = mission!.Pipeline.Elements.OfType<StepElement>().Single().Step;
        Assert.Equal("PrincipalReviewer", step.ExpertName);
        Assert.Equal("fast", step.Using);
        Assert.Single(step.Context);
    }

    // ── when() guards ─────────────────────────────────────────────────────────

    [Fact]
    public void WhenClause_StringEquals_ParsesCorrectly()
    {
        var source = """
            mission HandleRequest(input) = {
                Classifier
                -> Architect when(mode: "design")
            }
            """;

        var result = MclParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        var steps = mission!.Pipeline.Elements.OfType<StepElement>().ToList();
        Assert.Equal(2, steps.Count);
        Assert.Null(steps[0].Step.When);
        var guard = Assert.IsType<StringEqualsWhen>(steps[1].Step.When);
        Assert.Equal("mode", guard.Key);
        Assert.Equal("design", guard.Value);
    }

    [Fact]
    public void WhenClause_Else_ParsesCorrectly()
    {
        var source = """
            mission HandleRequest(input) = {
                Classifier
                -> Architect when(mode: "design")
                -> Planner   when(else)
            }
            """;

        var result = MclParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        var steps = mission!.Pipeline.Elements.OfType<StepElement>().ToList();
        Assert.IsType<ElseWhen>(steps[2].Step.When);
    }

    [Fact]
    public void WhenClause_FullRouterMission_ParsesCorrectly()
    {
        var source = """
            mission HandleRequest(input) = {
                Classifier
                -> Architect when(mode: "design")
                -> Developer when(mode: "task")
                -> Reviewer  when(mode: "review")
                -> Planner   when(else)
            }
            """;

        var result = MclParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        var steps = mission!.Pipeline.Elements.OfType<StepElement>().ToList();
        Assert.Equal(5, steps.Count);
        Assert.Null(steps[0].Step.When);
        Assert.IsType<StringEqualsWhen>(steps[1].Step.When);
        Assert.IsType<StringEqualsWhen>(steps[2].Step.When);
        Assert.IsType<StringEqualsWhen>(steps[3].Step.When);
        Assert.IsType<ElseWhen>(steps[4].Step.When);
    }

    [Theory]
    [InlineData(">",  CompOp.Gt,  0.8)]
    [InlineData("<",  CompOp.Lt,  0.5)]
    [InlineData(">=", CompOp.Gte, 0.75)]
    [InlineData("<=", CompOp.Lte, 0.9)]
    [InlineData("==", CompOp.Eq,  1.0)]
    public void WhenClause_NumericCompare_ParsesCorrectly(string op, CompOp expectedOp, double threshold)
    {
        var source = $$"""
            mission Score = {
                Scorer
                -> HighPath when(score {{op}} {{threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)}})
                -> LowPath  when(else)
            }
            """;

        var result  = MclParser.Parse(source);
        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        var steps   = mission!.Pipeline.Elements.OfType<StepElement>().ToList();
        var guard   = Assert.IsType<NumericCompareWhen>(steps[1].Step.When);
        Assert.Equal("score",       guard.Key);
        Assert.Equal(expectedOp,    guard.Op);
        Assert.Equal(threshold,     guard.Threshold);
    }

    [Fact]
    public void WhenClause_NumericCompare_IntLiteral_ParsesCorrectly()
    {
        var source = """
            mission Demo = {
                Scorer
                -> Pass when(score >= 7)
            }
            """;

        var result  = MclParser.Parse(source);
        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        var step    = mission!.Pipeline.Elements.OfType<StepElement>().Last().Step;
        var guard   = Assert.IsType<NumericCompareWhen>(step.When);
        Assert.Equal(7.0, guard.Threshold);
    }

    [Fact]
    public void WhenClause_NumericCompare_UnderscoreKey_ParsesCorrectly()
    {
        var source = """
            mission Audit = {
                RiskScorer
                -> HighRiskAnalyst when(risk_score >= 0.6)
                -> LowRiskSummary  when(risk_score < 0.6)
            }
            """;

        var result  = MclParser.Parse(source);
        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        var steps   = mission!.Pipeline.Elements.OfType<StepElement>().ToList();

        var high = Assert.IsType<NumericCompareWhen>(steps[1].Step.When);
        Assert.Equal("risk_score", high.Key);
        Assert.Equal(CompOp.Gte,   high.Op);
        Assert.Equal(0.6,          high.Threshold);

        var low = Assert.IsType<NumericCompareWhen>(steps[2].Step.When);
        Assert.Equal("risk_score", low.Key);
        Assert.Equal(CompOp.Lt,    low.Op);
    }

    // ── loop(N) ───────────────────────────────────────────────────────────────

    [Fact]
    public void LoopN_ParsedIntoMissionDeclaration()
    {
        var source = """
            mission Demo loop(4) = {
                Worker
            }
            """;
        var mission = MclParser.Parse(source).Declarations.OfType<MissionDeclaration>().Single();
        Assert.Equal(4, mission.MaxLoops);
    }

    [Fact]
    public void NoLoop_MaxLoopsDefaultsToOne()
    {
        var mission = MclParser.Parse("mission Demo = { Worker }").Declarations.OfType<MissionDeclaration>().Single();
        Assert.Equal(1, mission.MaxLoops);
    }

    [Fact]
    public void OldLoopSyntax_IsParseError()
    {
        // Old: mission Demo = Worker loop 3  (no parens, no braces)
        Assert.Throws<ParseException>(() => MclParser.Parse("mission Demo = Worker loop 3"));
    }

    // ── parallel {} ───────────────────────────────────────────────────────────

    [Fact]
    public void ParallelBlock_ParsesCorrectly()
    {
        var source = """
            mission Analysis(input) = {
                DataExtractor
                -> parallel {
                    Summariser
                    FactChecker
                    Critic
                }
                -> Synthesiser
            }
            """;

        var result = MclParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        var elements = mission!.Pipeline.Elements.ToList();
        Assert.Equal(3, elements.Count);
        Assert.IsType<StepElement>(elements[0]);
        var parallel = Assert.IsType<ParallelElement>(elements[1]);
        Assert.Equal(["Summariser", "FactChecker", "Critic"],
            parallel.Steps.Select(s => s.ExpertName));
        Assert.IsType<StepElement>(elements[2]);
    }

    // ── let bindings ──────────────────────────────────────────────────────────

    [Fact]
    public void LetBinding_StringLiteral_ParsesCorrectly()
    {
        var source = """
            let goal = "Design a K8s operator"
            mission BuildOperator = { KubernetesArchitect }
            """;

        var result = MclParser.Parse(source);

        Assert.Single(result.Bindings);
        var binding = result.Bindings[0];
        Assert.Equal("goal", binding.Name);
        var value = Assert.IsType<StringLetValue>(binding.Value);
        Assert.Equal("Design a K8s operator", value.Text);
    }

    [Fact]
    public void LetBinding_EnvCall_ParsesCorrectly()
    {
        var source = """
            let apiKey = env("OPENAI_API_KEY")
            mission BuildOperator = { KubernetesArchitect }
            """;

        var result = MclParser.Parse(source);

        var binding = Assert.Single(result.Bindings);
        var value = Assert.IsType<EnvLetValue>(binding.Value);
        Assert.Equal("OPENAI_API_KEY", value.VarName);
        Assert.Null(value.DefaultValue);
    }

    [Fact]
    public void LetBinding_EnvCallWithDefault_ParsesCorrectly()
    {
        var source = """
            let model = env("MCL_MODEL", "gpt-4o-mini")
            mission BuildOperator = { KubernetesArchitect }
            """;

        var result = MclParser.Parse(source);

        var binding = Assert.Single(result.Bindings);
        var value = Assert.IsType<EnvLetValue>(binding.Value);
        Assert.Equal("MCL_MODEL", value.VarName);
        Assert.Equal("gpt-4o-mini", value.DefaultValue);
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public void LowercaseIdentifier_ThrowsParseException()
    {
        var source = "mission BuildOperator = { kubernetesArchitect }";
        var ex = Assert.Throws<ParseException>(() => MclParser.Parse(source));
        Assert.Contains("PascalCase", ex.Message);
    }

    [Fact]
    public void MissingEquals_ThrowsParseException()
    {
        var source = "mission BuildOperator { KubernetesArchitect }";
        Assert.Throws<ParseException>(() => MclParser.Parse(source));
    }

    [Fact]
    public void EmptyPipeline_ThrowsParseException()
    {
        var source = "mission BuildOperator = { }";
        Assert.Throws<ParseException>(() => MclParser.Parse(source));
    }

    [Fact]
    public void MissingBraces_ThrowsParseException()
    {
        // Old no-braces form is now invalid
        var source = "mission BuildOperator = KubernetesArchitect";
        Assert.Throws<ParseException>(() => MclParser.Parse(source));
    }

    [Fact]
    public void DebateBlock_ThrowsNotImplementedParseException()
    {
        var source = """
            mission Demo = {
                debate(rounds: 3) {
                    ExpertA
                    ExpertB
                }
            }
            """;
        var ex = Assert.Throws<ParseException>(() => MclParser.Parse(source));
        Assert.Contains("debate {} is not yet implemented", ex.Message);
        Assert.Contains("parallel {}", ex.Message);
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Fact]
    public void LineComment_IsIgnored()
    {
        var result = MclParser.Parse("""
            // This is a comment
            mission BuildOperator = { KubernetesArchitect } // inline comment
            """);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        Assert.NotNull(mission);
        Assert.Equal("BuildOperator", mission.Name);
    }

    [Fact]
    public void LineComment_OnlyLine_ParsesAsEmpty()
    {
        var result = MclParser.Parse("// just a comment");
        Assert.Empty(result.Declarations);
        Assert.Empty(result.Bindings);
    }
}
