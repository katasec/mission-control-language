using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.ClientRuntime.TransportHost;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Tools;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ClientRuntimeSessionStoreTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("forge-session-store-").FullName;
    private readonly string _recovery = Directory.CreateTempSubdirectory("forge-session-store-recovery-").FullName;
    private readonly ConversationResumeStore _resumeStore;
    private readonly ConversationToolResultLedger _ledger;
    private readonly ClientRuntimeSessionStore _store;

    public ClientRuntimeSessionStoreTests()
    {
        _resumeStore = new ConversationResumeStore(_recovery);
        _ledger = new ConversationToolResultLedger(_recovery);
        _store = new ClientRuntimeSessionStore(new ClientRuntimeEventHub(), new ConfigurationBuilder().Build(), _resumeStore);
    }

    public void Dispose()
    {
        Directory.Delete(_workspace, recursive: true);
        Directory.Delete(_recovery, recursive: true);
    }

    [Fact]
    public async Task CreateAsync_NoMission_LeavesMissionNull()
    {
        var session = await _store.CreateAsync(_workspace);

        Assert.Null(session.Mission);
    }

    [Fact]
    public async Task CreateAsync_WithMission_StoresIt()
    {
        var session = await _store.CreateAsync(_workspace, "websearch");

        Assert.Equal("websearch", session.Mission);
    }

    [Fact]
    public async Task CreateAsync_WithMission_IsRetrievableViaTryGet()
    {
        var created = await _store.CreateAsync(_workspace, "websearch");

        Assert.True(_store.TryGet(created.Id, out var found));
        Assert.Equal("websearch", found!.Mission);
    }

    [Fact]
    public async Task CreateAsync_NoRuntime_DefaultsToMission()
    {
        var session = await _store.CreateAsync(_workspace);

        Assert.Equal(SessionRuntimeKind.Mission, session.Runtime);
    }

    [Fact]
    public async Task CreateAsync_DurableConversationRuntime_IsRetained()
    {
        var session = await _store.CreateAsync(_workspace, "Janus", SessionRuntimeKind.DurableConversation);

        Assert.Equal(SessionRuntimeKind.DurableConversation, session.Runtime);
    }

    [Fact]
    public async Task CreateAsync_WithReplacesSessionId_RemovesThePriorSession()
    {
        var original = await _store.CreateAsync(_workspace);

        await _store.CreateAsync(_workspace, replacesSessionId: original.Id);

        Assert.False(_store.TryGet(original.Id, out _));
    }

    [Fact]
    public async Task CreateAsync_WithUnknownReplacesSessionId_IsANoOp()
    {
        var session = await _store.CreateAsync(_workspace, replacesSessionId: "does-not-exist");

        Assert.NotNull(session);
    }

    // Phase 43.16 Task 8d — resume-candidates/resume admission rule: WorkspaceRoot and MissionRef
    // are always derived from the caller's already-established session, never accepted as input.

    [Fact]
    public async Task GetResumeCandidatesAsync_NonDurableSession_ReturnsEmpty()
    {
        var session = await _store.CreateAsync(_workspace, "websearch", SessionRuntimeKind.Mission);
        await _resumeStore.UpsertAsync(
            new ResumeRecord(Guid.NewGuid(), _workspace, "websearch", ConversationRunStatus.Queued, DateTimeOffset.UtcNow),
            CancellationToken.None);

        var candidates = await _store.GetResumeCandidatesAsync(session.Id, CancellationToken.None);

        Assert.Empty(candidates); // Mission-kind sessions never resume — Runtime must be DurableConversation
    }

    [Fact]
    public async Task GetResumeCandidatesAsync_DurableSession_ReturnsOnlyRecordsMatchingItsOwnWorkspaceAndMission()
    {
        var session = await _store.CreateAsync(_workspace, "Janus", SessionRuntimeKind.DurableConversation);
        var matching = new ResumeRecord(Guid.NewGuid(), _workspace, "Janus", ConversationRunStatus.WaitingForTool, DateTimeOffset.UtcNow);
        await _resumeStore.UpsertAsync(matching, CancellationToken.None);
        await _resumeStore.UpsertAsync(
            new ResumeRecord(Guid.NewGuid(), "/some/other/workspace", "Janus", ConversationRunStatus.Queued, DateTimeOffset.UtcNow),
            CancellationToken.None);

        var candidates = await _store.GetResumeCandidatesAsync(session.Id, CancellationToken.None);

        var single = Assert.Single(candidates);
        Assert.Equal(matching.ConversationId, single.ConversationId);
    }

    [Fact]
    public async Task ResumeConversationAsync_ForARecordScopedToADifferentWorkspace_ReturnsNull_AndNeverAttaches()
    {
        var session = await _store.CreateAsync(_workspace, "Janus", SessionRuntimeKind.DurableConversation);
        var elsewhereConversationId = Guid.NewGuid();
        await _resumeStore.UpsertAsync(
            new ResumeRecord(elsewhereConversationId, "/some/other/workspace", "Janus", ConversationRunStatus.Queued, DateTimeOffset.UtcNow),
            CancellationToken.None);
        var factoryCalled = false;

        var status = await _store.ResumeConversationAsync(session.Id, elsewhereConversationId,
            () => { factoryCalled = true; return NewSession(new UnreachableHandler(), session.Id); },
            CancellationToken.None);

        Assert.Null(status);
        Assert.False(factoryCalled); // never even attempts to attach an out-of-scope conversation
    }

    [Fact]
    public async Task ResumeConversationAsync_ForAKnownRecord_AttachesAndReturnsTheHostStatus()
    {
        var session = await _store.CreateAsync(_workspace, "Janus", SessionRuntimeKind.DurableConversation);
        var conversationId = Guid.NewGuid();
        await _resumeStore.UpsertAsync(
            new ResumeRecord(conversationId, _workspace, "Janus", ConversationRunStatus.Queued, DateTimeOffset.UtcNow),
            CancellationToken.None);
        var handler = new SingleConversationGetHandler(conversationId);

        var status = await _store.ResumeConversationAsync(session.Id, conversationId,
            () => NewSession(handler, session.Id), CancellationToken.None);

        Assert.Equal(ConversationRunStatus.WaitingForTool, status);
    }

    private ConversationRuntimeSession NewSession(HttpMessageHandler handler, string sessionId)
    {
        var workspace = new LocalDiskWorkspace(_workspace);
        var capabilities = new CapabilityRegistry([new WorkspaceFileProvider(workspace), new WorkspaceTerminalProvider(workspace)]);
        var dispatcher = new CapabilityDispatcher(
            capabilities, new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default), new InMemoryCapabilityAuditLog());
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://conversation-host.test/") };
        return new ConversationRuntimeSession(
            sessionId, "Janus", _workspace, new ConversationHostClient(http), capabilities, dispatcher, _ => { },
            CancellationToken.None, _resumeStore, _ledger);
    }

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("Must never reach the Host for an out-of-scope resume attempt.");
    }

    private sealed class SingleConversationGetHandler(Guid conversationId) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == $"/conversations/{conversationId}")
            {
                var payload = JsonSerializer.Serialize(new
                {
                    snapshot = new
                    {
                        conversationId,
                        missionRef = "Janus",
                        activeRunId = (Guid?)null,
                        lastSequence = 0,
                        status = "waitingForTool",
                        expectedToolRequestId = (Guid?)null,
                        updatedAtUtc = DateTimeOffset.UtcNow,
                    },
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                });
            }

            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/events", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "text/event-stream"),
                });

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri!.AbsolutePath}");
        }
    }
}
