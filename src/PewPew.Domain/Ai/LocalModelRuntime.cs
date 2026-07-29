using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Ai;

public enum LocalModelRuntimeStatus
{
    Unloaded,
    Loading,
    Ready,
    Busy,
    Unloading,
    Failed
}

/// <summary>
/// Owns the resource-safe lifecycle of one local model runtime. It has no
/// provider dependency and never performs model I/O itself.
/// </summary>
public sealed class LocalModelRuntime
{
    public LocalModelRuntime(EntityId id, string modelId, long reservedMemoryBytes)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("A model identifier is required.", nameof(modelId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reservedMemoryBytes);

        Id = id;
        ModelId = modelId.Trim();
        ReservedMemoryBytes = reservedMemoryBytes;
    }

    public EntityId Id { get; }

    public string ModelId { get; }

    public long ReservedMemoryBytes { get; }

    public LocalModelRuntimeStatus Status { get; private set; } = LocalModelRuntimeStatus.Unloaded;

    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public void BeginLoad()
    {
        Require(LocalModelRuntimeStatus.Unloaded);
        Status = LocalModelRuntimeStatus.Loading;
    }

    public void MarkReady(DateTimeOffset now)
    {
        Require(LocalModelRuntimeStatus.Loading);
        LastUsedAtUtc = now;
        Status = LocalModelRuntimeStatus.Ready;
    }

    public void BeginInference()
    {
        Require(LocalModelRuntimeStatus.Ready);
        Status = LocalModelRuntimeStatus.Busy;
    }

    public void CompleteInference(DateTimeOffset now)
    {
        Require(LocalModelRuntimeStatus.Busy);
        LastUsedAtUtc = now;
        Status = LocalModelRuntimeStatus.Ready;
    }

    public bool BeginUnloadIfIdle(DateTimeOffset now, TimeSpan idleTtl, bool memoryPressure)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleTtl, TimeSpan.Zero);

        if (Status != LocalModelRuntimeStatus.Ready || LastUsedAtUtc is null)
        {
            return false;
        }

        if (!memoryPressure && now - LastUsedAtUtc < idleTtl)
        {
            return false;
        }

        Status = LocalModelRuntimeStatus.Unloading;
        return true;
    }

    public void Fail()
    {
        if (Status is LocalModelRuntimeStatus.Unloaded or LocalModelRuntimeStatus.Unloading)
        {
            throw new InvalidOperationException($"Runtime failure cannot transition from {Status}.");
        }

        Status = LocalModelRuntimeStatus.Failed;
    }

    public void BeginFailedCleanup()
    {
        Require(LocalModelRuntimeStatus.Failed);
        Status = LocalModelRuntimeStatus.Unloading;
    }

    public void MarkUnloaded()
    {
        Require(LocalModelRuntimeStatus.Unloading);
        LastUsedAtUtc = null;
        Status = LocalModelRuntimeStatus.Unloaded;
    }

    private void Require(LocalModelRuntimeStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Local model runtime transition denied from {Status}.");
        }
    }
}
