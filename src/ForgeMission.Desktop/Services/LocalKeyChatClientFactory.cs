using System.ClientModel;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Resolution;
using ForgeMission.Core.Runtime;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ForgeMission.Desktop.Services;

public sealed class LocalKeyChatClientFactory : IChatClientFactory
{
    public IChatClient Create(ProviderProfile profile)
    {
        if (!string.Equals(profile.Provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Forge Desktop currently supports only the openai provider; profile '{profile.Provider}' is not supported.");
        }

        var credential = CredentialStore.GetProvider(profile.Provider);
        if (credential is null)
        {
            throw new InvalidOperationException(
                "No API key configured for provider 'openai'. Add it to ~/.forge/credentials.json under providers.openai.apiKey.");
        }

        var options = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(profile.Endpoint))
            options.Endpoint = new Uri(profile.Endpoint);

        return new OpenAIClient(new ApiKeyCredential(credential.ApiKey), options)
            .GetChatClient(profile.Model)
            .AsIChatClient();
    }
}
