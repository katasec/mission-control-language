using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using ForgeUI.Models;
using MclProgram = ForgeMission.Parser.Program;

namespace ForgeUI.Services;

public class MissionService(
    MclProgram                           ast,
    Dictionary<string, ExpertDefinition> experts,
    IExpertRunner                        runner)
{
    public async Task<ChatMessage> RunAsync(
        string                     userText,
        Action<PipelineTraceEvent> onStep,
        CancellationToken          ct = default)
    {
        var trace   = new List<PipelineTraceEvent>();
        var attempt = 1;

        var mission   = ast.Declarations.OfType<MissionDeclaration>().First();
        var paramName = mission.Params.FirstOrDefault() ?? "goal";
        var vars      = new Dictionary<string, string>(StringComparer.Ordinal) { [paramName] = userText };

        var options = new PipelineRunOptions(
            mission.Name,
            vars,
            OnStepComplete: (expertName, envelope) =>
            {
                if (envelope.Status == "fail") attempt++;
                var ev = new PipelineTraceEvent(expertName, envelope, DateTime.UtcNow, attempt);
                trace.Add(ev);
                onStep(ev);
            });

        var result = await new PipelineRunner(runner).RunAsync(ast, experts, options, ct);

        var trust = new TrustSignal(
            Verified:   result.Status == MissionStatus.Pass,
            StepCount:  trace.Count,
            RetryCount: result.Attempts - 1);

        return new ChatMessage(userText, result.Text, trust, trace);
    }
}
