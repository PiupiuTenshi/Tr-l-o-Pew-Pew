using PewPew.Application.Actions;
using PewPew.Application.UiAutomation;
using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Permissions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class WindowsUiAutomationControlPathTests
{
    [Fact]
    public void BindsProcessIdIntoTheScopedTargetIdentifier()
    {
        var target = new UiTargetScope("notepad", ProcessId: 4242);

        Assert.Equal("notepad:window:pid-4242", target.TargetId);
    }

    [Fact]
    public async Task DeniesBeforeUiAutomationWhenPermissionIsNotActive()
    {
        var scenario = CreateScenario(permissionActive: false);
        var channel = new FakeChannel(new(scenario.Target.TargetId, "invoked", null));

        var result = await WindowsUiAutomationControlPath.ExecuteAsync(scenario.Request, channel, null, CancellationToken.None);

        Assert.Equal(WindowsUiAutomationOutcome.Denied, result.Outcome);
        Assert.Equal("permission_denied", result.ReasonCode);
        Assert.Equal(0, channel.CallCount);
        Assert.Equal(ActionTaskStatus.Queued, scenario.Task.Status);
    }

    [Fact]
    public async Task ConsumesConfirmationAndCompletesOnlyWhenIndependentReadbackMatches()
    {
        var scenario = CreateScenario(permissionActive: true);
        var channel = new FakeChannel(new(scenario.Target.TargetId, "invoked", null));

        var result = await WindowsUiAutomationControlPath.ExecuteAsync(scenario.Request, channel, null, CancellationToken.None);

        Assert.Equal("verified", result.ReasonCode);
        Assert.Equal(WindowsUiAutomationOutcome.Verified, result.Outcome);
        Assert.Equal(1, channel.CallCount);
        Assert.Equal(ConfirmationStatus.Consumed, scenario.Request.Authorization.Confirmation!.Status);
        Assert.Equal(ActionTaskStatus.Completed, scenario.Task.Status);
    }

    [Fact]
    public async Task AmbiguousOrMismatchedReadbackFailsWithoutFabricatedSuccess()
    {
        var scenario = CreateScenario(permissionActive: true);
        var channel = new FakeChannel(new(scenario.Target.TargetId, "readback_mismatch", null));

        var result = await WindowsUiAutomationControlPath.ExecuteAsync(scenario.Request, channel, null, CancellationToken.None);

        Assert.Equal(WindowsUiAutomationOutcome.Failed, result.Outcome);
        Assert.Equal("uia_readback_mismatch", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Failed, scenario.Task.Status);
    }

    [Fact]
    public async Task MissingReadbackAfterAuthorizedActionIsUnknownAndNotRetried()
    {
        var scenario = CreateScenario(permissionActive: true);
        var channel = new FakeChannel(null);

        var result = await WindowsUiAutomationControlPath.ExecuteAsync(scenario.Request, channel, null, CancellationToken.None);

        Assert.Equal(WindowsUiAutomationOutcome.Unknown, result.Outcome);
        Assert.Equal("uia_readback_lost_after_action", result.ReasonCode);
        Assert.Equal(1, channel.CallCount);
        Assert.Equal(ActionTaskStatus.Unknown, scenario.Task.Status);
    }

    [Fact]
    public async Task UiAutomationTargetResolutionFailureMarksTaskFailed()
    {
        var scenario = CreateScenario(permissionActive: true);

        var result = await WindowsUiAutomationControlPath.ExecuteAsync(scenario.Request, new FailingChannel(), null, CancellationToken.None);

        Assert.Equal(WindowsUiAutomationOutcome.Failed, result.Outcome);
        Assert.Equal("uia_window_ambiguous", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Failed, scenario.Task.Status);
    }

    [Fact]
    public async Task CancellationCancelsActiveTaskAndDoesNotReportVerified()
    {
        var scenario = CreateScenario(permissionActive: true);
        var channel = new CancellingChannel();

        var result = await WindowsUiAutomationControlPath.ExecuteAsync(scenario.Request, channel, null, CancellationToken.None);

        Assert.Equal(WindowsUiAutomationOutcome.Denied, result.Outcome);
        Assert.Equal("cancelled", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Cancelled, scenario.Task.Status);
    }

    private static Scenario CreateScenario(bool permissionActive)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = EntityId.New();
        var sessionId = EntityId.New();
        var deviceId = EntityId.New();
        var target = new UiTargetScope("notepad", "Untitled*", "btnSave");
        var scope = PermissionScope.Create(userId, deviceId, "windows.uia", target.TargetId, UiActionKind.Click.ToString(), false);
        var grant = new PermissionGrant(EntityId.New(), scope, now.AddMinutes(1));
        grant.Submit();
        if (permissionActive)
        {
            grant.Approve();
        }

        var profile = new AssistantProfile(EntityId.New(), userId);
        profile.CompleteProvisioning();
        var definition = StructuredActionPlan.Create(EntityId.New(), 1, "windows.uia", target.TargetId, UiActionKind.Click.ToString());
        var plan = new ActionPlan(definition, now.AddMinutes(1));
        plan.SubmitForPolicyReview();
        plan.RequireConfirmation();
        var confirmation = new ConfirmationRequest(EntityId.New(), userId, sessionId, deviceId, definition.Hash, now.AddMinutes(1));
        var task = new ActionTask(EntityId.New(), definition.Id, userId, now.AddMinutes(1), true);
        const string correlationId = "uia-control-correlation";
        var authorization = new ActionDispatchRequest(profile, plan, grant, scope, confirmation, task, userId, sessionId, deviceId, now, correlationId);
        var command = new VerifiedWindowsUiAutomationCommand(
            "uia-command", target, UiActionKind.Click, definition.Hash,
            null, "invoked", correlationId);
        return new(new(authorization, command), task, target);
    }

    private sealed record Scenario(WindowsUiAutomationControlRequest Request, ActionTask Task, UiTargetScope Target);

    private sealed class FakeChannel(WindowsUiAutomationReadback? readback) : IVerifiedWindowsUiAutomationChannel
    {
        public int CallCount { get; private set; }
        public Task<WindowsUiAutomationReadback?> ExecuteAndReadbackAsync(UiTargetScope target, UiActionKind action, string? valuePayload, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(readback);
        }
    }

    private sealed class CancellingChannel : IVerifiedWindowsUiAutomationChannel
    {
        public Task<WindowsUiAutomationReadback?> ExecuteAndReadbackAsync(UiTargetScope target, UiActionKind action, string? valuePayload, CancellationToken cancellationToken) =>
            Task.FromCanceled<WindowsUiAutomationReadback?>(new CancellationToken(canceled: true));
    }

    private sealed class FailingChannel : IVerifiedWindowsUiAutomationChannel
    {
        public Task<WindowsUiAutomationReadback?> ExecuteAndReadbackAsync(UiTargetScope target, UiActionKind action, string? valuePayload, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("uia_window_ambiguous");
    }
}
