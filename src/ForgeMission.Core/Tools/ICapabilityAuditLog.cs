namespace ForgeMission.Core.Tools;

public interface ICapabilityAuditLog
{
    IReadOnlyList<CapabilityAuditRecord> Records { get; }

    void Record(CapabilityAuditRecord record);
}
