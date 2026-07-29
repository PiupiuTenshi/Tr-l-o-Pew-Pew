using PewPew.Application.EmergencyStop;
using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Audit;
using PewPew.Domain.Workers;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class EmergencyStopExecutionServiceTests
{
    [Fact]
    public async Task EmergencyStopStopsRegisteredProcessTreeCancelsTokenAndSealsAudit()
    {
        var profile = ActiveProfile();
        var task = RunningTask(supportsCancellation: true);
        var worker = RunningWorker(task);
        var stopper = new RecordingStopper(LocalWorkerProcessStopResult.Stopped("tree_killed"));
        var ownership = new LocalWorkerOwnershipRegistry();
        var cancellation = ownership.Register(task, worker, stopper);

        var result = await new EmergencyStopExecutionService(ownership).ActivateAsync(profile, [task], Request());

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(1, stopper.CallCount);
        Assert.Equal(WorkerProcessStatus.Killed, worker.Status);
        Assert.Equal(ActionTaskStatus.Cancelled, task.Status);
        Assert.True(result.LocalWorkerStops.Single().IsStopped);
        Assert.Equal(AuditRecordStatus.Sealed, result.AuditRecord.Status);
        Assert.Equal("emergency_stop", result.AuditRecord.Action);
    }

    [Fact]
    public async Task EmergencyStopMakesNonCancellableTaskUnknownAndBlocksAnyResume()
    {
        var profile = ActiveProfile();
        var task = RunningTask(supportsCancellation: false);
        var worker = RunningWorker(task);
        var ownership = new LocalWorkerOwnershipRegistry();
        ownership.Register(task, worker, new RecordingStopper(LocalWorkerProcessStopResult.Stopped("tree_killed")));

        await new EmergencyStopExecutionService(ownership).ActivateAsync(profile, [task], Request());

        Assert.Equal(ActionTaskStatus.Unknown, task.Status);
        Assert.True(ownership.IsStopRequested(task.Id));
        Assert.Throws<InvalidOperationException>(() => ownership.Register(task, worker, new RecordingStopper(LocalWorkerProcessStopResult.Stopped("unused"))));
        Assert.Throws<InvalidOperationException>(() => task.Complete("cannot_resume"));
    }

    [Fact]
    public async Task RepeatedEmergencyStopDoesNotRestartOrKillProcessTreeTwice()
    {
        var profile = ActiveProfile();
        var task = RunningTask();
        var worker = RunningWorker(task);
        var stopper = new RecordingStopper(LocalWorkerProcessStopResult.Stopped("tree_killed"));
        var ownership = new LocalWorkerOwnershipRegistry();
        ownership.Register(task, worker, stopper);
        var service = new EmergencyStopExecutionService(ownership);

        await service.ActivateAsync(profile, [task], Request());
        var retry = await service.ActivateAsync(profile, [task], Request());

        Assert.Equal(1, stopper.CallCount);
        Assert.Equal(ActionTaskStatus.Cancelled, task.Status);
        Assert.True(retry.LocalWorkerStops.Single().IsStopped);
        Assert.Equal(AuditRecordStatus.Sealed, retry.AuditRecord.Status);
    }

    [Fact]
    public async Task FailedTreeStopLeavesTaskUnknownAndNeverAllowsNewRegistration()
    {
        var profile = ActiveProfile();
        var task = RunningTask(supportsCancellation: false);
        var worker = RunningWorker(task);
        var ownership = new LocalWorkerOwnershipRegistry();
        ownership.Register(task, worker, new RecordingStopper(LocalWorkerProcessStopResult.Failed("stop_denied")));

        var result = await new EmergencyStopExecutionService(ownership).ActivateAsync(profile, [task], Request());

        Assert.False(result.LocalWorkerStops.Single().IsStopped);
        Assert.Equal("stop_denied", result.LocalWorkerStops.Single().FailureReason);
        Assert.Equal(ActionTaskStatus.Unknown, task.Status);
        Assert.True(ownership.IsStopRequested(task.Id));
        Assert.Equal(AuditRecordStatus.Sealed, result.AuditRecord.Status);
    }

    [Fact]
    public void WorkerProcessRejectsUnownedOrTerminalReuse()
    {
        var task = RunningTask();
        var worker = new WorkerProcess(EntityId.New(), task.Id, EntityId.New(), DateTimeOffset.UtcNow.AddMinutes(1));

        worker.Start();
        worker.MarkRunning(4321);
        worker.EmergencyKill();

        Assert.Equal(WorkerProcessStatus.Killed, worker.Status);
        Assert.Throws<InvalidOperationException>(() => worker.EmergencyKill());
        Assert.Throws<InvalidOperationException>(() => worker.RequestStop());
    }

    private static AssistantProfile ActiveProfile()
    {
        var profile = new AssistantProfile(EntityId.New(), EntityId.New());
        profile.CompleteProvisioning();
        return profile;
    }

    private static ActionTask RunningTask(bool supportsCancellation = true)
    {
        var task = new ActionTask(EntityId.New(), EntityId.New(), EntityId.New(), DateTimeOffset.UtcNow.AddMinutes(1), supportsCancellation);
        task.Dispatch();
        return task;
    }

    private static WorkerProcess RunningWorker(ActionTask task)
    {
        var worker = new WorkerProcess(EntityId.New(), task.Id, EntityId.New(), DateTimeOffset.UtcNow.AddMinutes(1));
        worker.Start();
        worker.MarkRunning(4321);
        return worker;
    }

    private static EmergencyStopRequest Request() => new("emergency-stop-correlation", EntityId.New(), EntityId.New(), EntityId.New());

    private sealed class RecordingStopper(LocalWorkerProcessStopResult result) : ILocalWorkerProcessStopper
    {
        public int CallCount { get; private set; }

        public Task<LocalWorkerProcessStopResult> StopProcessTreeAsync(WorkerProcess worker, CancellationToken cancellationToken)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }
}
