using System.Text.Json;
using Katasec.AnthropicServer;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Adapters;

/// <summary>
/// Neutral tool mapping + essentials allowlist (Phase 42.3 task 1), verified against the
/// sanitized wire fixtures captured from the real claude CLI (2.1.195, 2026-07-16).
/// The server is a relay: declarations in (filtered), tool_use out, tool_result back in —
/// nothing executes server-side. Re-capture and re-verify on CLI version bumps.
/// </summary>
public sealed class ToolMappingTests
{
    private static readonly string FixtureDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "anthropic-wire");

    private static AnthropicRequest LoadRequest(string fixture)
    {
        var json    = File.ReadAllText(Path.Combine(FixtureDir, fixture));
        var request = JsonSerializer.Deserialize(json, AnthropicJsonContext.Default.AnthropicRequest);
        Assert.NotNull(request);
        return request;
    }

    // ------------------------------------------------------------------
    // Essentials allowlist — the 28 captured declarations filter to exactly 4
    // ------------------------------------------------------------------

    [Fact]
    public void MapDeclaredTools_CapturedToolSet_FiltersToEssentials()
    {
        var request = LoadRequest("main-loop-tools.json");
        Assert.Equal(28, request.Tools.Count); // full captured declaration set

        var mapped = ToolMapping.MapDeclaredTools(request);
        var names  = mapped.Select(t => t.Name).OrderBy(n => n).ToList();

        Assert.Equal(["Bash", "Edit", "Read", "Write"], names);
    }

    [Fact]
    public void MapDeclaredTools_NeverForwards_McpConnectorsOrHarnessTools()
    {
        var request = LoadRequest("main-loop-tools.json");
        var names   = ToolMapping.MapDeclaredTools(request).Select(t => t.Name).ToHashSet();

        Assert.DoesNotContain(names, n => n.StartsWith("mcp__"));   // privacy
        Assert.DoesNotContain("Agent", names);                      // subagents excluded
        Assert.DoesNotContain("WebFetch", names);                   // retrieval is the mission's job
        Assert.DoesNotContain("WebSearch", names);
        Assert.DoesNotContain("NotebookEdit", names);
    }

    [Fact]
    public void MappedTool_RelaysClientSchemaVerbatim()
    {
        var request = LoadRequest("main-loop-tools.json");
        var bash    = (AIFunction)ToolMapping.MapDeclaredTools(request).Single(t => t.Name == "Bash");

        Assert.True(bash.JsonSchema.TryGetProperty("properties", out var props));
        Assert.True(props.TryGetProperty("command", out _)); // the real captured schema, untouched
    }

    [Fact]
    public async Task MappedTool_IsNeverExecutableServerSide()
    {
        var request = LoadRequest("main-loop-tools.json");
        var tool    = (AIFunction)ToolMapping.MapDeclaredTools(request).First();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => tool.InvokeAsync(new AIFunctionArguments()).AsTask());
    }

    // ------------------------------------------------------------------
    // IsToolContinuation — drives the three-segment gate
    // ------------------------------------------------------------------

    [Fact]
    public void IsToolContinuation_UserTextTurn_IsFalse()
    {
        Assert.False(ToolMapping.IsToolContinuation(LoadRequest("main-loop-4-block-user.json")));
    }

    [Fact]
    public void IsToolContinuation_ToolResultEndedTurn_IsTrue()
    {
        var request = ParseRequest("""
        {
          "model": "claude-sonnet-4-6",
          "messages": [
            { "role": "user", "content": [ { "type": "text", "text": "read the file" } ] },
            { "role": "assistant", "content": [
                { "type": "tool_use", "id": "toolu_01", "name": "Read", "input": { "file_path": "/tmp/x" } } ] },
            { "role": "user", "content": [
                { "type": "tool_result", "tool_use_id": "toolu_01", "content": "the magic word is PLATYPUS" } ] }
          ]
        }
        """);

        Assert.True(ToolMapping.IsToolContinuation(request));
    }

    [Fact]
    public void IsToolContinuation_PlainStringContent_IsFalse()
    {
        var request = ParseRequest("""
        { "model": "m", "messages": [ { "role": "user", "content": "hello" } ] }
        """);

        Assert.False(ToolMapping.IsToolContinuation(request));
    }

    // ------------------------------------------------------------------
    // History mapping — tool_use / tool_result survive into M.E.AI shapes
    // ------------------------------------------------------------------

    [Fact]
    public void BuildChatHistory_MapsToolUseAndToolResult()
    {
        var request = ParseRequest("""
        {
          "model": "claude-sonnet-4-6",
          "messages": [
            { "role": "user", "content": [ { "type": "text", "text": "read the file" } ] },
            { "role": "assistant", "content": [
                { "type": "text", "text": "Let me read it." },
                { "type": "tool_use", "id": "toolu_01", "name": "Read", "input": { "file_path": "/tmp/x" } } ] },
            { "role": "user", "content": [
                { "type": "tool_result", "tool_use_id": "toolu_01", "content": "the magic word is PLATYPUS" } ] }
          ]
        }
        """);

        var history = AnthropicServer.BuildChatHistory(request);

        var call = Assert.Single(history[1].Contents.OfType<FunctionCallContent>());
        Assert.Equal("toolu_01", call.CallId);
        Assert.Equal("Read", call.Name);
        Assert.True(call.Arguments!.ContainsKey("file_path"));

        var result = Assert.Single(history[2].Contents.OfType<FunctionResultContent>());
        Assert.Equal("toolu_01", result.CallId);
        Assert.Contains("PLATYPUS", result.Result!.ToString());
    }

    private static AnthropicRequest ParseRequest(string json)
    {
        var request = JsonSerializer.Deserialize(json, AnthropicJsonContext.Default.AnthropicRequest);
        Assert.NotNull(request);
        return request;
    }
}
