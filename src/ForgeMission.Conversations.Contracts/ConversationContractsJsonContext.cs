using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeMission.Conversations.Contracts;

/// <summary>Source-generated, AOT-safe JSON metadata for every durable conversation contract type.
/// Shared verbatim by ConversationHost and, from Task 7, ClientRuntime — this is the one wire
/// format both sides serialize/deserialize against.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ConversationParticipant))]
[JsonSerializable(typeof(ConversationPurpose))]
[JsonSerializable(typeof(ConversationEventKind))]
[JsonSerializable(typeof(ConversationRunStatus))]
[JsonSerializable(typeof(ConversationApprovalOutcome))]
[JsonSerializable(typeof(ConversationCommandKind))]
[JsonSerializable(typeof(ConversationApproval))]
[JsonSerializable(typeof(ConversationToolRequest))]
[JsonSerializable(typeof(ConversationToolResult))]
[JsonSerializable(typeof(ConversationArtifactReference))]
[JsonSerializable(typeof(ConversationCapabilityDeclaration))]
[JsonSerializable(typeof(ConversationCapabilityDeclaration[]))]
[JsonSerializable(typeof(ConversationEvent))]
[JsonSerializable(typeof(ConversationSnapshot))]
[JsonSerializable(typeof(ConversationCommand))]
[JsonSerializable(typeof(ConversationProgress))]
[JsonSerializable(typeof(StartConversationRequest))]
[JsonSerializable(typeof(StartConversationResponse))]
[JsonSerializable(typeof(SubmitConversationCommandRequest))]
[JsonSerializable(typeof(SubmitConversationCommandResponse))]
[JsonSerializable(typeof(SubmitToolResultRequest))]
[JsonSerializable(typeof(SubmitToolResultResponse))]
[JsonSerializable(typeof(GetConversationRequest))]
[JsonSerializable(typeof(GetConversationResponse))]
[JsonSerializable(typeof(ReadConversationEventsRequest))]
[JsonSerializable(typeof(CreateProjectControlConversationRequest))]
[JsonSerializable(typeof(CreateProjectControlConversationResponse))]
[JsonSerializable(typeof(SubmitProjectControlMessageRequest))]
[JsonSerializable(typeof(SubmitProjectControlMessageResponse))]
// 43.21 task 1 — the universal Project Mission invocation pair.
[JsonSerializable(typeof(CreateProjectMissionContainerRequest))]
[JsonSerializable(typeof(CreateProjectMissionContainerResponse))]
[JsonSerializable(typeof(StartProjectMissionRunRequest))]
[JsonSerializable(typeof(StartProjectMissionRunResponse))]
[JsonSerializable(typeof(ConversationApiError))]
[JsonSerializable(typeof(ProjectRunSummary))]
[JsonSerializable(typeof(ProjectRunSummary[]))]
[JsonSerializable(typeof(ProjectRunCursor))]
[JsonSerializable(typeof(ProjectRunPage))]
[JsonSerializable(typeof(ProjectRunDetail))]
[JsonSerializable(typeof(ProjectRunEventPage))]
[JsonSerializable(typeof(ProjectCommandReceipt))]
[JsonSerializable(typeof(JsonElement))]
public partial class ConversationContractsJsonContext : JsonSerializerContext;
