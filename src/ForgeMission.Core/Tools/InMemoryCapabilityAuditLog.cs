namespace ForgeMission.Core.Tools;

// Queryable for the lifetime of the Client Runtime. Persistent audit storage is not part of v1.
public sealed class InMemoryCapabilityAuditLog : ICapabilityAuditLog
{
    private readonly object _gate = new();
    private readonly List<CapabilityAuditRecord> _records = [];

    public IReadOnlyList<CapabilityAuditRecord> Records
    {
        get
        {
            lock (_gate)
                return _records.ToList();
        }
    }

    public void Record(CapabilityAuditRecord record)
    {
        lock (_gate)
            _records.Add(record);
    }
}
