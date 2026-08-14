using System.Text.Json.Serialization;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Transport;

// Dedicated source-generated context for the local ClientRuntime<->Presentation loopback
// envelope that carries a durable ConversationEvent. Mirrors ConversationContractsJsonContext's
// own options exactly (camelCase, omit-null, string enums) so the embedded event's wire bytes
// stay identical to what ConversationHost produces/consumes — a runtime-built
// JsonSerializerOptions/TypeInfoResolverChain is deliberately not used here; this is pure source
// generation, no reflection fallback. ClientRuntimeJsonContext keeps every other Client Runtime
// request/response type untouched.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ClientRuntimeEvent))]
[JsonSerializable(typeof(ConversationEvent))]
public partial class ConversationRelayJsonContext : JsonSerializerContext;
