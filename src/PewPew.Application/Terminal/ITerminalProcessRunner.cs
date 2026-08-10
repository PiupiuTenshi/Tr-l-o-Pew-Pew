using PewPew.Domain.Workers;

namespace PewPew.Application.Terminal;

/// <summary>
/// Infrastructure boundary for running one already-authorized structured terminal
/// process. Implementations must not invoke a shell interpreter.
/// </summary>
public interface ITerminalProcessRunner
{
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
    WorkerResourceQuota Quota);

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
