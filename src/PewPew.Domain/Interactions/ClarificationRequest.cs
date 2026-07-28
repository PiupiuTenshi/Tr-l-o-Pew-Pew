using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Interactions;

public enum ClarificationStatus { Created, Presented, Answered, Unresolved, Expired, Cancelled }

public sealed class ClarificationRequest
{
    public ClarificationRequest(EntityId id, string field, DateTimeOffset expiresAtUtc) { if (string.IsNullOrWhiteSpace(field)) { throw new ArgumentException("Field is required.", nameof(field)); } Id = id; Field = field; ExpiresAtUtc = expiresAtUtc; }
    public EntityId Id { get; }
    public string Field { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public ClarificationStatus Status { get; private set; } = ClarificationStatus.Created;
    public void Present() => Move(ClarificationStatus.Created, ClarificationStatus.Presented);
    public void AcceptAnswer() => Move(ClarificationStatus.Presented, ClarificationStatus.Answered);
    public void MarkUnresolved() => Move(ClarificationStatus.Presented, ClarificationStatus.Unresolved);
    public void PresentFollowUp() => Move(ClarificationStatus.Unresolved, ClarificationStatus.Presented);
    public void Cancel() { EnsureOpen(); Status = ClarificationStatus.Cancelled; }
    public void Expire(DateTimeOffset now) { if (now < ExpiresAtUtc) { throw new InvalidOperationException("Clarification has not expired."); } EnsureOpen(); Status = ClarificationStatus.Expired; }
    private void Move(ClarificationStatus expected, ClarificationStatus next) { if (Status != expected) { throw new InvalidOperationException("Clarification transition denied."); } Status = next; }
    private void EnsureOpen() { if (Status is ClarificationStatus.Answered or ClarificationStatus.Expired or ClarificationStatus.Cancelled) { throw new InvalidOperationException("Clarification is terminal."); } }
}
