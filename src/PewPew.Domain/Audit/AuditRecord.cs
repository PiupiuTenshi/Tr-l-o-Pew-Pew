using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Audit;

public enum AuditRecordStatus { Created, Sealed, Archived, Purged }

public sealed class AuditRecord
{
    public AuditRecord(EntityId id, string correlationId, EntityId actorId, EntityId sourceDeviceId, EntityId targetDeviceId, string action, string policyResult)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(policyResult)) { throw new ArgumentException("Audit metadata is required."); }
        Id=id; CorrelationId=correlationId; ActorId=actorId; SourceDeviceId=sourceDeviceId; TargetDeviceId=targetDeviceId; Action=action; PolicyResult=policyResult;
    }
    public EntityId Id { get; } public string CorrelationId { get; } public EntityId ActorId { get; } public EntityId SourceDeviceId { get; } public EntityId TargetDeviceId { get; } public string Action { get; } public string PolicyResult { get; }
    public AuditRecordStatus Status { get; private set; } = AuditRecordStatus.Created;
    public void Seal() => Move(AuditRecordStatus.Created, AuditRecordStatus.Sealed);
    public void Archive() => Move(AuditRecordStatus.Sealed, AuditRecordStatus.Archived);
    private void Move(AuditRecordStatus expected, AuditRecordStatus next) { if (Status != expected) { throw new InvalidOperationException("Audit record transition denied."); } Status=next; }
}
