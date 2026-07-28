using PewPew.Domain.Actions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class ActionPlanLifecycleTests
{
    [Fact]
    public void PolicyReviewCanApproveAPlan()
    {
        var plan = NewPlan();

        plan.SubmitForPolicyReview();
        plan.ApproveByPolicy();

        Assert.Equal(ActionPlanStatus.Approved, plan.Status);
    }

    [Fact]
    public void PolicyReviewCanRejectAPlanWithAnExplainableReason()
    {
        var plan = NewPlan();

        plan.SubmitForPolicyReview();
        plan.RejectByPolicy("Permission is not granted.");

        Assert.Equal(ActionPlanStatus.Rejected, plan.Status);
        Assert.Equal("Permission is not granted.", plan.RejectionReason);
    }

    [Fact]
    public void ApprovalAndRejectionCannotBypassPolicyReview()
    {
        var plan = NewPlan();

        Assert.Throws<InvalidOperationException>(plan.ApproveByPolicy);
        Assert.Throws<InvalidOperationException>(() => plan.RejectByPolicy("Denied."));
        Assert.Equal(ActionPlanStatus.Draft, plan.Status);
    }

    [Fact]
    public void ChangedPlanCanBeSupersededBeforeApproval()
    {
        var plan = NewPlan();

        plan.SubmitForPolicyReview();
        plan.RequireConfirmation();
        plan.Supersede();

        Assert.Equal(ActionPlanStatus.Superseded, plan.Status);
        Assert.Throws<InvalidOperationException>(plan.ApproveByPolicy);
    }

    [Fact]
    public void WaitingConfirmationPlanExpiresOnlyAtItsTtl()
    {
        var expiresAt = DateTimeOffset.UtcNow;
        var plan = NewPlan(expiresAt);
        plan.SubmitForPolicyReview();
        plan.RequireConfirmation();

        Assert.Throws<InvalidOperationException>(() => plan.Expire(expiresAt.AddTicks(-1)));
        plan.Expire(expiresAt);

        Assert.Equal(ActionPlanStatus.Expired, plan.Status);
        Assert.Throws<InvalidOperationException>(plan.ApproveByPolicy);
    }

    private static ActionPlan NewPlan(DateTimeOffset? expiresAtUtc = null) => new(
        StructuredActionPlan.Create(EntityId.New(), 1, "send_message", "contact:123", "hello"),
        expiresAtUtc ?? DateTimeOffset.UtcNow.AddMinutes(1));
}
