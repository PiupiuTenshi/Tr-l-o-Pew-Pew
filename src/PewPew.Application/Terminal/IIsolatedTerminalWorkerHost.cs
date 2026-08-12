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
