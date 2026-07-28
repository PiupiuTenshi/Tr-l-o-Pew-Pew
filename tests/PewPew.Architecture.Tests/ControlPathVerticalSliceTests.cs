using PewPew.Application.Actions;
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
        var scenario = NewScenario(permissionActive: false, requiresConfirmation: false);

        var result = ActionDispatchService.Dispatch(scenario.Request);

        Assert.False(result.IsAllowed);
        Assert.Equal("permission_denied", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Queued, scenario.Task.Status);
        Assert.Equal(AuditRecordStatus.Sealed, result.AuditRecord.Status);
    }

    [Fact]
    public void ConfirmedVerifiedPathCompletesAndCreatesAuditEvidence()
    {
        var scenario = NewScenario(permissionActive: true, requiresConfirmation: true);

        var dispatch = ActionDispatchService.Dispatch(scenario.Request);
        scenario.Task.Complete("fake adapter verified");

        Assert.True(dispatch.IsAllowed);
        Assert.Equal(ConfirmationStatus.Consumed, scenario.Confirmation!.Status);
        Assert.Equal(ActionTaskStatus.Completed, scenario.Task.Status);
        Assert.Equal(AuditRecordStatus.Sealed, dispatch.AuditRecord.Status);
    }

    private static Scenario NewScenario(bool permissionActive, bool requiresConfirmation)
    {
        var user = EntityId.New();
        var session = EntityId.New();
        var device = EntityId.New();
        var scope = PermissionScope.Create(user, device, "fake", "resource", "write", false);
        var permission = new PermissionGrant(EntityId.New(), scope, DateTimeOffset.UtcNow.AddMinutes(1));
        permission.Submit();
        if (permissionActive)
        {
            permission.Approve();
        }

        var profile = new PewPew.Domain.Assistant.AssistantProfile(EntityId.New(), user);
        profile.CompleteProvisioning();
        var definition = StructuredActionPlan.Create(EntityId.New(), 1, "fake", "resource", "payload");
        var plan = new ActionPlan(definition, DateTimeOffset.UtcNow.AddMinutes(1));
        plan.SubmitForPolicyReview();
        ConfirmationRequest? confirmation = null;
        if (requiresConfirmation)
        {
            plan.RequireConfirmation();
            confirmation = new ConfirmationRequest(EntityId.New(), user, session, device, definition.Hash, DateTimeOffset.UtcNow.AddMinutes(1));
        }
        else
        {
            plan.ApproveByPolicy();
        }

        var task = new ActionTask(EntityId.New(), definition.Id, user, DateTimeOffset.UtcNow.AddMinutes(1), true);
        return new Scenario(
            task,
            confirmation,
            new ActionDispatchRequest(profile, plan, permission, scope, confirmation, task, user, session, device, DateTimeOffset.UtcNow, "corr"));
    }

    private sealed record Scenario(ActionTask Task, ConfirmationRequest? Confirmation, ActionDispatchRequest Request);
}
