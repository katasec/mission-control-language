using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;

namespace ForgeMission.Tests.Runtime;

public class StepMeasurementTests
{
    [Fact]
    public async Task ParallelAndExecSteps_EmitIndependentCompleteMeasurements()
    {
        var ast = MclParser.Parse("""
            mission Review = {
                parallel {
                    ExecStep
                    OtherStep
                }
            }
            """);
        var scriptDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(scriptDirectory);
        var script = Path.Combine(scriptDirectory, "usage.py");
        await File.WriteAllTextAsync(script, "import json; print(json.dumps({'result':'done','usage':{'inputTokens':11,'cachedInputTokens':2,'outputTokens':7,'reasoningOutputTokens':3,'toolDurationMs':5}}))");
        var runner = new StubExpertRunner((_, _) => new StepEnvelope("done"));
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
        {
            ["ExecStep"] = new("ExecStep", "", "", "", Kind: "exec", Command: "python3", Args: ["usage.py"], OutputKey: "result", ExpertDirectory: scriptDirectory),
            ["OtherStep"] = new("OtherStep", "", "", "", Kind: "llm"),
        };
        var measurements = new List<StepMeasurement>();

        try
        {
            await new PipelineRunner(runner).RunAsync(ast, experts,
                new PipelineRunOptions("Review", OnStepMeasured: measurements.Add));
        }
        finally { Directory.Delete(scriptDirectory, recursive: true); }

        Assert.Equal(2, measurements.Count);
        var exec = Assert.Single(measurements, m => m.StepName == "ExecStep");
        Assert.Equal("exec", exec.Kind);
        Assert.Equal(11, exec.InputTokens);
        Assert.Equal(2, exec.CachedInputTokens);
        Assert.Equal(7, exec.OutputTokens);
        Assert.Equal(3, exec.ReasoningOutputTokens);
        Assert.Equal(18, exec.TotalTokens);
        Assert.Equal(5, exec.ToolDurationMs);
        Assert.True(exec.DurationMs >= 0);
        Assert.All(measurements, measurement => Assert.Equal("pass", measurement.Status));
    }

    [Fact]
    public async Task SubMission_ForwardsMeasurementWithParentStep()
    {
        var ast = MclParser.Parse("""
            mission Inner = { Worker }
            mission Outer = { Inner }
            """);
        var measurements = new List<StepMeasurement>();

        await new PipelineRunner(new StubExpertRunner()).RunAsync(ast,
            new Dictionary<string, ExpertDefinition> { ["Worker"] = new("Worker", "", "", "") },
            new PipelineRunOptions("Outer", OnStepMeasured: measurements.Add));

        var measurement = Assert.Single(measurements);
        Assert.Equal("Worker", measurement.StepName);
        Assert.Equal("Inner", measurement.ParentStep);
    }
}
