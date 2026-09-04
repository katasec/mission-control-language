using System.Text.Json.Serialization;

namespace ForgeMission.ClientRuntime.Transport;

// ClientRuntimeEvent is deliberately not declared here — it now embeds a Contracts
// ConversationEvent and is (de)serialized through ConversationRelayJsonContext instead, which
// mirrors ConversationContractsJsonContext's string-enum options so that embedded payload's wire
// format matches what ConversationHost produces. Every other type below is unaffected: their
// enums (e.g. CapabilityRequestData's CapabilityOperation) keep serializing under this context's
// own (numeric) default.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SessionSetupRequest))]
[JsonSerializable(typeof(SessionSetupResponse))]
[JsonSerializable(typeof(ProjectDraftRequest))]
[JsonSerializable(typeof(ProjectDraftResponse))]
[JsonSerializable(typeof(ProjectCreateRequest))]
[JsonSerializable(typeof(ProjectOpenRequest))]
[JsonSerializable(typeof(ProjectOperationResponse))]
[JsonSerializable(typeof(OpenProjectMissionControlRequest))]
[JsonSerializable(typeof(OpenProjectMissionControlResponse))]
[JsonSerializable(typeof(SubmitProjectMissionControlTurnRequest))]
[JsonSerializable(typeof(SubmitProjectMissionControlTurnResponse))]
// 43.20 task 3. Every payload type is registered explicitly rather than left to graph
// reachability from its response root, so a later refactor that stops referencing one of them
// from a registered type cannot silently drop its metadata and fail only at runtime under AOT.
[JsonSerializable(typeof(GetProjectWorkbenchRequest))]
[JsonSerializable(typeof(GetProjectWorkbenchResponse))]
[JsonSerializable(typeof(ProjectWorkbenchProjection))]
[JsonSerializable(typeof(ProjectExplorerEntry))]
[JsonSerializable(typeof(IReadOnlyList<ProjectExplorerEntry>))]
[JsonSerializable(typeof(ProjectExplorerEntryKind))]
[JsonSerializable(typeof(OpenProjectDocumentRequest))]
[JsonSerializable(typeof(OpenProjectDocumentResponse))]
[JsonSerializable(typeof(ProjectDocument))]
[JsonSerializable(typeof(ProjectOperationError))]
[JsonSerializable(typeof(CapabilityDispatchRequest))]
[JsonSerializable(typeof(CapabilityDispatchResponse))]
[JsonSerializable(typeof(PromptRequest))]
[JsonSerializable(typeof(PromptResponse))]
[JsonSerializable(typeof(ConfirmationResponseRequest))]
[JsonSerializable(typeof(ConfirmationResponse))]
[JsonSerializable(typeof(CapabilityRequestData))]
public partial class ClientRuntimeJsonContext : JsonSerializerContext;
