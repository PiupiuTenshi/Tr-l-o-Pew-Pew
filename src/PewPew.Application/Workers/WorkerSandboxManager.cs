using PewPew.Application.EmergencyStop;
using PewPew.Domain.Workers;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Application.Workers;

/// <summary>
/// Metrics snapshot returned by a worker monitoring provider.
/// </summary>
public sealed record WorkerResourceMetrics(int CurrentRamMb, long OutputSizeBytes);

/// <summary>
/// Application service responsible for worker sandbox creation, quota enforcement,
/// timeout monitoring, process tree termination triggers, and output size sanitization.
/// </summary>
public static class WorkerSandboxManager
{
    private const string TruncationSuffix = "\n[OUTPUT TRUNCATED - QUOTA EXCEEDED]";

    /// <summary>
    /// Creates a new <see cref="WorkerProcess"/> configured with the given quota and duration.
    /// </summary>
    public static WorkerProcess CreateWorker(
        EntityId ownerTaskId,
        EntityId deviceId,
        WorkerResourceQuota? quota = null,
        DateTimeOffset? now = null)
    {
        var activeQuota = quota ?? WorkerResourceQuota.Default;
        var current = now ?? DateTimeOffset.UtcNow;
        var timeoutAt = current.Add(activeQuota.MaxExecutionDuration);

        return new WorkerProcess(
            EntityId.New(),
            ownerTaskId,
            deviceId,
            timeoutAt,
            activeQuota);
    }

    /// <summary>
    /// Monitors an active worker process for execution timeout or resource quota breaches.
    /// If a breach or timeout is detected, records the failure and attempts process tree termination.
    /// </summary>
    public static async Task MonitorWorkerAsync(
        WorkerProcess worker,
        WorkerResourceMetrics? metrics,
        ILocalWorkerProcessStopper? stopper,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        if (!worker.IsActive)
        {
            return;
        }

        // 1. Check timeout
        worker.CheckTimeout(now);

        // 2. Check metrics & quota breach
        if (metrics is not null && worker.IsActive)
        {
            worker.RecordResourceUsage(metrics.CurrentRamMb, metrics.OutputSizeBytes);
        }

        // 3. If process failed or timed out during check, trigger stopper cleanup
        if (!worker.IsActive && stopper is not null && worker.RootProcessId.HasValue)
        {
            await stopper.StopProcessTreeAsync(worker, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Truncates worker output string if it exceeds the quota's maximum output size.
    /// Appends a truncation warning suffix when truncated.
    /// </summary>
    public static string SanitizeAndTruncateOutput(string? rawOutput, WorkerResourceQuota quota)
    {
        if (string.IsNullOrEmpty(rawOutput))
        {
            return string.Empty;
        }

        var activeQuota = quota ?? WorkerResourceQuota.Default;
        var maxBytes = activeQuota.MaxOutputSizeBytes;

        var byteCount = System.Text.Encoding.UTF8.GetByteCount(rawOutput);
        if (byteCount <= maxBytes)
        {
            return rawOutput;
        }

        // Output exceeds max bytes — perform byte-accurate string truncation
        var allowedBytes = Math.Max(0, (int)maxBytes - System.Text.Encoding.UTF8.GetByteCount(TruncationSuffix));
        var truncatedString = TruncateStringToUtf8ByteCount(rawOutput, allowedBytes);

        return truncatedString + TruncationSuffix;
    }

    private static string TruncateStringToUtf8ByteCount(string input, int maxBytes)
    {
        if (maxBytes <= 0)
        {
            return string.Empty;
        }

        var encoding = System.Text.Encoding.UTF8;
        var bytes = encoding.GetBytes(input);

        if (bytes.Length <= maxBytes)
        {
            return input;
        }

        return encoding.GetString(bytes, 0, maxBytes);
    }
}
