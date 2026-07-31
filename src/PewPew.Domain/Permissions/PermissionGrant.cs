using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Permissions;

public enum PermissionGrantStatus
{
    Requested,
    PendingApproval,
    Active,
    RevalidationRequired,
    Suspended,
    Expired,
    Revoked,
    Denied
}

public sealed class PermissionGrant
{
    public PermissionGrant(EntityId id, PermissionScope scope, DateTimeOffset expiresAtUtc)
    {
        Id = id;
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        ExpiresAtUtc = expiresAtUtc;
    }

    public EntityId Id { get; }

    public PermissionScope Scope { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public PermissionGrantStatus Status { get; private set; } = PermissionGrantStatus.Requested;

    public void Submit() => Move(PermissionGrantStatus.Requested, PermissionGrantStatus.PendingApproval);

    public void Approve() => Move(PermissionGrantStatus.PendingApproval, PermissionGrantStatus.Active);

    public void Deny() => Move(PermissionGrantStatus.PendingApproval, PermissionGrantStatus.Denied);

    public void RequireRevalidation() => Move(PermissionGrantStatus.Active, PermissionGrantStatus.RevalidationRequired);

    public void Reapprove() => Move(PermissionGrantStatus.RevalidationRequired, PermissionGrantStatus.Active);

    public void Suspend() => Move(PermissionGrantStatus.Active, PermissionGrantStatus.Suspended);

    public void Restore() => Move(PermissionGrantStatus.Suspended, PermissionGrantStatus.Active);

    public void Revoke()
    {
        if (Status is not (PermissionGrantStatus.Active or PermissionGrantStatus.RevalidationRequired or PermissionGrantStatus.Suspended))
        {
            throw new InvalidOperationException("Only a valid permission grant can be revoked.");
        }

        Status = PermissionGrantStatus.Revoked;
    }

    public void Expire(DateTimeOffset now)
    {
        if (now < ExpiresAtUtc)
        {
            throw new InvalidOperationException("Permission grant has not expired.");
        }

        if (Status is not (PermissionGrantStatus.Active or PermissionGrantStatus.RevalidationRequired or PermissionGrantStatus.Suspended))
        {
            throw new InvalidOperationException("Only a valid permission grant can expire.");
        }

        Status = PermissionGrantStatus.Expired;
    }

    public bool Allows(PermissionScope request, DateTimeOffset now) =>
        request is not null &&
        Status == PermissionGrantStatus.Active &&
        now < ExpiresAtUtc &&
        Scope == request;

    private void Move(PermissionGrantStatus expected, PermissionGrantStatus next)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException("Permission grant transition denied.");
        }

        Status = next;
    }
}
