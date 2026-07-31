using PewPew.Domain.Interactions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class InteractionLifecycleTests
{
    [Fact]
    public void ClarificationAnswerReturnsSessionToUnderstanding()
    {
        var session = NewSession();
        session.BeginInput(); session.CaptureInput(); session.RequireClarification(); session.AnswerClarification();
        Assert.Equal(InteractionStatus.Understanding, session.Status);
    }

    [Fact]
    public void AmbiguousAnswerRequiresFollowUpInsteadOfSelectingTarget()
    {
        var request = NewRequest();
        request.Present(); request.MarkUnresolved(); request.PresentFollowUp();
        Assert.Equal(ClarificationStatus.Presented, request.Status);
    }

    [Fact]
    public void CancellationIsTerminal()
    {
        var request = NewRequest();
        request.Cancel();
        Assert.Throws<InvalidOperationException>(request.Present);
    }

    [Fact]
    public void ExpiryRejectsFurtherUse()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new InteractionSession(EntityId.New(), now);
        session.Expire(now);
        Assert.Throws<InvalidOperationException>(session.BeginInput);
    }

    private static InteractionSession NewSession() => new(EntityId.New(), DateTimeOffset.UtcNow.AddMinutes(1));
    private static ClarificationRequest NewRequest() => new(EntityId.New(), "target", DateTimeOffset.UtcNow.AddMinutes(1));
}
