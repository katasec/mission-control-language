using System.Text.Json;
using ForgeMission.Api;

namespace ForgeMission.Rooms.Tests;

/// <summary>Agent-turn fields use the source-generated API wire context, including its camelCase policy.</summary>
public sealed class MessagesSerializationTests
{
    [Fact]
    public void Agent_turn_messages_round_trip_through_messages_context()
    {
        var request = new ExecuteMission
        {
            Mission = "vanilla",
            Input = "Read the file",
            History =
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
            Tools =
            [
                new MissionToolDecl
                {
                    Name = "Read",
                    Description = "Read a workspace file",
                    InputSchema = ParseElement("""{"type":"object","required":["path"]}""")
                }
            ]
        };

        var json = JsonSerializer.Serialize(request, MessagesJsonContext.Default.ExecuteMission);
        var roundTripped = JsonSerializer.Deserialize(json, MessagesJsonContext.Default.ExecuteMission);

        Assert.Contains("\"history\"", json, StringComparison.Ordinal);
        Assert.Contains("\"tools\"", json, StringComparison.Ordinal);
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
    public void Tool_use_response_round_trips_through_messages_context()
    {
        var response = new ExecuteMissionResponse
        {
            RunId = "run-1",
            ToolUse =
            [
                new ToolUseCall
                {
                    Id = "call-1",
                    Name = "Read",
                    Arguments = ParseElement("""{"path":"notes.txt"}""")
                }
            ]
        };

        var json = JsonSerializer.Serialize(response, MessagesJsonContext.Default.ExecuteMissionResponse);
        var roundTripped = JsonSerializer.Deserialize(json, MessagesJsonContext.Default.ExecuteMissionResponse);

        Assert.Contains("\"toolUse\"", json, StringComparison.Ordinal);
        Assert.NotNull(roundTripped);
        var toolUse = Assert.Single(roundTripped.ToolUse!);
        Assert.Equal("call-1", toolUse.Id);
        Assert.Equal("Read", toolUse.Name);
        Assert.Equal("notes.txt", toolUse.Arguments.GetProperty("path").GetString());
    }

    private static JsonElement ParseElement(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
