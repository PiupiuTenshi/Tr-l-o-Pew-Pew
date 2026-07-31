using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Actions;

public enum ActionTaskStatus
{
    Queued,
    WaitingConfirmation,
    Running,
    Completed,
    Failed,
    Cancelled,
    Unknown
}

public sealed class ActionTask
{
    public ActionTask(
        EntityId id,
        EntityId planId,
        EntityId ownerId,
        DateTimeOffset timeoutAtUtc,
        bool supportsCancellation)
    {
        Id = id;
        PlanId = planId;
        OwnerId = ownerId;
        TimeoutAtUtc = timeoutAtUtc;
        SupportsCancellation = supportsCancellation;
    }

    public EntityId Id { get; }

    public EntityId PlanId { get; }

    public EntityId OwnerId { get; }

    public DateTimeOffset TimeoutAtUtc { get; }

    public bool SupportsCancellation { get; }

    public ActionTaskStatus Status { get; private set; } = ActionTaskStatus.Queued;

    public string? VerificationEvidence { get; private set; }

    public string? FailureReason { get; private set; }

    internal void RequireConfirmation() => Move(ActionTaskStatus.Queued, ActionTaskStatus.WaitingConfirmation);

    internal void Dispatch() => Move(ActionTaskStatus.Queued, ActionTaskStatus.Running);

    internal void ConsumeConfirmation() => Move(ActionTaskStatus.WaitingConfirmation, ActionTaskStatus.Running);

    public void Complete(string verificationEvidence)
    {
        if (string.IsNullOrWhiteSpace(verificationEvidence))
        {
            throw new ArgumentException("Verification evidence is required.", nameof(verificationEvidence));
        }

        Move(ActionTaskStatus.Running, ActionTaskStatus.Completed);
        VerificationEvidence = verificationEvidence;
    }

    public void Fail(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(reason));
        }

        if (Status is not (ActionTaskStatus.Queued or ActionTaskStatus.WaitingConfirmation or ActionTaskStatus.Running))
        {
            throw new InvalidOperationException("Only an active action task can fail.");
        }

        Status = ActionTaskStatus.Failed;
        FailureReason = reason;
    }

    public void Cancel()
    {
        if (Status is ActionTaskStatus.Queued or ActionTaskStatus.WaitingConfirmation)
        {
            Status = ActionTaskStatus.Cancelled;
            return;
        }

        if (Status == ActionTaskStatus.Running && SupportsCancellation)
        {
            Status = ActionTaskStatus.Cancelled;
            return;
        }

        throw new InvalidOperationException("Action task cannot be cancelled in its current state.");
    }

    public void MarkOutcomeUnknown() => Move(ActionTaskStatus.Running, ActionTaskStatus.Unknown);

    public void ReconcileCompleted(string verificationEvidence)
    {
        if (string.IsNullOrWhiteSpace(verificationEvidence))
        {
            throw new ArgumentException("Verification evidence is required.", nameof(verificationEvidence));
        }

        Move(ActionTaskStatus.Unknown, ActionTaskStatus.Completed);
        VerificationEvidence = verificationEvidence;
    }

    public void ReconcileFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(reason));
        }

        Move(ActionTaskStatus.Unknown, ActionTaskStatus.Failed);
        FailureReason = reason;
    }

    private void Move(ActionTaskStatus expected, ActionTaskStatus next)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException("Action task transition denied.");
        }

        Status = next;
    }
}
