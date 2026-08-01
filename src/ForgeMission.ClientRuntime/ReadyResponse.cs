using System.Text.Json.Serialization;

namespace ForgeMission.ClientRuntime;

internal sealed record ReadyResponse(string Url);

[JsonSerializable(typeof(ReadyResponse))]
internal partial class ReadyResponseJsonContext : JsonSerializerContext;
