namespace ForgeMission.ClientRuntime.Transport;

public interface IClientRuntimeChannel
{
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct);

    IAsyncEnumerable<ClientRuntimeEvent> Subscribe(CancellationToken ct);
}
