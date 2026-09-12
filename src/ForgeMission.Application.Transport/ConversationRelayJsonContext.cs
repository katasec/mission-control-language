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
// Phase 48's Mission Chat family lives here, not in ApplicationJsonContext: these responses carry
// durable ConversationEvent values, and this context is the one that mirrors ConversationHost's own
// camelCase/omit-null/string-enum options so those embedded events keep byte parity. The durable
// MissionConversationEventPage is deliberately absent — a page never crosses this boundary.
[JsonSerializable(typeof(StartMissionChatRequest))]
[JsonSerializable(typeof(StartMissionChatResponse))]
[JsonSerializable(typeof(CreateMissionChatRequest))]
[JsonSerializable(typeof(CreateMissionChatResponse))]
[JsonSerializable(typeof(OpenMissionChatRequest))]
[JsonSerializable(typeof(OpenMissionChatResponse))]
[JsonSerializable(typeof(SubmitMissionChatTurnRequest))]
[JsonSerializable(typeof(SubmitMissionChatTurnResponse))]
[JsonSerializable(typeof(MissionChatPin))]
[JsonSerializable(typeof(MissionChatMember))]
[JsonSerializable(typeof(MissionChatRow))]
[JsonSerializable(typeof(MissionChatAccess))]
[JsonSerializable(typeof(MissionChatFailure))]
public partial class ConversationRelayJsonContext : JsonSerializerContext;
