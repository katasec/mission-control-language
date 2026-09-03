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
[JsonSerializable(typeof(CapabilityDispatchRequest))]
[JsonSerializable(typeof(CapabilityDispatchResponse))]
[JsonSerializable(typeof(PromptRequest))]
[JsonSerializable(typeof(PromptResponse))]
[JsonSerializable(typeof(ConfirmationResponseRequest))]
[JsonSerializable(typeof(ConfirmationResponse))]
[JsonSerializable(typeof(CapabilityRequestData))]
public partial class ClientRuntimeJsonContext : JsonSerializerContext;
