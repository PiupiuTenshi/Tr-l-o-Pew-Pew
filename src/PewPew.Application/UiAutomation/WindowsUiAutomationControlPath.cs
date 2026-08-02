using System.Security.Cryptography;
using System.Text;

namespace PewPew.Application.UiAutomation;

/// <summary>Policy and confirmation gate for one bound Windows UIA command.</summary>
public static class WindowsUiAutomationControlPath
{
    public static async Task<WindowsUiAutomationControlResult> ExecuteAsync(
        WindowsUiAutomationControlRequest request,
        IVerifiedWindowsUiAutomationChannel channel,
        string? valuePayload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(channel);

        if (!IsBound(request, valuePayload))
        {
            return new(WindowsUiAutomationOutcome.Denied, "uia_command_authorization_binding_denied", null);
        }

        var authorization = Actions.ActionDispatchService.Dispatch(request.Authorization);
        if (!authorization.IsAllowed)
        {
            return new(WindowsUiAutomationOutcome.Denied, authorization.ReasonCode, authorization.AuditRecord.PolicyResult);
        }

        try
        {
            var readback = await channel.ExecuteAndReadbackAsync(request.Command.Target, request.Command.Action, valuePayload, cancellationToken)
                .ConfigureAwait(false);
            if (readback is null)
            {
                request.Authorization.Task.MarkOutcomeUnknown();
                return new(WindowsUiAutomationOutcome.Unknown, "uia_readback_lost_after_action", authorization.AuditRecord.PolicyResult);
            }

            if (!string.Equals(readback.TargetId, request.Command.Target.TargetId, StringComparison.Ordinal) ||
                !string.Equals(readback.State, request.Command.ExpectedReadback, StringComparison.Ordinal) ||
                (request.Command.PayloadHash is not null && !string.Equals(readback.ValueHash, request.Command.PayloadHash, StringComparison.Ordinal)))
            {
                request.Authorization.Task.Fail("uia_readback_mismatch");
                return new(WindowsUiAutomationOutcome.Failed, "uia_readback_mismatch", authorization.AuditRecord.PolicyResult);
            }

            request.Authorization.Task.Complete($"uia:{request.Command.CommandId}:verified");
            return new(WindowsUiAutomationOutcome.Verified, "verified", authorization.AuditRecord.PolicyResult);
        }
        catch (OperationCanceledException)
        {
            request.Authorization.Task.Cancel();
            return new(WindowsUiAutomationOutcome.Denied, "cancelled", authorization.AuditRecord.PolicyResult);
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("uia_", StringComparison.Ordinal))
        {
            request.Authorization.Task.Fail(exception.Message);
            return new(WindowsUiAutomationOutcome.Failed, exception.Message, authorization.AuditRecord.PolicyResult);
        }
    }

    private static bool IsBound(WindowsUiAutomationControlRequest request, string? valuePayload)
    {
        var command = request.Command;
        var authorization = request.Authorization;
        return !string.IsNullOrWhiteSpace(command.CommandId) &&
               string.Equals(command.AuthorizationId, authorization.CorrelationId, StringComparison.Ordinal) &&
               string.Equals(command.PlanHash, authorization.Plan.Definition.Hash, StringComparison.Ordinal) &&
               (valuePayload is null
                   ? command.PayloadHash is null
                   : string.Equals(command.PayloadHash, Hash(command.Target.TargetId, command.Action.ToString(), valuePayload), StringComparison.Ordinal)) &&
               string.Equals(authorization.RequestedScope.Resource, command.Target.TargetId, StringComparison.Ordinal) &&
               string.Equals(authorization.RequestedScope.Action, command.Action.ToString(), StringComparison.Ordinal) &&
               !UiAutomationTargetAllowlist.IsProhibitedProcess(command.Target.ProcessName);
    }

    public static string Hash(string targetId, string action, string? valuePayload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{targetId}\n{action}\n{valuePayload ?? string.Empty}"));
        return Convert.ToHexString(bytes);
    }
}
