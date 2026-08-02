using PewPew.Application.Actions;
using PewPew.Application.Automation;
using PewPew.Application.BrowserExtension;
using PewPew.Application.EmergencyStop;
using PewPew.Application.UiAutomation;
using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Audit;
using PewPew.Domain.Permissions;
using PewPew.Domain.Workers;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>Security journeys shared by the browser and Windows UIA adapters. All channels are deterministic fakes.</summary>
public sealed class CrossAdapterSecurityJourneyTests
{
    [Fact]
    public async Task ExpiredConfirmationDeniesBothAdaptersBeforeIoAndSealsMetadataOnlyAudit()
    {
        var now = DateTimeOffset.UtcNow;
        var browser = CreateBrowser(now, now.AddSeconds(-1));
        var uia = CreateUia(now, now.AddSeconds(-1));
        var browserChannel = new BrowserChannel("observed_paused");
        var uiaChannel = new UiaChannel(new(uia.Target.TargetId, "focused", null));

        var browserResult = await BrowserActionControlPath.ExecuteAsync(browser.Request, browserChannel, CancellationToken.None);
        var uiaResult = await WindowsUiAutomationControlPath.ExecuteAsync(uia.Request, uiaChannel, null, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, browserResult.Outcome);
        Assert.Equal(WindowsUiAutomationOutcome.Denied, uiaResult.Outcome);
        Assert.Equal("confirmation_invalid", browserResult.ReasonCode);
        Assert.Equal("confirmation_invalid", uiaResult.ReasonCode);
        Assert.Equal("confirmation_invalid", browserResult.AuditPolicyResult);
        Assert.Equal("confirmation_invalid", uiaResult.AuditPolicyResult);
        Assert.Equal(0, browserChannel.CallCount);
        Assert.Equal(0, uiaChannel.CallCount);
        Assert.Equal(ActionTaskStatus.Queued, browser.Task.Status);
        Assert.Equal(ActionTaskStatus.Queued, uia.Task.Status);
    }

    [Fact]
    public async Task ReplayIsDeniedBeforeSecondIoForBothSingleUseConfirmations()
    {
        var now = DateTimeOffset.UtcNow;
        var browser = CreateBrowser(now, now.AddMinutes(1));
        var uia = CreateUia(now, now.AddMinutes(1));
        var browserChannel = new BrowserChannel("observed_paused");
        var uiaChannel = new UiaChannel(new(uia.Target.TargetId, "focused", null));

        Assert.Equal(BrowserExecutorOutcome.Verified, (await BrowserActionControlPath.ExecuteAsync(browser.Request, browserChannel, CancellationToken.None)).Outcome);
        Assert.Equal(WindowsUiAutomationOutcome.Verified, (await WindowsUiAutomationControlPath.ExecuteAsync(uia.Request, uiaChannel, null, CancellationToken.None)).Outcome);

        var browserReplay = await BrowserActionControlPath.ExecuteAsync(browser.Request, browserChannel, CancellationToken.None);
        var uiaReplay = await WindowsUiAutomationControlPath.ExecuteAsync(uia.Request, uiaChannel, null, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, browserReplay.Outcome);
        Assert.Equal(WindowsUiAutomationOutcome.Denied, uiaReplay.Outcome);
        Assert.Equal("invalid_action_context", browserReplay.ReasonCode);
        Assert.Equal("invalid_action_context", uiaReplay.ReasonCode);
        Assert.Equal(1, browserChannel.CallCount);
        Assert.Equal(1, uiaChannel.CallCount);
    }

