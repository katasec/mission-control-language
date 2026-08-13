using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Runtime;

/// <summary>
/// Phase 43.16 Task 3 — the awaited PipelineTraceEvent seam. Covers the nested Janus-shaped
/// trace, awaited started/completed ordering (including the gate proof), and the closed
/// tool-request shape. The Scout lifecycle test (SearchMissionPipelineTests) covers the same
/// seam for the pre-existing OnStepStart-derived behavior, now observed through OnTrace.
/// </summary>
public class PipelineTraceTests
{
    private static ExpertDefinition Expert(string name) =>
        new(name, "Input", "Output", $"You are {name}.");

    private static Dictionary<string, ExpertDefinition> Experts(params string[] names) =>
        names.ToDictionary(n => n, Expert, StringComparer.Ordinal);

    // ── 1. Deterministic nested Janus-shaped trace ──────────────────────────────

    [Fact]
    public async Task JanusShapedMission_TracesNegotiateRetryThenImplementAtCorrectPaths()
    {
        var ast = MclParser.Parse("""
            mission Negotiate loop(2) = {
                Proposer
                -> Approver
            }
            mission Implement = {
                Implementer
            }
            mission Janus = {
                Negotiate
                -> Implement
            }
            """);

        var approverCalls = 0;
        var stub = new StubExpertRunner((name, _) =>
        {
            if (name == "Approver" && ++approverCalls == 1)
                return new StepEnvelope("missing rollback", "fail", "include a rollback plan");
            return new StepEnvelope($"{name} output");
        });

        var events = new List<PipelineTraceEvent>();
        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Proposer", "Approver", "Implementer"),
                new PipelineRunOptions("Janus", OnTrace: (evt, _) =>
                {
                    events.Add(evt);
                    return Task.CompletedTask;
                }));

        Assert.Equal(MissionStatus.Pass, result.Status);

        var negotiatePath = new[] { "Janus", "Negotiate" };
        var implementPath = new[] { "Janus", "Implement" };

        var proposerCompleted = events.OfType<PipelineStepCompleted>()
            .Where(e => e.ExpertName == "Proposer").ToList();
        var approverCompleted = events.OfType<PipelineStepCompleted>()
            .Where(e => e.ExpertName == "Approver").ToList();
        var implementerStarted = events.OfType<PipelineStepStarted>()
            .Single(e => e.ExpertName == "Implementer");

        // Proposer and Approver completed at [Janus, Negotiate] — the nested sub-mission path,
        // not the root [Janus] path. This is the essential Janus fix CreateChildOptions provides.
        Assert.Equal(2, proposerCompleted.Count);
        Assert.All(proposerCompleted, e => Assert.Equal(negotiatePath, e.MissionPath));
        Assert.Equal(2, approverCompleted.Count);
        Assert.All(approverCompleted, e => Assert.Equal(negotiatePath, e.MissionPath));

        // The first Approver completion carries the failed verdict at attempt 1; the second
        // negotiation attempt then occurs and approves at attempt 2.
        Assert.Equal(1, approverCompleted[0].Attempt);
        Assert.Equal("fail", approverCompleted[0].Envelope.Status);
        Assert.Equal(2, approverCompleted[1].Attempt);
        Assert.Equal("pass", approverCompleted[1].Envelope.Status);

        // Implementer runs at [Janus, Implement] only after the approving Approver completion —
        // never before, and never for the rejected first attempt.
        Assert.Equal(implementPath, implementerStarted.MissionPath);
        var implementerStartedIndex = events.IndexOf(implementerStarted);
        var approvingCompletedIndex = events.IndexOf(approverCompleted[1]);
        Assert.True(implementerStartedIndex > approvingCompletedIndex,
            "Implementer must start only after the approving Approver completion.");
    }

    // ── 2. Awaited started/completed ordering ───────────────────────────────────

    [Fact]
    public async Task OnTrace_GatesStepInvocation_AndObservesCompletedBeforeNextStarted()
    {
        var ast = MclParser.Parse("""
            mission TwoStep = {
                A
                -> B
            }
            """);
        var stub = new StubExpertRunner((name, _) => new StepEnvelope($"{name} done"));
        var events = new List<string>();
        var gate = new TaskCompletionSource();
        var reachedGate = new TaskCompletionSource();

        var options = new PipelineRunOptions("TwoStep", OnTrace: async (evt, _) =>
        {
            events.Add(evt switch
            {
                PipelineStepStarted s   => $"Started:{s.ExpertName}",
                PipelineStepCompleted c => $"Completed:{c.ExpertName}",
                _                        => "Other",
            });

            // Block only A's started fact — proves the runner awaits this sink before invoking
            // A's expert, not merely fires-and-forgets it.
            if (evt is PipelineStepStarted { ExpertName: "A" })
            {
                reachedGate.SetResult();
                await gate.Task;
            }
        });

        var runTask = new PipelineRunner(stub).RunAsync(ast, Experts("A", "B"), options);

        // Deterministic: once this completes, the started sink for A is provably parked on the
        // gate, so the runner has not — and cannot yet have — invoked A's expert.
        await reachedGate.Task;
        Assert.Empty(stub.Calls);
        Assert.Equal(["Started:A"], events);

        gate.SetResult();
        await runTask;

        // Completed for A is observed strictly before the next step's started fact.
        Assert.Equal(["Started:A", "Completed:A", "Started:B", "Completed:B"], events);
        Assert.Equal(["A", "B"], stub.Calls.Select(c => c.ExpertName));
    }

    // ── 3. Closed tool-request shape ─────────────────────────────────────────────

    [Fact]
    public async Task ToolCapableStep_EmitsToolRequested_AfterCompleted_WithClosedShape()
    {
        var ast = MclParser.Parse("""
            mission ToolTask(goal) = {
                Enrich
                -> Respond
            }
            """);
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
        {
            ["Enrich"]  = Expert("Enrich"),
            ["Respond"] = new("Respond", "any", "text", "You respond.", Role: "agent"),
        };

        var stub = new StubExpertRunner((name, context) =>
        {
            if (name != "Respond")
                return new StepEnvelope($"{name} done");

            context["tool_calls"] = new List<FunctionCallContent>
            {
                new("call-1", "read_file", new Dictionary<string, object?>
                {
                    ["path"] = "mission/notes.md",
                    ["recursive"] = true,
                }),
            };
            return new StepEnvelope(string.Empty);
        });

        var events = new List<PipelineTraceEvent>();
        var result = await new PipelineRunner(stub)
            .RunAsync(ast, experts, new PipelineRunOptions("ToolTask",
                new Dictionary<string, string> { ["goal"] = "read the probe file" },
                OnTrace: (evt, _) =>
                {
                    events.Add(evt);
                    return Task.CompletedTask;
                }));

        Assert.NotNull(result.ToolCalls);
        Assert.Single(result.ToolCalls!);

        // Never a raw provider-SDK object on the trace — only the closed PipelineToolCall shape.
        var toolRequested = Assert.Single(events.OfType<PipelineToolRequested>());
        var respondCompletedIndex = events.IndexOf(
            events.OfType<PipelineStepCompleted>().Single(e => e.ExpertName == "Respond"));
        Assert.True(events.IndexOf(toolRequested) > respondCompletedIndex,
            "PipelineToolRequested must follow that step's completed fact.");

        var call = Assert.Single(toolRequested.Calls);
        Assert.Equal("call-1", call.CallId);
        Assert.Equal("read_file", call.Name);
        Assert.Equal("mission/notes.md", call.Arguments.GetProperty("path").GetString());
        Assert.True(call.Arguments.GetProperty("recursive").GetBoolean());
    }
}
