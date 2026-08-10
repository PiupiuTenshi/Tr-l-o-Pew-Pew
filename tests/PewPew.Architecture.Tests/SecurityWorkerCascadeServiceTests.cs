using PewPew.Application.EmergencyStop;
using PewPew.Domain.Actions;
using PewPew.Domain.Audit;
using PewPew.Domain.Permissions;
using PewPew.Domain.Skills;
using PewPew.Domain.Workers;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class SecurityWorkerCascadeServiceTests
{
    private static readonly string ValidHash = new('a', 64);

    [Fact]
    public async Task RevokePermissionStopsOnlyExactlyBoundWorkerAndCancelsItsTask()
    {
        var (grant, task, worker, ownership, stopper, cancellation) = CreatePermissionBoundWorker();

        var result = await new SecurityWorkerCascadeService(ownership).RevokePermissionAsync(
            grant,
            [task],
            Request());

        Assert.Equal(PermissionGrantStatus.Revoked, grant.Status);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(1, stopper.CallCount);
        Assert.Equal(WorkerProcessStatus.Killed, worker.Status);
        Assert.Equal(ActionTaskStatus.Cancelled, task.Status);
        Assert.Equal([task.Id], result.CancelledTaskIds);
        Assert.Empty(result.UnknownTaskIds);
        Assert.Equal(AuditRecordStatus.Sealed, result.AuditRecord.Status);
        Assert.Equal("permission_revoke_cascade", result.AuditRecord.Action);
    }

    [Fact]
    public async Task QuarantineSkillMarksUncertainStopUnknownAndDoesNotTouchOtherSkill()
    {
        var skill = EnabledSkill("browser.media");
        var otherSkill = EnabledSkill("terminal.run_build");
        var permission = ActivePermission("browser.media");
        var otherPermission = ActivePermission("terminal.run_build");
        var uncertainTask = RunningTask(supportsCancellation: false);
        var unrelatedTask = RunningTask();
        var uncertainWorker = RunningWorker(uncertainTask);
        var unrelatedWorker = RunningWorker(unrelatedTask);
        var ownership = new LocalWorkerOwnershipRegistry();
        var failingStopper = new Stopper(LocalWorkerProcessStopResult.Failed("stop_adapter_denied"));
        var unrelatedStopper = new Stopper(LocalWorkerProcessStopResult.Stopped("unrelated_tree_stopped"));
        ownership.Register(uncertainTask, uncertainWorker, failingStopper, new WorkerOwnershipBinding(permission.Id, skill.Id));
        ownership.Register(unrelatedTask, unrelatedWorker, unrelatedStopper, new WorkerOwnershipBinding(otherPermission.Id, otherSkill.Id));

        var result = await new SecurityWorkerCascadeService(ownership).QuarantineSkillAsync(
            skill,
            "integrity_violation",
            [uncertainTask, unrelatedTask],
            Request());

        Assert.Equal(SkillPackageStatus.Quarantined, skill.Status);
        Assert.Equal(1, failingStopper.CallCount);
        Assert.Equal(0, unrelatedStopper.CallCount);
        Assert.Equal(ActionTaskStatus.Unknown, uncertainTask.Status);
        Assert.Equal(WorkerProcessStatus.Running, uncertainWorker.Status);
        Assert.Equal(ActionTaskStatus.Running, unrelatedTask.Status);
        Assert.Equal([uncertainTask.Id], result.UnknownTaskIds);
        Assert.Empty(result.CancelledTaskIds);
        Assert.Equal("skill_quarantine_cascade", result.AuditRecord.Action);
    }

    [Fact]
    public async Task RepeatingRevocationIsIdempotentAndDoesNotStopWorkerTwice()
    {
        var (grant, task, _, ownership, stopper, _) = CreatePermissionBoundWorker();
        var service = new SecurityWorkerCascadeService(ownership);

        await service.RevokePermissionAsync(grant, [task], Request());
        var retry = await service.RevokePermissionAsync(grant, [task], Request());

        Assert.Equal(PermissionGrantStatus.Revoked, grant.Status);
        Assert.Equal(1, stopper.CallCount);
        Assert.Empty(retry.CancelledTaskIds);
        Assert.Empty(retry.UnknownTaskIds);
        Assert.Equal(AuditRecordStatus.Sealed, retry.AuditRecord.Status);
    }

    [Fact]
    public async Task QuarantineRejectsNonCanonicalReasonBeforeStoppingWorker()
    {
        var (grant, task, worker, ownership, stopper, _) = CreatePermissionBoundWorker();
        var skill = EnabledSkill("browser.media");
        ownership.Deregister(task.Id);
        ownership.Register(task, worker, stopper, new WorkerOwnershipBinding(grant.Id, skill.Id));

        await Assert.ThrowsAsync<ArgumentException>(() => new SecurityWorkerCascadeService(ownership).QuarantineSkillAsync(
            skill,
            "raw failure: token=value",
            [task],
            Request()));

        Assert.Equal(SkillPackageStatus.Enabled, skill.Status);
        Assert.Equal(ActionTaskStatus.Running, task.Status);
        Assert.Equal(0, stopper.CallCount);
    }

    private static (PermissionGrant Grant, ActionTask Task, WorkerProcess Worker, LocalWorkerOwnershipRegistry Ownership, Stopper Stopper, CancellationToken Cancellation)
        CreatePermissionBoundWorker()
    {
        var grant = ActivePermission("browser.media");
        var task = RunningTask();
        var worker = RunningWorker(task);
        var ownership = new LocalWorkerOwnershipRegistry();
        var stopper = new Stopper(LocalWorkerProcessStopResult.Stopped("tree_stopped"));
        var cancellation = ownership.Register(task, worker, stopper, new WorkerOwnershipBinding(grant.Id, EnabledSkill("browser.media").Id));
        return (grant, task, worker, ownership, stopper, cancellation);
    }

    private static PermissionGrant ActivePermission(string skill)
    {
        var scope = PermissionScope.Create(EntityId.New(), EntityId.New(), skill, "bound-resource", "execute", false);
        var grant = new PermissionGrant(EntityId.New(), scope, DateTimeOffset.UtcNow.AddMinutes(5));
        grant.Submit();
        grant.Approve();
        return grant;
    }

    private static SkillPackage EnabledSkill(string capability)
    {
        var skill = new SkillPackage(EntityId.New(), "test-skill", "1.0.0", ValidHash, [capability]);
        skill.VerifyHash(ValidHash, DateTimeOffset.UtcNow);
        return skill;
    }

    private static ActionTask RunningTask(bool supportsCancellation = true)
    {
        var task = new ActionTask(EntityId.New(), EntityId.New(), EntityId.New(), DateTimeOffset.UtcNow.AddMinutes(5), supportsCancellation);
        task.Dispatch();
        return task;
    }

    private static WorkerProcess RunningWorker(ActionTask task)
    {
        var worker = new WorkerProcess(EntityId.New(), task.Id, EntityId.New(), DateTimeOffset.UtcNow.AddMinutes(5));
        worker.Start();
        worker.MarkRunning(4444);
        return worker;
    }

    private static WorkerCascadeRequest Request() => new("cascade-correlation", EntityId.New(), EntityId.New(), EntityId.New());

    private sealed class Stopper(LocalWorkerProcessStopResult result) : ILocalWorkerProcessStopper
    {
        public int CallCount { get; private set; }

        public Task<LocalWorkerProcessStopResult> StopProcessTreeAsync(WorkerProcess worker, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}
