using System.Collections.Concurrent;
using PewPew.Application.BrowserExtension;

namespace PewPew.Desktop;

/// <summary>
/// Desktop-side rendezvous for an already authorized browser command and its
/// independently returned extension readback. This class is transport only:
/// callers must complete policy and confirmation before queuing a command.
/// </summary>
public sealed class NativeMessagingBrowserCommandBroker
{
    private readonly ConcurrentDictionary<string, PendingCommand> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _pendingOrder = new();

    public bool TryQueue(NativeMessagingBrowserCommand command, out string reasonCode)
    {
        if (!NativeMessagingTransportProtocol.TryValidateBrowserCommand(command, out reasonCode))
        {
            return false;
        }

        var pending = new PendingCommand(command);
        if (!_pending.TryAdd(command.CommandId, pending))
        {
            reasonCode = "browser_command_replay_denied";
            return false;
        }

        _pendingOrder.Enqueue(command.CommandId);
        reasonCode = string.Empty;
        return true;
    }

    public NativeMessagingTransportResponse Poll(NativeMessagingTransportRequest request)
    {
        while (_pendingOrder.TryDequeue(out var commandId))
        {
            if (!_pending.TryGetValue(commandId, out var pending))
            {
                continue;
            }

            var command = pending.Command;
            if (!string.Equals(command.SessionToken, request.SessionToken, StringComparison.Ordinal) ||
                !string.Equals(command.TargetOrigin, request.TargetOrigin, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(command.TabId, request.TabId, StringComparison.OrdinalIgnoreCase))
            {
                _pendingOrder.Enqueue(commandId);
                return NativeMessagingTransportResponse.Rejected("browser_command_binding_denied");
            }

            return NativeMessagingTransportResponse.Accepted(browserCommand: command);
        }

        return NativeMessagingTransportResponse.Accepted();
    }

    public NativeMessagingTransportResponse CompleteReadback(NativeMessagingTransportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CommandId) || !_pending.TryGetValue(request.CommandId, out var pending))
        {
            return NativeMessagingTransportResponse.Rejected("browser_readback_command_unknown");
        }

        var command = pending.Command;
        if (!string.Equals(command.SessionToken, request.SessionToken, StringComparison.Ordinal) ||
            !string.Equals(command.TargetOrigin, request.TargetOrigin, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(command.TabId, request.TabId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(command.SnapshotId, request.SnapshotId, StringComparison.Ordinal) ||
            command.SnapshotVersion != request.SnapshotVersion ||
            command.NavigationGeneration != request.NavigationGeneration ||
            !string.Equals(command.PayloadHash, request.PayloadHash, StringComparison.Ordinal))
        {
            pending.Readback.TrySetResult(NativeMessagingTransportResponse.Rejected("browser_readback_binding_denied"));
            return NativeMessagingTransportResponse.Rejected("browser_readback_binding_denied");
        }

        var response = NativeMessagingTransportResponse.Accepted();
        response = response with { ReasonCode = request.ReadbackStatus! };
        pending.Readback.TrySetResult(response);
        return response;
    }

    public Task<NativeMessagingTransportResponse> WaitForReadbackAsync(string commandId, CancellationToken cancellationToken)
    {
        if (!_pending.TryGetValue(commandId, out var pending))
        {
            return Task.FromResult(NativeMessagingTransportResponse.Rejected("browser_command_unknown"));
        }

        return WaitAndRemoveAsync(commandId, pending, cancellationToken);
    }

    private async Task<NativeMessagingTransportResponse> WaitAndRemoveAsync(
        string commandId,
        PendingCommand pending,
        CancellationToken cancellationToken)
    {
        try
        {
            return await pending.Readback.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(commandId, out _);
        }
    }

    private sealed class PendingCommand(NativeMessagingBrowserCommand command)
    {
        public NativeMessagingBrowserCommand Command { get; } = command;
        public TaskCompletionSource<NativeMessagingTransportResponse> Readback { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
