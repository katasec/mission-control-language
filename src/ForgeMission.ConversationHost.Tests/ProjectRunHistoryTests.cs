using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;
using Azure;

namespace ForgeMission.ConversationHost.Tests;

[Collection("Azurite")]
public sealed class ProjectRunHistoryTests(AzuriteFixture fixture)
{
    [Fact]
    public async Task CreateStartAndReadHistory_UsesTypedContractsAndExactEvents()
    {
        await using var host = await fixture.StartHostAsync();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var projectId = Guid.NewGuid();
        var create = new CreateProjectMissionContainerRequest(projectId,
            ConversationDeterministicIds.ProjectMissionContainerCreate(projectId), "Ship the feature");

        var createResponse = await client.PostAsJsonAsync("/conversations/project-mission", create,
            ConversationContractsJsonContext.Default.CreateProjectMissionContainerRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var container = await createResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.CreateProjectMissionContainerResponse);
        Assert.NotNull(container);

        var commandId = Guid.NewGuid();
        var start = new StartProjectMissionRunRequest(container!.ContainerId, commandId, ProjectMissionNames.Janus, "Implement the history view");
        var startResponse = await client.PostAsJsonAsync($"/conversations/{container.ContainerId}/mission-runs", start,
            ConversationContractsJsonContext.Default.StartProjectMissionRunRequest);
        Assert.Equal(HttpStatusCode.Accepted, startResponse.StatusCode);
        var accepted = await startResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.StartProjectMissionRunResponse);
        Assert.NotNull(accepted);

