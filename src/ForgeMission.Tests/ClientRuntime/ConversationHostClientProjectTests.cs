using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ConversationHostClientProjectTests
{
    [Fact]
    public async Task ProjectErrorDecoder_PreservesTypedErrorAndRejectsMalformedBodies()
    {
        using var http = new HttpClient(new ErrorHandler("{\"code\":\"historySynchronizing\",\"message\":\"Retry shortly.\"}"))
        { BaseAddress = new Uri("http://localhost/") };
        var client = new ConversationHostClient(http);

        var error = await Assert.ThrowsAsync<ConversationHostProjectException>(() =>
            client.ReadProjectRunsAsync(Guid.NewGuid(), null, null, CancellationToken.None));
        Assert.Equal("historySynchronizing", error.Error.Code);

        using var malformed = new HttpClient(new ErrorHandler("not-json")) { BaseAddress = new Uri("http://localhost/") };
        var malformedClient = new ConversationHostClient(malformed);
        await Assert.ThrowsAsync<ConversationHostProtocolException>(() =>
            malformedClient.ReadProjectRunsAsync(Guid.NewGuid(), null, null, CancellationToken.None));
    }

    private sealed class ErrorHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
