using System.Text.Json.Serialization;

namespace ForgeMission.ClientRuntime.Services;

// forge.project.json is a file a person may open and read, so it is written camelCase, indented,
// and with string enums — mirroring ConversationContractsJsonContext's options so the embedded
// ConversationRunStatus keeps the same JsonStringEnumMemberName values the durable stream uses.
//
// DefaultIgnoreCondition is deliberately NOT WhenWritingNull: an explicit
// "missionControlConversationId": null says "no Mission Control conversation yet" far more clearly
// than an absent key, and the same holds for every optional hash/digest in the graph.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true)]
[JsonSerializable(typeof(ProjectManifest))]
internal partial class ProjectManifestJsonContext : JsonSerializerContext;
