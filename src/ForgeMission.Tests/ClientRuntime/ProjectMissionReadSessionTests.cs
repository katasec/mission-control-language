using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ProjectMissionReadSessionTests : IDisposable
{
    private readonly string _profile = Directory.CreateTempSubdirectory("forge-project-read-").FullName;

    public void Dispose() => Directory.Delete(_profile, recursive: true);

    [Fact]
    public async Task StateRead_EstablishesSubscriptionBeforeTheInitialRunPage()
    {
        var projects = new ProjectStore(Path.Combine(_profile, "Forge", "Projects"));
        var project = projects.Create("Read Project Mission history.", null, null);
        var containerId = Guid.NewGuid();
        await projects.SetProjectMissionContainerIdAsync(project.Home, containerId, CancellationToken.None);
        var handler = new ReadHandler(containerId, project.Manifest.ProjectId);
        var host = new ConversationHostClient(new HttpClient(handler) { BaseAddress = new Uri("https://conversation-host.test/") });
        await using var read = new ProjectMissionReadSession("session", project.Home, projects, host, _ => { }, CancellationToken.None);

        var response = await read.GetStateAsync(CancellationToken.None);

        Assert.Null(response.Error);
        Assert.NotNull(response.State);
        Assert.Equal(["events", "snapshot", "runs"], handler.Calls.Take(3));
    }

    [Fact]
    public async Task InitialSubscriptionFailure_ReturnsAvailabilityMetadata_ThenALaterReadReconnects()
    {
        var projects = new ProjectStore(Path.Combine(_profile, "Forge", "Projects"));
        var project = projects.Create("Recover Project Mission history.", null, null);
        var containerId = Guid.NewGuid();
        await projects.SetProjectMissionContainerIdAsync(project.Home, containerId, CancellationToken.None);
        var handler = new ReadHandler(containerId, project.Manifest.ProjectId) { FailFirstSubscription = true };
        var host = new ConversationHostClient(new HttpClient(handler) { BaseAddress = new Uri("https://conversation-host.test/") });
        await using var read = new ProjectMissionReadSession("session", project.Home, projects, host, _ => { }, CancellationToken.None);

        var unavailable = await read.GetStateAsync(CancellationToken.None);
        var recovered = await read.GetStateAsync(CancellationToken.None);

        Assert.Equal(ProjectOperationErrorCode.HistoryUnavailable, unavailable.State!.HistoryError!.Code);
        Assert.Null(recovered.Error);
        Assert.NotNull(recovered.State!.Runs);
        Assert.True(handler.Calls.Count(call => call == "events") >= 2);
    }

    private sealed class ReadHandler(Guid containerId, Guid projectId) : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];
        public bool FailFirstSubscription { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/events", StringComparison.Ordinal))
            {
                Calls.Add("events");
                if (FailFirstSubscription)
                {
                    FailFirstSubscription = false;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    { Content = new StringContent("unavailable", Encoding.UTF8, "text/plain") });
                }
                return Task.FromResult(Json(string.Empty, "text/event-stream"));
            }
            if (path == $"/conversations/{containerId}")
            {
                Calls.Add("snapshot");
                return Task.FromResult(Json(JsonSerializer.Serialize(new
                {
                    snapshot = new { conversationId = containerId, missionRef = (string?)null, activeRunId = (Guid?)null,
                        lastSequence = 0, status = "queued", expectedToolRequestId = (Guid?)null,
                        updatedAtUtc = DateTimeOffset.UtcNow, purpose = "projectMission", projectId },
                }), "application/json"));
            }
            if (path.EndsWith("/runs", StringComparison.Ordinal))
            {
                Calls.Add("runs");
                return Task.FromResult(Json(JsonSerializer.Serialize(new
                {
                    containerId, indexedSequence = 0, targetSequence = 0, synchronizing = false,
                    runs = Array.Empty<object>(), next = (object?)null,
                }), "application/json"));
            }
            throw new InvalidOperationException($"Unexpected request: {path}");
        }

        private static HttpResponseMessage Json(string body, string type) => new(HttpStatusCode.OK)
        { Content = new StringContent(body, Encoding.UTF8, type) };
    }
}
