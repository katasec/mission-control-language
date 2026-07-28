using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using System.Diagnostics;
using System.Text.Json;

namespace ForgeMission.Tests.Adapters;

public class ExecExpertRunnerTests : IDisposable
{
    private static readonly Lazy<PythonInvocation?> WorkingPython = new(FindWorkingPython);

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
        string timeout   = "")
    {
        var python = RequirePython();
        return new("TestExec", "Input", "Output", "",
            Kind: "exec", Command: python.Command, Args: [..python.PrefixArgs, args], Inputs: [inputs],
            OutputKey: outputKey, Timeout: timeout,
            ExpertDirectory: _dir);
    }

    [SkippableFact]
    public void ParseTimeout_Hours_ReturnsFourHours()
        => Assert.Equal(TimeSpan.FromHours(4), ExecExpertRunner.ParseTimeout("4h"));

    [SkippableFact]
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

    [SkippableFact]
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

    [SkippableFact]
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

    [SkippableFact]
    public async Task RunAsync_MissingOutputKey_ThrowsExpertLoadException()
    {
        var script  = Script("import json,sys\njson.load(sys.stdin)\nprint(json.dumps({'wrong_key':1}))\n");
        var runner  = new ExecExpertRunner();
        var expert  = ExecExpert(script);
        var context = new Dictionary<string, object> { ["input"] = "x" };

        await Assert.ThrowsAsync<ExpertLoadException>(() => runner.RunAsync(expert, context));
    }

    [SkippableFact]
    public async Task RunAsync_InvalidJson_ThrowsExpertLoadException()
    {
        var script  = Script("print('not json')\n");
        var runner  = new ExecExpertRunner();
        var expert  = ExecExpert(script);
        var context = new Dictionary<string, object> { ["input"] = "x" };

        await Assert.ThrowsAsync<ExpertLoadException>(() => runner.RunAsync(expert, context));
    }

    [SkippableFact]
    public async Task RunAsync_MultipleInputKeys_AllSerializedToStdin()
    {
        var python = RequirePython();
        var script = Script(
            "import sys,json\n" +
            "d=json.load(sys.stdin)\n" +
            "print(json.dumps({'result': d['a'] + ' ' + d['b']}))\n");
        var runner  = new ExecExpertRunner();
        var expert  = new ExpertDefinition("TestExec", "Input", "Output", "",
            Kind: "exec", Command: python.Command, Args: [..python.PrefixArgs, script], Inputs: ["a", "b"],
            OutputKey: "result", ExpertDirectory: _dir);
        var context = new Dictionary<string, object> { ["a"] = "hello", ["b"] = "world" };

        var envelope = await runner.RunAsync(expert, context);

        Assert.Equal("pass",        envelope.Status);
        Assert.Equal("hello world", envelope.Text);
    }

    [SkippableFact]
    public async Task RunAsync_CommandPlusArgs_DockerStyle()
    {
        var python = RequirePython();
        // command: python, args: script.py - mirrors docker/k8s command+args pattern
        var script  = Script("import sys,json\nd=json.load(sys.stdin)\nprint(json.dumps({'result':'ok'}))\n");
        var runner  = new ExecExpertRunner();
        var expert  = new ExpertDefinition("TestExec", "Input", "Output", "",
            Kind: "exec", Command: python.Command, Args: [..python.PrefixArgs, script],
            Inputs: ["input"], OutputKey: "result", ExpertDirectory: _dir);
        var context = new Dictionary<string, object> { ["input"] = "x" };

        var envelope = await runner.RunAsync(expert, context);

        Assert.Equal("pass", envelope.Status);
        Assert.Equal("ok",   envelope.Text);
    }

    [SkippableFact]
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

    private static PythonInvocation RequirePython()
    {
        var python = WorkingPython.Value;
        Skip.If(python is null, "No working Python 3 interpreter found (python3/python/py -3 probes failed)");
        return python!;
    }

    private static PythonInvocation? FindWorkingPython()
    {
        PythonInvocation[] candidates =
        [
            new("python3", []),
            new("python", []),
            new("py", ["-3"]),
        ];

        return candidates.FirstOrDefault(IsWorkingPython);
    }

    private static bool IsWorkingPython(PythonInvocation candidate)
    {
        const string probe = "import json,sys; print(json.dumps({'major': sys.version_info[0]}))";
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(candidate.Command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            }
        };

        foreach (var arg in candidate.PrefixArgs)
            process.StartInfo.ArgumentList.Add(arg);
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add(probe);

        try
        {
            process.Start();
        }
        catch
        {
            return false;
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(3_000))
        {
            process.Kill(entireProcessTree: true);
            return false;
        }

        _ = stderrTask.Result;
        var stdout = stdoutTask.Result;
        if (process.ExitCode != 0)
            return false;

        try
        {
            using var json = JsonDocument.Parse(stdout);
            return json.RootElement.TryGetProperty("major", out var major)
                && major.GetInt32() >= 3;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record PythonInvocation(string Command, IReadOnlyList<string> PrefixArgs);
}
