namespace ForgeMission.Orchestration;

public interface IMissionRuntimeLauncher : IAsyncDisposable
{
    string BaseUrl { get; }
}
