using PewPew.Domain.Actions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class ActionTaskLifecycleTests
{
    [Fact]
    public void RunningTaskCompletesOnlyWithVerificationEvidence()
    {
        var task = NewTask();
        task.Dispatch();

        Assert.Throws<ArgumentException>(() => task.Complete(string.Empty));
        task.Complete("provider receipt: abc");

        Assert.Equal(ActionTaskStatus.Completed, task.Status);
        Assert.Equal("provider receipt: abc", task.VerificationEvidence);
    }

    [Fact]
    public void ActiveTaskFailsWithAnExplainableReason()
    {
        var task = NewTask();
        task.RequireConfirmation();

        task.Fail("Confirmation expired.");

        Assert.Equal(ActionTaskStatus.Failed, task.Status);
        Assert.Equal("Confirmation expired.", task.FailureReason);
    }

    [Fact]
    public void CancellableRunningTaskCanBeCancelledButCannotResume()
    {
        var task = NewTask(supportsCancellation: true);
        task.Dispatch();
        task.Cancel();

        Assert.Equal(ActionTaskStatus.Cancelled, task.Status);
        Assert.Throws<InvalidOperationException>(task.Dispatch);
    }

    [Fact]
    public void NonCancellableRunningTaskRejectsCancellation()
    {
        var task = NewTask(supportsCancellation: false);
        task.Dispatch();

        Assert.Throws<InvalidOperationException>(task.Cancel);
        Assert.Equal(ActionTaskStatus.Running, task.Status);
    }

    [Fact]
    public void UnknownOutcomeCannotRetryAndRequiresAuthoritativeReconciliation()
    {
        var task = NewTask();
        task.Dispatch();
        task.MarkOutcomeUnknown();

        Assert.Throws<InvalidOperationException>(task.Dispatch);
        Assert.Throws<InvalidOperationException>(() => task.Complete("late receipt"));
        task.ReconcileCompleted("authoritative readback");

        Assert.Equal(ActionTaskStatus.Completed, task.Status);
        Assert.Equal("authoritative readback", task.VerificationEvidence);
    }

    [Fact]
    public void UnknownOutcomeCanBeReconciledAsFailed()
    {
        var task = NewTask();
        task.Dispatch();
        task.MarkOutcomeUnknown();

        task.ReconcileFailed("Authoritative provider status is failed.");

        Assert.Equal(ActionTaskStatus.Failed, task.Status);
        Assert.Equal("Authoritative provider status is failed.", task.FailureReason);
    }

    private static ActionTask NewTask(bool supportsCancellation = true) => new(
        EntityId.New(),
        EntityId.New(),
        EntityId.New(),
        DateTimeOffset.UtcNow.AddMinutes(1),
        supportsCancellation);
}
