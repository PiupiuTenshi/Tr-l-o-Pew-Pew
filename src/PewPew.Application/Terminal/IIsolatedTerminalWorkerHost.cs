using PewPew.Domain.Workers;

namespace PewPew.Application.Terminal;

/// <summary>
/// Dedicated OS-isolated terminal-worker boundary. Implementations must enforce
/// AppContainer network denial and Job Object process-tree ownership before
/// accepting an invocation. A false readiness result means no process starts.
/// </summary>
public interface IIsolatedTerminalWorkerHost : ITerminalProcessRunner
{
    IsolatedTerminalWorkerReadiness GetReadiness();
}

/// <summary>Metadata-only proof required before a terminal worker may start.</summary>
public sealed record IsolatedTerminalWorkerReadiness(
    bool IsReady,
    string ReasonCode,
    bool HasNetworkDeniedAppContainer,
    bool HasRestrictedJobObject,
    bool HasAuthenticatedLocalIpc);

/// <summary>
/// One-time binding for an isolated-worker request. The caller must pass this
/// verbatim to the worker IPC endpoint; the worker rejects a replayed nonce.
/// </summary>
public sealed record IsolatedTerminalWorkerBinding(
    string CorrelationId,
    string Nonce,
    string WorkflowId,
    string WorkflowVersion,
    string WorkflowHash)
{
    public static IsolatedTerminalWorkerBinding Create(
        string correlationId,
        string workflowId,
        string workflowVersion,
        string workflowHash) =>
        new(
            Require(correlationId, nameof(correlationId)),
            Guid.NewGuid().ToString("N"),
            Require(workflowId, nameof(workflowId)),
            Require(workflowVersion, nameof(workflowVersion)),
            Require(workflowHash, nameof(workflowHash)));

    private static string Require(string value, string parameterName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException("A non-empty isolated-worker binding value is required.", parameterName);
}

/// <summary>
/// Typed, one-time invocation delivered only over the worker's authenticated
/// local IPC channel. It contains no environment values or raw output.
/// </summary>
public sealed record IsolatedTerminalWorkerInvocation(
    IsolatedTerminalWorkerBinding Binding,
    TerminalProcessLaunchRequest LaunchRequest);

/// <summary>
/// Serialization-only wire shape for the local worker. It deliberately avoids
/// interface-typed collections and domain value objects at the process
/// boundary; the worker recreates the validated request before execution.
/// </summary>
public sealed record IsolatedTerminalWorkerWireInvocation(
    IsolatedTerminalWorkerBinding Binding,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    int MaxRamMb,
    int MaxCpuPercent,
    long MaxExecutionDurationMilliseconds,
    long MaxOutputSizeBytes,
    bool AllowNetworkAccess,
    bool AllowFileSystemWrite)
{
    public static IsolatedTerminalWorkerWireInvocation From(IsolatedTerminalWorkerInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var request = invocation.LaunchRequest;
        return new(
            invocation.Binding,
            request.ExecutablePath,
            request.Arguments.ToArray(),
            request.WorkingDirectory,
            request.Quota.MaxRamMb,
            request.Quota.MaxCpuPercent,
            checked((long)request.Quota.MaxExecutionDuration.TotalMilliseconds),
            request.Quota.MaxOutputSizeBytes,
            request.Quota.AllowNetworkAccess,
            request.Quota.AllowFileSystemWrite);
    }

    public IsolatedTerminalWorkerInvocation ToInvocation() =>
        new(
            Binding,
            new TerminalProcessLaunchRequest(
                ExecutablePath,
                Arguments,
                WorkingDirectory,
                new WorkerResourceQuota(
                    MaxRamMb,
                    MaxCpuPercent,
                    TimeSpan.FromMilliseconds(MaxExecutionDurationMilliseconds),
                    MaxOutputSizeBytes,
                    AllowNetworkAccess,
                    AllowFileSystemWrite),
                Binding: Binding));
}

/// <summary>
/// Desktop-facing worker broker boundary. Implementations must reject a nonce
/// replay, correlation mismatch or malformed result before exposing a process
/// result to Application.
/// </summary>
public interface IIsolatedTerminalWorkerBroker
{
    bool IsAuthenticated { get; }

    bool ProvidesRestrictedJobObject { get; }

    Task<TerminalProcessRunResult> ExecuteAsync(
        IsolatedTerminalWorkerInvocation invocation,
        Action<int> onProcessStarted,
        CancellationToken cancellationToken);
}
