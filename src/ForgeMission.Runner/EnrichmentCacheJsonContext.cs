using System.Text.Json.Serialization;

namespace ForgeMission.Runner;

[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class EnrichmentCacheJsonContext : JsonSerializerContext;
