using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Audit;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class EmergencyStopControllerTests
{
    [Fact]
    public void EmergencyStopBlocksNewActionsCancelsCancellableTasksAndSealsAudit()
    {
        var profile = ActiveProfile();
        var queued = NewTask();
        var running = NewTask();
        running.Dispatch();
        var result = EmergencyStopController.Activate(profile, [queued, running], NewRequest());

        Assert.Equal(AssistantProfileStatus.SafeMode, profile.Status);
        Assert.Equal(ActionTaskStatus.Cancelled, queued.Status);
        Assert.Equal(ActionTaskStatus.Cancelled, running.Status);
        Assert.Equal([queued.Id, running.Id], result.CancelledTaskIds);
        Assert.Empty(result.ExternalStopRequiredTaskIds);
        Assert.Equal(AuditRecordStatus.Sealed, result.AuditRecord.Status);
        Assert.Equal("emergency_stop", result.AuditRecord.Action);
        Assert.Equal("safe_mode_activated", result.AuditRecord.PolicyResult);
        Assert.Throws<InvalidOperationException>(() => EmergencyStopController.Dispatch(profile, NewTask()));
    }

    [Fact]
    public void EmergencyStopMarksNonCancellableRunningTaskForExternalStopWithoutAutoResume()
    {
        var profile = ActiveProfile();
        var running = NewTask(supportsCancellation: false);
        running.Dispatch();
        var result = EmergencyStopController.Activate(profile, [running], NewRequest());

        Assert.Equal(ActionTaskStatus.Running, running.Status);
        Assert.Equal([running.Id], result.ExternalStopRequiredTaskIds);
        Assert.Throws<InvalidOperationException>(() => EmergencyStopController.Dispatch(profile, NewTask()));

        profile.RecoverToPaused();

        Assert.Equal(AssistantProfileStatus.Paused, profile.Status);
        Assert.Throws<InvalidOperationException>(() => EmergencyStopController.Dispatch(profile, NewTask()));
        Assert.Equal(ActionTaskStatus.Running, running.Status);
    }

    [Fact]
    public void RepeatedEmergencyStopIsIdempotentForAlreadyCancelledTasks()
    {
        var profile = ActiveProfile();
        var queued = NewTask();
        EmergencyStopController.Activate(profile, [queued], NewRequest());
        var retry = EmergencyStopController.Activate(profile, [queued], NewRequest());

        Assert.Equal(ActionTaskStatus.Cancelled, queued.Status);
        Assert.Empty(retry.CancelledTaskIds);
        Assert.Equal(AuditRecordStatus.Sealed, retry.AuditRecord.Status);
    }

    private static AssistantProfile ActiveProfile()
    {
        var profile = new AssistantProfile(EntityId.New(), EntityId.New());
        profile.CompleteProvisioning();
        return profile;
    }

    private static ActionTask NewTask(bool supportsCancellation = true) => new(
        EntityId.New(),
        EntityId.New(),
        EntityId.New(),
        DateTimeOffset.UtcNow.AddMinutes(1),
        supportsCancellation);

    private static EmergencyStopRequest NewRequest() => new(
        "emergency-stop-correlation",
        EntityId.New(),
        EntityId.New(),
        EntityId.New());
}
