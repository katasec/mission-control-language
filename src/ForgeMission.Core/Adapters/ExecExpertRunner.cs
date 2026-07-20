using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;

namespace ForgeMission.Core.Adapters;

public class ExecExpertRunner(string defaultTimeout = "30s") : IExpertRunner
{
    public async Task<StepEnvelope> RunAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        CancellationToken ct = default)
    {
        var inputJson = BuildInputJson(expert, context);
        var workDir   = string.IsNullOrEmpty(expert.ExpertDirectory) ? Directory.GetCurrentDirectory() : expert.ExpertDirectory;
        var timeout   = ParseTimeout(string.IsNullOrWhiteSpace(expert.Timeout) ? defaultTimeout : expert.Timeout);

        using var cts  = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var psi = new ProcessStartInfo(expert.Command)
        {
            UseShellExecute        = false,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            WorkingDirectory       = workDir,
        };
        foreach (var arg in expert.Args ?? [])
            psi.ArgumentList.Add(arg);
        AddForgeEnvironment(psi, context);

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new StepEnvelope("", "fail", $"Failed to start '{expert.Command}': {ex.Message}");
        }

        // Write input and close stdin; read stdout and stderr concurrently to avoid deadlock.
        await process.StandardInput.WriteAsync(inputJson);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return new StepEnvelope("", "fail", $"Expert '{expert.Name}' timed out after {expert.Timeout}.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            return new StepEnvelope(stderr, "fail", $"Expert '{expert.Name}' exited with code {process.ExitCode}. stderr: {stderr}".TrimEnd());

        // Parse stdout as JSON and extract the declared outputKey into the context bag.
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(stdout).RootElement;
        }
        catch (JsonException ex)
        {
            throw new ExpertLoadException(
                $"Expert '{expert.Name}' produced invalid JSON on stdout: {ex.Message}. " +
                "kind:exec experts must write a JSON object to stdout.");
        }

        if (!root.TryGetProperty(expert.OutputKey, out var outputValue))
            throw new ExpertLoadException(
                $"Expert '{expert.Name}' stdout JSON is missing declared outputKey '{expert.OutputKey}'.");

        var outputText = outputValue.ValueKind == JsonValueKind.String
            ? outputValue.GetString() ?? ""
            : outputValue.GetRawText();

        context[expert.OutputKey] = outputText;
        context["output"]         = outputText;

        var status = root.TryGetProperty("status", out var sv) ? sv.GetString() : null;
        var reason = root.TryGetProperty("reason",  out var rv) ? rv.GetString() : null;

        // For judge experts that fail, write feedback so the next loop iteration can use it.
        if (expert.IsJudge && status == "fail")
        {
            var feedback = !string.IsNullOrWhiteSpace(reason)   ? reason
                         : !string.IsNullOrWhiteSpace(expert.OnFail) ? expert.OnFail
                         : "Verification failed.";
            context["feedback"] = feedback;
        }

        return new StepEnvelope(outputText, status ?? "pass", reason);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Process backend produces output only on exit; no true streaming for exec.
        // Yield only envelope.Text — not the JSON envelope — so content writers (Open WebUI,
        // CLI) receive plain text. ParseStreamedEnvelope handles non-JSON as a pass envelope.
        var envelope = await RunAsync(expert, context, ct);
        yield return envelope.Text ?? string.Empty;
    }

    // Serialise the declared inputs keys from the context bag to a JSON object.
    private static string BuildInputJson(ExpertDefinition expert, Dictionary<string, object> context)
    {
        var keys = expert.Inputs ?? [];
        var sb   = new StringBuilder("{");
        var first = true;
        foreach (var key in keys)
        {
            if (!context.TryGetValue(key, out var value)) continue;
            if (!first) sb.Append(',');
            sb.Append(JsonSerializer.Serialize(key, ExecSerializerContext.Default.String));
            sb.Append(':');
            sb.Append(value is string s
                ? JsonSerializer.Serialize(s, ExecSerializerContext.Default.String)
                : JsonSerializer.Serialize(value?.ToString() ?? "", ExecSerializerContext.Default.String));
            first = false;
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static void AddForgeEnvironment(ProcessStartInfo psi, Dictionary<string, object> context)
    {
        foreach (var (key, value) in context)
        {
            if (!key.StartsWith("FORGE_", StringComparison.Ordinal)) continue;
            psi.Environment[key] = value?.ToString() ?? "";
        }
    }

    private static TimeSpan ParseTimeout(string timeout)
    {
        if (string.IsNullOrWhiteSpace(timeout))
            return TimeSpan.FromSeconds(30);

        if (timeout.EndsWith('s') && int.TryParse(timeout[..^1], out var secs))
            return TimeSpan.FromSeconds(secs);

        if (timeout.EndsWith('m') && int.TryParse(timeout[..^1], out var mins))
            return TimeSpan.FromMinutes(mins);

        return TimeSpan.FromSeconds(30);
    }
}

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(string))]
internal partial class ExecSerializerContext : JsonSerializerContext { }

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(ForgeMission.Core.Runtime.StepEnvelope))]
internal partial class ExecEnvelopeSerializerContext : JsonSerializerContext { }
