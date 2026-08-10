using System.ClientModel;
using Anthropic;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Runtime;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ForgeMission.ChatClients;

public static class ChatClients
{
    public static IExpertRunner Build(ProviderProfile profile) =>
        new DirectExpertRunner(BuildChatClient(profile));

    public static IChatClient BuildChatClient(ProviderProfile profile) =>
        profile.Provider.ToLowerInvariant() switch
        {
            "openai" or "azure" => BuildOpenAiClient(profile),
            "ollama"            => BuildOllamaClient(profile),
            "anthropic"         => BuildAnthropicClient(profile),
            "xai"               => BuildXaiClient(profile),
            _ => throw new InvalidOperationException($"Unknown provider '{profile.Provider}'")
        };

    private static IChatClient BuildOpenAiClient(ProviderProfile profile)
    {
        var options = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(profile.Endpoint))
            options.Endpoint = new Uri(profile.Endpoint);
        return new OpenAIClient(new ApiKeyCredential(profile.ApiKey ?? string.Empty), options)
            .GetChatClient(profile.Model)
            .AsIChatClient();
    }

    // Ollama is OpenAI-compatible — point the OpenAI client at the local Ollama endpoint.
    private static IChatClient BuildOllamaClient(ProviderProfile profile)
    {
        var endpoint = string.IsNullOrWhiteSpace(profile.Endpoint)
            ? "http://localhost:11434/v1"
            : profile.Endpoint;
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        return new OpenAIClient(new ApiKeyCredential("ollama"), options)
            .GetChatClient(profile.Model)
            .AsIChatClient();
    }

    // xAI (Grok) is OpenAI-compatible — point the OpenAI client at api.x.ai.
    private static IChatClient BuildXaiClient(ProviderProfile profile)
    {
        var endpoint = string.IsNullOrWhiteSpace(profile.Endpoint)
            ? "https://api.x.ai/v1"
            : profile.Endpoint;
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        return new OpenAIClient(new ApiKeyCredential(profile.ApiKey ?? string.Empty), options)
            .GetChatClient(profile.Model)
            .AsIChatClient();
    }

    private static IChatClient BuildAnthropicClient(ProviderProfile profile)
    {
        var client = string.IsNullOrWhiteSpace(profile.Endpoint)
            ? new AnthropicClient(profile.ApiKey ?? string.Empty)
            : new AnthropicClient(
                httpClient: null,
                baseUri: new Uri(profile.Endpoint),
                authorizations:
                [
                    new EndPointAuthorization
                    {
                        Type = "ApiKey",
                        Location = "Header",
                        Name = "x-api-key",
                        Value = profile.ApiKey ?? string.Empty
                    }
                ]);

        return new AnthropicResponseFormatChatClient(client, profile.Model);
    }
}

// tryAGI.Anthropic implements IChatClient directly, but receives native structured-output
// configuration only through RawRepresentationFactory rather than ChatOptions.ResponseFormat.
internal sealed class AnthropicResponseFormatChatClient(IChatClient inner, string modelId) : IChatClient
{
    private const int DefaultMaxTokens = 4096;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options = EnsureModelId(options);
        TranslateResponseFormat(options);
        return inner.GetResponseAsync(messages, options, cancellationToken);
    }

    // Streaming uses DirectExpertRunner's prompt-level JSON instruction rather than ResponseFormat.
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        inner.GetStreamingResponseAsync(messages, EnsureModelId(options), cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();

    private ChatOptions EnsureModelId(ChatOptions? options)
    {
        options ??= new ChatOptions();
        options.ModelId ??= modelId;
        return options;
    }

    private static void TranslateResponseFormat(ChatOptions? options)
    {
        if (options?.ResponseFormat is not ChatResponseFormatJson format)
            return;

        options.RawRepresentationFactory = _ => new CreateMessageParams
        {
            Model = string.Empty,
            Messages = [],
            MaxTokens = options.MaxOutputTokens ?? DefaultMaxTokens,
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat(format.Schema ?? throw new InvalidOperationException(
                    "Anthropic structured output requires a JSON schema."))
            }
        };
    }
}
