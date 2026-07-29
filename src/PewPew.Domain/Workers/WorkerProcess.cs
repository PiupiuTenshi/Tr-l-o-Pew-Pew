using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Workers;

public enum WorkerProcessStatus
{
    Created,
    Starting,
    Running,
    Stopping,
    Stopped,
    Killed,
    Failed
}

/// <summary>
/// Represents one assistant-owned local process tree. A worker is never reused
/// after it is stopped or emergency-killed; a later execution must use a new
/// ActionTask and WorkerProcess.
/// </summary>
public sealed class WorkerProcess
{
    public WorkerProcess(EntityId id, EntityId ownerTaskId, EntityId deviceId, DateTimeOffset timeoutAtUtc)
    {
        if (timeoutAtUtc <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutAtUtc), "Worker timeout must be in the future.");
        }

        Id = id;
        OwnerTaskId = ownerTaskId;
        DeviceId = deviceId;
        TimeoutAtUtc = timeoutAtUtc;
    }

    public EntityId Id { get; }

    public EntityId OwnerTaskId { get; }

    public EntityId DeviceId { get; }

    public DateTimeOffset TimeoutAtUtc { get; }

    public int? RootProcessId { get; private set; }

    public WorkerProcessStatus Status { get; private set; } = WorkerProcessStatus.Created;

    public void Start() => Move(WorkerProcessStatus.Created, WorkerProcessStatus.Starting);

    public void MarkRunning(int rootProcessId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rootProcessId);

        Move(WorkerProcessStatus.Starting, WorkerProcessStatus.Running);
        RootProcessId = rootProcessId;
    }

    public void RequestStop() => Move(WorkerProcessStatus.Running, WorkerProcessStatus.Stopping);

    public void MarkStopped() => Move(WorkerProcessStatus.Stopping, WorkerProcessStatus.Stopped);

    public void EmergencyKill()
    {
        if (Status is not (WorkerProcessStatus.Starting or WorkerProcessStatus.Running or WorkerProcessStatus.Stopping))
        {
            throw new InvalidOperationException("Only an active worker process can be emergency-killed.");
        }

        Status = WorkerProcessStatus.Killed;
    }

    public void MarkFailed()
    {
        if (Status is not (WorkerProcessStatus.Starting or WorkerProcessStatus.Running or WorkerProcessStatus.Stopping))
        {
            throw new InvalidOperationException("Only an active worker process can fail.");
        }

        Status = WorkerProcessStatus.Failed;
    }

    private void Move(WorkerProcessStatus expected, WorkerProcessStatus next)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException("Worker process transition denied.");
        }

        Status = next;
    }
}
