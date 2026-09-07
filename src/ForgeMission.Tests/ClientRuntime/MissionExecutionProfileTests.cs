using ForgeMission.ClientRuntime;
using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class MissionExecutionProfileTests
{
    [Fact]
    public async Task NoHands_DeclaresNothing_AndWorkspaceProfile_HasNoTerminal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"forge-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await using var noHands = ClientExecutionSession.CreateForMission(root, MissionExecutionProfile.NoHands,
                CapabilityAuthorizationPolicy.Default, AcceptingConfirmation.Instance, CancellationToken.None);
            await using var workspace = ClientExecutionSession.CreateForMission(root, MissionExecutionProfile.ProjectWorkspace,
                CapabilityAuthorizationPolicy.Default, AcceptingConfirmation.Instance, CancellationToken.None);

            Assert.Empty(noHands.AvailableCapabilities);
            Assert.Empty(noHands.ToolDeclarations);
            Assert.Equal(["file"], workspace.AvailableCapabilities);
            Assert.DoesNotContain(workspace.AvailableCapabilities, capability => capability == "terminal");
            var denied = await noHands.DispatchAsync("file", new ReadFileCapabilityRequest("anything", 0, null), CancellationToken.None);
            var terminalDenied = await workspace.DispatchAsync("terminal", new ExecuteTerminalCapabilityRequest("pwd"), CancellationToken.None);
            var unknownDenied = await workspace.DispatchAsync("unknown", new ReadFileCapabilityRequest("anything", 0, null), CancellationToken.None);
            var rootEscape = await workspace.DispatchAsync("file", new ReadFileCapabilityRequest("../outside", 0, null), CancellationToken.None);
            Assert.True(denied.IsError);
            Assert.True(terminalDenied.IsError);
            Assert.True(unknownDenied.IsError);
            Assert.True(rootEscape.IsError);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task TerminalProfile_UsesMacOsDenyDefaultBoundary()
    {
        if (!OperatingSystem.IsMacOS() || !File.Exists("/usr/bin/sandbox-exec"))
            return;

        var root = Path.Combine(Path.GetTempPath(), $"forge-profile-{Guid.NewGuid():N}");
        var escape = Path.Combine(Path.GetTempPath(), $"forge-escape-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await using var session = ClientExecutionSession.CreateForMission(root,
                MissionExecutionProfile.ProjectWorkspaceAndTerminal, CapabilityAuthorizationPolicy.Default,
                AcceptingConfirmation.Instance, CancellationToken.None);

            var inRoot = await session.DispatchAsync("terminal",
                new ExecuteTerminalCapabilityRequest("printf contained > proof.txt"), CancellationToken.None);
            var escaped = await session.DispatchAsync("terminal",
                new ExecuteTerminalCapabilityRequest($"touch '{escape}'"), CancellationToken.None);

            Assert.True(!inRoot.IsError, inRoot.Content);
            Assert.True(File.Exists(Path.Combine(root, "proof.txt")));
            Assert.True(escaped.IsError);
            Assert.False(File.Exists(escape));
        }
        finally
        {
            if (File.Exists(escape)) File.Delete(escape);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TerminalProfile_RejectsEnvironmentProcessNetworkCredentialSymlinkAndWorkingDirectoryEscapes()
    {
        if (!OperatingSystem.IsMacOS() || !File.Exists("/usr/bin/sandbox-exec"))
            return;

        var root = Path.Combine(Path.GetTempPath(), $"forge-profile-{Guid.NewGuid():N}");
        var outside = Path.Combine(Path.GetTempPath(), $"forge-outside-{Guid.NewGuid():N}");
        var credential = Path.Combine(outside, "credential.txt");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(credential, "not-a-secret");
        File.CreateSymbolicLink(Path.Combine(root, "outside-link"), credential);
        try
        {
            await using var session = ClientExecutionSession.CreateForMission(root,
                MissionExecutionProfile.ProjectWorkspaceAndTerminal, CapabilityAuthorizationPolicy.Default,
                AcceptingConfirmation.Instance, CancellationToken.None);

            var environment = await session.DispatchAsync("terminal",
                new ExecuteTerminalCapabilityRequest("/usr/bin/env"), CancellationToken.None);
            var credentialEscape = await session.DispatchAsync("terminal",
                new ExecuteTerminalCapabilityRequest($"cat '{credential}'"), CancellationToken.None);
            var symlinkEscape = await session.DispatchAsync("terminal",
                new ExecuteTerminalCapabilityRequest("cat outside-link"), CancellationToken.None);
            var workingDirectoryEscape = await session.DispatchAsync("terminal",
                new ExecuteTerminalCapabilityRequest("cd .. && /bin/pwd"), CancellationToken.None);
            var processEscape = await session.DispatchAsync("terminal",
                new ExecuteTerminalCapabilityRequest("kill -0 1"), CancellationToken.None);
            var networkEscape = await session.DispatchAsync("terminal",
                new ExecuteTerminalCapabilityRequest("/usr/bin/curl --connect-timeout 1 --max-time 2 https://example.com >/dev/null"), CancellationToken.None);

            Assert.False(environment.IsError, environment.Content);
            Assert.DoesNotContain("AWS_SECRET_ACCESS_KEY=", environment.Content, StringComparison.Ordinal);
            Assert.Contains($"HOME={root}", environment.Content, StringComparison.Ordinal);
            Assert.True(credentialEscape.IsError, credentialEscape.Content);
            Assert.True(symlinkEscape.IsError, symlinkEscape.Content);
            Assert.True(workingDirectoryEscape.IsError, workingDirectoryEscape.Content);
            Assert.True(processEscape.IsError, processEscape.Content);
            Assert.True(networkEscape.IsError, networkEscape.Content);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task MissionProfile_UsesTheInjectedConfirmationRequiredPolicy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"forge-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var policy = new CapabilityAuthorizationPolicy(
            [
                new KeyValuePair<string, CapabilityAuthorizationRule>("file",
                    new CapabilityAuthorizationRule(AuthorizationOutcome.RequiresUserConfirmation)),
            ]);
            await using var session = ClientExecutionSession.CreateForMission(root, MissionExecutionProfile.ProjectWorkspace,
                policy, RejectingConfirmation.Instance, CancellationToken.None);

            var result = await session.DispatchAsync("file", new WriteFileCapabilityRequest("proof.txt", "must not write"), CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal(1, RejectingConfirmation.Instance.Calls);
            Assert.False(File.Exists(Path.Combine(root, "proof.txt")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class AcceptingConfirmation : ICapabilityConfirmationHandler
    {
        public static readonly AcceptingConfirmation Instance = new();
        public Task<bool> ConfirmAsync(CapabilityConfirmationRequest request, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class RejectingConfirmation : ICapabilityConfirmationHandler
    {
        public static readonly RejectingConfirmation Instance = new();
        public int Calls { get; private set; }
        public Task<bool> ConfirmAsync(CapabilityConfirmationRequest request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(false);
        }
    }
}
