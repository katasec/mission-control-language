using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;

namespace ForgeMission.Tests.Adapters;

public class ExecExpertRunnerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public ExecExpertRunnerTests() => Directory.CreateDirectory(_dir);
    public void Dispose()          => Directory.Delete(_dir, recursive: true);

    private string Script(string content, string name = "script.py")
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return name; // relative name — WorkingDirectory is _dir
    }

    private ExpertDefinition ExecExpert(
        string args,
        string inputs    = "input",
        string outputKey = "result",
        string timeout   = "") =>
        new("TestExec", "Input", "Output", "",
            Kind: "exec", Command: "python3", Args: [args], Inputs: [inputs],
            OutputKey: outputKey, Timeout: timeout,
            ExpertDirectory: _dir);

    [Fact]
    public async Task RunAsync_ValidScript_WritesOutputKeyToContext()
    {
        var script = Script("import sys,json\nd=json.load(sys.stdin)\nprint(json.dumps({'result':d['input']}))\n");
        var runner  = new ExecExpertRunner();
        var expert  = ExecExpert(script);
        var context = new Dictionary<string, object> { ["input"] = "hello" };

        var envelope = await runner.RunAsync(expert, context);

        Assert.Equal("pass",  envelope.Status);
        Assert.Equal("hello", envelope.Text);
        Assert.Equal("hello", context["result"]);
        Assert.Equal("hello", context["output"]);
    }

    [Fact]
    public async Task RunAsync_NonZeroExit_ReturnsFailEnvelope()
    {
        var script  = Script("import sys\nsys.exit(1)\n");
        var runner  = new ExecExpertRunner();
        var expert  = ExecExpert(script);
        var context = new Dictionary<string, object> { ["input"] = "x" };

        var envelope = await runner.RunAsync(expert, context);

        Assert.Equal("fail", envelope.Status);
        Assert.Contains("exited with code 1", envelope.Reason);
    }

    [Fact]
    public async Task RunAsync_Timeout_ReturnsFailEnvelope()
    {
        var script  = Script("import time\ntime.sleep(10)\n");
        var runner  = new ExecExpertRunner();
        var expert  = ExecExpert(script, timeout: "1s");
        var context = new Dictionary<string, object> { ["input"] = "x" };

        var envelope = await runner.RunAsync(expert, context);

        Assert.Equal("fail", envelope.Status);
        Assert.Contains("timed out", envelope.Reason);
    }

    [Fact]
    public async Task RunAsync_MissingOutputKey_ThrowsExpertLoadException()
    {
        var script  = Script("import json,sys\njson.load(sys.stdin)\nprint(json.dumps({'wrong_key':1}))\n");
        var runner  = new ExecExpertRunner();
        var expert  = ExecExpert(script);
        var context = new Dictionary<string, object> { ["input"] = "x" };

        await Assert.ThrowsAsync<ExpertLoadException>(() => runner.RunAsync(expert, context));
    }

    [Fact]
    public async Task RunAsync_InvalidJson_ThrowsExpertLoadException()
    {
        var script  = Script("print('not json')\n");
        var runner  = new ExecExpertRunner();
        var expert  = ExecExpert(script);
        var context = new Dictionary<string, object> { ["input"] = "x" };

        await Assert.ThrowsAsync<ExpertLoadException>(() => runner.RunAsync(expert, context));
    }

    [Fact]
    public async Task RunAsync_MultipleInputKeys_AllSerializedToStdin()
    {
        var script = Script(
            "import sys,json\n" +
            "d=json.load(sys.stdin)\n" +
            "print(json.dumps({'result': d['a'] + ' ' + d['b']}))\n");
        var runner  = new ExecExpertRunner();
        var expert  = new ExpertDefinition("TestExec", "Input", "Output", "",
            Kind: "exec", Command: "python3", Args: [script], Inputs: ["a", "b"],
            OutputKey: "result", ExpertDirectory: _dir);
        var context = new Dictionary<string, object> { ["a"] = "hello", ["b"] = "world" };

        var envelope = await runner.RunAsync(expert, context);

        Assert.Equal("pass",        envelope.Status);
        Assert.Equal("hello world", envelope.Text);
    }

    [Fact]
    public async Task RunAsync_CommandPlusArgs_DockerStyle()
    {
        // command: python3, args: script.py — mirrors docker/k8s command+args pattern
        var script  = Script("import sys,json\nd=json.load(sys.stdin)\nprint(json.dumps({'result':'ok'}))\n");
        var runner  = new ExecExpertRunner();
        var expert  = new ExpertDefinition("TestExec", "Input", "Output", "",
            Kind: "exec", Command: "python3", Args: [script],
            Inputs: ["input"], OutputKey: "result", ExpertDirectory: _dir);
        var context = new Dictionary<string, object> { ["input"] = "x" };

        var envelope = await runner.RunAsync(expert, context);

        Assert.Equal("pass", envelope.Status);
        Assert.Equal("ok",   envelope.Text);
    }

    [Fact]
    public async Task RunAsync_ForwardsForgeEnvironmentVariables()
    {
        var outputDir = Path.Combine(_dir, "outputs");
        Directory.CreateDirectory(outputDir);
        var script = Script(
            "import os,json\n" +
            "out=os.environ['FORGE_OUTPUT_DIR']\n" +
            "open(os.path.join(out, 'proof.txt'), 'w').write('artifact proof')\n" +
            "print(json.dumps({'result':'wrote proof'}))\n");
        var runner  = new ExecExpertRunner();
        var expert  = ExecExpert(script);
        var context = new Dictionary<string, object>
        {
            ["input"] = "x",
            ["FORGE_OUTPUT_DIR"] = outputDir,
        };

        var envelope = await runner.RunAsync(expert, context);

        Assert.Equal("pass", envelope.Status);
        Assert.Equal("wrote proof", envelope.Text);
        Assert.Equal("artifact proof", File.ReadAllText(Path.Combine(outputDir, "proof.txt")));
    }
}
