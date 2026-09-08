using System.Text.Json.Serialization;

namespace ForgeMission.Application;

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
[JsonSerializable(typeof(ProjectSubmission))]
[JsonSerializable(typeof(ProjectSubmissionAcceptance))]
[JsonSerializable(typeof(ProjectSubmissionRejection))]
[JsonSerializable(typeof(MissionVersionLaunch))]
[JsonSerializable(typeof(MissionVersionLaunch[]))]
[JsonSerializable(typeof(ProjectMissionDefinition))]
[JsonSerializable(typeof(ProjectMissionDefinition[]))]
[JsonSerializable(typeof(MissionDraft))]
[JsonSerializable(typeof(MissionVersion))]
[JsonSerializable(typeof(MissionVersion[]))]
[JsonSerializable(typeof(EvaluationCase))]
[JsonSerializable(typeof(EvaluationCase[]))]
[JsonSerializable(typeof(EvaluationResult))]
[JsonSerializable(typeof(EvaluationResult[]))]
[JsonSerializable(typeof(EvaluationTraceOrigin))]
internal partial class ProjectManifestJsonContext : JsonSerializerContext;
