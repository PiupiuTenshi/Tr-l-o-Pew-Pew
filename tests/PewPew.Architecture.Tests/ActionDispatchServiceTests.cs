using System.Reflection;
using PewPew.Application.Actions;
using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Audit;
using PewPew.Domain.Permissions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class ActionDispatchServiceTests
{
    [Fact]
    public void DeniesDispatchByDefaultAndSealsAnAuditRecord()
    {
        var scenario = NewScenario(permissionActive: false, planRequiresConfirmation: false);

        var result = ActionDispatchService.Dispatch(scenario.Request);

        Assert.False(result.IsAllowed);
        Assert.Equal("permission_denied", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Queued, scenario.Task.Status);
        Assert.Equal(AuditRecordStatus.Sealed, result.AuditRecord.Status);
        Assert.Equal("action_dispatch", result.AuditRecord.Action);
    }

    [Fact]
    public void ConsumesBoundConfirmationBeforeDispatchingAndAuditsApproval()
    {
        var scenario = NewScenario(permissionActive: true, planRequiresConfirmation: true);

        var result = ActionDispatchService.Dispatch(scenario.Request);

        Assert.True(result.IsAllowed);
        Assert.Equal("allowed", result.ReasonCode);
        Assert.Equal(ConfirmationStatus.Consumed, scenario.Confirmation!.Status);
        Assert.Equal(ActionTaskStatus.Running, scenario.Task.Status);
        Assert.Equal(AuditRecordStatus.Sealed, result.AuditRecord.Status);
    }

    [Fact]
    public void DeniesSafeModeWithoutConsumingConfirmation()
    {
        var scenario = NewScenario(permissionActive: true, planRequiresConfirmation: true);
        scenario.Profile.EnterSafeMode();

        var result = ActionDispatchService.Dispatch(scenario.Request);

        Assert.False(result.IsAllowed);
        Assert.Equal("assistant_not_active", result.ReasonCode);
        Assert.Equal(ConfirmationStatus.Requested, scenario.Confirmation!.Status);
        Assert.Equal(ActionTaskStatus.Queued, scenario.Task.Status);
    }

    [Fact]
    public void InvalidConfirmationBindingDoesNotDispatchOrConsumeTheRequest()
    {
        var scenario = NewScenario(permissionActive: true, planRequiresConfirmation: true, confirmationSessionId: EntityId.New());

        var result = ActionDispatchService.Dispatch(scenario.Request);

        Assert.False(result.IsAllowed);
        Assert.Equal("confirmation_invalid", result.ReasonCode);
        Assert.Equal(ConfirmationStatus.Requested, scenario.Confirmation!.Status);
        Assert.Equal(ActionTaskStatus.Queued, scenario.Task.Status);
    }

    [Fact]
    public void PermissionForAnotherTargetDoesNotDispatchThePlan()
    {
        var scenario = NewScenario(permissionActive: true, planRequiresConfirmation: false);
        var mismatchedScope = PermissionScope.Create(
            scenario.Request.UserId,
            scenario.Request.DeviceId,
            "fake",
            "another-resource",
            "write",
            false);
        var permission = new PermissionGrant(EntityId.New(), mismatchedScope, DateTimeOffset.UtcNow.AddMinutes(1));
        permission.Submit();
        permission.Approve();
        var request = scenario.Request with { PermissionGrant = permission, RequestedScope = mismatchedScope };

        var result = ActionDispatchService.Dispatch(request);

        Assert.False(result.IsAllowed);
        Assert.Equal("invalid_action_context", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Queued, scenario.Task.Status);
    }

    [Fact]
    public void InvalidCorrelationIsDeniedAndStillCreatesSealedAuditEvidence()
    {
        var scenario = NewScenario(permissionActive: true, planRequiresConfirmation: false);
        var request = scenario.Request with { CorrelationId = " " };

        var result = ActionDispatchService.Dispatch(request);

        Assert.False(result.IsAllowed);
        Assert.Equal("invalid_correlation", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Queued, scenario.Task.Status);
        Assert.Equal(AuditRecordStatus.Sealed, result.AuditRecord.Status);
        Assert.StartsWith("rejected-", result.AuditRecord.CorrelationId, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionTaskStartTransitionsAreNotPublicBypassPoints()
    {
        Assert.Null(typeof(ActionTask).GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(typeof(ActionTask).GetMethod("ConsumeConfirmation", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(typeof(ActionTask).GetMethod("RequireConfirmation", BindingFlags.Instance | BindingFlags.Public));
    }

    private static Scenario NewScenario(
        bool permissionActive,
        bool planRequiresConfirmation,
        EntityId? confirmationSessionId = null)
    {
        var userId = EntityId.New();
        var sessionId = EntityId.New();
        var deviceId = EntityId.New();
        var scope = PermissionScope.Create(userId, deviceId, "fake", "resource", "write", false);
        var permission = new PermissionGrant(EntityId.New(), scope, DateTimeOffset.UtcNow.AddMinutes(1));
        permission.Submit();
        if (permissionActive)
        {
            permission.Approve();
        }

        var profile = new AssistantProfile(EntityId.New(), userId);
        profile.CompleteProvisioning();
        var definition = StructuredActionPlan.Create(EntityId.New(), 1, "fake", "resource", "payload");
        var plan = new ActionPlan(definition, DateTimeOffset.UtcNow.AddMinutes(1));
        plan.SubmitForPolicyReview();
        ConfirmationRequest? confirmation = null;
        if (planRequiresConfirmation)
        {
            plan.RequireConfirmation();
            confirmation = new ConfirmationRequest(
                EntityId.New(),
                userId,
                confirmationSessionId ?? sessionId,
                deviceId,
                definition.Hash,
                DateTimeOffset.UtcNow.AddMinutes(1));
        }
        else
        {
            plan.ApproveByPolicy();
        }

        var task = new ActionTask(EntityId.New(), definition.Id, userId, DateTimeOffset.UtcNow.AddMinutes(1), true);
        var request = new ActionDispatchRequest(
            profile,
            plan,
            permission,
            scope,
            confirmation,
            task,
            userId,
            sessionId,
            deviceId,
            DateTimeOffset.UtcNow,
            "action-dispatch-correlation");
        return new Scenario(profile, task, confirmation, request);
    }

    private sealed record Scenario(
        AssistantProfile Profile,
        ActionTask Task,
        ConfirmationRequest? Confirmation,
        ActionDispatchRequest Request);
}
