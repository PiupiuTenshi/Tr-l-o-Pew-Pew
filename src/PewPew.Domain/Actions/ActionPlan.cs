using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Actions;

public enum ActionPlanStatus
{
    Draft,
    PolicyReview,
    WaitingConfirmation,
    Approved,
    Rejected,
    Expired,
    Superseded
}

public sealed class ActionPlan
{
    public ActionPlan(StructuredActionPlan definition, DateTimeOffset expiresAtUtc)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        ExpiresAtUtc = expiresAtUtc;
    }

    public StructuredActionPlan Definition { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public ActionPlanStatus Status { get; private set; } = ActionPlanStatus.Draft;

    public string? RejectionReason { get; private set; }

    public void SubmitForPolicyReview() => Move(ActionPlanStatus.Draft, ActionPlanStatus.PolicyReview);

    public void ApproveByPolicy() => Move(ActionPlanStatus.PolicyReview, ActionPlanStatus.Approved);

    public void RejectByPolicy(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A policy rejection reason is required.", nameof(reason));
        }

        Move(ActionPlanStatus.PolicyReview, ActionPlanStatus.Rejected);
        RejectionReason = reason;
    }

    public void RequireConfirmation() => Move(ActionPlanStatus.PolicyReview, ActionPlanStatus.WaitingConfirmation);

    public void Supersede()
    {
        if (Status is not (ActionPlanStatus.Draft or ActionPlanStatus.PolicyReview or ActionPlanStatus.WaitingConfirmation))
        {
            throw new InvalidOperationException("Only an unapproved plan can be superseded.");
        }

        Status = ActionPlanStatus.Superseded;
    }

    public void Expire(DateTimeOffset now)
    {
        if (now < ExpiresAtUtc)
        {
            throw new InvalidOperationException("Action plan has not expired.");
        }

        Move(ActionPlanStatus.WaitingConfirmation, ActionPlanStatus.Expired);
    }

    private void Move(ActionPlanStatus expected, ActionPlanStatus next)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException("Action plan transition denied.");
        }

        Status = next;
    }
}
