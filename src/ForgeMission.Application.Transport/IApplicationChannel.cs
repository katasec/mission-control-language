namespace ForgeMission.Application.Transport;

public interface IApplicationChannel
{
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct);

    IAsyncEnumerable<ApplicationEvent> Subscribe(CancellationToken ct);
}
