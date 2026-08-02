using PewPew.Application.Actions;
using PewPew.Application.BrowserExtension;
using PewPew.Domain.Actions;

namespace PewPew.Application.Automation;

public interface IVerifiedBrowserActionChannel
{
    Task<string?> DispatchAndReadbackAsync(NativeMessagingBrowserCommand command, CancellationToken cancellationToken);
}

public sealed record BrowserActionControlRequest(ActionDispatchRequest Authorization, NativeMessagingBrowserCommand Command);
public sealed record BrowserActionControlResult(BrowserExecutorOutcome Outcome, string ReasonCode, string? AuditPolicyResult);

/// <summary>Application control path: policy/confirmation before I/O, then independent readback reconciliation.</summary>
public static class BrowserActionControlPath
{
    public static async Task<BrowserActionControlResult> ExecuteAsync(
        BrowserActionControlRequest request,
        IVerifiedBrowserActionChannel channel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(channel);

        if (!NativeMessagingTransportProtocol.TryValidateBrowserCommand(request.Command, out var commandReason) ||
            !string.Equals(request.Authorization.CorrelationId, request.Command.AuthorizationId, StringComparison.Ordinal) ||
            !string.Equals(request.Authorization.Plan.Definition.Hash, request.Command.PayloadHash, StringComparison.Ordinal) ||
            !string.Equals(request.Authorization.RequestedScope.Resource, request.Command.TargetOrigin, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Authorization.RequestedScope.Action, request.Command.ActionKind, StringComparison.Ordinal))
        {
            var denied = ActionDispatchService.RejectBeforeDispatch(request.Authorization, "browser_command_authorization_binding_denied");
            return new(BrowserExecutorOutcome.Denied, denied.ReasonCode, denied.AuditRecord.PolicyResult);
        }

        var authorization = ActionDispatchService.Dispatch(request.Authorization);
        if (!authorization.IsAllowed)
        {
            return new(BrowserExecutorOutcome.Denied, authorization.ReasonCode, authorization.AuditRecord.PolicyResult);
        }

        try
        {
            var status = await channel.DispatchAndReadbackAsync(request.Command, cancellationToken).ConfigureAwait(false);
            if (IsExpectedReadback(request.Command.ActionKind, status))
            {
                request.Authorization.Task.Complete($"browser:{request.Command.CommandId}:verified");
                return new(BrowserExecutorOutcome.Verified, "verified", authorization.AuditRecord.PolicyResult);
            }

            if (status is null)
            {
                request.Authorization.Task.MarkOutcomeUnknown();
                return new(BrowserExecutorOutcome.Unknown, "readback_lost_after_action", authorization.AuditRecord.PolicyResult);
            }

            request.Authorization.Task.Fail("readback_mismatch");
            return new(BrowserExecutorOutcome.Failed, $"readback_mismatch:{status}", authorization.AuditRecord.PolicyResult);
        }
        catch (OperationCanceledException)
        {
            request.Authorization.Task.Cancel();
            return new(BrowserExecutorOutcome.Denied, "cancelled", authorization.AuditRecord.PolicyResult);
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("browser_", StringComparison.Ordinal))
        {
            request.Authorization.Task.Fail(exception.Message);
            return new(BrowserExecutorOutcome.Failed, exception.Message, authorization.AuditRecord.PolicyResult);
        }
    }

    private static bool IsExpectedReadback(string action, string? status) =>
        (action, status) switch
        {
            ("play", "observed_playing") => true,
            ("pause", "observed_paused") => true,
            ("mute", "observed_muted") => true,
            ("unmute", "observed_unmuted") => true,
            _ => false
        };
}
