using System.Text.Json.Serialization;

namespace Scout.Grok;

// Internal wire DTOs for the xAI Responses API (POST https://api.x.ai/v1/responses), verified live
// 2026-07-12. These never leak past GrokWebSearch — callers see only the provider-neutral types in
// IWebSearch.cs. Only the fields Scout needs are modelled; unknown fields are ignored on deserialize.

// ---- Request ------------------------------------------------------------------------------------

internal sealed class GrokRequest
{
    [JsonPropertyName("model")] public required string Model { get; init; }
    [JsonPropertyName("input")] public required IReadOnlyList<GrokInputMessage> Input { get; init; }
    [JsonPropertyName("tools")] public required IReadOnlyList<GrokTool> Tools { get; init; }
}

internal sealed class GrokInputMessage
{
    [JsonPropertyName("role")] public required string Role { get; init; }
    [JsonPropertyName("content")] public required string Content { get; init; }
}

internal sealed class GrokTool
{
    [JsonPropertyName("type")] public required string Type { get; init; }        // "web_search"
    [JsonPropertyName("filters")] public GrokToolFilters? Filters { get; init; } // omitted when null
}

internal sealed class GrokToolFilters
{
    [JsonPropertyName("allowed_domains")] public IReadOnlyList<string>? AllowedDomains { get; init; }
}

// ---- Response -----------------------------------------------------------------------------------
// output[] is a typed-item array: "reasoning" | "web_search_call" | "message". The synthesized answer
// + citations live on the single "message" item: content[type=="output_text"].{text, annotations[]}.

internal sealed class GrokResponse
{
    [JsonPropertyName("output")] public List<GrokOutputItem>? Output { get; init; }
    [JsonPropertyName("usage")] public GrokUsage? Usage { get; init; }
    [JsonPropertyName("model")] public string? Model { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
}

internal sealed class GrokOutputItem
{
    [JsonPropertyName("type")] public string? Type { get; init; }            // "message" is the one we read
    [JsonPropertyName("content")] public List<GrokContent>? Content { get; init; }
}

internal sealed class GrokContent
{
    [JsonPropertyName("type")] public string? Type { get; init; }            // "output_text"
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("annotations")] public List<GrokAnnotation>? Annotations { get; init; }
}

internal sealed class GrokAnnotation
{
    [JsonPropertyName("type")] public string? Type { get; init; }            // "url_citation"
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }          // numeric label ("1"), not a page title
}

internal sealed class GrokUsage
{
    [JsonPropertyName("input_tokens")] public int InputTokens { get; init; }
    [JsonPropertyName("output_tokens")] public int OutputTokens { get; init; }
    [JsonPropertyName("total_tokens")] public int TotalTokens { get; init; }
}

// ---- STJ source-gen context (AOT-safe; no bare JsonSerializerOptions per CLAUDE.md) --------------

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GrokRequest))]
[JsonSerializable(typeof(GrokResponse))]
internal partial class GrokJsonContext : JsonSerializerContext;
