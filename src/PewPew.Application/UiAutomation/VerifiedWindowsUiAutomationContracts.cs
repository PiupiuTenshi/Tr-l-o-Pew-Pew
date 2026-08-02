using PewPew.Application.Actions;

namespace PewPew.Application.UiAutomation;

/// <summary>Exact, allowlisted UIA command. Values are represented by hashes only.</summary>
public sealed record VerifiedWindowsUiAutomationCommand(
    string CommandId,
    UiTargetScope Target,
    UiActionKind Action,
    string PlanHash,
    string? PayloadHash,
    string ExpectedReadback,
    string AuthorizationId);

public sealed record WindowsUiAutomationControlRequest(
    ActionDispatchRequest Authorization,
    VerifiedWindowsUiAutomationCommand Command);

public enum WindowsUiAutomationOutcome
{
    Verified = 1,
    Failed = 2,
    Unknown = 3,
    Denied = 4
}

public sealed record WindowsUiAutomationControlResult(
    WindowsUiAutomationOutcome Outcome,
    string ReasonCode,
    string? AuditPolicyResult);

/// <summary>Metadata-only post-action observation produced by the Windows UIA adapter.</summary>
public sealed record WindowsUiAutomationReadback(
    string TargetId,
    string State,
    string? ValueHash);

/// <summary>
/// Windows adapter boundary. It must resolve the target anew after the action;
/// it may never treat requested values as successful readback.
/// </summary>
public interface IVerifiedWindowsUiAutomationChannel
{
    Task<WindowsUiAutomationReadback?> ExecuteAndReadbackAsync(
        UiTargetScope target,
        UiActionKind action,
        string? valuePayload,
        CancellationToken cancellationToken);
}
