using System.Text.Json;
using ForgeMission.Runner.Contracts;

namespace ForgeMission.Runner.Tests;

/// <summary>Agent-turn fields use the source-generated runner-contract wire context.</summary>
public sealed class RunContractsSerializationTests
{
    [Fact]
    public void Agent_turn_request_round_trips_through_contracts_context()
    {
        var request = new RunRequest(
            MissionLabel: "vanilla",
            Goal: "Read the file",
            Vars: null,
            Policy: RunPolicy.Trusted,
            History:
            [
                new TurnMessage
                {
                    Role = "assistant",
                    Content =
                    [
                        new TurnContent
                        {
                            Type = "tool_use",
                            ToolUseId = "call-1",
                            ToolName = "Read",
                            ToolInput = ParseElement("""{"path":"notes.txt"}""")
                        },
                        new TurnContent
                        {
                            Type = "tool_result",
                            ToolUseId = "call-1",
                            ToolResult = "mission notes"
                        }
                    ]
                }
            ],
            Tools:
            [
                new MissionToolDecl
                {
                    Name = "Read",
                    Description = "Read a workspace file",
                    InputSchema = ParseElement("""{"type":"object","required":["path"]}""")
                }
            ]);

        var json = JsonSerializer.Serialize(request, RunContractsContext.Default.RunRequest);
        var roundTripped = JsonSerializer.Deserialize(json, RunContractsContext.Default.RunRequest);

        Assert.Contains("\"History\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Tools\"", json, StringComparison.Ordinal);
        Assert.NotNull(roundTripped);
        var history = Assert.Single(roundTripped.History!);
        var toolUse = history.Content[0];
        Assert.Equal("assistant", history.Role);
        Assert.Equal("tool_use", toolUse.Type);
        Assert.Equal("notes.txt", toolUse.ToolInput!.Value.GetProperty("path").GetString());
        Assert.Equal("mission notes", history.Content[1].ToolResult);
        Assert.Equal("Read", Assert.Single(roundTripped.Tools!).Name);
    }

    [Fact]
    public void Tool_use_response_round_trips_through_contracts_context()
    {
        var response = new RunResponse(
            AgentText: "",
            Verified: false,
            StepCount: 1,
            RetryCount: 0,
            Trace: [],
            Usage: new RunUsage(0, 0, 0, null),
            ToolUse:
            [
                new ToolUseCall
                {
                    Id = "call-1",
                    Name = "Read",
                    Arguments = ParseElement("""{"path":"notes.txt"}""")
                }
            ]);

        var json = JsonSerializer.Serialize(response, RunContractsContext.Default.RunResponse);
        var roundTripped = JsonSerializer.Deserialize(json, RunContractsContext.Default.RunResponse);

        Assert.Contains("\"ToolUse\"", json, StringComparison.Ordinal);
        Assert.NotNull(roundTripped);
        var toolUse = Assert.Single(roundTripped.ToolUse!);
        Assert.Equal("call-1", toolUse.Id);
        Assert.Equal("Read", toolUse.Name);
        Assert.Equal("notes.txt", toolUse.Arguments.GetProperty("path").GetString());
    }

    private static JsonElement ParseElement(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
