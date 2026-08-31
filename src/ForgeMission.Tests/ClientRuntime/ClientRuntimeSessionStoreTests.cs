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
    public void CreateForProject_NoMission_LeavesMissionNull()
    {
        var session = _store.CreateForProject(_workspace);

        Assert.Null(session.Mission);
    }

    [Fact]
    public void CreateForProject_WithMission_StoresIt()
    {
        var session = _store.CreateForProject(_workspace, "websearch");

        Assert.Equal("websearch", session.Mission);
    }

    [Fact]
    public void CreateForProject_WithMission_IsRetrievableViaTryGet()
    {
        var created = _store.CreateForProject(_workspace, "websearch");

        Assert.True(_store.TryGet(created.Id, out var found));
        Assert.Equal("websearch", found!.Mission);
    }

    [Fact]
    public void CreateForProject_NoRuntime_DefaultsToMission()
    {
        var session = _store.CreateForProject(_workspace);

        Assert.Equal(SessionRuntimeKind.Mission, session.Runtime);
    }

    [Fact]
    public void CreateForProject_DurableConversationRuntime_IsRetained()
    {
        var session = _store.CreateForProject(_workspace, "Janus", SessionRuntimeKind.DurableConversation);

        Assert.Equal(SessionRuntimeKind.DurableConversation, session.Runtime);
    }

    [Fact]
    public async Task ReplaceAsync_RemovesThePriorSession()
    {
        var original = _store.CreateForProject(_workspace);

        var replacement = await _store.ReplaceAsync(original.Id, _workspace, "Janus");

        Assert.False(_store.TryGet(original.Id, out _));
        Assert.True(_store.TryGet(replacement.Id, out _));
        Assert.Equal(_workspace, replacement.Workspace.Root);
    }

    // 43.20 task 1: replacement is the *only* thing this path may do. An unknown session ID used to
    // be a silent no-op that still minted a session — exactly the second way to open an arbitrary
    // root the Project contracts are meant to be the only source of.
    [Fact]
    public async Task ReplaceAsync_UnknownSessionId_IsRejectedAndCreatesNothing()
    {
        await Assert.ThrowsAsync<SessionReplacementRejectedException>(
            () => _store.ReplaceAsync("does-not-exist", _workspace));
    }

    [Fact]
    public async Task ReplaceAsync_ARootOtherThanTheSessionsOwn_IsRejectedAndLeavesItInPlace()
    {
        var original = _store.CreateForProject(_workspace);
        var elsewhere = Directory.CreateTempSubdirectory("forge-session-store-other-").FullName;

        try
        {
            await Assert.ThrowsAsync<SessionReplacementRejectedException>(
                () => _store.ReplaceAsync(original.Id, elsewhere));

            Assert.True(_store.TryGet(original.Id, out _));
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }
}
