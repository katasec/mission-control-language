using System.Runtime.CompilerServices;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ForgeMission.Core.Adapters;

public class OnnxExpertRunner : IExpertRunner
{
    public Task<StepEnvelope> RunAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        CancellationToken ct = default)
    {
        var inputs   = expert.Inputs ?? [];
        var features = new float[inputs.Count];

        for (var i = 0; i < inputs.Count; i++)
        {
            var key = inputs[i];
            if (!context.TryGetValue(key, out var raw))
                throw new InvalidOperationException(
                    $"ONNX feature '{key}' not found in context. Ensure a prior step writes it.");
            features[i] = Convert.ToSingle(raw);
        }

        var tensor = new DenseTensor<float>(features, [1, inputs.Count]);
        var ortInputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", tensor)
        };

        var modelPath = Path.IsPathRooted(expert.Model)
            ? expert.Model
            : Path.GetFullPath(Path.Combine(expert.ExpertDirectory, expert.Model));

        using var session = new InferenceSession(modelPath);
        using var results = session.Run(ortInputs);

        // sklearn ONNX models emit two outputs: label (int64) then probabilities (float[1,2]).
        // Take the probability for class 1 (the "positive" / high-risk class).
        var probOutput = results.FirstOrDefault(r => r.Name == "probabilities") ?? results.Last();
        var probs      = probOutput.AsEnumerable<float>().ToArray();
        var score      = probs.Length >= 2 ? probs[1] : probs[0];
        context[expert.OutputKey] = (double)score;

        var threshold = float.Parse(expert.Threshold);
        var status    = score > threshold ? "fail" : "pass";
        var reason    = score > threshold
            ? $"Anomaly score {score:F4} exceeds threshold {threshold}"
            : null;

        return Task.FromResult(new StepEnvelope(score.ToString("F4"), status, reason));
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var envelope = await RunAsync(expert, context, ct);
        yield return envelope.Text;
    }
}