    [Fact]
    public async Task BindingChangeDeniesBeforeIoAndEmitsNoSensitivePayload()
    {
        const string sensitiveValue = "password=never-log-this";
        var now = DateTimeOffset.UtcNow;
        var browser = CreateBrowser(now, now.AddMinutes(1), commandAuthorizationId: "changed-correlation");
        var uia = CreateUia(now, now.AddMinutes(1));
        var browserChannel = new BrowserChannel("observed_paused");
        var uiaChannel = new UiaChannel(new(uia.Target.TargetId, "value_set", "not-a-match"));

        var browserResult = await BrowserActionControlPath.ExecuteAsync(browser.Request, browserChannel, CancellationToken.None);
        var uiaResult = await WindowsUiAutomationControlPath.ExecuteAsync(uia.Request, uiaChannel, sensitiveValue, CancellationToken.None);

        Assert.Equal("browser_command_authorization_binding_denied", browserResult.ReasonCode);
        Assert.Equal("uia_command_authorization_binding_denied", uiaResult.ReasonCode);
        Assert.Equal(browserResult.ReasonCode, browserResult.AuditPolicyResult);
        Assert.Equal(uiaResult.ReasonCode, uiaResult.AuditPolicyResult);
        Assert.DoesNotContain(sensitiveValue, browserResult.AuditPolicyResult!, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveValue, uiaResult.AuditPolicyResult!, StringComparison.Ordinal);
        Assert.Equal(0, browserChannel.CallCount);
        Assert.Equal(0, uiaChannel.CallCount);

        var auditOnlyRejection = ActionDispatchService.RejectBeforeDispatch(uia.Request.Authorization, "uia_command_authorization_binding_denied");
        Assert.Equal(AuditRecordStatus.Sealed, auditOnlyRejection.AuditRecord.Status);
        Assert.Equal("uia_command_authorization_binding_denied", auditOnlyRejection.AuditRecord.PolicyResult);
        Assert.DoesNotContain(sensitiveValue, auditOnlyRejection.AuditRecord.PolicyResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadbackMismatchNeverFabricatesVerifiedOutcomeAcrossAdapters()
    {
        var now = DateTimeOffset.UtcNow;
        var browser = CreateBrowser(now, now.AddMinutes(1));
        var uia = CreateUia(now, now.AddMinutes(1));

        var browserResult = await BrowserActionControlPath.ExecuteAsync(browser.Request, new BrowserChannel("observed_playing"), CancellationToken.None);
        var uiaResult = await WindowsUiAutomationControlPath.ExecuteAsync(uia.Request, new UiaChannel(new(uia.Target.TargetId, "invoked", null)), null, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Failed, browserResult.Outcome);
        Assert.StartsWith("readback_mismatch:", browserResult.ReasonCode, StringComparison.Ordinal);
        Assert.Equal(ActionTaskStatus.Failed, browser.Task.Status);
        Assert.Equal(WindowsUiAutomationOutcome.Failed, uiaResult.Outcome);
        Assert.Equal("uia_readback_mismatch", uiaResult.ReasonCode);
        Assert.Equal(ActionTaskStatus.Failed, uia.Task.Status);
    }

    [Fact]
    public async Task CancellationAndChannelFailureNeverFabricateVerifiedOutcome()
    {
        var now = DateTimeOffset.UtcNow;
        var browser = CreateBrowser(now, now.AddMinutes(1));
        var uia = CreateUia(now, now.AddMinutes(1));

        var browserResult = await BrowserActionControlPath.ExecuteAsync(browser.Request, new CancellingBrowserChannel(), CancellationToken.None);
        var uiaResult = await WindowsUiAutomationControlPath.ExecuteAsync(uia.Request, new CancellingUiaChannel(), null, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, browserResult.Outcome);
        Assert.Equal("cancelled", browserResult.ReasonCode);
        Assert.Equal(ActionTaskStatus.Cancelled, browser.Task.Status);
        Assert.Equal(WindowsUiAutomationOutcome.Denied, uiaResult.Outcome);
        Assert.Equal("cancelled", uiaResult.ReasonCode);
        Assert.Equal(ActionTaskStatus.Cancelled, uia.Task.Status);
    }

    [Fact]
    public async Task BrowserChannelFailureReconcilesTaskToFailed()
    {
        var browser = CreateBrowser(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));

        var result = await BrowserActionControlPath.ExecuteAsync(browser.Request, new ThrowingBrowserChannel(), CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Failed, result.Outcome);
        Assert.Equal("browser_channel_unavailable", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Failed, browser.Task.Status);
    }

    [Fact]
    public async Task EmergencyStopCancelsAdapterOwnedWorkersAndSealsAuditWithoutResume()
    {
        var profile = ActiveProfile();
        var browserTask = RunningTask();
        var uiaTask = RunningTask();
        var browserWorker = RunningWorker(browserTask);
        var uiaWorker = RunningWorker(uiaTask);
        var ownership = new LocalWorkerOwnershipRegistry();
        var browserStopper = new Stopper();
        var uiaStopper = new Stopper();
        ownership.Register(browserTask, browserWorker, browserStopper);
        ownership.Register(uiaTask, uiaWorker, uiaStopper);

        var result = await new EmergencyStopExecutionService(ownership).ActivateAsync(
            profile,
            [browserTask, uiaTask],
            new EmergencyStopRequest("cross-adapter-stop", EntityId.New(), EntityId.New(), EntityId.New()));

        Assert.Equal(ActionTaskStatus.Cancelled, browserTask.Status);
        Assert.Equal(ActionTaskStatus.Cancelled, uiaTask.Status);
        Assert.Equal(WorkerProcessStatus.Killed, browserWorker.Status);
        Assert.Equal(WorkerProcessStatus.Killed, uiaWorker.Status);
        Assert.Equal(1, browserStopper.CallCount);
        Assert.Equal(1, uiaStopper.CallCount);
        Assert.Equal(AuditRecordStatus.Sealed, result.AuditRecord.Status);
        Assert.Equal("emergency_stop", result.AuditRecord.Action);
        Assert.Throws<InvalidOperationException>(() => browserTask.Complete("resume_denied"));
        Assert.Throws<InvalidOperationException>(() => uiaTask.Complete("resume_denied"));
    }

    private static BrowserScenario CreateBrowser(DateTimeOffset now, DateTimeOffset confirmationExpiry, string? commandAuthorizationId = null)
    {
        var userId = EntityId.New();
        var sessionId = EntityId.New();
        var deviceId = EntityId.New();
        const string origin = "https://trusted.example";
        const string correlationId = "cross-browser-correlation";
        var scope = PermissionScope.Create(userId, deviceId, "browser.media", origin, "pause", false);
        var grant = new PermissionGrant(EntityId.New(), scope, now.AddMinutes(2));
        grant.Submit();
        grant.Approve();
        var plan = StructuredActionPlan.Create(EntityId.New(), 1, "browser.media", origin, "pause");
        var actionPlan = new ActionPlan(plan, now.AddMinutes(2));
        actionPlan.SubmitForPolicyReview();
        actionPlan.RequireConfirmation();
        var task = new ActionTask(EntityId.New(), plan.Id, userId, now.AddMinutes(2), true);
        var authorization = new ActionDispatchRequest(ActiveProfile(userId), actionPlan, grant, scope,
            new ConfirmationRequest(EntityId.New(), userId, sessionId, deviceId, plan.Hash, confirmationExpiry), task,
            userId, sessionId, deviceId, now, correlationId);
        var command = new NativeMessagingBrowserCommand("cross-browser", "token", origin, "tab-1", "snapshot-1", 1, 0,
            "pause", plan.Hash, commandAuthorizationId ?? correlationId);
        return new(new(authorization, command), task);
    }

    private static UiaScenario CreateUia(DateTimeOffset now, DateTimeOffset confirmationExpiry)
    {
        var userId = EntityId.New();
        var sessionId = EntityId.New();
        var deviceId = EntityId.New();
        const string correlationId = "cross-uia-correlation";
        var target = new UiTargetScope("notepad", ProcessId: 1234);
        var scope = PermissionScope.Create(userId, deviceId, "windows.uia", target.TargetId, UiActionKind.Focus.ToString(), false);
        var grant = new PermissionGrant(EntityId.New(), scope, now.AddMinutes(2));
        grant.Submit();
        grant.Approve();
        var plan = StructuredActionPlan.Create(EntityId.New(), 1, "windows.uia", target.TargetId, UiActionKind.Focus.ToString());
        var actionPlan = new ActionPlan(plan, now.AddMinutes(2));
        actionPlan.SubmitForPolicyReview();
        actionPlan.RequireConfirmation();
        var task = new ActionTask(EntityId.New(), plan.Id, userId, now.AddMinutes(2), true);
        var authorization = new ActionDispatchRequest(ActiveProfile(userId), actionPlan, grant, scope,
            new ConfirmationRequest(EntityId.New(), userId, sessionId, deviceId, plan.Hash, confirmationExpiry), task,
            userId, sessionId, deviceId, now, correlationId);
        var command = new VerifiedWindowsUiAutomationCommand("cross-uia", target, UiActionKind.Focus, plan.Hash, null, "focused", correlationId);
        return new(new(authorization, command), task, target);
    }

    private static AssistantProfile ActiveProfile(EntityId? userId = null)
    {
        var profile = new AssistantProfile(EntityId.New(), userId ?? EntityId.New());
        profile.CompleteProvisioning();
        return profile;
    }

    private static ActionTask RunningTask()
    {
        var task = new ActionTask(EntityId.New(), EntityId.New(), EntityId.New(), DateTimeOffset.UtcNow.AddMinutes(1), true);
        task.Dispatch();
        return task;
    }

    private static WorkerProcess RunningWorker(ActionTask task)
    {
        var worker = new WorkerProcess(EntityId.New(), task.Id, EntityId.New(), DateTimeOffset.UtcNow.AddMinutes(1));
        worker.Start();
        worker.MarkRunning(1234);
        return worker;
    }

    private sealed record BrowserScenario(BrowserActionControlRequest Request, ActionTask Task);
    private sealed record UiaScenario(WindowsUiAutomationControlRequest Request, ActionTask Task, UiTargetScope Target);

    private sealed class BrowserChannel(string? status) : IVerifiedBrowserActionChannel
    {
        public int CallCount { get; private set; }
        public Task<string?> DispatchAndReadbackAsync(NativeMessagingBrowserCommand command, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(status);
        }
    }

    private sealed class UiaChannel(WindowsUiAutomationReadback? readback) : IVerifiedWindowsUiAutomationChannel
    {
        public int CallCount { get; private set; }
        public Task<WindowsUiAutomationReadback?> ExecuteAndReadbackAsync(UiTargetScope target, UiActionKind action, string? valuePayload, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(readback);
        }
    }

    private sealed class ThrowingBrowserChannel : IVerifiedBrowserActionChannel
    {
        public Task<string?> DispatchAndReadbackAsync(NativeMessagingBrowserCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("browser_channel_unavailable");
    }

    private sealed class CancellingBrowserChannel : IVerifiedBrowserActionChannel
    {
        public Task<string?> DispatchAndReadbackAsync(NativeMessagingBrowserCommand command, CancellationToken cancellationToken) =>
            Task.FromCanceled<string?>(new CancellationToken(canceled: true));
    }

    private sealed class CancellingUiaChannel : IVerifiedWindowsUiAutomationChannel
    {
        public Task<WindowsUiAutomationReadback?> ExecuteAndReadbackAsync(UiTargetScope target, UiActionKind action, string? valuePayload, CancellationToken cancellationToken) =>
            Task.FromCanceled<WindowsUiAutomationReadback?>(new CancellationToken(canceled: true));
    }

    private sealed class Stopper : ILocalWorkerProcessStopper
    {
        public int CallCount { get; private set; }
        public Task<LocalWorkerProcessStopResult> StopProcessTreeAsync(WorkerProcess worker, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(LocalWorkerProcessStopResult.Stopped("tree_killed"));
        }
    }
}
