using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Parser;
using Katasec.AnthropicServer;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Integration;

/// <summary>
/// The CI-runnable half of Phase 42.3 task 8: a mock host that ACTUALLY EXECUTES tool calls
/// against the full HTTP stack (wire parse → classify → segments → tool_use out → execute →
/// tool_result in → resume → verify). Pass criterion is PLANTED tool-derived content — a
/// chained two-hop plant (file A holds the path of file B; file B holds the magic word) so
/// every hop of the loop is load-bearing. Status fields are never proof (no-false-green rule):
/// a hollow loop cannot produce the planted guid.
/// </summary>
public sealed class MockClaudeHostTests
{
    private static readonly Program Ast = MclParser.Parse("""
        mission Task(goal) = {
            Enrich
            -> Respond
            -> Verify
        }
        output(Task)
        """);

    private static Dictionary<string, ExpertDefinition> Experts() => new(StringComparer.Ordinal)
    {
        ["Enrich"]  = new("Enrich",  "any", "text", "ENRICH-PROMPT"),
        ["Respond"] = new("Respond", "any", "text", "RESPOND-PROMPT", Role: "agent"),
        ["Verify"]  = new("Verify",  "any", "text", "VERIFY-PROMPT"),
    };

    [Fact]
    public async Task MultiToolTask_ChainedPlant_EnrichOnce_VerifyOnce()
    {
        // The chained plant: the model cannot know the magic word without BOTH real reads.
        var magicWord = $"XYZZY-{Guid.NewGuid():N}";
        var fileB = Path.Combine(Path.GetTempPath(), $"forge-plant-b-{Guid.NewGuid():N}.txt");
        var fileA = Path.Combine(Path.GetTempPath(), $"forge-plant-a-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(fileB, $"the magic word is {magicWord}");
        await File.WriteAllTextAsync(fileA, $"the word lives in {fileB}");

        var provider = new ChainedToolProvider();
        var mission  = new MissionChatClient(Ast, Experts(), new DirectExpertRunner(provider), fullConversation: true);
        await using var fixture = await AnthropicServerFixture.StartAsync(mission);

        var finalAnswer = await new MockClaudeHost(fixture.BaseUrl)
            .RunTaskAsync($"Read {fileA}, follow it, and tell me the magic word.");

        // (planted content) The guid can only arrive via two REAL tool round-trips.
        Assert.Contains(magicWord, finalAnswer);
        // (a) enrich-once across the whole N-tool task.
        Assert.Equal(1, provider.EnrichCalls);
        // (b) post-agent ran exactly once, on the final continuation.
        Assert.Equal(1, provider.VerifyCalls);
        Assert.Contains("VERIFIED:", finalAnswer);
        // The loop really was multi-hop.
        Assert.Equal(3, provider.AgentCalls);
    }

    // ------------------------------------------------------------------
    // Mock claude host: drives /v1/messages and EXECUTES Read tool calls for real.
    // Frozen beliefs about the client; the real-CLI test catches the client evolving.
    // ------------------------------------------------------------------
    private sealed class MockClaudeHost(string baseUrl)
    {
        private const int MaxHops = 8;

        public async Task<string> RunTaskAsync(string prompt)
        {
            var messages = new List<object>
            {
                new { role = "user", content = new object[] { new { type = "text", text = prompt } } },
            };

            for (var hop = 0; hop < MaxHops; hop++)
            {
                var root = await PostAsync(messages);
                var content = root.GetProperty("content").EnumerateArray().ToList();

                if (root.GetProperty("stop_reason").GetString() != "tool_use")
                    return string.Concat(content
                        .Where(b => b.GetProperty("type").GetString() == "text")
                        .Select(b => b.GetProperty("text").GetString()));

                // Echo the assistant turn back verbatim, then EXECUTE each tool call for real.
                messages.Add(new { role = "assistant", content = content.Select(RawBlock).ToArray() });
                var results = content
                    .Where(b => b.GetProperty("type").GetString() == "tool_use")
                    .Select(b => (object)new
                    {
                        type        = "tool_result",
                        tool_use_id = b.GetProperty("id").GetString(),
                        content     = Execute(b),
                    })
                    .ToArray();
                messages.Add(new { role = "user", content = results });
            }

            throw new InvalidOperationException($"tool loop did not terminate in {MaxHops} hops");
        }

        // Real execution — the plant is unreachable unless this actually runs.
        private static string Execute(JsonElement toolUse)
        {
            var name = toolUse.GetProperty("name").GetString();
            if (name != "Read") return $"tool {name} is not available";
            var path = toolUse.GetProperty("input").GetProperty("file_path").GetString()!;
            return File.ReadAllText(path);
        }

        private static object RawBlock(JsonElement block)
            => JsonSerializer.Deserialize<object>(block.GetRawText())!;

        private async Task<JsonElement> PostAsync(List<object> messages)
        {
            var request = new
            {
                model      = "claude-sonnet-4-6",
                max_tokens = 1024,
                stream     = false,
                messages,
                tools = new object[]
                {
                    new
                    {
                        name         = "Read",
                        description  = "Reads a file",
                        input_schema = new { type = "object", properties = new { file_path = new { type = "string" } }, required = new[] { "file_path" } },
                    },
                },
            };

            using var http = new HttpClient();
            var response = await http.PostAsync($"{baseUrl}/v1/messages",
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
            return JsonDocument.Parse(body).RootElement.Clone();
        }
    }

    // ------------------------------------------------------------------
    // Scripted provider: follows the chain USING the tool results it receives —
    // nothing is hardcoded, so a broken hop breaks the answer.
    // ------------------------------------------------------------------
    private sealed class ChainedToolProvider : IChatClient
    {
        public int EnrichCalls { get; private set; }
        public int AgentCalls  { get; private set; }
        public int VerifyCalls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            var all    = messages.ToList();
            var system = all.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? "";

            if (system.Contains("ENRICH-PROMPT")) { EnrichCalls++; return Envelope("enriched"); }
            if (system.Contains("VERIFY-PROMPT"))
            {
                VerifyCalls++;
                // Post-agent transform: stamp the answer so its presence proves Verify ran.
                var answer = all.Last(m => m.Role == ChatRole.User).Text;
                return Envelope($"VERIFIED: {answer}");
            }

            AgentCalls++;
            var results = all.SelectMany(m => m.Contents).OfType<FunctionResultContent>().ToList();

            return Task.FromResult(new ChatResponse([results.Count switch
            {
                // Hop 1: read the path the user named (parsed from the prompt, not hardcoded).
                0 => ToolCall("toolu_chain_1", PathAfter(all.Last(m => m.Role == ChatRole.User && m.Contents.OfType<TextContent>().Any()).Text, "Read ")),
                // Hop 2: follow the path found INSIDE hop 1's real tool result.
                1 => ToolCall("toolu_chain_2", PathAfter(results[0].Result!.ToString()!, "lives in ")),
                // Done: answer FROM hop 2's real tool result.
                _ => new ChatMessage(ChatRole.Assistant, results[^1].Result!.ToString()!),
            }]));
        }

        private static string PathAfter(string text, string marker)
        {
            var start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            var end   = text.IndexOfAny([',', '\n'], start);
            return (end < 0 ? text[start..] : text[start..end]).Trim().TrimEnd('.');
        }

        private static ChatMessage ToolCall(string id, string path)
            => new(ChatRole.Assistant,
                [new FunctionCallContent(id, "Read", new Dictionary<string, object?> { ["file_path"] = path })]);

        private static Task<ChatResponse> Envelope(string text)
            => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant,
                JsonSerializer.Serialize(new { text, status = "pass", reason = (string?)null }))]));

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
}
