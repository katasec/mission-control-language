using ForgeMission.ChatClients;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Runtime;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>One deployment-owned durable runner.  Package content never selects this binding.</summary>
public sealed class WorkerProviderOptions
{
    public string? DefaultProvider { get; init; }
    public string? DefaultModel { get; init; }
    public string? DefaultEndpoint { get; init; }
    public string? DefaultApiKey { get; init; }

    public IExpertRunner BuildDefaultRunner()
    {
        if (string.IsNullOrWhiteSpace(DefaultProvider) || string.IsNullOrWhiteSpace(DefaultModel))
            throw new InvalidOperationException("ConversationWorker:DefaultProvider and ConversationWorker:DefaultModel are required.");
        return ForgeMission.ChatClients.ChatClients.Build(new ProviderProfile
        {
            Provider = DefaultProvider,
            Model = DefaultModel,
            Endpoint = DefaultEndpoint,
            ApiKey = DefaultApiKey,
        });
    }
}
