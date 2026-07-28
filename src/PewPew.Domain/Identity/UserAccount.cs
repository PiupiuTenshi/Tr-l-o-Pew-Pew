using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Identity;

public enum UserAccountStatus { PendingActivation, Active, Locked, Suspended, DeletionPending, Deleted }

public sealed class UserAccount
{
    public UserAccount(EntityId id) => Id = id;

    public EntityId Id { get; }
    public UserAccountStatus Status { get; private set; } = UserAccountStatus.PendingActivation;

    public void Activate() => Transition(UserAccountStatus.PendingActivation, UserAccountStatus.Active);
    public void Lock() => Transition(UserAccountStatus.Active, UserAccountStatus.Locked);
    public void Unlock() => Transition(UserAccountStatus.Locked, UserAccountStatus.Active);
    public void Suspend() => Transition([UserAccountStatus.Active, UserAccountStatus.Locked], UserAccountStatus.Suspended);
    public void Restore() => Transition(UserAccountStatus.Suspended, UserAccountStatus.Active);
    public void RequestDeletion() => Transition([UserAccountStatus.Active, UserAccountStatus.Locked, UserAccountStatus.Suspended], UserAccountStatus.DeletionPending);
    public void CancelDeletion() => Transition(UserAccountStatus.DeletionPending, UserAccountStatus.Active);
    public void CompleteDeletion() => Transition(UserAccountStatus.DeletionPending, UserAccountStatus.Deleted);

    private void Transition(UserAccountStatus expected, UserAccountStatus next)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Cannot transition account from {Status} to {next}.");
        }
        Status = next;
    }

    private void Transition(UserAccountStatus[] expected, UserAccountStatus next)
    {
        if (!expected.Contains(Status))
        {
            throw new InvalidOperationException($"Cannot transition account from {Status} to {next}.");
        }
        Status = next;
    }
}
