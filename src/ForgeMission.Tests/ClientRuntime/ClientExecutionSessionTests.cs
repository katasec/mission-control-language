using ForgeMission.ClientRuntime;
using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ClientExecutionSessionTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("forge-client-runtime-").FullName;

    [Fact]
    public async Task Create_ConstructsTheFixedCapabilitySet()
    {
        await using var state = ClientExecutionSession.Create(root, CapabilityAuthorizationPolicy.Default,
            new NoConfirmation(), CancellationToken.None);

        Assert.Equal(["file", "terminal"], state.AvailableCapabilities);
    }

    [Fact]
    public async Task DisposeAsync_ClosesAdmissionForARetainedDispatcher()
    {
        var state = ClientExecutionSession.Create(root, CapabilityAuthorizationPolicy.Default,
            new NoConfirmation(), CancellationToken.None);

        await state.DisposeAsync();
        await state.DisposeAsync();

        var result = await state.DispatchAsync("file", new ReadFileCapabilityRequest("missing.txt", 0, null), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("This execution session is closed.", result.Content);
    }

    [Fact]
    public async Task DisposeAsync_CancelsAndJoinsAnAlreadyAdmittedDispatch()
    {
        var confirmation = new BlockingConfirmation();
        var policy = new CapabilityAuthorizationPolicy(
        [
            new KeyValuePair<string, CapabilityAuthorizationRule>(
                "file", new CapabilityAuthorizationRule(AuthorizationOutcome.RequiresUserConfirmation)),
        ]);
        var state = ClientExecutionSession.Create(root, policy, confirmation, CancellationToken.None);

        var dispatch = state.DispatchAsync("file", new ReadFileCapabilityRequest("missing.txt", 0, null), CancellationToken.None);
        await confirmation.Started.Task;

        var firstDispose = state.DisposeAsync().AsTask();
        var secondDispose = state.DisposeAsync().AsTask();

        Assert.Same(firstDispose, secondDispose);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatch);
        await firstDispose;
    }

    private sealed class NoConfirmation : ICapabilityConfirmationHandler
    {
        public Task<bool> ConfirmAsync(CapabilityConfirmationRequest request, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class BlockingConfirmation : ICapabilityConfirmationHandler
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> ConfirmAsync(CapabilityConfirmationRequest request, CancellationToken ct)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return false;
        }
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
