using PewPew.Domain.Permissions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class ConfirmationRequestTests
{
    [Fact] public void MatchingBindingConsumesOnce() { var c = NewRequest(); c.Consume(c.UserId, c.SessionId, c.DeviceId, c.PlanHash, DateTimeOffset.UtcNow); Assert.Equal(ConfirmationStatus.Consumed, c.Status); Assert.Throws<InvalidOperationException>(() => c.Consume(c.UserId, c.SessionId, c.DeviceId, c.PlanHash, DateTimeOffset.UtcNow)); }
    [Fact] public void ChangedPlanCannotConsume() { var c = NewRequest(); Assert.Throws<InvalidOperationException>(() => c.Consume(c.UserId, c.SessionId, c.DeviceId, "changed", DateTimeOffset.UtcNow)); Assert.Equal(ConfirmationStatus.Requested, c.Status); }
    [Fact] public void ExpiredConfirmationCannotConsume() { var now = DateTimeOffset.UtcNow; var c = NewRequest(now); Assert.Throws<InvalidOperationException>(() => c.Consume(c.UserId, c.SessionId, c.DeviceId, c.PlanHash, now)); Assert.Equal(ConfirmationStatus.Expired, c.Status); }
    [Fact] public void InvalidatedConfirmationCannotConsume() { var c = NewRequest(); c.Invalidate(); Assert.Throws<InvalidOperationException>(() => c.Consume(c.UserId, c.SessionId, c.DeviceId, c.PlanHash, DateTimeOffset.UtcNow)); }
    [Fact] public void DeviceMismatchCannotConsume() { var c = NewRequest(); Assert.Throws<InvalidOperationException>(() => c.Consume(c.UserId, c.SessionId, EntityId.New(), c.PlanHash, DateTimeOffset.UtcNow)); }
    private static ConfirmationRequest NewRequest(DateTimeOffset? expiry = null) => new(EntityId.New(), EntityId.New(), EntityId.New(), EntityId.New(), "hash", expiry ?? DateTimeOffset.UtcNow.AddMinutes(1));
}
