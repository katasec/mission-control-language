using System.Text.Json.Serialization;

namespace ForgeMission.Api;

/// <summary>
/// The gateway's own JSON error envelope (42.6). Shaped like Anthropic's <c>/v1</c> error so Claude
/// Code surfaces a clean message on a 401; harmless to an OpenAI client, which only reads the status.
/// <c>{ "type": "error", "error": { "type": "authentication_error", "message": "…" } }</c>.
/// </summary>
public sealed record ApiError(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("error")] ApiErrorDetail Error)
{
    public static ApiError Auth(string message) =>
        new("error", new ApiErrorDetail("authentication_error", message));
}

public sealed record ApiErrorDetail(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] string Message);

/// <summary>
/// STJ source-gen context for the gateway's own responses. The relayed <c>/v1</c> bodies pass through
/// as opaque byte streams (never deserialized here), so only the gateway-authored types need a
/// resolver — this keeps <c>ForgeMission.Api</c> AOT-clean (no runtime <c>JsonSerializerOptions</c>).
/// </summary>
[JsonSerializable(typeof(ApiError))]
internal partial class ApiJsonContext : JsonSerializerContext;
