using ForgeMission.Core.Experts;
using ForgeMission.Core.Parser;
using ForgeMission.Core.Runtime;

namespace ForgeMission.Tests.Runtime;

public class PipelineRunnerTests
{
    private static ExpertDefinition Expert(string name) =>
        new(name, "Input", "Output", $"You are {name}.");

    private static Dictionary<string, ExpertDefinition> Experts(params string[] names) =>
        names.ToDictionary(n => n, Expert, StringComparer.Ordinal);

    [Fact]
    public async Task SingleStep_CallsRunnerOnce_ReturnsOutput()
    {
        var ast    = FmsParser.Parse("mission BuildOperator = KubernetesArchitect");
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
        var ast = FmsParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
                |> PrincipalReviewer
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
        var ast = FmsParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> PrincipalReviewer
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
        var ast = FmsParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
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
        var ast = FmsParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
            """);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PipelineRunner(new StubExpertRunner())
                .RunAsync(ast, Experts("KubernetesArchitect", "SecurityArchitect"),
                          new PipelineRunOptions("BuildOperator"),
                          cts.Token));
    }

    [Fact]
    public async Task LetBindings_SeedContext()
    {
        var ast = FmsParser.Parse("""
            let goal = "Design a K8s operator"
            mission BuildOperator = KubernetesArchitect
            """);

        var stub = new StubExpertRunner();
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect"), new PipelineRunOptions("BuildOperator"));

        Assert.Equal("Design a K8s operator", stub.Calls[0].Context["goal"].ToString());
    }

    [Fact]
    public async Task WithClause_OverridesContextForStep()
    {
        var ast = FmsParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect with { style = "terse" }
            """);

        var stub = new StubExpertRunner();
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect"), new PipelineRunOptions("BuildOperator"));

        Assert.Equal("terse", stub.Calls[0].Context["style"].ToString());
    }

    [Fact]
    public async Task VarFlag_OverridesLetBinding()
    {
        var ast = FmsParser.Parse("""
            let goal = "original"
            mission BuildOperator = KubernetesArchitect
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
        var ast = FmsParser.Parse("""
            let apiKey = env("FMLTEST_MISSING_VAR_XYZ")
            mission BuildOperator = KubernetesArchitect
            """);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new PipelineRunner(new StubExpertRunner())
                .RunAsync(ast, Experts("KubernetesArchitect"),
                          new PipelineRunOptions("BuildOperator"))
                .GetAwaiter().GetResult());

        Assert.Contains("FMLTEST_MISSING_VAR_XYZ", ex.Message);
    }
}
