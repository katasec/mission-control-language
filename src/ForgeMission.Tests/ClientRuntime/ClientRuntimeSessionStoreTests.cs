using ForgeMission.ClientRuntime.TransportHost;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ClientRuntimeSessionStoreTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("forge-session-store-").FullName;
    private readonly ClientRuntimeSessionStore _store = new(new ClientRuntimeEventHub(), new ConfigurationBuilder().Build());

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    [Fact]
    public void Create_NoMission_LeavesMissionNull()
    {
        var session = _store.Create(_workspace);

        Assert.Null(session.Mission);
    }

    [Fact]
    public void Create_WithMission_StoresIt()
    {
        var session = _store.Create(_workspace, "websearch");

        Assert.Equal("websearch", session.Mission);
    }

    [Fact]
    public void Create_WithMission_IsRetrievableViaTryGet()
    {
        var created = _store.Create(_workspace, "websearch");

        Assert.True(_store.TryGet(created.Id, out var found));
        Assert.Equal("websearch", found!.Mission);
    }
}
