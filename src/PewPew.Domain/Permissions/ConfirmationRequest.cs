using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Permissions;

public enum ConfirmationStatus { Requested, Consumed, Denied, Expired, Cancelled, Invalidated }

public sealed class ConfirmationRequest
{
    public ConfirmationRequest(EntityId id, EntityId userId, EntityId sessionId, EntityId deviceId, string planHash, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(planHash))
        {
            throw new ArgumentException("Plan hash is required.", nameof(planHash));
        }
        Id = id; UserId = userId; SessionId = sessionId; DeviceId = deviceId; PlanHash = planHash; ExpiresAtUtc = expiresAtUtc;
    }
    public EntityId Id { get; }
    public EntityId UserId { get; }
    public EntityId SessionId { get; }
    public EntityId DeviceId { get; }
    public string PlanHash { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public ConfirmationStatus Status { get; private set; } = ConfirmationStatus.Requested;
    public void Consume(EntityId userId, EntityId sessionId, EntityId deviceId, string planHash, DateTimeOffset now)
    {
        if (now >= ExpiresAtUtc)
        {
            Expire(now);
            throw new InvalidOperationException("Confirmation has expired.");
        }
        if (Status != ConfirmationStatus.Requested || UserId != userId || SessionId != sessionId || DeviceId != deviceId || PlanHash != planHash)
        {
            throw new InvalidOperationException("Confirmation binding is invalid.");
        }
        Status = ConfirmationStatus.Consumed;
    }
    public void Invalidate() => Move(ConfirmationStatus.Invalidated);
    public void Cancel() => Move(ConfirmationStatus.Cancelled);
    public void Expire(DateTimeOffset now) { if (now < ExpiresAtUtc) { throw new InvalidOperationException("Confirmation has not expired."); } Move(ConfirmationStatus.Expired); }
    private void Move(ConfirmationStatus next) { if (Status != ConfirmationStatus.Requested) { throw new InvalidOperationException("Confirmation is terminal."); } Status = next; }
}
