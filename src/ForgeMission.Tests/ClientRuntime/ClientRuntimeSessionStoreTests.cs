using ForgeMission.ClientRuntime.Transport;
using ForgeMission.ClientRuntime.TransportHost;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ClientRuntimeSessionStoreTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("forge-session-store-").FullName;
    private readonly ClientRuntimeSessionStore _store = new(new ClientRuntimeEventHub(), new ConfigurationBuilder().Build());

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

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
}
