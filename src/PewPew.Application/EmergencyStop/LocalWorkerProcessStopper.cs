using PewPew.Domain.Workers;

namespace PewPew.Application.EmergencyStop;

/// <summary>
/// Infrastructure boundary for terminating an already owned local process
/// tree. It never accepts a path, executable name or model-provided PID.
/// </summary>
public interface ILocalWorkerProcessStopper
{
    Task<LocalWorkerProcessStopResult> StopProcessTreeAsync(
        WorkerProcess worker,
        CancellationToken cancellationToken);
}

public sealed record LocalWorkerProcessStopResult(bool IsStopped, string Evidence, string? FailureReason = null)
{
    public static LocalWorkerProcessStopResult Stopped(string evidence) =>
        new(true, RequireText(evidence, nameof(evidence)));

    public static LocalWorkerProcessStopResult Failed(string reason) =>
        new(false, "worker_stop_failed", RequireText(reason, nameof(reason)));

    private static string RequireText(string value, string parameterName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("A non-empty process stop result is required.", parameterName);
}
