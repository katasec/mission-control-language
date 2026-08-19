using System.ComponentModel;
using System.Diagnostics;
using ForgeMission.Orchestration;

namespace ForgeMission.Tests.Orchestration;

// The tunnel's whole authority is one loopback port-forward. These prove the exact command it may
// run, that a missing kubectl becomes a named prerequisite failure rather than a crash, and that
// disposal is safe when there is nothing of its own to stop — no cluster or real process involved.
public sealed class LocalKindConversationRuntimeTunnelTests
{
    [Fact]
    public void PortForwardStartInfo_IsExactlyTheLoopbackPortForward()
    {
        var startInfo = LocalKindConversationRuntimeTunnel.PortForwardStartInfo();

        Assert.Equal("kubectl", startInfo.FileName);
        Assert.Equal(
        [
            "port-forward", "--address", "127.0.0.1",
            "--namespace", "forge-durable",
            "service/conversation-host", "18080:8080",
        ], startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void Start_KubectlUnavailable_ThrowsNamingThePrerequisite()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => LocalKindConversationRuntimeTunnel.Start(_ => throw new Win32Exception("No such file")));

        Assert.Contains("kubectl", error.Message);
        Assert.Contains("350-conversation-kind-up", error.Message);
    }

    [Fact]
    public void Start_NoProcessStarted_ThrowsNamingThePrerequisite()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => LocalKindConversationRuntimeTunnel.Start(_ => null));

        Assert.Contains("kubectl", error.Message);
    }

    [Fact]
    public async Task DisposeAsync_ProcessThatIsNotRunning_IsASafeNoOpAndIsIdempotent()
    {
        var tunnel = LocalKindConversationRuntimeTunnel.Start(_ => new Process());

        await tunnel.DisposeAsync();
        await tunnel.DisposeAsync();
    }
}
