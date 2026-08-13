using System.Collections.Concurrent;
using PewPew.Application.Terminal;

namespace PewPew.Desktop;

/// <summary>
/// One-time, authenticated IPC broker for a dedicated terminal worker. The
/// wire client owns the pipe implementation; this boundary owns binding and
/// replay validation and never logs command data or worker output.
/// </summary>
public sealed class AuthenticatedIsolatedTerminalWorkerBroker : IIsolatedTerminalWorkerBroker
{
    private readonly IIsolatedTerminalWorkerWireClient _wireClient;
    private readonly ConcurrentDictionary<string, byte> _consumedNonces = new(StringComparer.Ordinal);

    public AuthenticatedIsolatedTerminalWorkerBroker(IIsolatedTerminalWorkerWireClient wireClient)
    {
        _wireClient = wireClient ?? throw new ArgumentNullException(nameof(wireClient));
    }

    public bool IsAuthenticated => _wireClient.IsAuthenticated;

    public async Task<TerminalProcessRunResult> ExecuteAsync(
        IsolatedTerminalWorkerInvocation invocation,
        Action<int> onProcessStarted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(onProcessStarted);

        if (!IsAuthenticated)
        {
            throw new TerminalProcessBoundaryViolationException("isolated_terminal_worker_ipc_unauthenticated");
        }

        ValidateInvocation(invocation);
        if (!_consumedNonces.TryAdd(invocation.Binding.Nonce, 0))
        {
            throw new TerminalProcessBoundaryViolationException("isolated_terminal_worker_replay_denied");
        }

        try
        {
            return await _wireClient.SendAsync(invocation, onProcessStarted, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TerminalProcessBoundaryViolationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new TerminalProcessBoundaryViolationException("isolated_terminal_worker_ipc_unavailable");
        }
    }

    private static void ValidateInvocation(IsolatedTerminalWorkerInvocation invocation)
    {
        var binding = invocation.Binding;
        var request = invocation.LaunchRequest;
        if (string.IsNullOrWhiteSpace(binding.CorrelationId) ||
            string.IsNullOrWhiteSpace(binding.Nonce) ||
            string.IsNullOrWhiteSpace(binding.WorkflowId) ||
            string.IsNullOrWhiteSpace(binding.WorkflowVersion) ||
            string.IsNullOrWhiteSpace(binding.WorkflowHash) ||
            request.Binding != binding ||
            request.Environment is not null)
        {
            throw new TerminalProcessBoundaryViolationException("isolated_terminal_worker_binding_invalid");
        }
    }
}

/// <summary>Windows-native named-pipe client boundary for a dedicated worker.</summary>
public interface IIsolatedTerminalWorkerWireClient
{
    bool IsAuthenticated { get; }

    Task<TerminalProcessRunResult> SendAsync(
        IsolatedTerminalWorkerInvocation invocation,
        Action<int> onProcessStarted,
        CancellationToken cancellationToken);
}
