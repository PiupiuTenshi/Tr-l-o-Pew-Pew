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
    public WorkerProcess(
        EntityId id,
        EntityId ownerTaskId,
        EntityId deviceId,
        DateTimeOffset timeoutAtUtc,
        WorkerResourceQuota? quota = null)
    {
        if (timeoutAtUtc <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutAtUtc), "Worker timeout must be in the future.");
        }

        Id = id;
        OwnerTaskId = ownerTaskId;
        DeviceId = deviceId;
        TimeoutAtUtc = timeoutAtUtc;
        Quota = quota ?? WorkerResourceQuota.Default;
    }

    public EntityId Id { get; }
    public EntityId OwnerTaskId { get; }
    public EntityId DeviceId { get; }
    public DateTimeOffset TimeoutAtUtc { get; }
    public WorkerResourceQuota Quota { get; }
    public int? RootProcessId { get; private set; }
    public WorkerProcessStatus Status { get; private set; } = WorkerProcessStatus.Created;
    public string? FailureReason { get; private set; }
    public int PeakRamMb { get; private set; }
    public long OutputSizeBytes { get; private set; }

    public bool IsTimedOut(DateTimeOffset now) => now >= TimeoutAtUtc;

    public bool IsActive => Status is WorkerProcessStatus.Starting or WorkerProcessStatus.Running or WorkerProcessStatus.Stopping;

    public void Start() => Move(WorkerProcessStatus.Created, WorkerProcessStatus.Starting);

    public void MarkRunning(int? rootProcessId = null)
    {
        if (rootProcessId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rootProcessId));
        }

        Move(WorkerProcessStatus.Starting, WorkerProcessStatus.Running);
        RootProcessId = rootProcessId;
    }

    /// <summary>Associates a real OS process only after the worker adapter has started it.</summary>
    public void AttachRootProcessId(int rootProcessId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rootProcessId);
        if (Status != WorkerProcessStatus.Running || RootProcessId.HasValue)
        {
            throw new InvalidOperationException("A root process can only be attached once to a running worker.");
        }

        RootProcessId = rootProcessId;
    }

    public void RequestStop() => Move(WorkerProcessStatus.Running, WorkerProcessStatus.Stopping);

    public void MarkStopped() => Move(WorkerProcessStatus.Stopping, WorkerProcessStatus.Stopped);

    public void EmergencyKill()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Only an active worker process can be emergency-killed.");
        }

        Status = WorkerProcessStatus.Killed;
    }

    public void MarkFailed(string? reason = null)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Only an active worker process can fail.");
        }

        Status = WorkerProcessStatus.Failed;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            FailureReason = reason;
        }
    }

    public void CheckTimeout(DateTimeOffset now)
    {
        if (IsTimedOut(now) && IsActive)
        {
            MarkTimedOut();
        }
    }

    public void MarkTimedOut()
    {
        if (!IsActive)
        {
            return;
        }

        Status = WorkerProcessStatus.Failed;
        FailureReason = "execution_timeout";
    }

    public void RecordResourceUsage(int currentRamMb, long outputSizeBytes)
    {
        if (currentRamMb > PeakRamMb)
        {
            PeakRamMb = currentRamMb;
        }

        OutputSizeBytes = outputSizeBytes;

        if (currentRamMb > Quota.MaxRamMb)
        {
            MarkQuotaExceeded($"RAM quota exceeded: {currentRamMb} MB > {Quota.MaxRamMb} MB");
            return;
        }

        if (outputSizeBytes > Quota.MaxOutputSizeBytes)
        {
            MarkQuotaExceeded($"Output size quota exceeded: {outputSizeBytes} bytes > {Quota.MaxOutputSizeBytes} bytes");
        }
    }

    public void MarkQuotaExceeded(string reason)
    {
        if (!IsActive)
        {
            return;
        }

        Status = WorkerProcessStatus.Failed;
        FailureReason = reason;
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
