using ForgeMission.Rooms;

namespace ForgeUI.Models;

/// <summary>Manual entity → DTO mapping. No AutoMapper (by design — see phase-38.1).</summary>
public static class RoomsMappings
{
    public static RoomMessageDto ToDto(this Message message, string senderName) => new(
        message.Id,
        message.RoomId,
        message.SenderId,
        senderName,
        message.SenderKind == MemberKind.Agent ? "agent" : "human",
        message.Payload.Text,
        message.ReplyTo,
        message.CreatedAt,
        ToTrustDto(message.Payload.Agent),
        message.Payload.Artifacts.Count == 0
            ? null
            : message.Payload.Artifacts
                .Select(a => new ArtifactDto(a.Id, a.Filename, a.ContentType, a.Size))
                .ToList());

    private static AgentTrustDto? ToTrustDto(AgentMeta? agent)
    {
        if (agent is null)
            return null;

        return new AgentTrustDto(
            Handle: agent.Handle,
            // The single source of truth for green — computed through the guard here so a
            // false-green can never even reach the wire (38.3 task 5).
            Verified: TrustIntegrity.IsVerified(agent),
            StepCount: agent.StepCount,
            RetryCount: agent.RetryCount,
            Trace: agent.Trace
                .Select(s => new AgentTraceStepDto(s.ExpertName, s.Status, s.Text, s.Reason, s.Attempt))
                .ToList());
    }
}
