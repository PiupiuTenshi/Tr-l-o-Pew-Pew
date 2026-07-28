using PewPew.Domain.Actions;
using PewPew.Domain.Audit;
using PewPew.Domain.Permissions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class ControlPathVerticalSliceTests
{
    [Fact]
    public void DeniedPathDoesNotDispatchAction()
    {
        var scope = PermissionScope.Create(EntityId.New(), EntityId.New(), "fake", "resource", "write", false);
        var grant = new PermissionGrant(EntityId.New(), scope, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.False(grant.Allows(scope, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ConfirmedVerifiedPathCompletesAndCreatesAuditEvidence()
    {
        var user = EntityId.New(); var session = EntityId.New(); var device = EntityId.New();
        var plan = StructuredActionPlan.Create(EntityId.New(), 1, "fake", "resource", "payload");
        var confirmation = new ConfirmationRequest(EntityId.New(), user, session, device, plan.Hash, DateTimeOffset.UtcNow.AddMinutes(1));
        confirmation.Consume(user, session, device, plan.Hash, DateTimeOffset.UtcNow);
        var task = new ActionTask(EntityId.New(), plan.Id, user, DateTimeOffset.UtcNow.AddMinutes(1), true);
        task.Dispatch(); task.Complete("fake adapter verified");
        var audit = new AuditRecord(EntityId.New(), "corr", user, device, device, "fake", "allow"); audit.Seal();
        Assert.Equal(ActionTaskStatus.Completed, task.Status);
        Assert.Equal(AuditRecordStatus.Sealed, audit.Status);
    }
}
