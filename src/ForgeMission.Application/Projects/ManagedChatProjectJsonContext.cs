using System.Text.Json.Serialization;

namespace ForgeMission.Application;

// mission-chat-project.json is a file a person may open and read, so it is written camelCase and
// indented, matching forge.project.json's own conventions. Source-generated: this assembly is
// Native AOT compatible and builds no runtime JsonSerializerOptions.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(ManagedChatProjectMarker))]
internal partial class ManagedChatProjectJsonContext : JsonSerializerContext;
