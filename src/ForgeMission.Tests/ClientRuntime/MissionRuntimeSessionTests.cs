using System.Runtime.CompilerServices;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.Core.Tools;
using ForgeMission.Tests.Integration;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.ClientRuntime;

// Task 2a's real client/server proof: the Client Runtime talks HTTP + SSE to an in-process
// AnthropicServer, then uses the production Edit executor against a real temporary workspace.
public sealed class MissionRuntimeSessionTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("forge-client-runtime-session-").FullName;

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    [Fact]
    public async Task SendAsync_StreamsToolRoundTrip_AndEditsWorkspace()
    {
        var notesPath = Path.Combine(_workspace, "notes.txt");
        await File.WriteAllTextAsync(notesPath, "status: before-PLATYPUS");

        var client = new ScriptedAgentClient(notesPath);
        await using var fixture = await AnthropicServerFixture.StartAsync(client);
        using var http = new HttpClient { BaseAddress = new Uri(fixture.BaseUrl) };
        var session = new MissionRuntimeSession(http);
        var workspace = new LocalDiskWorkspace(_workspace);
        var capabilities = new CapabilityRegistry(
        [
            new WorkspaceFileProvider(workspace),
            new WorkspaceTerminalProvider(workspace),
        ]);
        var streamed = new List<string>();
        var toolLog = new List<string>();

        var answer = await session.SendAsync(
            "Update notes.txt and report the final status.",
            capabilities,
            streamed.Add,
            notification => toolLog.Add($"{notification.State}:{notification.Call.Name}"));

        Assert.Equal("The final status is after-PLATYPUS.", answer);
        Assert.Equal(answer, string.Concat(streamed));
        Assert.Equal(["Running:Edit", "Done:Edit"], toolLog);
        Assert.Equal("status: after-PLATYPUS", await File.ReadAllTextAsync(notesPath));
        Assert.Equal(2, client.CallCount);
        Assert.Equal($"Edited {notesPath}", client.ToolResult);
    }

    private sealed class ScriptedAgentClient(string notesPath) : IChatClient
    {
        public int CallCount { get; private set; }
        public string? ToolResult { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            CallCount++;
            var results = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToList();
            ToolResult = results.SingleOrDefault()?.Result?.ToString();
            var reply = results.Count == 0
                ? new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("edit_1", "Edit", new Dictionary<string, object?>
                    {
                        ["file_path"] = notesPath,
                        ["old_string"] = "before-PLATYPUS",
                        ["new_string"] = "after-PLATYPUS",
                    })])
                : new ChatMessage(ChatRole.Assistant, "The final status is after-PLATYPUS.");

            return Task.FromResult(new ChatResponse([reply]));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var response = await GetResponseAsync(messages, options, ct);
            yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
        }

        public void Dispose() { }
        public object? GetService(Type serviceType, object? key = null) => null;
    }
}
