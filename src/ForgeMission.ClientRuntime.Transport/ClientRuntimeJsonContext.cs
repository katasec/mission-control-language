using System.Text.Json.Serialization;

namespace ForgeMission.ClientRuntime.Transport;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SessionSetupRequest))]
[JsonSerializable(typeof(SessionSetupResponse))]
[JsonSerializable(typeof(CapabilityDispatchRequest))]
[JsonSerializable(typeof(CapabilityDispatchResponse))]
[JsonSerializable(typeof(PromptRequest))]
[JsonSerializable(typeof(PromptResponse))]
[JsonSerializable(typeof(ConfirmationResponseRequest))]
[JsonSerializable(typeof(ConfirmationResponse))]
[JsonSerializable(typeof(CapabilityRequestData))]
[JsonSerializable(typeof(ClientRuntimeEvent))]
public partial class ClientRuntimeJsonContext : JsonSerializerContext;
