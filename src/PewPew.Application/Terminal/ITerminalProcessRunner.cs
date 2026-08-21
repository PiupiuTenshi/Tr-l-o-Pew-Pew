using PewPew.Domain.Workers;

namespace PewPew.Application.Terminal;

/// <summary>
/// Infrastructure boundary for running one already-authorized structured terminal
/// process. Implementations must not invoke a shell interpreter.
/// </summary>
public interface ITerminalProcessRunner
{
    /// <summary>
    /// True only when the adapter can enforce the workflow's network policy at
    /// the operating-system boundary. Declaring false makes execution fail closed.
    /// </summary>
    bool ProvidesNetworkIsolation { get; }

    Task<TerminalProcessRunResult> RunAsync(
        TerminalProcessLaunchRequest request,
        Action<int> onProcessStarted,
        CancellationToken cancellationToken);
}

/// <summary>Validated process data that can cross from Application to an adapter.</summary>
public sealed record TerminalProcessLaunchRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    WorkerResourceQuota Quota,
    IReadOnlyDictionary<string, string>? Environment = null,
    IsolatedTerminalWorkerBinding? Binding = null,
    string? ExpectedExecutableSha256Hash = null);

/// <summary>
/// Metadata-only process result. Output content is intentionally not retained by
/// this contract so it cannot accidentally become audit or model input.
/// </summary>
public sealed record TerminalProcessRunResult(
    int ExitCode,
    long OutputBytes,
    bool OutputLimitExceeded,
    int PeakRamMb,
    TimeSpan Duration);

/// <summary>Metadata-only denial raised by an adapter before a process is started.</summary>
public sealed class TerminalProcessBoundaryViolationException(string reasonCode) : InvalidOperationException(reasonCode)
{
    public string ReasonCode { get; } = reasonCode;
}
