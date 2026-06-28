using System.ClientModel;
using Anthropic;
using Anthropic.Core;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Runtime;
using Microsoft.Extensions.AI;
using OpenAI;

// Builds an IExpertRunner from a ProviderProfile declared in forge.toml.
// Lives in CLI (not Core) because it depends on provider-specific packages.
public static class ProviderClientBuilder
{
    public static IExpertRunner Build(ProviderProfile profile) =>
        new DirectExpertRunner(BuildChatClient(profile));

    public static IChatClient BuildChatClient(ProviderProfile profile) =>
        profile.Provider.ToLowerInvariant() switch
        {
            "openai" or "azure" => BuildOpenAiClient(profile),
            "ollama"            => BuildOllamaClient(profile),
            "anthropic"         => BuildAnthropicClient(profile),
            _ => throw new InvalidOperationException($"Unknown provider '{profile.Provider}'")
        };

    private static IChatClient BuildOpenAiClient(ProviderProfile p)
    {
        var options = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(p.Endpoint))
            options.Endpoint = new Uri(p.Endpoint);
        return new OpenAIClient(new ApiKeyCredential(p.ApiKey ?? string.Empty), options)
            .GetChatClient(p.Model)
            .AsIChatClient();
    }

    // Ollama is OpenAI-compatible — point the OpenAI client at the local Ollama endpoint.
    private static IChatClient BuildOllamaClient(ProviderProfile p)
    {
        var endpoint = string.IsNullOrWhiteSpace(p.Endpoint)
            ? "http://localhost:11434/v1"
            : p.Endpoint;
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        return new OpenAIClient(new ApiKeyCredential("ollama"), options)
            .GetChatClient(p.Model)
            .AsIChatClient();
    }

    private static IChatClient BuildAnthropicClient(ProviderProfile p)
    {
        var options = new ClientOptions { ApiKey = p.ApiKey ?? string.Empty };
        if (!string.IsNullOrWhiteSpace(p.Endpoint))
            options = options with { BaseUrl = p.Endpoint };
        return new AnthropicClient(options).AsIChatClient(p.Model);
    }
}
