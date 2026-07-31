using PewPew.Application.Actions;
using PewPew.Application.EmergencyStop;
using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Permissions;
using PewPew.Domain.Skills;
using PewPew.Domain.Workers;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Unit tests for <see cref="WorkerActionDispatcher"/> covering policy dispatch,
/// <see cref="SkillPackage"/> capability authorization, worker execution,
/// verification evidence recording, and task outcome reconciliation.
/// </summary>
public sealed class WorkerActionDispatcherTests
{
    private static readonly EntityId UserId = EntityId.New();
    private static readonly EntityId SessionId = EntityId.New();
    private static readonly EntityId DeviceId = EntityId.New();
    private static readonly string ValidHash = new('a', 64);
    private static readonly string[] AllowedCapabilities = ["browser.read_tab", "terminal.run_build"];

    [Fact]
    public async Task DispatchAndExecuteAsyncCompletePath()
    {
        var (request, skillPackage, stopper, registry) = CreateValidContext();

        var result = await WorkerActionDispatcher.DispatchAndExecuteAsync(
            request,
            skillPackage,
            WorkerResourceQuota.Default,
            stopper,
            registry,
            (_, _) => Task.FromResult(WorkerExecutionOutcome.Success("element_verified_success")),
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.True(result.IsExecutedSuccessfully);
        Assert.Equal("allowed", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Completed, request.Task.Status);
        Assert.Equal("element_verified_success", request.Task.VerificationEvidence);
        Assert.Equal(WorkerProcessStatus.Stopped, result.Worker!.Status);
    }

    [Fact]
    public async Task DispatchAndExecuteAsyncUnknownPath()
    {
        var (request, skillPackage, stopper, registry) = CreateValidContext();

        var result = await WorkerActionDispatcher.DispatchAndExecuteAsync(
            request,
            skillPackage,
            WorkerResourceQuota.Default,
            stopper,
            registry,
            (_, _) => Task.FromResult(WorkerExecutionOutcome.Unknown("unconfirmed_window_title")),
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.False(result.IsExecutedSuccessfully);
        Assert.Equal(ActionTaskStatus.Unknown, request.Task.Status);
    }

    [Fact]
    public async Task DispatchAndExecuteAsyncReconcileUnknownTask()
    {
        var (request, skillPackage, stopper, registry) = CreateValidContext();

        // 1. Dispatch into Unknown state
        await WorkerActionDispatcher.DispatchAndExecuteAsync(
            request,
            skillPackage,
            WorkerResourceQuota.Default,
            stopper,
            registry,
            (_, _) => Task.FromResult(WorkerExecutionOutcome.Unknown("pending_window_verification")),
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(ActionTaskStatus.Unknown, request.Task.Status);

        // 2. Reconcile to Completed
        var reconcileResult = WorkerActionDispatcher.ReconcileUnknownTask(
            request.Task,
            WorkerExecutionOutcome.Success("window_title_verified_later"));

        Assert.True(reconcileResult);
        Assert.Equal(ActionTaskStatus.Completed, request.Task.Status);
        Assert.Equal("window_title_verified_later", request.Task.VerificationEvidence);
    }

    [Fact]
    public async Task DispatchAndExecuteAsyncCancelledPath()
    {
        var (request, skillPackage, stopper, registry) = CreateValidContext();

        var result = await WorkerActionDispatcher.DispatchAndExecuteAsync(
            request,
            skillPackage,
            WorkerResourceQuota.Default,
            stopper,
            registry,
            (_, _) => Task.FromResult(WorkerExecutionOutcome.Cancelled("user_pressed_cancel")),
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.False(result.IsExecutedSuccessfully);
        Assert.Equal(ActionTaskStatus.Cancelled, request.Task.Status);
        Assert.Equal(WorkerProcessStatus.Killed, result.Worker!.Status);
    }

    [Fact]
    public async Task DispatchAndExecuteAsyncSkillCapabilityDeniedPath()
    {
        var (request, _, stopper, registry) = CreateValidContext();

        // Package missing 'browser.read_tab' capability
        var restrictedSkillPackage = new SkillPackage(
            EntityId.New(), "RestrictedSkill", "1.0.0", ValidHash, ["terminal.run_build"]);
        restrictedSkillPackage.VerifyHash(ValidHash, DateTimeOffset.UtcNow);

        var result = await WorkerActionDispatcher.DispatchAndExecuteAsync(
            request,
            restrictedSkillPackage,
            WorkerResourceQuota.Default,
            stopper,
            registry,
            (_, _) => Task.FromResult(WorkerExecutionOutcome.Success("should_not_run")),
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.False(result.IsExecutedSuccessfully);
        Assert.Equal("skill_capability_denied", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Failed, request.Task.Status);
        Assert.Equal("skill_capability_denied", request.Task.FailureReason);
    }

    [Fact]
    public async Task DispatchAndExecuteAsyncPolicyDeniedPath()
    {
        var (request, skillPackage, stopper, registry) = CreateValidContext();

        // Profile that was not completed/provisioned causes Policy Engine denial
        var unprovisionedProfile = new AssistantProfile(EntityId.New(), UserId);

        var deniedRequest = request with { Profile = unprovisionedProfile };

        var result = await WorkerActionDispatcher.DispatchAndExecuteAsync(
            deniedRequest,
            skillPackage,
            WorkerResourceQuota.Default,
            stopper,
            registry,
            (_, _) => Task.FromResult(WorkerExecutionOutcome.Success("should_not_run")),
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.False(result.IsExecutedSuccessfully);
        Assert.Equal("assistant_not_active", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Queued, deniedRequest.Task.Status);
    }

    [Fact]
    public async Task DispatchAndExecuteAsyncExceptionPath()
    {
        var (request, skillPackage, stopper, registry) = CreateValidContext();

        var result = await WorkerActionDispatcher.DispatchAndExecuteAsync(
            request,
            skillPackage,
            WorkerResourceQuota.Default,
            stopper,
            registry,
            (_, _) => throw new InvalidOperationException("process_crashed"),
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.False(result.IsExecutedSuccessfully);
        Assert.Equal("worker_exception", result.ReasonCode);
        Assert.Equal(ActionTaskStatus.Failed, request.Task.Status);
        Assert.Contains("process_crashed", request.Task.FailureReason);
    }

    private static (ActionDispatchRequest Request, SkillPackage Skill, ILocalWorkerProcessStopper Stopper, LocalWorkerOwnershipRegistry Registry) CreateValidContext()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = new AssistantProfile(EntityId.New(), UserId);
        profile.CompleteProvisioning();

        var scope = PermissionScope.Create(UserId, DeviceId, "browser.read_tab", "tab_title", "read", false);
        var grant = new PermissionGrant(EntityId.New(), scope, now.AddMinutes(10));
        grant.Submit();
        grant.Approve();

        var definition = StructuredActionPlan.Create(EntityId.New(), 1, "browser.read_tab", "tab_title", "payload");
        var plan = new ActionPlan(definition, now.AddMinutes(5));
        plan.SubmitForPolicyReview();
        plan.ApproveByPolicy();

        var task = new ActionTask(EntityId.New(), definition.Id, UserId, now.AddMinutes(5), supportsCancellation: true);
        var request = new ActionDispatchRequest(
            profile, plan, grant, scope, null, task, UserId, SessionId, DeviceId, now, "corr-p03-t04");

        var skillPackage = new SkillPackage(EntityId.New(), "TestBrowserSkill", "1.0.0", ValidHash, AllowedCapabilities);
        skillPackage.VerifyHash(ValidHash, now); // Moves status to Enabled

        var stopper = new StopperStub();
        var registry = new LocalWorkerOwnershipRegistry();

        return (request, skillPackage, stopper, registry);
    }

    private sealed class StopperStub : ILocalWorkerProcessStopper
    {
        public Task<LocalWorkerProcessStopResult> StopProcessTreeAsync(WorkerProcess worker, CancellationToken cancellationToken) =>
            Task.FromResult(LocalWorkerProcessStopResult.Stopped("stopped_by_stub"));
    }
}
