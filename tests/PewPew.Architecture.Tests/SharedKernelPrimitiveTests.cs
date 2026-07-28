using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class SharedKernelPrimitiveTests
{
    [Fact]
    public void EntityIdRejectsEmptyGuidAndCreatesUsableIds()
    {
        Assert.Throws<ArgumentException>(() => new EntityId(Guid.Empty));
        Assert.NotEqual(Guid.Empty, EntityId.New().Value);
    }

    [Fact]
    public void ResultKeepsSuccessAndFailureInvariants()
    {
        var success = Result.Success("value");
        var failure = Result.Failure<string>(new DomainError("invalid", "Invalid input."));

        Assert.True(success.IsSuccess);
        Assert.Equal("value", success.Value);
        Assert.True(failure.IsFailure);
        Assert.Equal("invalid", failure.Error.Code);
        Assert.Throws<InvalidOperationException>(() => _ = failure.Value);
    }

    [Fact]
    public void DomainEventExposesOccurrenceTime()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var domainEvent = new TestDomainEvent(occurredAt);

        Assert.Equal(occurredAt, domainEvent.OccurredAtUtc);
    }

    private sealed record TestDomainEvent(DateTimeOffset OccurredAtUtc) : DomainEvent(OccurredAtUtc);
}
