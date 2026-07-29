using PewPew.Application.Actions;
using PewPew.Application.DeviceSkills;
using PewPew.Application.IntentRouting;
using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Audit;
using PewPew.Domain.Permissions;
using PewPew.SharedKernel.Primitives;
using System.Reflection;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class LocalDeviceSkillServiceTests
{
    [Fact]
    public void CommandFactoryOnlyAcceptsTheExplicitDeviceAllowlist()
    {
        var allowed = new LocalIntentProposal(
            1,
            LocalIntentName.OpenApplication,
            new Dictionary<string, string> { ["applicationId"] = "calculator" });
        var rejected = new LocalIntentProposal(
            1,
            LocalIntentName.OpenApplication,
            new Dictionary<string, string> { ["applicationId"] = "powershell" });

        Assert.True(LocalDeviceSkillCommand.TryCreate(allowed, out var command));
        Assert.Equal("open_application", command!.Skill);
        Assert.Equal("calculator", command.Resource);
        Assert.False(LocalDeviceSkillCommand.TryCreate(rejected, out _));
    }

    [Fact]
    public void AdapterExecutionLeaseCannotBeConstructedByPresentationCode()
    {
        Assert.Empty(typeof(AuthorizedLocalDeviceSkillCommand).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public async Task MissingPermissionDeniesBeforeCallingWindowsAdapterAndSealsAudit()
    {
        var scenario = NewScenario(permissionActive: false);
        var adapter = new RecordingAdapter();
        var service = new LocalDeviceSkillService(adapter);

        var result = await service.ExecuteAsync(scenario.Request, scenario.Command, CancellationToken.None);

        Assert.Equal("permission_denied", result.ReasonCode);
        Assert.False(adapter.WasCalled);
        Assert.Equal(ActionTaskStatus.Queued, scenario.Task.Status);
        Assert.NotNull(result.Dispatch);
        Assert.Equal(AuditRecordStatus.Sealed, result.Dispatch!.AuditRecord.Status);
    }

    [Fact]
    public async Task ApprovedScopedCommandExecutesOnlyAfterDispatchAndRecordsVerification()
    {
        var scenario = NewScenario(permissionActive: true);
        var adapter = new RecordingAdapter(LocalDeviceSkillAdapterResult.Verified("process_started:calculator"));
        var service = new LocalDeviceSkillService(adapter);

        var result = await service.ExecuteAsync(scenario.Request, scenario.Command, CancellationToken.None);

        Assert.True(adapter.WasCalled);
        Assert.Equal(ActionTaskStatus.Completed, scenario.Task.Status);
        Assert.Equal("process_started:calculator", scenario.Task.VerificationEvidence);
        Assert.Equal("completed", result.ReasonCode);
        Assert.NotNull(result.Dispatch);
        Assert.True(result.Dispatch!.IsAllowed);
        Assert.Equal(AuditRecordStatus.Sealed, result.Dispatch.AuditRecord.Status);
    }

    [Fact]
    public async Task CancellationAfterApprovalPreventsAdapterSideEffectAndCancelsTask()
    {
        var scenario = NewScenario(permissionActive: true);
        var adapter = new RecordingAdapter();
        var service = new LocalDeviceSkillService(adapter);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await service.ExecuteAsync(scenario.Request, scenario.Command, cancellation.Token);

        Assert.False(adapter.WasCalled);
        Assert.Equal(ActionTaskStatus.Cancelled, scenario.Task.Status);
        Assert.Equal("cancelled", result.ReasonCode);
        Assert.NotNull(result.Dispatch);
        Assert.True(result.Dispatch!.IsAllowed);
        Assert.Equal(AuditRecordStatus.Sealed, result.Dispatch.AuditRecord.Status);
    }

    [Fact]
    public async Task AdapterFailureDoesNotReportCompletion()
    {
        var scenario = NewScenario(permissionActive: true);
        var adapter = new RecordingAdapter(LocalDeviceSkillAdapterResult.Failed("windows_input_not_accepted"));
        var service = new LocalDeviceSkillService(adapter);

        var result = await service.ExecuteAsync(scenario.Request, scenario.Command, CancellationToken.None);

        Assert.True(adapter.WasCalled);
        Assert.Equal(ActionTaskStatus.Failed, scenario.Task.Status);
        Assert.Equal("windows_input_not_accepted", scenario.Task.FailureReason);
        Assert.Equal("windows_input_not_accepted", result.ReasonCode);
    }

    private static Scenario NewScenario(bool permissionActive)
    {
        var userId = EntityId.New();
        var sessionId = EntityId.New();
        var deviceId = EntityId.New();
        var command = new LocalDeviceSkillCommand(LocalIntentName.OpenApplication, "calculator");
        var scope = PermissionScope.Create(userId, deviceId, command.Skill, command.Resource, "execute", false);
        var permission = new PermissionGrant(EntityId.New(), scope, DateTimeOffset.UtcNow.AddMinutes(1));
        permission.Submit();
        if (permissionActive)
        {
            permission.Approve();
        }

        var profile = new AssistantProfile(EntityId.New(), userId);
        profile.CompleteProvisioning();
        var definition = StructuredActionPlan.Create(EntityId.New(), 1, command.Skill, command.Resource, "");
        var plan = new ActionPlan(definition, DateTimeOffset.UtcNow.AddMinutes(1));
        plan.SubmitForPolicyReview();
        plan.ApproveByPolicy();
        var task = new ActionTask(EntityId.New(), definition.Id, userId, DateTimeOffset.UtcNow.AddMinutes(1), true);

        return new Scenario(
            command,
            task,
            new ActionDispatchRequest(
                profile,
                plan,
                permission,
                scope,
                null,
                task,
                userId,
                sessionId,
                deviceId,
                DateTimeOffset.UtcNow,
                "local-device-skill-correlation"));
    }

    private sealed record Scenario(LocalDeviceSkillCommand Command, ActionTask Task, ActionDispatchRequest Request);

    private sealed class RecordingAdapter(LocalDeviceSkillAdapterResult? result = null) : ILocalDeviceSkillAdapter
    {
        public bool WasCalled { get; private set; }

        public Task<LocalDeviceSkillAdapterResult> ExecuteAsync(
            AuthorizedLocalDeviceSkillCommand command,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(result ?? LocalDeviceSkillAdapterResult.Verified("adapter_verified"));
        }
    }
}
