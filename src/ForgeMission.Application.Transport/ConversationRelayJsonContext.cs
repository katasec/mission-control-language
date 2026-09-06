using System.Text.Json.Serialization;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Application.Transport;

// Dedicated source-generated context for the local Application Host<->Presentation loopback
// envelope that carries a durable ConversationEvent. Mirrors ConversationContractsJsonContext's
// own options exactly (camelCase, omit-null, string enums) so the embedded event's wire bytes
// stay identical to what ConversationHost produces/consumes — a runtime-built
// JsonSerializerOptions/TypeInfoResolverChain is deliberately not used here; this is pure source
// generation, no reflection fallback. ApplicationJsonContext keeps every other application
// request/response type untouched.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ApplicationEvent))]
[JsonSerializable(typeof(ProjectMissionChange))]
[JsonSerializable(typeof(ConversationEvent))]
public partial class ConversationRelayJsonContext : JsonSerializerContext;