        var pageResponse = await client.GetAsync($"/conversations/{container.ContainerId}/runs");
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        var page = await pageResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.ProjectRunPage);
        Assert.NotNull(page);
        Assert.False(page!.Synchronizing);
        var summary = Assert.Single(page.Runs);
        Assert.Equal(accepted!.RunId, summary.RunId);
        Assert.Equal(commandId, summary.CommandId);
        Assert.Equal(ProjectMissionNames.Janus, summary.Mission);
        Assert.Equal("Implement the history view", summary.Title);

        var detailResponse = await client.GetAsync($"/conversations/{container.ContainerId}/runs/{accepted.RunId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.ProjectRunDetail);
        Assert.Equal(start.Input, detail!.Input);

        var traceResponse = await client.GetAsync($"/conversations/{container.ContainerId}/runs/{accepted.RunId}/events?after=0");
        var trace = await traceResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.ProjectRunEventPage);
        Assert.Equal(2, trace!.Events.Length);
        Assert.All(trace.Events, item => Assert.Equal(accepted.RunId, item.RunId));

        var receiptResponse = await client.GetAsync($"/conversations/{container.ContainerId}/project-commands/{commandId}");
        var receipt = await receiptResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.ProjectCommandReceipt);
        Assert.Equal(start.Input, receipt!.Input);
        Assert.Equal(create.ProjectGoal, receipt.ProjectGoal);
    }

    [Fact]
    public async Task StartRejectsUnknownMissionWithTypedErrorWithoutAppending()
    {
        await using var host = await fixture.StartHostAsync();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var projectId = Guid.NewGuid();
        var create = new CreateProjectMissionContainerRequest(projectId,
            ConversationDeterministicIds.ProjectMissionContainerCreate(projectId), "Goal");
        var createResponse = await client.PostAsJsonAsync("/conversations/project-mission", create,
            ConversationContractsJsonContext.Default.CreateProjectMissionContainerRequest);
        var container = await createResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.CreateProjectMissionContainerResponse);

        var response = await client.PostAsJsonAsync($"/conversations/{container!.ContainerId}/mission-runs",
            new StartProjectMissionRunRequest(container.ContainerId, Guid.NewGuid(), "Other", "input"),
            ConversationContractsJsonContext.Default.StartProjectMissionRunRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.ConversationApiError);
        Assert.Equal("unknownMission", error!.Code);
        Assert.Empty((await host.GetConversationGrain(new ConversationAddress("dev", container.ContainerId)).ReadAfterAsync(0)).EventJson);
    }

    [Fact]
    public async Task HistoryAdvance_IsBoundedAndResumesAcrossCalls()
    {
        await using var host = await fixture.StartHostAsync();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var projectId = Guid.NewGuid();
        var create = new CreateProjectMissionContainerRequest(projectId,
            ConversationDeterministicIds.ProjectMissionContainerCreate(projectId), "Goal");
        var createResponse = await client.PostAsJsonAsync("/conversations/project-mission", create,
            ConversationContractsJsonContext.Default.CreateProjectMissionContainerRequest);
        var container = await createResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.CreateProjectMissionContainerResponse);
        var address = new ConversationAddress("dev", container!.ContainerId);
        var grain = host.GetConversationGrain(address);

        for (var i = 0; i < 11; i++)
        {
            var commandId = Guid.NewGuid();
            var response = await client.PostAsJsonAsync($"/conversations/{container.ContainerId}/mission-runs",
                new StartProjectMissionRunRequest(container.ContainerId, commandId, ProjectMissionNames.Naive, $"task {i}"),
                ConversationContractsJsonContext.Default.StartProjectMissionRunRequest);
            var accepted = await response.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.StartProjectMissionRunResponse);
            var completed = new ConversationProgress(Guid.NewGuid(), container.ContainerId, accepted!.RunId,
                ConversationEventKind.RunStatus, ConversationParticipant.Forge, null, null, null, null, null, null, null,
                ConversationRunStatus.Completed, DateTimeOffset.UtcNow);
            await grain.RecordProgressAsync(new ConversationProgressInput(JsonSerializer.Serialize(completed,
                ConversationContractsJsonContext.Default.ConversationProgress)));
        }

        var first = await client.GetFromJsonAsync($"/conversations/{container.ContainerId}/runs",
            ConversationContractsJsonContext.Default.ProjectRunPage);
        Assert.True(first!.Synchronizing);
        Assert.True(first.IndexedSequence <= 25);

        var resumed = await client.GetFromJsonAsync($"/conversations/{container.ContainerId}/runs",
            ConversationContractsJsonContext.Default.ProjectRunPage);
        Assert.False(resumed!.Synchronizing);
        Assert.Equal(11, resumed.Runs.Length);
    }

    [Fact]
    public async Task CreateRejectsANonDeterministicCommandIdBeforeAllocatingAContainer()
    {
        await using var host = await fixture.StartHostAsync();
        var projectId = Guid.NewGuid();
        var outcome = await ForgeMission.ConversationHost.Api.ConversationApiEndpoints.HandleCreateProjectMissionContainerAsync(
            new CreateProjectMissionContainerRequest(projectId, Guid.NewGuid(), "Goal"), host.GrainFactory);

        Assert.Equal(ConversationCommandOutcome.Invalid, outcome.Outcome);
        var expectedContainer = ConversationDeterministicIds.Conversation(
            ConversationDeterministicIds.ProjectMissionContainerCreate(projectId));
        Assert.Empty((await host.GetConversationGrain(new ConversationAddress("dev", expectedContainer)).ReadAfterAsync(0)).EventJson);
    }

    [Fact]
    public async Task GrainRejectsUnknownMissionBeforeAnyAppend()
    {
        await using var host = await fixture.StartHostAsync();
        var projectId = Guid.NewGuid();
        var createCommand = ConversationDeterministicIds.ProjectMissionContainerCreate(projectId);
        var address = new ConversationAddress("dev", ConversationDeterministicIds.Conversation(createCommand));
        var grain = host.GetConversationGrain(address);
        await grain.AcceptProjectMissionContainerCreateAsync(
            new ConversationProjectMissionCreateInput(createCommand, projectId, "Goal"));

        var outcome = await grain.AcceptProjectMissionRunAsync(
            new ConversationProjectMissionRunInput(Guid.NewGuid(), "Other", "input"));

        Assert.Equal(ConversationCommandOutcome.Invalid, outcome.Outcome);
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);
    }

    [Fact]
    public async Task StorageFailureOnProjectStartReturnsTypedServiceUnavailable()
    {
        await using var host = await fixture.StartHostAsync(_ => new UnavailableEventStore());
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var projectId = Guid.NewGuid();
        var create = new CreateProjectMissionContainerRequest(projectId,
            ConversationDeterministicIds.ProjectMissionContainerCreate(projectId), "Goal");
        var createResponse = await client.PostAsJsonAsync("/conversations/project-mission", create,
            ConversationContractsJsonContext.Default.CreateProjectMissionContainerRequest);
        var container = await createResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.CreateProjectMissionContainerResponse);

        var response = await client.PostAsJsonAsync($"/conversations/{container!.ContainerId}/mission-runs",
            new StartProjectMissionRunRequest(container.ContainerId, Guid.NewGuid(), ProjectMissionNames.Janus, "input"),
            ConversationContractsJsonContext.Default.StartProjectMissionRunRequest);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.ConversationApiError);
        Assert.Equal("serviceUnavailable", error!.Code);
    }

    private sealed class UnavailableEventStore : IConversationEventStore
    {
        private static RequestFailedException Failure() => new(503, "Unavailable", "ServiceUnavailable", null);
        public Task<StoredConversationEvent?> FindByEventIdAsync(ConversationAddress address, Guid eventId, CancellationToken ct) => Task.FromException<StoredConversationEvent?>(Failure());
        public Task<ConversationEvent> AppendAsync(ConversationAddress address, ConversationEvent item, string? command, CancellationToken ct) => Task.FromException<ConversationEvent>(Failure());
        public async IAsyncEnumerable<ConversationEvent> ReadAfterAsync(ConversationAddress address, long sequence, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) { await Task.CompletedTask; yield break; }
        public Task<ConversationEvent?> ReadLatestForRunAsync(ConversationAddress address, Guid runId, CancellationToken ct) => Task.FromException<ConversationEvent?>(Failure());
    }
}
