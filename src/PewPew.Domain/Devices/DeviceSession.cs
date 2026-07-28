using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Devices;

public enum DeviceSessionStatus { Created, Active, Idle, ReauthRequired, Expired, Revoked, Terminated }

public sealed class DeviceSession
{
    public DeviceSession(EntityId id, EntityId deviceId, DateTimeOffset expiresAtUtc) { Id = id; DeviceId = deviceId; ExpiresAtUtc = expiresAtUtc; }
    public EntityId Id { get; }
    public EntityId DeviceId { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public DeviceSessionStatus Status { get; private set; } = DeviceSessionStatus.Created;
    public void CompleteHandshake() => Transition(DeviceSessionStatus.Active, DeviceSessionStatus.Created);
    public void MarkIdle() => Transition(DeviceSessionStatus.Idle, DeviceSessionStatus.Active);
    public void Resume() => Transition(DeviceSessionStatus.Active, DeviceSessionStatus.Idle);
    public void RequireReauth() { if (Status is not (DeviceSessionStatus.Active or DeviceSessionStatus.Idle)) { throw new InvalidOperationException("Session reauthentication denied."); } Status = DeviceSessionStatus.ReauthRequired; }
    public void Reauthenticate() => Transition(DeviceSessionStatus.Active, DeviceSessionStatus.ReauthRequired);
    public void Expire(DateTimeOffset now) { if (now < ExpiresAtUtc) { throw new InvalidOperationException("Session has not expired."); } if (Status is not (DeviceSessionStatus.Active or DeviceSessionStatus.Idle or DeviceSessionStatus.ReauthRequired)) { throw new InvalidOperationException("Session expiry denied."); } Status = DeviceSessionStatus.Expired; }
    public void Revoke() { if (Status is not (DeviceSessionStatus.Active or DeviceSessionStatus.Idle or DeviceSessionStatus.ReauthRequired)) { throw new InvalidOperationException("Session revoke denied."); } Status = DeviceSessionStatus.Revoked; }
    private void Transition(DeviceSessionStatus next, DeviceSessionStatus expected) { if (Status != expected) { throw new InvalidOperationException($"Session transition denied from {Status}."); } Status = next; }
}
