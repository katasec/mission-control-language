using System.Runtime.CompilerServices;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;
using ForgeMission.Desktop.Services;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Desktop;

public sealed class VanillaMissionSessionFactoryTests : IDisposable
{
    private readonly string workspacePath = Directory.CreateTempSubdirectory("forge-desktop-").FullName;

    public void Dispose() => Directory.Delete(workspacePath, recursive: true);

    [Fact]
    public async Task StreamingToolCall_ReadsPlantedFile_StreamsFinalAnswer()
    {
        var filePath = Path.Combine(workspacePath, "notes.txt");
        await File.WriteAllTextAsync(filePath, "streamed marker: NOVA-43");

        var client = new ScriptedStreamingToolClient(filePath);
        var factory = new VanillaMissionSessionFactory(new ScriptedChatClientFactory(client));
        var session = factory.Create(new LocalDiskWorkspace(workspacePath));
        using var streamed = new StringWriter();

        var result = await session.AgenticSession.RunAsync(new PipelineRunOptions(
            session.MissionName,
            new Dictionary<string, string> { ["goal"] = "Read notes.txt and report its marker." },
            ContentWriter: streamed));

        Assert.Equal(MissionStatus.Pass, result.Status);
        Assert.Contains("NOVA-43", result.Text);
        Assert.Contains("NOVA-43", streamed.ToString());
        Assert.Equal(2, client.CallCount);
        Assert.All(client.ToolCounts, count => Assert.NotEqual(0, count));
        Assert.Equal("streamed marker: NOVA-43", await File.ReadAllTextAsync(filePath));
    }

    private sealed class ScriptedChatClientFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(ProviderProfile profile)
        {
            Assert.Equal("openai", profile.Provider);
            return client;
        }
    }

    private sealed class ScriptedStreamingToolClient(string filePath) : IChatClient
    {
        public int CallCount { get; private set; }

        public List<int> ToolCounts { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException("This test exercises the streaming path only.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            CallCount++;
            ToolCounts.Add(options?.Tools?.Count ?? 0);
            var toolResults = messages.SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .ToList();

            if (toolResults.Count == 0)
            {
                var call = new FunctionCallContent("call_read_1", "Read",
                    new Dictionary<string, object?> { ["file_path"] = filePath });
                var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, [call])]);
                foreach (var update in response.ToChatResponseUpdates())
                    yield return update;
                yield break;
            }

            Assert.Contains("NOVA-43", toolResults[0].Result?.ToString() ?? string.Empty);
            yield return new ChatResponseUpdate(ChatRole.Assistant, "{\"text\":\"The marker is ");
            yield return new ChatResponseUpdate(ChatRole.Assistant, "NOVA-43\",\"status\":\"pass\",\"reason\":null}");
            await Task.CompletedTask;
        }

        public void Dispose() { }

        public object? GetService(Type serviceType, object? key = null) => null;
    }
}
