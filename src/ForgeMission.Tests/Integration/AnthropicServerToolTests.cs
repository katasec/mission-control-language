using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Katasec.AnthropicServer;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Integration;

/// <summary>
/// Tool round-trip through the wire (Phase 42.3 tasks 2-3): tools in via ChatOptions
/// (allowlist-filtered), tool_use out (non-streaming JSON + streaming SSE), tool_result
/// history back in. Driven over real HTTP against the server fixture with a scripted
/// IChatClient, so the assertions cover the wire serialization, not just the mapping.
/// </summary>
public sealed class AnthropicServerToolTests
{
    // ------------------------------------------------------------------
    // Non-streaming: model calls a tool → tool_use block, stop_reason "tool_use"
    // ------------------------------------------------------------------
    [Fact]
    public async Task NonStreaming_ModelCallsTool_EmitsToolUseBlock()
    {
        var fake = new ScriptedChatClient();
        await using var fixture = await AnthropicServerFixture.StartAsync(fake);

        var body = await PostAsync(fixture.BaseUrl, UserTurnWithTools("read the probe file"));
        var root = JsonDocument.Parse(body).RootElement;

        Assert.Equal("tool_use", root.GetProperty("stop_reason").GetString());
        var toolUse = root.GetProperty("content").EnumerateArray()
            .Single(b => b.GetProperty("type").GetString() == "tool_use");
        Assert.Equal("Read", toolUse.GetProperty("name").GetString());
        Assert.Equal("toolu_scripted_1", toolUse.GetProperty("id").GetString());
        Assert.Equal("/tmp/probe.txt", toolUse.GetProperty("input").GetProperty("file_path").GetString());

        // The allowlist filtered the declarations before they reached the model.
        Assert.NotNull(fake.LastOptions?.Tools);
        Assert.Equal(["Bash", "Edit", "Read", "Write"],
            fake.LastOptions!.Tools!.Select(t => t.Name).OrderBy(n => n).ToList());
    }

    // ------------------------------------------------------------------
    // Non-streaming continuation: tool_result history in → FunctionResultContent seen
    // ------------------------------------------------------------------
    [Fact]
    public async Task NonStreaming_ToolResultContinuation_ReachesModelAndEndsTurn()
    {
        var fake = new ScriptedChatClient();
        await using var fixture = await AnthropicServerFixture.StartAsync(fake);

        var body = await PostAsync(fixture.BaseUrl, ToolResultContinuation());
        var root = JsonDocument.Parse(body).RootElement;

        Assert.Equal("end_turn", root.GetProperty("stop_reason").GetString());
        var text = root.GetProperty("content").EnumerateArray()
            .Single(b => b.GetProperty("type").GetString() == "text")
            .GetProperty("text").GetString();
        Assert.Contains("PLATYPUS", text);

        // The model saw the tool result as a structured part, not prose.
        var results = fake.LastMessages!.SelectMany(m => m.Contents).OfType<FunctionResultContent>().ToList();
        var result  = Assert.Single(results);
        Assert.Equal("toolu_prev_1", result.CallId);
    }

    // ------------------------------------------------------------------
    // Streaming: tool_use block start + input_json_delta + stop_reason "tool_use"
    // ------------------------------------------------------------------
    [Fact]
    public async Task Streaming_ModelCallsTool_EmitsToolUseSseSequence()
    {
        var fake = new ScriptedChatClient();
        await using var fixture = await AnthropicServerFixture.StartAsync(fake);

        var sse = await PostAsync(fixture.BaseUrl, UserTurnWithTools("read the probe file", stream: true));

        var events = ParseSse(sse);
        var start  = events.Single(e => e.Event == "content_block_start"
            && e.Data.GetProperty("content_block").GetProperty("type").GetString() == "tool_use");
        Assert.Equal("Read", start.Data.GetProperty("content_block").GetProperty("name").GetString());

        var delta = events.Single(e => e.Event == "content_block_delta"
            && e.Data.GetProperty("delta").GetProperty("type").GetString() == "input_json_delta");
        var partial = delta.Data.GetProperty("delta").GetProperty("partial_json").GetString();
        Assert.Equal("/tmp/probe.txt",
            JsonDocument.Parse(partial!).RootElement.GetProperty("file_path").GetString());

        var messageDelta = events.Single(e => e.Event == "message_delta");
        Assert.Equal("tool_use", messageDelta.Data.GetProperty("delta").GetProperty("stop_reason").GetString());
    }

    // ------------------------------------------------------------------
    // Scripted client: tool_result in history → final text; otherwise → tool call
    // ------------------------------------------------------------------
    private sealed class ScriptedChatClient : IChatClient
    {
        public List<ChatMessage>? LastMessages { get; private set; }
        public ChatOptions?       LastOptions  { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            LastMessages = messages.ToList();
            LastOptions  = options;

            var sawToolResult = LastMessages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Any();
            var reply = sawToolResult
                ? new ChatMessage(ChatRole.Assistant, "the magic word is PLATYPUS")
                : new ChatMessage(ChatRole.Assistant,
                  [
                      new TextContent("Let me read it."),
                      new FunctionCallContent("toolu_scripted_1", "Read",
                          new Dictionary<string, object?> { ["file_path"] = JsonDocument.Parse("\"/tmp/probe.txt\"").RootElement }),
                  ]);

            return Task.FromResult(new ChatResponse([reply]));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var response = await GetResponseAsync(messages, options, ct);
            yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
        }

        public void Dispose() { }
        public object? GetService(Type serviceType, object? key = null) => null;
    }

    // ------------------------------------------------------------------
    // Request builders + wire helpers
    // ------------------------------------------------------------------

    private static string UserTurnWithTools(string prompt, bool stream = false)
    {
        var tools = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "anthropic-wire", "main-loop-tools.json"));
        var toolsArray = JsonDocument.Parse(tools).RootElement.GetProperty("tools").GetRawText();

        return $$"""
        {
          "model": "claude-sonnet-4-6",
          "max_tokens": 1024,
          "stream": {{(stream ? "true" : "false")}},
          "messages": [ { "role": "user", "content": [ { "type": "text", "text": "{{prompt}}" } ] } ],
          "tools": {{toolsArray}}
        }
        """;
    }

    private static string ToolResultContinuation() => """
    {
      "model": "claude-sonnet-4-6",
      "max_tokens": 1024,
      "stream": false,
      "messages": [
        { "role": "user", "content": [ { "type": "text", "text": "read the probe file" } ] },
        { "role": "assistant", "content": [
            { "type": "tool_use", "id": "toolu_prev_1", "name": "Read", "input": { "file_path": "/tmp/probe.txt" } } ] },
        { "role": "user", "content": [
            { "type": "tool_result", "tool_use_id": "toolu_prev_1", "content": "the magic word is PLATYPUS" } ] }
      ],
      "tools": [ { "name": "Read", "description": "Reads a file", "input_schema": { "type": "object" } } ]
    }
    """;

    private static async Task<string> PostAsync(string baseUrl, string json)
    {
        using var http = new HttpClient();
        var response = await http.PostAsync($"{baseUrl}/v1/messages",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        return body;
    }

    private static List<(string Event, JsonElement Data)> ParseSse(string sse)
    {
        var events = new List<(string, JsonElement)>();
        string? currentEvent = null;
        foreach (var line in sse.Split('\n'))
        {
            if (line.StartsWith("event: ")) currentEvent = line["event: ".Length..].Trim();
            else if (line.StartsWith("data: ") && currentEvent is not null)
                events.Add((currentEvent, JsonDocument.Parse(line["data: ".Length..]).RootElement.Clone()));
        }
        return events;
    }
}
