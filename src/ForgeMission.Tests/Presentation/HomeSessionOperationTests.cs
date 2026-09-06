using Bunit;
using ForgeMission.Presentation.Components;
using ForgeMission.Presentation.Pages;
using ForgeMission.Application.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Tests.Presentation;

public sealed class HomeSessionOperationTests : BunitContext
{
    [Fact]
    public void Boot_OnlyRendersTheZeroAuthorityLauncher()
    {
        var channel = new NoCallsChannel();
        Services.AddSingleton<IApplicationChannel>(channel);

        var page = Render<Home>();

        Assert.Single(page.FindAll(".pl-goal"));
        Assert.Empty(channel.Requests);
        Assert.DoesNotContain("Project Explorer", page.Markup);
    }

    private sealed class NoCallsChannel : IApplicationChannel
    {
        public List<object> Requests { get; } = [];
        public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
        { Requests.Add(request!); throw new InvalidOperationException("The launcher must not make a call before a person acts."); }
        public async IAsyncEnumerable<ApplicationEvent> Subscribe([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) { await Task.Delay(Timeout.Infinite, ct); yield break; }
    }
}
