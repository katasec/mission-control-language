using Bunit;
using ForgeMission.ClientRuntime.Presentation.Components;
using ForgeMission.ClientRuntime.Presentation.Pages;
using ForgeMission.ClientRuntime.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Tests.Presentation;

public sealed class HomeSessionOperationTests : BunitContext
{
    [Fact]
    public void Boot_OnlyRendersTheZeroAuthorityLauncher()
    {
        var channel = new NoCallsChannel();
        Services.AddSingleton<IClientRuntimeChannel>(channel);

        var page = Render<Home>();

        Assert.Single(page.FindAll(".pl-goal"));
        Assert.Empty(channel.Requests);
        Assert.DoesNotContain("Project Explorer", page.Markup);
    }

    private sealed class NoCallsChannel : IClientRuntimeChannel
    {
        public List<object> Requests { get; } = [];
        public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
        { Requests.Add(request!); throw new InvalidOperationException("The launcher must not make a call before a person acts."); }
        public async IAsyncEnumerable<ClientRuntimeEvent> Subscribe([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) { await Task.Delay(Timeout.Infinite, ct); yield break; }
    }
}
