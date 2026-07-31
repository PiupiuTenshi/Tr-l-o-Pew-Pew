namespace PewPew.Application.UiAutomation;

/// <summary>
/// Result returned by <see cref="IWindowsAccessibilityAdapter"/>.
/// Contains post-action readback evidence for action verification.
/// </summary>
public sealed record UiAutomationResult(
    bool IsSuccess,
    string ActionEvidence,
    string? FailureReason = null)
{
    public static UiAutomationResult Success(string evidence) =>
        new(true, ActionEvidence: evidence);

    public static UiAutomationResult Failed(string reason) =>
        new(false, ActionEvidence: string.Empty, FailureReason: reason);
}
